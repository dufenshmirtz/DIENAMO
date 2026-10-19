"""
Online convergence experiment (thesis: online IL agent), analysis.
Per target T: player C_T (scripted persona) and agent M_T (theta, online mirror of C_T's EMA) play matches 1..N in order.
Before match k the agent gets the EMA (alpha 0.35) of C_T's matches 1..k-1 (match 1: neutral style).

Everything in z units (axis value / style_norm std of the training dataset), RMS over the 5 style axes:
  player mean P  : mean measured style of C_T over all its matches in this run (its "true" style here)
  est_err(k)     : |given style before match k - P|          -> how well the EMA knows the player
  agent_err(k)   : |agent measured in match k - P|          -> how close the agent PLAYS to the player
  player_noise   : |C_T measured in one match - P| (leave-one-out) -> the floor: one match of the player itself
  ratio(k)       : agent_err(k) / player_noise              -> ~1 = as close as another match of the player
"""
import argparse
import csv
import json
import os
import sys

import numpy as np
import pandas as pd

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import synthetic_style_report as SR  # noqa: E402

AXES = SR.STYLE_CORE


def rms(v):
    return float(np.sqrt(np.mean(np.square(v))))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--folder", required=True)
    ap.add_argument("--schedule", required=True)
    ap.add_argument("--dataset-json", required=True)
    ap.add_argument("--root", default=SR.default_root())
    ap.add_argument("--json-out", default=None)
    a = ap.parse_args()

    sd = np.array([json.load(open(a.dataset_json, encoding="utf-8"))["style_norm"]["std"][x] for x in AXES])
    sched = json.load(open(a.schedule, encoding="utf-8"))
    sname = os.path.splitext(os.path.basename(a.schedule))[0]
    tel = os.path.join(a.root, "Telemetry", a.folder)
    rl = {r["matchId"]: int(r["index"]) for r in csv.DictReader(open(os.path.join(tel, f"_runlog_{sname}.csv"), encoding="utf-8"))}
    rnd = {i: m["round"] for i, m in enumerate(sched["matches"])}

    rows = SR.load_rounds(tel)
    rows["mid"] = rows.file.str.extract(r"_([0-9a-f]{12})\.json$")[0]
    rows["k"] = rows.mid.map(rl).map(rnd)
    rows = rows[rows.k.notna()]   # only matches in the run log (e.g. a stalemate-restarted match saved at 180 s is not)
    player = rows[rows.persona.str.startswith("C_")].copy()
    player["T"] = player.persona.str.replace(r"^C_", "", regex=True).str.replace(r"_[^_]+$", "", regex=True)

    ag = pd.read_csv(os.path.join(a.root, "ImitationLogs", "_style_tracker", f"agent_style_log_{a.folder}.csv"))
    ag["k"] = ag.match_id.map(rl).map(rnd)
    ag = ag[ag.k.notna()]
    ag["T"] = ag.opponent_key.str.replace("persona:C_", "", regex=False).str.replace(r"_[^_]+$", "", regex=True)

    out, per_k = [], []
    for T, g in player.groupby("T"):
        X = g.sort_values("k")[AXES].to_numpy() / sd
        P = X.mean(0)
        n = len(X)
        loo = [rms(X[i] - (X.sum(0) - X[i]) / (n - 1)) for i in range(n)]
        noise = float(np.mean(loo))
        h = ag[ag["T"] == T].sort_values("k")
        for _, r in h.iterrows():
            given = np.array([r["given_" + x] for x in AXES]) / sd
            meas = np.array([r["measured_" + x] for x in AXES]) / sd
            per_k.append({"T": T, "k": int(r.k), "source": r.style_source, "clamped": int(r.style_clamped),
                          "est_err": rms(given - P), "agent_err": rms(meas - P), "noise": noise,
                          "ratio": rms(meas - P) / noise})
        out.append({"T": T, "matches": n, "player_noise": round(noise, 3)})
    d = pd.DataFrame(per_k)

    print(f"=== {a.folder}: {d['T'].nunique()} targets, {len(d)} agent matches ===\n")
    print("Per match number k (mean over targets):")
    print(d.groupby("k")[["est_err", "agent_err", "ratio", "clamped"]].mean().round(3).to_string())
    late = d[d.k >= 6]
    first = d[d.k == 1]
    print(f"\nmatch 1 (neutral)  : agent_err {first.agent_err.mean():.3f}  ratio {first.ratio.mean():.2f}")
    print(f"matches 6-10       : agent_err {late.agent_err.mean():.3f}  ratio {late.ratio.mean():.2f}   "
          f"(player noise {d.noise.mean():.3f})")
    print("\nPer target (matches 6-10):")
    t = late.groupby("T")[["est_err", "agent_err", "ratio", "clamped"]].mean()
    t = t.join(first.set_index("T")[["agent_err"]].rename(columns={"agent_err": "err_k1"}))
    print(t.round(3).sort_values("ratio").to_string())
    if a.json_out:
        json.dump({"per_k": d.groupby("k")[["est_err", "agent_err", "ratio", "clamped"]].mean().reset_index().to_dict("records"),
                   "per_target": t.reset_index().to_dict("records"), "targets": out}, open(a.json_out, "w"), indent=1)


if __name__ == "__main__":
    main()
