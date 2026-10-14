"""
Style-conditioned BC with a JOINT action vocabulary (thesis: IL agent, theta3).

Why: the 9 heads of train_bc_v2 are sampled independently, so the agent produces button combinations the
teachers (almost) never used, e.g. charge + attack 20x, block + attack 2.3x, block + charge 3x more often than
in the data -> cancelled heavies, broken charges. Here every distinct 9-head combination seen in the TRAINING
data is one class of a single softmax (129 combos; the top 30 cover 99 %), so sampling can only produce
combinations the teachers actually used, with their real co-occurrence.

Inputs are identical to train_bc_v2 (--history --ego --quick): obs (ego) + quick + history + style.
Comparable metric: joint NLL = -log p(full action). For the factorised model it is the sum of the head NLLs,
so the numbers of both scripts can be compared directly.
Rows whose combination never occurs in train (val/test/unseen only) are mapped to the closest train combination
(Hamming distance) and counted in the report.
"""

import argparse
import json
import os
import time

import numpy as np

import train_bc_v2 as T


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--data-dir", required=True)
    ap.add_argument("--out-dir", required=True)
    ap.add_argument("--no-style", action="store_true")
    ap.add_argument("--hidden", type=int, default=256)
    ap.add_argument("--lr", type=float, default=1e-3)
    ap.add_argument("--batch", type=int, default=512)
    ap.add_argument("--epochs", type=int, default=40)
    ap.add_argument("--patience", type=int, default=6)
    ap.add_argument("--weight-decay", type=float, default=1e-5)
    ap.add_argument("--seed", type=int, default=1)
    args = ap.parse_args()
    use_style = not args.no_style

    x, y, split, persona, meta, mu, sd = T.load(args.data_dir, use_style, True, True, True)
    tr, va = split == 0, split == 1

    # vocabulary from TRAIN rows only
    vocab, inv = np.unique(y[tr], axis=0, return_inverse=True)
    index = {tuple(v): i for i, v in enumerate(vocab)}
    unmapped = {}
    cls = np.empty(len(y), np.int64)
    for i, row in enumerate(map(tuple, y)):
        k = index.get(row)
        if k is None:
            k = int(np.argmin((vocab != np.array(row)).sum(1)))
            unmapped[row] = unmapped.get(row, 0) + 1
        cls[i] = k
    K = len(vocab)
    print(f"vocabulary {K} combos from train; rows mapped to nearest: {sum(unmapped.values())}")

    rng = np.random.default_rng(args.seed)
    def he(a, b): return (rng.standard_normal((a, b)) * np.sqrt(2.0 / a)).astype(np.float32)
    p = {"W1": he(x.shape[1], args.hidden), "b1": np.zeros(args.hidden, np.float32),
         "W2": he(args.hidden, args.hidden), "b2": np.zeros(args.hidden, np.float32),
         "W3": (rng.standard_normal((args.hidden, K)) * 0.01).astype(np.float32), "b3": np.zeros(K, np.float32)}
    # start the output bias at the log class frequencies (faster, better calibrated start)
    freq = np.bincount(cls[tr], minlength=K) / tr.sum()
    p["b3"] = np.log(freq + 1e-6).astype(np.float32)

    def forward(xx):
        h1 = np.maximum(xx @ p["W1"] + p["b1"], 0)
        h2 = np.maximum(h1 @ p["W2"] + p["b2"], 0)
        return h1, h2, h2 @ p["W3"] + p["b3"]

    def nll(rows):
        out = []
        for i in range(0, rows.sum(), 8192):
            idx = np.flatnonzero(rows)[i:i + 8192]
            _, _, lg = forward(x[idx])
            lg = lg - lg.max(1, keepdims=True)
            lp = lg - np.log(np.exp(lg).sum(1, keepdims=True))
            out.append(-lp[np.arange(len(idx)), cls[idx]])
        return float(np.concatenate(out).mean())

    m = {k: np.zeros_like(v) for k, v in p.items()}; v2 = {k: np.zeros_like(v) for k, v in p.items()}
    step, best, best_p, best_epoch, bad, curve = 0, np.inf, None, 0, 0, []
    xtr, ctr = x[tr], cls[tr]
    for epoch in range(1, args.epochs + 1):
        t0 = time.time()
        order = rng.permutation(len(xtr)); losses = []
        for i in range(0, len(order), args.batch):
            idx = order[i:i + args.batch]; xb, cb = xtr[idx], ctr[idx]; n = len(idx)
            h1, h2, lg = forward(xb)
            lg = lg - lg.max(1, keepdims=True); e = np.exp(lg); pr = e / e.sum(1, keepdims=True)
            losses.append(float(-np.log(pr[np.arange(n), cb] + 1e-12).mean()))
            pr[np.arange(n), cb] -= 1; g = pr / n
            grads = {"W3": h2.T @ g, "b3": g.sum(0)}
            d2 = (g @ p["W3"].T) * (h2 > 0); grads["W2"], grads["b2"] = h1.T @ d2, d2.sum(0)
            d1 = (d2 @ p["W2"].T) * (h1 > 0); grads["W1"], grads["b1"] = xb.T @ d1, d1.sum(0)
            step += 1
            for k in p:
                gk = grads[k] + (args.weight_decay * p[k] if k.startswith("W") else 0)
                m[k] = 0.9 * m[k] + 0.1 * gk; v2[k] = 0.999 * v2[k] + 0.001 * gk * gk
                p[k] -= args.lr * (m[k] / (1 - 0.9 ** step)) / (np.sqrt(v2[k] / (1 - 0.999 ** step)) + 1e-8)
        vn = nll(va)
        curve.append({"epoch": epoch, "train_loss": round(float(np.mean(losses)), 4), "val_joint_nll": round(vn, 5)})
        print(f"epoch {epoch:2d} loss {np.mean(losses):.4f} val joint NLL {vn:.4f} ({time.time()-t0:.1f}s)", flush=True)
        if vn < best:
            best, best_p, best_epoch, bad = vn, {k: v.copy() for k, v in p.items()}, epoch, 0
        else:
            bad += 1
            if bad >= args.patience: break
    p = best_p
    res = {s: round(nll(split == c), 5) for s, c in T.SPLITS.items()}

    os.makedirs(args.out_dir, exist_ok=True)
    np.savez(os.path.join(args.out_dir, "model.npz"), **p, vocab=vocab.astype(np.int32))
    json.dump({"architecture": "MLP-joint", "hidden": [args.hidden, args.hidden], "activation": "relu",
               "input": {"obs_dim": int(meta["obs_dim"]), "ego": True, "quick_ready": True, "aligned": True,
                         "history": True, "history_dim": 28, "history_cap": 25, "style": use_style,
                         "style_axes": meta["style_axes"] if use_style else [],
                         "style_mean": mu.tolist() if use_style else [], "style_std": sd.tolist() if use_style else []},
               "output": {"type": "joint", "vocab_size": K, "vocab": vocab.tolist(),
                          "head_names": T.HEAD_NAMES, "head_sizes": T.HEAD_SIZES},
               "selected_epoch": best_epoch, "seed": args.seed, "args": vars(args)},
              open(os.path.join(args.out_dir, "model.json"), "w"), indent=1)
    json.dump({"joint_nll": res, "curve": curve, "unmapped_rows": int(sum(unmapped.values()))},
              open(os.path.join(args.out_dir, "metrics.json"), "w"), indent=1)
    print(f"best epoch {best_epoch} joint NLL " + " ".join(f"{k} {v:.4f}" for k, v in res.items()))


if __name__ == "__main__":
    main()
