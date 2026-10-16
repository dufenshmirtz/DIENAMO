"""
Build ONE behaviour-cloning dataset from several collection runs (thesis: dataset v3 for theta4).

Same rules as build_dataset.py (version v2: obs of the previous record + actions of this record, quick_ready from
telemetry), extended to several (run, schedule) pairs:
- personas are matched BY ID across runs (e.g. S05 of the main collection also plays in the extension run);
- style label of a persona = mean of its per-match style over ALL runs it played in (measured, never the knobs);
- hold-out = hold-out in any schedule (main: S07 S08 S15 S20 S22 S24; extension: X02 X03) -> split "unseen";
- every run is split by match on its own; the first run uses exactly the seed and procedure of build_dataset.py, so
  the main collection keeps the SAME train/val/test matches as dataset v2 (theta3 and theta4 are comparable);
- match ids are made unique across runs (offset per run).

Usage:
  python build_dataset_multi.py --runs Synthetic_main:schedule_main Extreme_pilot:schedule_extreme_pilot \
                                       Synthetic_ext:schedule_ext --out-dir <...>/_datasets/Synthetic_v3
"""

import argparse
import csv
import glob
import json
import os
import sys
from datetime import datetime, timezone

import numpy as np
import pandas as pd

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import synthetic_style_report as SR  # noqa: E402
from build_dataset import AXES, ACTION_BRANCHES, SPLIT_CODES, default_root  # noqa: E402

