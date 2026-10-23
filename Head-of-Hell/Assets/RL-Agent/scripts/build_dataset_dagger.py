"""
DAgger dataset (thesis, 23/10/2026): states visited by the IL AGENT, labelled with what the scripted TEACHER would have
done there (shadow expert, ImitationLoggerV2 "expertAct").

Input: DAgger rollout runs (make_dagger_schedule.py) as run_folder:schedule_name, e.g. Dagger_it1:schedule_dagger_it1.
Only the agent rows WITH an expert label are used (agent D_<T> with shadow expert T). The control agents C_<T> (no
shadow) and the scripted opponents are skipped - their data are not DAgger data.

Row = one agent decision, same alignment as every other dataset (v2): obs of the PREVIOUS record -> label of THIS record.
  act       = expertAct  (the TARGET: the teacher's choice in the agent's situation)
  hist_act  = the agent's OWN act (the STATE part: the action-history features must describe what the agent itself
              did before, exactly like ILAgentController computes them in game - not what the teacher would have done)
  style_raw = the v6 style label of the expert persona T (the agent was asked for exactly this style)
  persona   = index of T in the base dataset (--base-data, e.g. Synthetic_v6), so persona ids mean the same
  quick_ready from the telemetry of the agent's own quicks (the agent's real cooldown)
Split by match inside every persona (train/val/test). Style normalisation copied from the base dataset.
Also reports the agreement agent <-> expert per action branch.
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
from build_dataset import AXES, ACTION_BRANCHES, SPLIT_CODES, default_root  # noqa: E402

LOG_TO_TEL_OFFSET = 0.045


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--runs", nargs="+", required=True, help="run_folder:schedule_name")
    ap.add_argument("--base-data", required=True, help="bot dataset dir (style labels, persona index, style_norm), e.g. .../Synthetic_v6")
    ap.add_argument("--root", default=default_root())
    ap.add_argument("--out-dir", required=True)
    ap.add_argument("--val", type=float, default=0.15)
    ap.add_argument("--test", type=float, default=0.15)
    ap.add_argument("--seed", type=int, default=20261023)
    ap.add_argument("--quick-cooldown", type=float, default=3.14)
    ap.add_argument("--part", default=None, help="k/N: process only every N-th match (from k), save to <out-dir>/_parts "
                    "(big runs in steps: the local shell has a time limit); then run once more with --combine")
    ap.add_argument("--combine", action="store_true", help="join the saved parts, split, write the dataset")
    args = ap.parse_args()
    if args.part or args.combine:
        return parts_main(args)

    base = json.load(open(os.path.join(args.base_data, "dataset.json"), encoding="utf-8"))
    base_personas = base["personas"]
    pid_index = {p["id"]: i for i, p in enumerate(base_personas)}
    style = {p["id"]: [p["style_mean"][a] for a in AXES] for p in base_personas if p.get("style_mean")}
    holdout = set(base.get("holdout_personas", []))

    obs, act, hact, sty, per, mat, spl, slot, tick, qready = [], [], [], [], [], [], [], [], [], []
    matches_per_split = {"train": [], "val": [], "test": []}
    offset, run_info = 0, []
    for k, spec in enumerate(args.runs):
        run, sname = spec.split(":")
        tel_dir = os.path.join(args.root, "Telemetry", run)
        log_dir = os.path.join(args.root, "ImitationLogs", run)
        runlog = [r for r in csv.DictReader(open(os.path.join(tel_dir, f"_runlog_{sname}.csv"), encoding="utf-8"))
                  if r["matchId"] and r["matchId"] != "ABANDONED"]
        match_index = {r["matchId"]: i for i, r in enumerate(runlog)}   # own number per match (run-log order)

        # telemetry: fight start + quick times per seat
        fight_start, quick_times = {}, {}
        for tf in glob.glob(os.path.join(tel_dir, "match_*.json")):
            doc = json.load(open(tf, encoding="utf-8-sig"))
            if doc["matchId"] not in match_index:
                continue
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

        # pass 1: which expert persona every logged match belongs to (for the split by match inside each persona)
        logs, expert_of = {}, {}
        for f in sorted(glob.glob(os.path.join(log_dir, "match_*.jsonl"))):
            mid = os.path.basename(f)[6:-6]
            if mid not in match_index:
                continue
            rows = [json.loads(line) for line in open(f, encoding="utf-8")]
            ex = {r["expertPersona"] for r in rows if r.get("expertAct")}
            if len(ex) == 1:
                logs[mid], expert_of[mid] = rows, ex.pop()
        rng = np.random.default_rng(args.seed + k)
        msplit = {}
        for t in sorted(set(expert_of.values())):
            mis = sorted(match_index[m] for m, e in expert_of.items() if e == t)
            perm = [int(x) for x in rng.permutation(mis)]
            n_val, n_test = round(len(perm) * args.val), round(len(perm) * args.test)
            for j, mi in enumerate(perm):
                s = "val" if j < n_val else ("test" if j < n_val + n_test else "train")
                msplit[mi] = s
                matches_per_split[s].append(offset + mi)

        n0, bad_persona, agree = len(obs), 0, np.zeros(len(ACTION_BRANCHES))
        for mid, rows in logs.items():
            t, mi = expert_of[mid], match_index[mid]
            if t not in style or t in holdout:
                bad_persona += 1
                continue
            prev = {}
            for r in rows:
                last = prev.get(r["slot"])
                prev[r["slot"]] = r
                if last is None or not r.get("expertAct"):
                    continue                                   # first record, or not the agent with the shadow
                obs.append(last["obs"])
                qready.append(quick_ready(mid, last["slot"], last["t"]))
                act.append(r["expertAct"])
                hact.append(r["act"])
                agree += np.array(r["act"]) == np.array(r["expertAct"])
                sty.append(style[t])
                per.append(pid_index[t])
                mat.append(offset + mi)
                spl.append(SPLIT_CODES[msplit[mi]])
                slot.append(0 if r["slot"] == "P1" else 1)
                tick.append(r["tick"])
        n = len(obs) - n0
        info = {"run": run, "schedule": sname, "match_offset": offset, "matches": len(logs), "rows": n,
                "agreement_agent_expert": {b: round(float(v) / max(1, n), 4) for b, v in zip(ACTION_BRANCHES, agree)},
                "matches_skipped_persona": bad_persona}
        run_info.append(info)
        print(f"{run}: {len(logs)} DAgger matches, {n} rows (offset {offset}); agreement {info['agreement_agent_expert']}")
        offset += len(runlog)

    if not obs:
        sys.exit("No DAgger rows.")
    obs = np.asarray(obs, dtype=np.float32); act = np.asarray(act, dtype=np.int8); hact = np.asarray(hact, dtype=np.int8)
    sty = np.asarray(sty, dtype=np.float32); spl = np.asarray(spl, dtype=np.int8)
    os.makedirs(args.out_dir, exist_ok=True)
    np.savez_compressed(os.path.join(args.out_dir, "dataset.npz"), obs=obs, act=act, hist_act=hact, style_raw=sty,
                        persona=np.asarray(per, dtype=np.int16), match=np.asarray(mat, dtype=np.int32),
                        split=spl, slot=np.asarray(slot, dtype=np.int8), tick=np.asarray(tick, dtype=np.int32),
                        quick_ready=np.asarray(qready, dtype=np.float32))
    tr = spl == SPLIT_CODES["train"]
    freqs = {}
    for b, name in enumerate(ACTION_BRANCHES):
        vals, counts = np.unique(act[tr, b], return_counts=True)
        freqs[name] = {int(v): round(float(c) / max(1, tr.sum()), 5) for v, c in zip(vals, counts)}
    rows_per_split = {kk: int((spl == v).sum()) for kk, v in SPLIT_CODES.items()}
    used = sorted({base_personas[i]["id"] for i in set(per)})
    desc = {"createdUtc": datetime.now(timezone.utc).isoformat(timespec="seconds"), "runs": run_info,
            "split_seed": args.seed, "version": "dagger v1 (agent states, shadow-expert labels, v2 alignment)",
            "aligned_obs_previous_record": True, "quick_cooldown": args.quick_cooldown,
            "history_from": "hist_act (the agent's own actions)", "label": "expertAct",
            "obs_dim": int(obs.shape[1]), "action_branches": ACTION_BRANCHES,
            "action_sizes": [int(max(act[:, b].max(), hact[:, b].max())) + 1 for b in range(act.shape[1])],
            "style_axes": AXES, "style_norm": {**base["style_norm"], "note": "copied from " + os.path.basename(os.path.normpath(args.base_data))},
            "split_codes": SPLIT_CODES, "rows_per_split": rows_per_split,
            "matches_per_split": {s: sorted(v) for s, v in matches_per_split.items()},
            "holdout_personas": [], "train_action_freq": freqs, "personas": base_personas, "expert_personas_used": used}
    json.dump(desc, open(os.path.join(args.out_dir, "dataset.json"), "w", encoding="utf-8"), indent=1)
    print(f"rows {len(obs)}  " + "  ".join(f"{kk} {v}" for kk, v in rows_per_split.items()))
    print(f"expert personas {len(used)}; saved to {args.out_dir}")


# ---------------- big runs in steps (same rows / labels / split as main()) ----------------
def load_base(args):
    base = json.load(open(os.path.join(args.base_data, "dataset.json"), encoding="utf-8"))
    pid_index = {p["id"]: i for i, p in enumerate(base["personas"])}
    style = {p["id"]: [p["style_mean"][a] for a in AXES] for p in base["personas"] if p.get("style_mean")}
    return base, pid_index, style, set(base.get("holdout_personas", []))


def parts_main(args):
    base, pid_index, style, holdout = load_base(args)
    work = os.path.join(args.out_dir, "_parts")
    os.makedirs(work, exist_ok=True)
    if args.part:
        k, n = (int(v) for v in args.part.split("/"))
        for ri, spec in enumerate(args.runs):
            run, sname = spec.split(":")
            tel_dir = os.path.join(args.root, "Telemetry", run)
            log_dir = os.path.join(args.root, "ImitationLogs", run)
            runlog = [r for r in csv.DictReader(open(os.path.join(tel_dir, f"_runlog_{sname}.csv"), encoding="utf-8"))
                      if r["matchId"] and r["matchId"] != "ABANDONED"]
            match_index = {r["matchId"]: i for i, r in enumerate(runlog)}
            mids = sorted(m for m in match_index if match_index[m] % n == k)
            cols = {c: [] for c in ("obs", "act", "hist_act", "style_raw", "persona", "mi", "slot", "tick", "quick_ready")}
            experts, agree, rows_n = {}, np.zeros(len(ACTION_BRANCHES)), 0
            for mid in mids:
                f = os.path.join(log_dir, f"match_{mid}.jsonl")
                tf = glob.glob(os.path.join(tel_dir, f"match_*_{mid}.json"))
                if not os.path.isfile(f) or not tf:
                    continue
                rows = [json.loads(line) for line in open(f, encoding="utf-8")]
                ex = {r["expertPersona"] for r in rows if r.get("expertAct")}
                if len(ex) != 1:
                    continue                                   # control match (no shadow) or broken
                t = ex.pop()
                if t not in style or t in holdout:
                    continue
                doc = json.load(open(tf[0], encoding="utf-8-sig"))
                fs = float(doc.get("fightStartT") or 0.0)
                q = {"P1": [], "P2": []}
                for e in doc["events"]:
                    if e.get("eventType") == "Action" and e.get("actionType") == "Quick" and e.get("actorId") in q:
                        q[e["actorId"]].append(float(e["t"]))
                q = {kk: np.array(v) for kk, v in q.items()}
                experts[match_index[mid]] = t
                prev = {}
                for r in rows:
                    last = prev.get(r["slot"])
                    prev[r["slot"]] = r
                    if last is None or not r.get("expertAct"):
                        continue
                    qt = q[last["slot"]]; T = fs + last["t"] + LOG_TO_TEL_OFFSET
                    cols["quick_ready"].append(0.0 if len(qt) and ((qt <= T) & (qt > T - args.quick_cooldown)).any() else 1.0)
                    cols["obs"].append(last["obs"]); cols["act"].append(r["expertAct"]); cols["hist_act"].append(r["act"])
                    cols["style_raw"].append(style[t]); cols["persona"].append(pid_index[t]); cols["mi"].append(match_index[mid])
                    cols["slot"].append(0 if r["slot"] == "P1" else 1); cols["tick"].append(r["tick"])
                    agree += np.array(r["act"]) == np.array(r["expertAct"]); rows_n += 1
            dt = {"obs": np.float32, "act": np.int8, "hist_act": np.int8, "style_raw": np.float32, "persona": np.int16,
                  "mi": np.int32, "slot": np.int8, "tick": np.int32, "quick_ready": np.float32}
            np.savez_compressed(os.path.join(work, f"run{ri}_part{k}of{n}.npz"),
                                **{c: np.asarray(v, dtype=dt[c]) for c, v in cols.items()})
            json.dump({"run": run, "schedule": sname, "runlog_len": len(runlog), "experts": {str(m): e for m, e in experts.items()},
                       "agree": agree.tolist(), "rows": rows_n},
                      open(os.path.join(work, f"run{ri}_part{k}of{n}.json"), "w"))
            print(f"{run} part {k}/{n}: {len(experts)} DAgger matches, {rows_n} rows")
        return

    # --combine
    obs, act, hact, sty, per, mat, spl, slot, tick, qready = [], [], [], [], [], [], [], [], [], []
    matches_per_split = {"train": [], "val": [], "test": []}
    offset, run_info = 0, []
    for ri, spec in enumerate(args.runs):
        run, sname = spec.split(":")
        parts = sorted(glob.glob(os.path.join(work, f"run{ri}_part*.json")))
        metas = [json.load(open(p)) for p in parts]
        ns = {os.path.basename(p).split("of")[-1] for p in parts}
        if len(ns) != 1 or len(parts) != int(ns.pop().split(".")[0]):
            sys.exit(f"{run}: parts incomplete or mixed: {[os.path.basename(p) for p in parts]}")
        experts = {int(m): e for meta in metas for m, e in meta["experts"].items()}
        rng = np.random.default_rng(args.seed + ri)                 # same procedure as main()
        msplit = {}
        for t in sorted(set(experts.values())):
            mis = sorted(m for m, e in experts.items() if e == t)
            perm = [int(x) for x in rng.permutation(mis)]
            n_val, n_test = round(len(perm) * args.val), round(len(perm) * args.test)
            for j, mi in enumerate(perm):
                sp = "val" if j < n_val else ("test" if j < n_val + n_test else "train")
                msplit[mi] = sp
                matches_per_split[sp].append(offset + mi)
        agree, n_rows = np.zeros(len(ACTION_BRANCHES)), 0
        for p, meta in zip(parts, metas):
            agree += np.array(meta["agree"]); n_rows += meta["rows"]
            if meta["rows"] == 0:
                continue                                       # e.g. a part with control matches only
            d = np.load(p[:-5] + ".npz")
            obs.append(d["obs"]); act.append(d["act"]); hact.append(d["hist_act"]); sty.append(d["style_raw"])
            per.append(d["persona"]); mat.append(d["mi"] + offset); slot.append(d["slot"]); tick.append(d["tick"])
            qready.append(d["quick_ready"]); spl.append(np.array([SPLIT_CODES[msplit[int(m)]] for m in d["mi"]], np.int8))
        run_info.append({"run": run, "schedule": sname, "match_offset": offset, "matches": len(experts), "rows": n_rows,
                         "agreement_agent_expert": {b: round(float(v) / max(1, n_rows), 4) for b, v in zip(ACTION_BRANCHES, agree)}})
        print(f"{run}: {len(experts)} DAgger matches, {n_rows} rows; agreement {run_info[-1]['agreement_agent_expert']}")
        offset += metas[0]["runlog_len"]
    cat = np.concatenate
    obs, act, hact, sty, spl = cat(obs), cat(act), cat(hact), cat(sty), cat(spl)
    per, mat, slot, tick, qready = cat(per), cat(mat), cat(slot), cat(tick), cat(qready)
    write(args, base, obs, act, hact, sty, per, mat, spl, slot, tick, qready, matches_per_split, run_info)


def write(args, base, obs, act, hact, sty, per, mat, spl, slot, tick, qready, matches_per_split, run_info):
    np.savez_compressed(os.path.join(args.out_dir, "dataset.npz"), obs=obs, act=act, hist_act=hact, style_raw=sty,
                        persona=np.asarray(per, dtype=np.int16), match=np.asarray(mat, dtype=np.int32),
                        split=spl, slot=np.asarray(slot, dtype=np.int8), tick=np.asarray(tick, dtype=np.int32),
                        quick_ready=np.asarray(qready, dtype=np.float32))
    tr = spl == SPLIT_CODES["train"]
    freqs = {}
    for b, name in enumerate(ACTION_BRANCHES):
        vals, counts = np.unique(act[tr, b], return_counts=True)
        freqs[name] = {int(v): round(float(c) / max(1, tr.sum()), 5) for v, c in zip(vals, counts)}
    rows_per_split = {kk: int((spl == v).sum()) for kk, v in SPLIT_CODES.items()}
    used = sorted({base["personas"][i]["id"] for i in set(int(x) for x in per)})
    desc = {"createdUtc": datetime.now(timezone.utc).isoformat(timespec="seconds"), "runs": run_info,
            "split_seed": args.seed, "version": "dagger v1 (agent states, shadow-expert labels, v2 alignment)",
            "aligned_obs_previous_record": True, "quick_cooldown": args.quick_cooldown,
            "history_from": "hist_act (the agent's own actions)", "label": "expertAct",
            "obs_dim": int(obs.shape[1]), "action_branches": ACTION_BRANCHES,
            "action_sizes": [int(max(act[:, b].max(), hact[:, b].max())) + 1 for b in range(act.shape[1])],
            "style_axes": AXES, "style_norm": {**base["style_norm"], "note": "copied from " + os.path.basename(os.path.normpath(args.base_data))},
            "split_codes": SPLIT_CODES, "rows_per_split": rows_per_split,
            "matches_per_split": {s: sorted(v) for s, v in matches_per_split.items()},
            "holdout_personas": [], "train_action_freq": freqs, "personas": base["personas"], "expert_personas_used": used}
    json.dump(desc, open(os.path.join(args.out_dir, "dataset.json"), "w", encoding="utf-8"), indent=1)
    print(f"rows {len(obs)}  " + "  ".join(f"{kk} {v}" for kk, v in rows_per_split.items()))
    print(f"expert personas {len(used)}; saved to {args.out_dir}")


if __name__ == "__main__":
    main()
