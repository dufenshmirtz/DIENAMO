"""
Style-conditioned behaviour cloning (thesis: IL agent, step 5 -> theta0).

Pure NumPy (no PyTorch needed): a small MLP is trained with mini-batch Adam.

Model
- input  = obs (67) [+ style z-scores (5) unless --no-style]
- hidden = 2 x 256, ReLU
- output = 9 heads (one softmax per action branch: move 3, jump 2, drop 2, light 2, heavy 2, block 2,
           special 2, charge 3, parry 2) -> 21 logits
Loss
- sum over heads of the class-weighted cross-entropy.
- class weight = (1 / train frequency) ** power (default power 0.5 = "square-root inverse frequency"),
  normalised so that the average weight of a training sample is 1 in every head. Without weights the model
  learns "never press a rare button" (97 % accuracy, useless agent).
Model selection
- after every epoch: mean macro-F1 over the 9 heads on VAL; the best epoch is kept (early stopping).
- test and unseen are evaluated only once, at the end, with the selected weights.

Outputs (--out-dir)
- model.npz      : W1 b1 W2 b2 W3 b3 (float32) + meta
- model.json     : architecture, input layout, style normalisation, head sizes, selected epoch, seed
                   (everything the game needs to run the policy)
- metrics.json   : per split and per head: accuracy, macro-F1, majority-class baseline; training curve
"""

import argparse
import json
import os
import time

import numpy as np

import egocentric as EGO
import history_features as HF

HEAD_SIZES = [3, 2, 2, 2, 2, 2, 2, 3, 2]
HEAD_NAMES = ["move", "jump", "drop", "light", "heavy", "block", "special", "charge", "parry"]
SPLITS = {"train": 0, "val": 1, "test": 2, "unseen": 3}


def load(data_dir, use_style, use_history=False, ego=False, quick=False):
    d = np.load(os.path.join(data_dir, "dataset.npz"))
    meta = json.load(open(os.path.join(data_dir, "dataset.json"), encoding="utf-8"))
    axes = meta["style_axes"]
    mu = np.array([meta["style_norm"]["mean"][a] for a in axes], dtype=np.float32)
    sd = np.array([meta["style_norm"]["std"][a] for a in axes], dtype=np.float32)
    x = d["obs"].astype(np.float32)
    act = d["act"]
    if ego:           # opponent always on the right, move head = away / none / towards
        x, dirs = EGO.obs_to_ego(x)
        act = EGO.act_to_ego(act, dirs)
    if quick:         # order: obs, quick_ready, history, style
        x = np.concatenate([x, d["quick_ready"][:, None]], axis=1).astype(np.float32)
    if use_history:
        x = np.concatenate([x, HF.compute(act, d["match"], d["slot"], d["tick"])], axis=1).astype(np.float32)
    if use_style:
        x = np.concatenate([x, (d["style_raw"] - mu) / sd], axis=1).astype(np.float32)
    y = act.astype(np.int64)
    return x, y, d["split"], d["persona"], meta, mu, sd


def class_weights(y_train, power):
    ws = []
    for h, k in enumerate(HEAD_SIZES):
        freq = np.bincount(y_train[:, h], minlength=k).astype(np.float64) / len(y_train)
        w = np.where(freq > 0, 1.0 / np.maximum(freq, 1e-9), 0.0) ** power
        w /= (freq * w).sum()  # average weight of a training sample = 1
        ws.append(w.astype(np.float32))
    return ws


def init(rng, n_in, hidden):
    def he(a, b):
        return (rng.standard_normal((a, b)) * np.sqrt(2.0 / a)).astype(np.float32)
    n_out = sum(HEAD_SIZES)
    return {"W1": he(n_in, hidden), "b1": np.zeros(hidden, np.float32),
            "W2": he(hidden, hidden), "b2": np.zeros(hidden, np.float32),
            "W3": (rng.standard_normal((hidden, n_out)) * 0.01).astype(np.float32), "b3": np.zeros(n_out, np.float32)}


def forward(p, x):
    h1 = np.maximum(x @ p["W1"] + p["b1"], 0)
    h2 = np.maximum(h1 @ p["W2"] + p["b2"], 0)
    return h1, h2, h2 @ p["W3"] + p["b3"]


def head_slices():
    out, s = [], 0
    for k in HEAD_SIZES:
        out.append(slice(s, s + k))
        s += k
    return out


SL = head_slices()


