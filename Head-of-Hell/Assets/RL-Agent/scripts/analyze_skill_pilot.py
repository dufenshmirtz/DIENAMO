"""
θ5 skill pilot analysis (make_skill_pilot_schedule.py): same knobs, skill 0.6 (T_k6) vs 0.9 (T_k9), every match vs REF.
  competence  : win rate, damage dealt / taken, hit rate (vs the same opponent REF)
  style shift : per target and axis z = (mean_k9 - mean_k6) / (pooled sd * sqrt(1/n9 + 1/n6)); summary in dataset z units
  human-like  : actions per second, heavy cancel rate (RL teachers: 6.2 a/s, 91% cancel; personas 2.2 a/s, ~13%)
"""
import argparse
import json
import os
import sys

import numpy as np
import pandas as pd

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import synthetic_style_report as SR  # noqa: E402

AXES = SR.STYLE_CORE


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--folder", default="Skill_pilot")
    ap.add_argument("--dataset-json", required=True)
    ap.add_argument("--root", default=SR.default_root())
    a = ap.parse_args()
    sd = pd.Series(json.load(open(a.dataset_json, encoding="utf-8"))["style_norm"]["std"])[AXES]

    df = SR.load_rounds(os.path.join(a.root, "Telemetry", a.folder))
    df["aps"] = df.actions_total / df.duration_s
    df["won"] = df.won_round.astype(float)
    t = df[df.persona != "REF"].copy()
    t["T"] = t.persona.str.rsplit("_", n=1).str[0]
    t["k"] = t.persona.str.rsplit("_", n=1).str[1]

    print("=== competence and human-likeness (mean over all targets) ===")
    cols = ["won", "damage_dealt", "damage_taken", "hit_rate", "dps_dealt", "aps", "heavy_cancel_rate", "duration_s"]
    print(t.groupby("k")[cols].mean().round(3).to_string())

    print("\n=== style per version (mean) ===")
    print(t.groupby("k")[AXES].mean().round(3).to_string())

    rows, zs = [], []
    for T, g in t.groupby("T"):
        a6, a9 = g[g.k == "k6"], g[g.k == "k9"]
        r = {"T": T, "win6": a6.won.mean(), "win9": a9.won.mean(), "dmg6": a6.damage_dealt.mean(), "dmg9": a9.damage_dealt.mean()}
        shift = []
        for ax in AXES:
            ps = np.sqrt((a6[ax].var(ddof=1) + a9[ax].var(ddof=1)) / 2)
            z = (a9[ax].mean() - a6[ax].mean()) / (ps * np.sqrt(1 / len(a6) + 1 / len(a9)))
            zs.append(z)
            r[ax] = round(z, 2)
            shift.append((a9[ax].mean() - a6[ax].mean()) / sd[ax])
        r["shift_dataset_z"] = round(float(np.sqrt(np.mean(np.square(shift)))), 3)
        rows.append(r)
    out = pd.DataFrame(rows)
    print("\n=== per target: k9 vs k6 (z of the mean difference per axis; shift = RMS in dataset z units) ===")
    print(out.round(2).to_string(index=False))
    zs = np.array(zs)
    print(f"\n{len(zs)} tests: mean z^2 {np.mean(zs ** 2):.2f} (no style change ~1), |z|>2 {np.mean(np.abs(zs) > 2) * 100:.1f}%")
    for ax in AXES:
        print(f"  {ax:>20}: mean z {out[ax].mean():+.2f}")
    print(f"  mean style shift (dataset z units, RMS 5 axes): {out.shift_dataset_z.mean():.3f}")


if __name__ == "__main__":
    main()