LOG_TO_TEL_OFFSET = 0.045


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--runs", nargs="+", required=True, help="run_folder:schedule_name (schedule in ../schedules)")
    ap.add_argument("--root", default=default_root())
    ap.add_argument("--out-dir", required=True)
    ap.add_argument("--val", type=float, default=0.15)
    ap.add_argument("--test", type=float, default=0.15)
    ap.add_argument("--seed", type=int, default=20261014)
    ap.add_argument("--quick-cooldown", type=float, default=3.14)
    args = ap.parse_args()

    runs = []
    for spec in args.runs:
        run, sname = spec.split(":")
        sched = json.load(open(os.path.join(HERE, "..", "schedules", sname + ".json"), encoding="utf-8"))
        runs.append((run, sched))

    # ---- persona table (union by id) ----
    personas, pid_index, holdout = [], {}, set()
    for run, sched in runs:
        for p in sched["personas"]:
            if p.get("kind", "Scripted") not in ("", "Scripted"):
                continue
            if p["id"] not in pid_index:
                pid_index[p["id"]] = len(personas)
                personas.append({k: p[k] for k in ("id", "aggression", "defense", "mobility", "patience")} |
                                {"holdout": False, "runs": []})
            if p.get("holdout"):
                holdout.add(p["id"])
            personas[pid_index[p["id"]]]["runs"].append(run)
    for p in personas:
        p["holdout"] = p["id"] in holdout

    # ---- style labels over all runs ----
    rounds = pd.concat([SR.load_rounds(os.path.join(args.root, "Telemetry", run)) for run, _ in runs], ignore_index=True)
    rounds = rounds[rounds["persona"].isin(pid_index)]
    g = rounds.groupby("persona")[AXES]
    style_mean, style_std, style_n = g.mean(), g.std(), g.size()
    played = [p["id"] for p in personas if p["id"] in style_mean.index]
    train_ids = [i for i in played if i not in holdout]
    norm_mean = style_mean.loc[train_ids].mean()
    norm_std = style_mean.loc[train_ids].std()

    obs, act, sty, per, mat, spl, slot, tick, qready = [], [], [], [], [], [], [], [], []
    matches_per_split = {"train": [], "val": [], "test": []}
    offset = 0
    run_info = []
    for k, (run, sched) in enumerate(runs):
        tel_dir = os.path.join(args.root, "Telemetry", run)
        log_dir = os.path.join(args.root, "ImitationLogs", run)
        runlog = list(csv.DictReader(open(os.path.join(tel_dir, f"_runlog_{sched['name']}.csv"), encoding="utf-8")))
        match_index = {r["matchId"]: int(r["index"]) for r in runlog if r["matchId"] and r["matchId"] != "ABANDONED"}

        # split by match (run 0: identical to build_dataset.py)
        rng = np.random.default_rng(args.seed + k)
        perm = list(rng.permutation(sorted(match_index.values())))
        n_val, n_test = round(len(perm) * args.val), round(len(perm) * args.test)
        msplit = {int(m): ("val" if j < n_val else ("test" if j < n_val + n_test else "train")) for j, m in enumerate(perm)}
        for m, s in msplit.items():
            matches_per_split[s].append(offset + m)

        quick_times, fight_start = {}, {}
        for tf in glob.glob(os.path.join(tel_dir, "*.json")):
            doc = json.load(open(tf, encoding="utf-8-sig"))
            fight_start[doc["matchId"]] = float(doc.get("fightStartT") or 0.0)
            q = {"P1": [], "P2": []}
            for e in doc["events"]:
                if e.get("eventType") == "Action" and e.get("actionType") == "Quick" and e.get("actorId") in q:
                    q[e["actorId"]].append(float(e["t"]))
            quick_times[doc["matchId"]] = {kk: np.array(v) for kk, v in q.items()}

        def quick_ready(mid, slot_id, t_rec):
            qt = quick_times.get(mid, {}).get(slot_id)
            if qt is None or len(qt) == 0:
                return 1.0
            T = fight_start[mid] + t_rec + LOG_TO_TEL_OFFSET
            recent = qt[(qt <= T) & (qt > T - args.quick_cooldown)]
            return 0.0 if len(recent) else 1.0

        n_rows0, missing, skipped_persona = len(obs), 0, 0
        for f in sorted(glob.glob(os.path.join(log_dir, "match_*.jsonl"))):
            mid = os.path.basename(f)[6:-6]
            if mid not in match_index:
                missing += 1
                continue
            mi = match_index[mid]
            prev = {}
            for line in open(f, encoding="utf-8"):
                r = json.loads(line)
                p = r["persona"]
                last = prev.get(r["slot"])
                prev[r["slot"]] = r
                if last is None:
                    continue
                if p not in pid_index or p not in style_mean.index:
                    skipped_persona += 1
                    continue
                obs.append(last["obs"])
                qready.append(quick_ready(mid, last["slot"], last["t"]))
                act.append(r["act"])
                sty.append(style_mean.loc[p, AXES].to_numpy())
                per.append(pid_index[p])
                mat.append(offset + mi)
                spl.append(SPLIT_CODES["unseen" if p in holdout else msplit[mi]])
                slot.append(0 if r["slot"] == "P1" else 1)
                tick.append(r["tick"])
        run_info.append({"run": run, "schedule": sched["name"], "match_offset": offset, "matches": len(match_index),
                         "rows": len(obs) - n_rows0, "logs_not_in_runlog": missing, "rows_unknown_persona": skipped_persona})
        print(f"{run}: {len(match_index)} matches, {len(obs) - n_rows0} rows (offset {offset}, missing logs {missing}, "
              f"unknown persona rows {skipped_persona})")
        offset += max(match_index.values()) + 1

    obs = np.asarray(obs, dtype=np.float32)
    act = np.asarray(act, dtype=np.int8)
    sty = np.asarray(sty, dtype=np.float32)
    spl = np.asarray(spl, dtype=np.int8)
    os.makedirs(args.out_dir, exist_ok=True)
    np.savez_compressed(os.path.join(args.out_dir, "dataset.npz"), obs=obs, act=act, style_raw=sty,
                        persona=np.asarray(per, dtype=np.int16), match=np.asarray(mat, dtype=np.int16),
                        split=spl, slot=np.asarray(slot, dtype=np.int8), tick=np.asarray(tick, dtype=np.int32),
                        quick_ready=np.asarray(qready, dtype=np.float32))

    train_act = act[spl == SPLIT_CODES["train"]]
    freqs = {}
    for b, name in enumerate(ACTION_BRANCHES):
        vals, counts = np.unique(train_act[:, b], return_counts=True)
        freqs[name] = {int(v): round(float(c) / len(train_act), 5) for v, c in zip(vals, counts)}
    persona_table = [{**p, "matches": int(style_n.get(p["id"], 0)),
                      "style_mean": {a: round(float(style_mean.loc[p["id"], a]), 5) for a in AXES} if p["id"] in style_mean.index else None,
                      "style_std": {a: round(float(style_std.loc[p["id"], a]), 5) for a in AXES} if p["id"] in style_mean.index else None}
                     for p in personas]
    rows_per_split = {k: int((spl == v).sum()) for k, v in SPLIT_CODES.items()}
    desc = {
        "createdUtc": datetime.now(timezone.utc).isoformat(timespec="seconds"),
        "runs": run_info, "split_seed": args.seed, "version": "v3 (multi-run, v2 alignment)",
        "aligned_obs_previous_record": True, "quick_cooldown": args.quick_cooldown,
        "obs_dim": int(obs.shape[1]), "action_branches": ACTION_BRANCHES,
        "action_sizes": [int(act[:, b].max()) + 1 for b in range(act.shape[1])],
        "style_axes": AXES,
        "style_norm": {"mean": {a: float(norm_mean[a]) for a in AXES}, "std": {a: float(norm_std[a]) for a in AXES},
                       "note": "z = (style_raw - mean) / std; stats over the training personas, one value per persona"},
        "split_codes": SPLIT_CODES, "rows_per_split": rows_per_split,
        "matches_per_split": {s: sorted(v) for s, v in matches_per_split.items()},
        "holdout_personas": sorted(holdout), "train_action_freq": freqs, "personas": persona_table,
    }
    json.dump(desc, open(os.path.join(args.out_dir, "dataset.json"), "w", encoding="utf-8"), indent=1)
    print(f"rows {len(obs)}  " + "  ".join(f"{k} {v}" for k, v in rows_per_split.items()))
    print(f"personas {len(played)} (train {len(train_ids)}, hold-out {sorted(holdout)})")
    print(f"saved to {args.out_dir}")


if __name__ == "__main__":
    main()