def softmax(z):
    z = z - z.max(axis=1, keepdims=True)
    e = np.exp(z)
    return e / e.sum(axis=1, keepdims=True)


def loss_and_grad(p, x, y, cw):
    h1, h2, logits = forward(p, x)
    n = len(x)
    g = np.zeros_like(logits)
    loss = 0.0
    for h, sl in enumerate(SL):
        pr = softmax(logits[:, sl])
        w = cw[h][y[:, h]]
        loss += float(-(w * np.log(pr[np.arange(n), y[:, h]] + 1e-12)).mean())
        pr[np.arange(n), y[:, h]] -= 1.0
        g[:, sl] = pr * (w / n)[:, None]
    grads = {"W3": h2.T @ g, "b3": g.sum(0)}
    d2 = (g @ p["W3"].T) * (h2 > 0)
    grads["W2"], grads["b2"] = h1.T @ d2, d2.sum(0)
    d1 = (d2 @ p["W2"].T) * (h1 > 0)
    grads["W1"], grads["b1"] = x.T @ d1, d1.sum(0)
    return loss, grads


def predict(p, x, batch=8192):
    """Argmax prediction per head + per-head probabilities."""
    preds, probs = [], []
    for i in range(0, len(x), batch):
        _, _, lg = forward(p, x[i:i + batch])
        preds.append(np.stack([lg[:, sl].argmax(1) for sl in SL], axis=1))
        probs.append(np.concatenate([softmax(lg[:, sl]) for sl in SL], axis=1))
    return np.concatenate(preds), np.concatenate(probs)


def macro_f1(y, yp, k):
    f1s = []
    for c in range(k):
        tp = np.sum((yp == c) & (y == c))
        fp = np.sum((yp == c) & (y != c))
        fn = np.sum((yp != c) & (y == c))
        if tp + fp + fn == 0:
            continue
        f1s.append(2 * tp / (2 * tp + fp + fn))
    return float(np.mean(f1s))


