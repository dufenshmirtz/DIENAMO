"""
Behaviour-cloning dataset from HUMAN rounds (thesis: θ6h = θ6 fine-tuned on human play).

Input: ILDataScene runs where a person played (schedule persona kind "Human" -> telemetry persona HUMAN_<id>),
given as run_folder:schedule_name, e.g.  HV_P00:schedule_hv1_P00N  HV_baseline:schedule_hv1_P00
Same rules as build_dataset_multi.py (obs of the previous record + actions of this record, quick_ready from telemetry),
with these differences:
- only the HUMAN side is kept (the opponent REF is a bot; its data are already in the bot dataset);
- the persona of a log row comes from the telemetry (the imitation log leaves it empty for humans);
- style label of a human persona = mean of its per-round measured style (same rule as for the bots);
- style normalisation = the BOT dataset's (--norm-from, e.g. Synthetic_v6), so z-scores mean the same for θ6;
- split by round inside every persona: train / val / test (no "unseen" personas);
- optional Telemetry/<run>/_relabel.csv (matchId,persona[,reason]) corrects a persona label without touching the
  match files (22/10: 24 P00A rounds were logged under schedule_hv1_P00N because the scene was not saved);
- every round gets its own match number (run log order), so repeated run-log indices can never merge two rounds.
Output has the same format as the bot datasets (dataset.npz + dataset.json), so train_bc_v2.load() reads it.
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
    ap.add_argument("--norm-from", required=True, help="bot dataset dir whose style_norm is reused (e.g. .../_datasets/Synthetic_v6)")
    ap.add_argument("--root", default=default_root())
    ap.add_argument("--out-dir", required=True)
    ap.add_argument("--val", type=float, default=0.15)
    ap.add_argument("--test", type=float, default=0.15)
    ap.add_argument("--seed", type=int, default=20261022)
    ap.add_argument("--quick-cooldown", type=float, default=3.14)
    args = ap.parse_args()
    norm = json.load(open(os.path.join(args.norm_from, "dataset.json"), encoding="utf-8"))["style_norm"]

    runs = []
    for spec in args.runs:
        run, sname = spec.split(":")
        runs.append((run, json.load(open(os.path.join(HERE, "..", "schedules", sname + ".json"), encoding="utf-8"))))

    # ---- human personas (telemetry id HUMAN_<id>) ----
    pid_index, personas = {}, []
    for run, sched in runs:
        for p in sched["personas"]:
            if p.get("kind") != "Human":
                continue
            hid = "HUMAN_" + p["id"]
            if hid not in pid_index:
                pid_index[hid] = len(personas)
                personas.append({"id": hid, "holdout": False, "runs": []})
            personas[pid_index[hid]]["runs"].append(run)

    # ---- persona corrections (_relabel.csv per run folder) ----
    relabel = {}
    for run in sorted({r for r, _ in runs}):
        rp = os.path.join(args.root, "Telemetry", run, "_relabel.csv")
        if os.path.isfile(rp):
            for r in csv.DictReader(open(rp, encoding="utf-8")):
                relabel[r["matchId"]] = r["persona"]
            print(f"{run}: _relabel.csv -> {sum(1 for _ in csv.DictReader(open(rp, encoding='utf-8')))} rounds relabelled")
    for hid in sorted(set(relabel.values())):
        if hid not in pid_index:
            pid_index[hid] = len(personas)
            personas.append({"id": hid, "holdout": False, "runs": ["(relabel)"]})

    # ---- style labels (mean per-round measured style) ----
    rounds = pd.concat([SR.load_rounds(os.path.join(args.root, "Telemetry", run))
                        for run in dict.fromkeys(r for r, _ in runs)], ignore_index=True)   # each folder once
    rounds["mid"] = rounds["file"].str.extract(r"_([0-9a-f]{12})\.json$")[0]
    fix = rounds["mid"].map(relabel)
    human_seat = rounds["persona"].str.startswith("HUMAN_")
    rounds.loc[human_seat & fix.notna(), "persona"] = fix[human_seat & fix.notna()]
    rounds = rounds[rounds["persona"].isin(pid_index)]
    g = rounds.groupby("persona")[AXES]
    style_mean, style_std, style_n = g.mean(), g.std(), g.size()

    obs, act, sty, per, mat, spl, slot, tick, qready = [], [], [], [], [], [], [], [], []
    matches_per_split = {"train": [], "val": [], "test": []}
    offset, run_info = 0, []
    for k, (run, sched) in enumerate(runs):
        tel_dir = os.path.join(args.root, "Telemetry", run)
        log_dir = os.path.join(args.root, "ImitationLogs", run)
        rl_path = os.path.join(tel_dir, f"_runlog_{sched['name']}.csv")
        if not os.path.isfile(rl_path):
            print(f"{run}:{sched['name']}: no run log yet, skipped")
            continue
        runlog = [r for r in csv.DictReader(open(rl_path, encoding="utf-8")) if r["matchId"] and r["matchId"] != "ABANDONED"]
        match_index = {r["matchId"]: i for i, r in enumerate(runlog)}   # own number per round (run-log order)

        # telemetry: persona per seat, fight start, quick times
        seat_persona, fight_start, quick_times = {}, {}, {}
        for tf in glob.glob(os.path.join(tel_dir, "match_*.json")):
            doc = json.load(open(tf, encoding="utf-8-sig"))
            mid, m = doc["matchId"], doc.get("meta") or {}
            if mid not in match_index:
                continue
            seat_persona[mid] = {"P1": m.get("p1PersonaId") or "", "P2": m.get("p2PersonaId") or ""}
            if mid in relabel:
                for seat in ("P1", "P2"):
                    if seat_persona[mid][seat].startswith("HUMAN_"):
                        seat_persona[mid][seat] = relabel[mid]
            fight_start[mid] = float(doc.get("fightStartT") or 0.0)
            q = {"P1": [], "P2": []}
            for e in doc["events"]:
                if e.get("eventType") == "Action" and e.get("actionType") == "Quick" and e.get("actorId") in q:
                    q[e["actorId"]].append(float(e["t"]))
            quick_times[mid] = {kk: np.array(v) for kk, v in q.items()}

        def quick_ready(mid, slot_id, t_rec):
            qt = quick_times.get(mid, {}).get(slot_id)
            if qt is None or len(qt) == 0:
                return 1.0
            T = fight_start[mid] + t_rec + LOG_TO_TEL_OFFSET
            recent = qt[(qt <= T) & (qt > T - args.quick_cooldown)]
            return 0.0 if len(recent) else 1.0

        # split by round inside each human persona of this run
        rng = np.random.default_rng(args.seed + k)
        msplit = {}
        by_persona = {}
        for mid, mi in match_index.items():
            hp = [p for p in seat_persona.get(mid, {}).values() if p in pid_index]
            if hp:
                by_persona.setdefault(hp[0], []).append(mi)
        for hp, mis in by_persona.items():
            perm = list(rng.permutation(sorted(mis)))
            n_val, n_test = round(len(perm) * args.val), round(len(perm) * args.test)
            for j, mi in enumerate(perm):
                mi = int(mi)
                s = "val" if j < n_val else ("test" if j < n_val + n_test else "train")
                msplit[mi] = s
                matches_per_split[s].append(offset + mi)

        n0, missing = len(obs), 0
        for f in sorted(glob.glob(os.path.join(log_dir, "match_*.jsonl"))):
            mid = os.path.basename(f)[6:-6]
            if mid not in match_index or mid not in seat_persona:
                missing += 1
                continue
            mi = match_index[mid]
            prev = {}
            for line in open(f, encoding="utf-8"):
                r = json.loads(line)
                p = seat_persona[mid].get(r["slot"], "")
                last = prev.get(r["slot"])
                prev[r["slot"]] = r
                if last is None or p not in pid_index or p not in style_mean.index:
                    continue
                obs.append(last["obs"])
                qready.append(quick_ready(mid, last["slot"], last["t"]))
                act.append(r["act"])
                sty.append(style_mean.loc[p, AXES].to_numpy())
                per.append(pid_index[p])
                mat.append(offset + mi)
                spl.append(SPLIT_CODES[msplit[mi]])
                slot.append(0 if r["slot"] == "P1" else 1)
                tick.append(r["tick"])
        run_info.append({"run": run, "schedule": sched["name"], "match_offset": offset, "matches": len(match_index),
                         "rows": len(obs) - n0, "logs_not_in_runlog": missing})
        print(f"{run}:{sched['name']}: {len(match_index)} rounds, {len(obs) - n0} human rows (offset {offset}, skipped logs {missing})")
        offset += (max(match_index.values()) + 1) if match_index else 0

    if not obs:
        sys.exit("No human rows.")
    obs = np.asarray(obs, dtype=np.float32); act = np.asarray(act, dtype=np.int8)
    sty = np.asarray(sty, dtype=np.float32); spl = np.asarray(spl, dtype=np.int8)
    os.makedirs(args.out_dir, exist_ok=True)
    np.savez_compressed(os.path.join(args.out_dir, "dataset.npz"), obs=obs, act=act, style_raw=sty,
                        persona=np.asarray(per, dtype=np.int16), match=np.asarray(mat, dtype=np.int16),
                        split=spl, slot=np.asarray(slot, dtype=np.int8), tick=np.asarray(tick, dtype=np.int32),
                        quick_ready=np.asarray(qready, dtype=np.float32))
    train_act = act[spl == SPLIT_CODES["train"]]
    freqs = {}
    for b, name in enumerate(ACTION_BRANCHES):
        vals, counts = np.unique(train_act[:, b], return_counts=True)
        freqs[name] = {int(v): round(float(c) / max(1, len(train_act)), 5) for v, c in zip(vals, counts)}
    table = [{**p, "matches": int(style_n.get(p["id"], 0)),
              "style_mean": {a: round(float(style_mean.loc[p["id"], a]), 5) for a in AXES} if p["id"] in style_mean.index else None,
              "style_std": {a: round(float(style_std.loc[p["id"], a]), 5) for a in AXES} if p["id"] in style_mean.index else None}
             for p in personas]
    rows_per_split = {k: int((spl == v).sum()) for k, v in SPLIT_CODES.items()}
    desc = {"createdUtc": datetime.now(timezone.utc).isoformat(timespec="seconds"), "runs": run_info,
            "split_seed": args.seed, "version": "human v1 (human side only, v2 alignment)",
            "aligned_obs_previous_record": True, "quick_cooldown": args.quick_cooldown,
            "obs_dim": int(obs.shape[1]), "action_branches": ACTION_BRANCHES,
            "action_sizes": [int(act[:, b].max()) + 1 for b in range(act.shape[1])], "style_axes": AXES,
            "style_norm": {**norm, "note": "copied from " + os.path.basename(os.path.normpath(args.norm_from))},
            "split_codes": SPLIT_CODES, "rows_per_split": rows_per_split,
            "matches_per_split": {s: sorted(v) for s, v in matches_per_split.items()},
            "holdout_personas": [], "train_action_freq": freqs, "personas": table}
    json.dump(desc, open(os.path.join(args.out_dir, "dataset.json"), "w", encoding="utf-8"), indent=1)
    print(f"rows {len(obs)}  " + "  ".join(f"{k} {v}" for k, v in rows_per_split.items()))
    for p in table:
        print(f"  {p['id']}: {p['matches']} rounds, style {p['style_mean']}")
    print(f"saved to {args.out_dir}")


if __name__ == "__main__":
    main()
