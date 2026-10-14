"""
Build the behaviour-cloning dataset (thesis: IL agent, step 4).

Input
- ImitationLogs/<run>/match_<matchId>.jsonl  : one row per player every 80 ms (obs[67], act[9], persona, ...)
- Telemetry/<run>/*.json                      : the same matches, used to MEASURE each persona's style
- the schedule JSON (make_schedule.py)        : personas, hold-out flags, schedule index of every match
- Telemetry/<run>/_runlog_<schedule>.csv      : schedule index <-> matchId

Style label of a persona = mean of its per-match style (5 core axes, same extractor as the game) over
all its matches. Stored RAW; the normalisation statistics (mean/std over the TRAINING personas, each
persona weighted equally) are saved in the JSON and applied by the training script and by the game.

Splits
- Rows of hold-out personas -> "unseen" (never used for training or model selection).
- All other rows: split BY MATCH (seeded) into train / val / test. Rows of one match never end up in two
  splits (consecutive frames are almost identical -> a frame-level split would leak).

Outputs (--out-dir)
- dataset.npz : obs float32 [N,67], act int8 [N,9], style_raw float32 [N,5], persona int16 [N],
                match int16 [N] (schedule index), split int8 [N] (0 train, 1 val, 2 test, 3 unseen),
                slot int8 [N] (0 P1, 1 P2), tick int32 [N]
- dataset.json: axes, persona table (knobs, hold-out, measured style mean/std/n), normalisation stats,
                split -> matches, row counts, action-class frequencies (train), source files and seed
"""

import argparse
import csv
import glob
import json
import os
import sys
from datetime import datetime, timezone

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import synthetic_style_report as SR  # noqa: E402  (same feature extraction as the game)

AXES = ["attack_rate", "mobility_rate", "defense_rate", "mobility_time_ratio", "block_time_ratio"]
ACTION_BRANCHES = ["move", "jump", "drop", "light", "heavy", "block", "special", "charge", "parry"]
SPLIT_CODES = {"train": 0, "val": 1, "test": 2, "unseen": 3}


