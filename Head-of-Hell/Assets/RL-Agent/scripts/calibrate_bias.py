"""Post-hoc prior correction of a joint model: shift the output biases so that the MEAN predicted probability of
every action combination on the training rows equals its real frequency (the condition a fully converged
cross-entropy model satisfies). Weights are untouched; only b3 changes. Reports val/test NLL before/after."""
import sys, json, os, numpy as np
import train_bc_v2 as T
src, dst = sys.argv[1], sys.argv[2]
D = "/mnt/user-data/uploads/ImitationLogs/_datasets/Synthetic_v3"
x, y, split, persona, meta, mu, sd = T.load(D, True, True, True, True)
w = dict(np.load(src + "/model.npz")); info = json.load(open(src + "/model.json"))
vocab = w["vocab"]; idx = {tuple(v): i for i, v in enumerate(vocab)}
cls = np.array([idx.get(tuple(r), int(np.argmin((vocab != r).sum(1)))) for r in y])
rng = np.random.default_rng(0)
tr = rng.choice(np.flatnonzero(split == 0), 120000, replace=False)
def H(rows):
    out = []
    for i in range(0, len(rows), 8192):
        _, h2, _ = T.forward(w, x[rows[i:i + 8192]]); out.append(h2)
    return np.concatenate(out)
def P(h, b):
    lg = h @ w["W3"] + b; lg -= lg.max(1, keepdims=True); e = np.exp(lg); return e / e.sum(1, keepdims=True)
def nll(rows, b):
    p = P(H(rows), b); return float(-np.log(p[np.arange(len(rows)), cls[rows]] + 1e-12).mean())
htr = H(tr); K = len(vocab)
true = np.bincount(cls[tr], minlength=K) / len(tr)
b = w["b3"].astype(np.float64).copy()
for it in range(30):
    pred = P(htr, b).mean(0)
    b += np.log((true + 1e-7) / (pred + 1e-7))
va, te = np.flatnonzero(split == 1), np.flatnonzero(split == 2)
print("val NLL before %.4f after %.4f | test before %.4f after %.4f" % (nll(va, w["b3"]), nll(va, b), nll(te, w["b3"]), nll(te, b)))
idle = np.array([1 if n == "move" else 0 for n in T.HEAD_NAMES]); ni = (vocab != idle).astype(float)
for nm, rows in (("train", tr), ("val", va)):
    h = H(rows) if nm != "train" else htr
    t = (y[rows] != idle).mean(0); p0 = (P(h, w["b3"]) @ ni).mean(0); p1 = (P(h, b) @ ni).mean(0)
    print(nm, " ".join(f"{n}: {t[j]:.4f} {p0[j]:.4f}->{p1[j]:.4f}" for j, n in enumerate(T.HEAD_NAMES) if n in ("move","heavy","light","block","parry","charge")))
os.makedirs(dst, exist_ok=True)
w2 = dict(w); w2["b3"] = b.astype(np.float32)
np.savez(dst + "/model.npz", **w2)
info["calibration"] = {"from": src, "method": "prior correction of b3 on 120k train rows, 30 iterations"}
json.dump(info, open(dst + "/model.json", "w"), indent=1)
