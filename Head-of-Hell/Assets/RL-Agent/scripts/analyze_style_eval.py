"""
Style-fidelity analysis (thesis): IL agent vs its scripted teacher, both vs REF.

Reads one or more StyleEval telemetry folders, groups every player round by persona id, and for every target T compares
the agent (persona id "IL_<prefix><T>") with the teacher ("T"). Teachers may come from another folder (reuse).
Reports per axis: correlation agent~teacher across targets, mean |error|, error vs the noise of the 8-match means;
identification (agent closest to its own teacher, z-scored 5 axes); competence (win rate, dps, heavy cancel).
"""
import argparse, glob, json, os, sys, copy
import numpy as np
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "..", "..", "StreamingAssets", "Analysis")))
import extractor_upgraded as X  # noqa
AXES = ["attack_rate", "mobility_rate", "defense_rate", "mobility_time_ratio", "block_time_ratio"]

def load(folders):
    by = {}
    for fd in folders:
        for p in sorted(glob.glob(os.path.join(fd, "match_*.json"))):
            d = json.load(open(p, encoding="utf-8-sig")); m = d.get("meta") or {}
            per = {"P1": m.get("p1PersonaId") or "", "P2": m.get("p2PersonaId") or ""}
            d2 = copy.deepcopy(d); m2 = d2["meta"]
            m2["p1ProfileId"] = m2["p1ProfileName"] = "A"; m2["p2ProfileId"] = m2["p2ProfileName"] = "B"
            for r in X.extract_round_features(d2, p):
                pid = per[r["slot_id"]]
                by.setdefault(pid, []).append(r)
    return by

def summ(rows):
    v = np.array([[r[a] for a in AXES] for r in rows])
    hc = [r["heavy_cancel_rate"] for r in rows if r["heavy_cancel_rate"] is not None]
    return {"n": len(rows), "mean": v.mean(0), "sd": v.std(0, ddof=1) if len(v) > 1 else np.zeros(5),
            "win": float(np.mean([1.0 if r["won_round"] else 0.0 for r in rows])),
            "dps": float(np.mean([r["dps_dealt"] for r in rows])), "hit": float(np.mean([r["hit_rate"] for r in rows])),
            "hcancel": float(np.mean(hc)) if hc else float("nan")}

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--agent-folder", required=True)
    ap.add_argument("--teacher-folders", nargs="+", required=True)
    ap.add_argument("--prefix", required=True, help="agent persona id = IL_<prefix><target>, e.g. T4_")
    ap.add_argument("--targets", nargs="+", required=True)
    ap.add_argument("--holdout", nargs="*", default=[])
    ap.add_argument("--dataset-json", required=True, help="for the z-score std (style_norm)")
    ap.add_argument("--json-out", default=None)
    a = ap.parse_args()
    ag = load([a.agent_folder]); te = load(a.teacher_folders)
    sd = np.array([json.load(open(a.dataset_json))["style_norm"]["std"][x] for x in AXES])
    T, A = [], []
    rows_out = []
    for t in a.targets:
        ar, tr = ag.get(f"IL_{a.prefix}{t}", []), te.get(t, [])
        if not ar or not tr:
            print("missing", t, len(ar), len(tr)); continue
        s_a, s_t = summ(ar), summ(tr); T.append(s_t); A.append(s_a)
        rows_out.append((t, s_t, s_a))
    tm = np.array([s["mean"] for s in T]); am = np.array([s["mean"] for s in A])
    names = [r[0] for r in rows_out]
    print(f"targets {len(names)}; agent rounds {sum(s['n'] for s in A)}, teacher rounds {sum(s['n'] for s in T)}")
    print("\nper target (teacher -> agent):")
    for (t, st, sa) in rows_out:
        tag = "*" if t in a.holdout else " "
        print(f"  {t}{tag} " + "  ".join(f"{x[:6]} {st['mean'][i]:.3f}->{sa['mean'][i]:.3f}" for i, x in enumerate(AXES)))
    res = {"axes": {}}
    print("\naxis           corr   MAE    MAE/noise")
    for i, x in enumerate(AXES):
        c = np.corrcoef(tm[:, i], am[:, i])[0, 1]
        mae = np.abs(tm[:, i] - am[:, i]).mean()
        noise = np.mean([np.sqrt(st["sd"][i] ** 2 / st["n"] + sa["sd"][i] ** 2 / sa["n"]) for _, st, sa in rows_out])
        res["axes"][x] = {"corr": round(float(c), 3), "mae": round(float(mae), 4), "mae_over_noise": round(float(mae / noise), 2)}
        print(f"  {x:22s} {c:5.2f}  {mae:.4f}  {mae / noise:.2f}")
    Z_t, Z_a = tm / sd, am / sd
    D = np.linalg.norm(Z_a[:, None, :] - Z_t[None, :, :], axis=2)
    ident = int((D.argmin(1) == np.arange(len(names))).sum())
    own = np.diag(D)
    print(f"\nidentification: {ident}/{len(names)} agents closest to their own teacher (chance 1/{len(names)})")
    for grp, sel in (("hold-out", [n in a.holdout for n in names]), ("training", [n not in a.holdout for n in names])):
        sel = np.array(sel)
        if sel.any():
            print(f"  {grp}: identified {int((D.argmin(1)[sel] == np.arange(len(names))[sel]).sum())}/{sel.sum()}, mean z-distance to own teacher {own[sel].mean():.2f}")
    W = lambda L, k: np.nanmean([s[k] for s in L])
    print(f"\ncompetence vs REF: win teacher {W(T,'win'):.0%} agent {W(A,'win'):.0%} | dps teacher {W(T,'dps'):.2f} agent {W(A,'dps'):.2f} "
          f"({W(A,'dps') / W(T,'dps'):.0%}) | hit rate {W(T,'hit'):.2f} / {W(A,'hit'):.2f} | heavy cancel {W(T,'hcancel'):.1%} / {W(A,'hcancel'):.1%}")
    res.update({"identification": ident, "n_targets": len(names), "own_distance": own.round(3).tolist(), "targets": names,
                "win": [W(T, 'win'), W(A, 'win')], "dps": [W(T, 'dps'), W(A, 'dps')], "hcancel": [W(T, 'hcancel'), W(A, 'hcancel')]})
    if a.json_out: json.dump(res, open(a.json_out, "w"), indent=1)

if __name__ == "__main__":
    main()
