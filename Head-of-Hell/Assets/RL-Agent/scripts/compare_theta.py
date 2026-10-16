"""theta3 vs theta4 on dataset v3: joint NLL per data group + per-persona action-rate fidelity (offline)."""
import json, sys, numpy as np
import train_bc_v2 as T
D = "/mnt/user-data/uploads/ImitationLogs/_datasets/Synthetic_v3"
x, y, split, persona, meta, mu4, sd4 = T.load(D, True, True, True, True)
d = np.load(D + "/dataset.npz"); raw = d["style_raw"]; match = d["match"]
pids = [p["id"] for p in meta["personas"]]
pid = np.array([pids[i] for i in persona])
models = {"theta3": "runs/j_s2", "theta4": "runs/t4_s2", "theta4cal": "runs/t4_s2_cal", "theta4_nostyle": "runs/t4_nostyle_s2"}
groups = {
    "main test (same as v2)": (match < 300) & (split == 2),
    "main unseen (S07..S24)": (match < 300) & (split == 3),
    "corner train personas, test matches": (match >= 300) & (split == 2),
    "corner hold-out X02 X03": np.isin(pid, ["X02", "X03"]),
}
def probs(name, path, rows):
    w = dict(np.load(path + "/model.npz")); info = json.load(open(path + "/model.json"))
    xx = x[rows].copy()
    if info["input"]["style"]:
        m3, s3 = np.array(info["input"]["style_mean"], np.float32), np.array(info["input"]["style_std"], np.float32)
        xx[:, -5:] = (raw[rows] - m3) / s3
    else:
        xx = xx[:, :-5]
    out = []
    for i in range(0, len(xx), 8192):
        _, _, lg = T.forward(w, xx[i:i + 8192]); lg -= lg.max(1, keepdims=True)
        e = np.exp(lg); out.append(e / e.sum(1, keepdims=True))
    return np.concatenate(out), w["vocab"]
res = {}
for gname, rows in groups.items():
    line = f"{gname:38s} n={rows.sum():6d}"
    for name, path in models.items():
        P, vocab = probs(name, path, rows)
        yy = y[rows]
        idx = {tuple(v): i for i, v in enumerate(vocab)}
        cls = np.array([idx.get(tuple(r), int(np.argmin((vocab != r).sum(1)))) for r in yy])
        nll = -np.log(P[np.arange(len(cls)), cls] + 1e-12).mean()
        line += f"  {name} {nll:.4f}"
    print(line)
# per-persona predicted vs true action rates on hold-out personas (all 8)
print("\nhold-out personas: corr(true rate, predicted rate) across the 8 personas, per action")
hold = meta["holdout_personas"]
for name, path in [("theta3", "runs/j_s2"), ("theta4cal", "runs/t4_s2_cal")]:
    tr_, pr_ = [], []
    for h in hold:
        rows = pid == h
        P, vocab = probs(name, path, rows)
        idle = np.array([1 if n == "move" else 0 for n in T.HEAD_NAMES])
        nonidle = (vocab != idle).astype(float)          # K x 9
        pr_.append((P @ nonidle).mean(0)); tr_.append((y[rows] != idle).mean(0))
    tr_, pr_ = np.array(tr_), np.array(pr_)
    cs = [np.corrcoef(tr_[:, j], pr_[:, j])[0, 1] for j in range(9)]
    mae = np.abs(tr_ - pr_).mean(0)
    print(f"  {name}: " + " ".join(f"{n}={c:.2f}" for n, c in zip(T.HEAD_NAMES, cs)))
    print(f"  {name} MAE: " + " ".join(f"{n}={m:.3f}" for n, m in zip(T.HEAD_NAMES, mae)))
    for h, t, p in zip(hold, tr_, pr_):
        if h.startswith("X"): print(f"    {h} move true {t[0]:.3f} pred {p[0]:.3f} | block {t[5]:.3f}/{p[5]:.3f} | heavy {t[4]:.3f}/{p[4]:.3f}")