def evaluate(p, x, y, majority):
    yp, pr = predict(p, x)
    per_head = {}
    for h, name in enumerate(HEAD_NAMES):
        k = HEAD_SIZES[h]
        per_head[name] = {
            "acc": round(float((yp[:, h] == y[:, h]).mean()), 4),
            "macro_f1": round(macro_f1(y[:, h], yp[:, h], k), 4),
            "majority_acc": round(float((y[:, h] == majority[h]).mean()), 4),
            "majority_macro_f1": round(macro_f1(y[:, h], np.full(len(y), majority[h]), k), 4),
            "pred_rate_nonzero": round(float((yp[:, h] != (1 if name == "move" else 0)).mean()), 4),
            "true_rate_nonzero": round(float((y[:, h] != (1 if name == "move" else 0)).mean()), 4),
            # unweighted negative log-likelihood of the true action (lower = better probabilities)
            "nll": round(float(-np.log(pr[np.arange(len(y)), SL[h].start + y[:, h]] + 1e-12).mean()), 5),
            # mean predicted probability of "doing something" (what a SAMPLING agent would do)
            "prob_rate_nonzero": round(float(1.0 - pr[:, SL[h].start + (1 if name == "move" else 0)].mean()), 4),
        }
    return {"mean_macro_f1": round(float(np.mean([v["macro_f1"] for v in per_head.values()])), 4),
            "nll": round(float(np.sum([v["nll"] for v in per_head.values()])), 5),
            "rows": int(len(y)), "heads": per_head}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--data-dir", required=True)
    ap.add_argument("--out-dir", required=True)
    ap.add_argument("--no-style", action="store_true", help="baseline: obs only")
    ap.add_argument("--history", action="store_true", help="add the 28 action-history features (history_features.py)")
    ap.add_argument("--ego", action="store_true", help="egocentric frame (egocentric.py)")
    ap.add_argument("--quick", action="store_true", help="add the quick_ready input (dataset v2)")
    ap.add_argument("--hidden", type=int, default=256)
    ap.add_argument("--lr", type=float, default=1e-3)
    ap.add_argument("--batch", type=int, default=512)
    ap.add_argument("--epochs", type=int, default=40)
    ap.add_argument("--patience", type=int, default=6)
    ap.add_argument("--weight-decay", type=float, default=1e-5)
    ap.add_argument("--class-weight-power", type=float, default=0.5)
    ap.add_argument("--seed", type=int, default=1)
    ap.add_argument("--select", choices=["f1", "nll"], default="nll",
                    help="model selection on VAL: lowest unweighted NLL (calibrated probabilities, for a sampling agent) or highest macro-F1")
    args = ap.parse_args()

    use_style = not args.no_style
    x, y, split, persona, meta, mu, sd = load(args.data_dir, use_style, args.history, args.ego, args.quick)
    tr, va = split == SPLITS["train"], split == SPLITS["val"]
    xtr, ytr = x[tr], y[tr]
    cw = class_weights(ytr, args.class_weight_power)
    majority = [int(np.bincount(ytr[:, h]).argmax()) for h in range(len(HEAD_SIZES))]

    rng = np.random.default_rng(args.seed)
    p = init(rng, x.shape[1], args.hidden)
    m = {k: np.zeros_like(v) for k, v in p.items()}
    v2 = {k: np.zeros_like(v) for k, v in p.items()}
    b1, b2, eps, step = 0.9, 0.999, 1e-8, 0

    best, best_epoch, best_p, bad, curve = -np.inf, -1, None, 0, []
    for epoch in range(1, args.epochs + 1):
        t0 = time.time()
        order = rng.permutation(len(xtr))
        losses = []
        for i in range(0, len(order), args.batch):
            idx = order[i:i + args.batch]
            loss, g = loss_and_grad(p, xtr[idx], ytr[idx], cw)
            losses.append(loss)
            step += 1
            for k in p:
                if k.startswith("W"):
                    g[k] = g[k] + args.weight_decay * p[k]
                m[k] = b1 * m[k] + (1 - b1) * g[k]
                v2[k] = b2 * v2[k] + (1 - b2) * g[k] * g[k]
                p[k] -= args.lr * (m[k] / (1 - b1 ** step)) / (np.sqrt(v2[k] / (1 - b2 ** step)) + eps)
        val = evaluate(p, x[va], y[va], majority)
        curve.append({"epoch": epoch, "train_loss": round(float(np.mean(losses)), 4),
                      "val_mean_macro_f1": val["mean_macro_f1"], "val_nll": val["nll"], "sec": round(time.time() - t0, 1)})
        print(f"epoch {epoch:2d}  loss {np.mean(losses):.4f}  val macroF1 {val['mean_macro_f1']:.4f}  "
              f"val nll {val['nll']:.4f}  ({time.time() - t0:.1f}s)", flush=True)
        score = val["mean_macro_f1"] if args.select == "f1" else -val["nll"]
        if score > best:
            best, best_epoch, best_p, bad = score, epoch, {k: v.copy() for k, v in p.items()}, 0
        else:
            bad += 1
            if bad >= args.patience:
                break

    p = best_p
    results = {s: evaluate(p, x[split == c], y[split == c], majority) for s, c in SPLITS.items()}

    os.makedirs(args.out_dir, exist_ok=True)
    np.savez(os.path.join(args.out_dir, "model.npz"), **p)
    json.dump({
        "architecture": "MLP", "hidden": [args.hidden, args.hidden], "activation": "relu",
        "input": {"obs_dim": int(meta["obs_dim"]), "style": use_style,
                  "ego": bool(args.ego), "quick_ready": bool(args.quick),
                  "aligned": bool(meta.get("aligned_obs_previous_record", False)),
                  "history": bool(args.history), "history_dim": HF.N_FEATURES if args.history else 0, "history_cap": HF.CAP, "style_axes": meta["style_axes"] if use_style else [],
                  "style_mean": mu.tolist() if use_style else [], "style_std": sd.tolist() if use_style else [],
                  "order": "obs (egocentric if ego), then quick_ready (if any), then history (if any), then (style - mean) / std"},
        "heads": {"names": HEAD_NAMES, "sizes": HEAD_SIZES},
        "selected_epoch": best_epoch, "seed": args.seed, "dataset": meta["run"], "args": vars(args),
    }, open(os.path.join(args.out_dir, "model.json"), "w"), indent=1)
    json.dump({"selected_epoch": best_epoch, "curve": curve, "class_weights": [w.tolist() for w in cw],
               "majority": majority, "results": results},
              open(os.path.join(args.out_dir, "metrics.json"), "w"), indent=1)
    print(f"best epoch {best_epoch}  " + "  ".join(f"{s} F1 {r['mean_macro_f1']:.4f} NLL {r['nll']:.4f}" for s, r in results.items()))


if __name__ == "__main__":
    main()
