"""
Fine-tune a joint style-conditioned BC model (θ6) on HUMAN data -> θ6h (thesis: human data as teacher).

- Starts from the base model's weights and keeps its action vocabulary (human combinations that the base never saw
  are mapped to the nearest base combination, Hamming distance, and counted).
- Inputs are built exactly as in training (train_bc_v2.load: ego obs + quick_ready + history + style z with the
  BASE dataset's normalisation, which build_dataset_human.py copies into the human dataset).
- Replay against forgetting: every batch = (1 - replay) human rows + replay bot rows from the base dataset's train split.
- Adam with a small learning rate; early stopping on the HUMAN val NLL.
- Report: joint NLL on the human splits and on the bot val split, base vs fine-tuned (the key numbers:
  human test NLL should drop = predicts the person better; bot val NLL should stay ~ the same = nothing forgotten).
"""
import argparse
import json
import os
import time

import numpy as np

import train_bc_v2 as T


def map_classes(y, vocab):
    index = {tuple(v): i for i, v in enumerate(vocab)}
    cls = np.empty(len(y), np.int64)
    unmapped = 0
    for i, row in enumerate(map(tuple, y)):
        k = index.get(row)
        if k is None:
            k = int(np.argmin((vocab != np.array(row)).sum(1)))
            unmapped += 1
        cls[i] = k
    return cls, unmapped


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--base-model", required=True, help="dir with model.npz + model.json (e.g. runs/t6_s2_cal)")
    ap.add_argument("--human-data", required=True)
    ap.add_argument("--bot-data", required=True, help="the base model's dataset (replay + forgetting check)")
    ap.add_argument("--out-dir", required=True)
    ap.add_argument("--lr", type=float, default=1e-4)
    ap.add_argument("--batch", type=int, default=512)
    ap.add_argument("--replay", type=float, default=0.5)
    ap.add_argument("--epochs", type=int, default=30)
    ap.add_argument("--patience", type=int, default=5)
    ap.add_argument("--weight-decay", type=float, default=1e-5)
    ap.add_argument("--seed", type=int, default=1)
    args = ap.parse_args()

    p = {k: v.astype(np.float32) for k, v in np.load(os.path.join(args.base_model, "model.npz")).items() if k != "vocab"}
    vocab = np.load(os.path.join(args.base_model, "model.npz"))["vocab"]
    info = json.load(open(os.path.join(args.base_model, "model.json")))

    xh, yh, sh, _, mh, _, _ = T.load(args.human_data, True, True, True, True)
    xb, yb, sb, _, _, _, _ = T.load(args.bot_data, True, True, True, True)
    ch, unm_h = map_classes(yh, vocab)
    cb, _ = map_classes(yb, vocab)
    print(f"human rows {len(xh)} (train {(sh == 0).sum()}, val {(sh == 1).sum()}, test {(sh == 2).sum()}); "
          f"human rows mapped to the nearest base combination: {unm_h} ({unm_h / len(xh) * 100:.2f}%)")

    def forward(pp, xx):
        h1 = np.maximum(xx @ pp["W1"] + pp["b1"], 0)
        h2 = np.maximum(h1 @ pp["W2"] + pp["b2"], 0)
        return h1, h2, h2 @ pp["W3"] + pp["b3"]

    def nll(pp, xx, cc):
        out = []
        for i in range(0, len(xx), 8192):
            _, _, lg = forward(pp, xx[i:i + 8192])
            lg = lg - lg.max(1, keepdims=True)
            lp = lg - np.log(np.exp(lg).sum(1, keepdims=True))
            out.append(-lp[np.arange(len(lg)), cc[i:i + 8192]])
        return float(np.concatenate(out).mean()) if out else float("nan")

    rng = np.random.default_rng(args.seed)
    bval = np.flatnonzero(sb == 1)
    bval = rng.choice(bval, min(30000, len(bval)), replace=False)

    def report(pp):
        return {"human_train": nll(pp, xh[sh == 0], ch[sh == 0]), "human_val": nll(pp, xh[sh == 1], ch[sh == 1]),
                "human_test": nll(pp, xh[sh == 2], ch[sh == 2]), "bot_val": nll(pp, xb[bval], cb[bval])}

    base = report(p)
    print("base        " + "  ".join(f"{k} {v:.4f}" for k, v in base.items()))

    htr, btr = np.flatnonzero(sh == 0), np.flatnonzero(sb == 0)
    nh = max(1, int(round(args.batch * (1 - args.replay)))); nb = args.batch - nh
    steps = max(50, int(np.ceil(len(htr) / nh)))
    m = {k: np.zeros_like(v) for k, v in p.items()}; v2 = {k: np.zeros_like(v) for k, v in p.items()}
    step, best, best_p, best_epoch, bad, curve = 0, base["human_val"], {k: v.copy() for k, v in p.items()}, 0, 0, []
    for epoch in range(1, args.epochs + 1):
        t0 = time.time(); losses = []
        for _ in range(steps):
            idx_h = rng.choice(htr, nh); idx_b = rng.choice(btr, nb)
            xb_ = np.concatenate([xh[idx_h], xb[idx_b]]); cb_ = np.concatenate([ch[idx_h], cb[idx_b]]); n = len(cb_)
            h1, h2, lg = forward(p, xb_)
            lg = lg - lg.max(1, keepdims=True); e = np.exp(lg); pr = e / e.sum(1, keepdims=True)
            losses.append(float(-np.log(pr[np.arange(n), cb_] + 1e-12).mean()))
            pr[np.arange(n), cb_] -= 1; g = pr / n
            grads = {"W3": h2.T @ g, "b3": g.sum(0)}
            d2 = (g @ p["W3"].T) * (h2 > 0); grads["W2"], grads["b2"] = h1.T @ d2, d2.sum(0)
            d1 = (d2 @ p["W2"].T) * (h1 > 0); grads["W1"], grads["b1"] = xb_.T @ d1, d1.sum(0)
            step += 1
            for k in p:
                gk = grads[k] + (args.weight_decay * p[k] if k.startswith("W") else 0)
                m[k] = 0.9 * m[k] + 0.1 * gk; v2[k] = 0.999 * v2[k] + 0.001 * gk * gk
                p[k] -= args.lr * (m[k] / (1 - 0.9 ** step)) / (np.sqrt(v2[k] / (1 - 0.999 ** step)) + 1e-8)
        hv = nll(p, xh[sh == 1], ch[sh == 1])
        curve.append({"epoch": epoch, "train_loss": round(float(np.mean(losses)), 4), "human_val_nll": round(hv, 5)})
        print(f"epoch {epoch:2d} loss {np.mean(losses):.4f} human val NLL {hv:.4f} ({time.time() - t0:.1f}s)", flush=True)
        if hv < best:
            best, best_p, best_epoch, bad = hv, {k: v.copy() for k, v in p.items()}, epoch, 0
        else:
            bad += 1
            if bad >= args.patience:
                break
    p = best_p
    fine = report(p)
    print("fine-tuned  " + "  ".join(f"{k} {v:.4f}" for k, v in fine.items()) + f"   (best epoch {best_epoch})")

    os.makedirs(args.out_dir, exist_ok=True)
    np.savez(os.path.join(args.out_dir, "model.npz"), **p, vocab=vocab.astype(np.int32))
    info = dict(info)
    info["finetuned_from"] = os.path.abspath(args.base_model)
    info["finetune"] = {"human_data": os.path.abspath(args.human_data), "selected_epoch": best_epoch, "args": vars(args)}
    json.dump(info, open(os.path.join(args.out_dir, "model.json"), "w"), indent=1)
    json.dump({"base": base, "finetuned": fine, "curve": curve, "human_unmapped_rows": unm_h,
               "human_rows": int(len(xh))}, open(os.path.join(args.out_dir, "metrics.json"), "w"), indent=1)


if __name__ == "__main__":
    main()