def default_root():
    return os.path.join(os.path.expanduser("~"), "Documents", "My Games", "Head of Hell")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--run", default="Synthetic_main")
    ap.add_argument("--root", default=default_root(), help="folder that contains Telemetry/ and ImitationLogs/")
    ap.add_argument("--schedule", default=os.path.normpath(os.path.join(HERE, "..", "schedules", "schedule_main.json")))
    ap.add_argument("--out-dir", default=None, help="default: <root>/ImitationLogs/_datasets/<run>_v1")
    ap.add_argument("--val", type=float, default=0.15)
    ap.add_argument("--test", type=float, default=0.15)
    ap.add_argument("--seed", type=int, default=20261014)
    ap.add_argument("--version", default="v2",
                    help="v1 = obs and act of the same record (as recorded). v2 = obs of the PREVIOUS record paired with the "
                         "actions of this record (the recorder captures obs at the record time, AFTER the presses of the "
                         "last 80 ms -> v1 leaks the action into the observation, e.g. HeavyAttacking=1 'predicts' heavy). "
                         "v2 also adds quick_ready (Skipler Blink ready, rebuilt from telemetry Quick events).")
    ap.add_argument("--quick-cooldown", type=float, default=3.14, help="Blink time 0.14 s + cooldown 3 s")
    args = ap.parse_args()

    tel_dir = os.path.join(args.root, "Telemetry", args.run)
    log_dir = os.path.join(args.root, "ImitationLogs", args.run)
    out_dir = args.out_dir or os.path.join(args.root, "ImitationLogs", "_datasets", f"{args.run}_{args.version}")
    aligned = args.version != "v1"

    sched = json.load(open(args.schedule, encoding="utf-8"))
    personas = sched["personas"]
    pid_index = {p["id"]: i for i, p in enumerate(personas)}
    holdout = {p["id"] for p in personas if p["holdout"]}

    runlog_path = os.path.join(tel_dir, f"_runlog_{sched['name']}.csv")
    runlog = list(csv.DictReader(open(runlog_path, encoding="utf-8")))
    match_index = {r["matchId"]: int(r["index"]) for r in runlog}

    # ---- 1. persona style labels (measured) ----
    rounds = SR.load_rounds(tel_dir)
    rounds = rounds[rounds["persona"].isin(pid_index)]
    g = rounds.groupby("persona")[AXES]
    style_mean, style_std, style_n = g.mean(), g.std(), g.size()

    train_ids = [p["id"] for p in personas if p["id"] not in holdout]
    norm_mean = style_mean.loc[train_ids].mean()
    norm_std = style_mean.loc[train_ids].std()

    # ---- 2. split matches ----
    rng = np.random.default_rng(args.seed)
    all_idx = sorted(match_index.values())
    perm = list(rng.permutation(all_idx))
    n_val, n_test = round(len(perm) * args.val), round(len(perm) * args.test)
    match_split = {}
    for k, m in enumerate(perm):
        match_split[int(m)] = "val" if k < n_val else ("test" if k < n_val + n_test else "train")

    # ---- telemetry Quick events (for quick_ready) ----
    quick_times, fight_start = {}, {}
    for tf in glob.glob(os.path.join(tel_dir, "*.json")):
        doc = json.load(open(tf, encoding="utf-8-sig"))
        fight_start[doc["matchId"]] = float(doc.get("fightStartT") or 0.0)
        q = {"P1": [], "P2": []}
        for e in doc["events"]:
            if e.get("eventType") == "Action" and e.get("actionType") == "Quick" and e.get("actorId") in q:
                q[e["actorId"]].append(float(e["t"]))
        quick_times[doc["matchId"]] = {k: np.array(v) for k, v in q.items()}
    LOG_TO_TEL_OFFSET = 0.045   # record time in telemetry clock ~ fightStartT + t + 0.045 (measured on 252 Quick events)

    def quick_ready(mid, slot_id, t_rec):
        qt = quick_times.get(mid, {}).get(slot_id)
        if qt is None or len(qt) == 0:
            return 1.0
        T = fight_start[mid] + t_rec + LOG_TO_TEL_OFFSET
        recent = qt[(qt <= T) & (qt > T - args.quick_cooldown)]
        return 0.0 if len(recent) else 1.0

    # ---- 3. rows ----
    obs, act, sty, per, mat, spl, slot, tick, qready = [], [], [], [], [], [], [], [], []
    files = sorted(glob.glob(os.path.join(log_dir, "match_*.jsonl")))
    missing = 0
    for f in files:
        mid = os.path.basename(f)[6:-6]
        if mid not in match_index:
            missing += 1
            continue
        mi = match_index[mid]
        prev = {}
        for line in open(f, encoding="utf-8"):
            r = json.loads(line)
            p = r["persona"]
            if aligned:
                last = prev.get(r["slot"])
                prev[r["slot"]] = r
                if last is None:            # first record of this player: no previous observation
                    continue
                o_rec = last
            else:
                o_rec = r
            obs.append(o_rec["obs"])
            qready.append(quick_ready(mid, o_rec["slot"], o_rec["t"]))
            act.append(r["act"])
            sty.append(style_mean.loc[p, AXES].to_numpy())
            per.append(pid_index[p])
            mat.append(mi)
            spl.append(SPLIT_CODES["unseen" if p in holdout else match_split[mi]])
            slot.append(0 if r["slot"] == "P1" else 1)
            tick.append(r["tick"])
    if missing:
        print(f"[warn] {missing} log files not in the run log were skipped")

    obs = np.asarray(obs, dtype=np.float32)
    act = np.asarray(act, dtype=np.int8)
    sty = np.asarray(sty, dtype=np.float32)
    spl = np.asarray(spl, dtype=np.int8)

    os.makedirs(out_dir, exist_ok=True)
    np.savez_compressed(os.path.join(out_dir, "dataset.npz"), obs=obs, act=act, style_raw=sty,
                        persona=np.asarray(per, dtype=np.int16), match=np.asarray(mat, dtype=np.int16),
                        split=spl, slot=np.asarray(slot, dtype=np.int8), tick=np.asarray(tick, dtype=np.int32),
                        quick_ready=np.asarray(qready, dtype=np.float32))

    # ---- 4. description ----
    train_act = act[spl == SPLIT_CODES["train"]]
    freqs = {}
    for b, name in enumerate(ACTION_BRANCHES):
        vals, counts = np.unique(train_act[:, b], return_counts=True)
        freqs[name] = {int(v): round(float(c) / len(train_act), 5) for v, c in zip(vals, counts)}

    persona_table = []
    for p in personas:
        persona_table.append({
            **p,
            "matches": int(style_n.get(p["id"], 0)),
            "style_mean": {a: round(float(style_mean.loc[p["id"], a]), 5) for a in AXES},
            "style_std": {a: round(float(style_std.loc[p["id"], a]), 5) for a in AXES},
        })

    rows_per_split = {k: int((spl == v).sum()) for k, v in SPLIT_CODES.items()}
    desc = {
        "createdUtc": datetime.now(timezone.utc).isoformat(timespec="seconds"),
        "run": args.run, "schedule": sched["name"], "split_seed": args.seed,
        "version": args.version, "aligned_obs_previous_record": aligned, "quick_cooldown": args.quick_cooldown,
        "obs_dim": int(obs.shape[1]), "action_branches": ACTION_BRANCHES,
        "action_sizes": [int(act[:, b].max()) + 1 for b in range(act.shape[1])],
        "style_axes": AXES,
        "style_norm": {"mean": {a: float(norm_mean[a]) for a in AXES}, "std": {a: float(norm_std[a]) for a in AXES},
                       "note": "z = (style_raw - mean) / std; stats over the training personas, one value per persona"},
        "split_codes": SPLIT_CODES,
        "rows_per_split": rows_per_split,
        "matches_per_split": {s: sorted(m for m, v in match_split.items() if v == s) for s in ("train", "val", "test")},
        "holdout_personas": sorted(holdout),
        "train_action_freq": freqs,
        "personas": persona_table,
    }
    with open(os.path.join(out_dir, "dataset.json"), "w", encoding="utf-8") as f:
        json.dump(desc, f, indent=1)

    print(f"rows {len(obs)}  " + "  ".join(f"{k} {v}" for k, v in rows_per_split.items()))
    print(f"matches train {len(desc['matches_per_split']['train'])} val {len(desc['matches_per_split']['val'])} "
          f"test {len(desc['matches_per_split']['test'])}")
    print(f"saved to {out_dir}")


if __name__ == "__main__":
    main()
