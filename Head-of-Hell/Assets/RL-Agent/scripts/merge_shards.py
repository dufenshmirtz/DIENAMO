"""
Joins the shard folders of a parallel run (run_parallel.py) into one folder, so that the existing scripts
(build_dataset_multi.py, analyze_style_eval.py, ...) see a normal run folder.

  Telemetry/<out>_s{k}of{N}/*.json            -> Telemetry/<out>/
  Telemetry/<out>_s{k}of{N}/_runlog_<s>.csv   -> Telemetry/<out>/_runlog_<s>.csv   (sorted by schedule index)
  Telemetry/<out>_s{k}of{N}/_environment_*    -> Telemetry/<out>/_environment_<shard>_*  (one per process)
  ImitationLogs/<out>_s{k}of{N}/*             -> ImitationLogs/<out>/
  ImitationLogs/_style_tracker/agent_style_log_<out>_s{k}of{N}.csv -> agent_style_log_<out>.csv

Files are COPIED (the shard folders stay as they are). Refuses to overwrite a different file with the same name.
Reports: matches per shard, missing / duplicate schedule indices, ABANDONED entries.
"""
import argparse
import csv
import filecmp
import json
import os
import shutil
import sys


def default_root() -> str:
    return os.path.join(os.path.expanduser("~"), "Documents", "My Games", "Head of Hell")


def copy_dir(src, dst, shard=""):
    n = 0
    if not os.path.isdir(src):
        return 0
    os.makedirs(dst, exist_ok=True)
    for name in sorted(os.listdir(src)):
        s = os.path.join(src, name)
        if not os.path.isfile(s) or name.startswith("_runlog_"):
            continue
        # environment files: every process writes one with the same name (same start second) -> keep all, tagged by shard
        d = os.path.join(dst, f"_environment_{shard}_" + name[len("_environment_"):]
                         if name.startswith("_environment_") and shard else name)
        if os.path.exists(d):
            if filecmp.cmp(s, d, shallow=False):
                continue
            sys.exit(f"[merge] CONFLICT: {d} exists with different content. Nothing overwritten; stopping.")
        shutil.copy2(s, d)
        n += 1
    return n


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    ap.add_argument("--n", type=int, required=True)
    ap.add_argument("--schedule", required=True, help="schedule json path (its file name = run log name)")
    ap.add_argument("--root", default=default_root())
    args = ap.parse_args()

    sched_name = os.path.splitext(os.path.basename(args.schedule))[0]
    total = len(json.load(open(args.schedule, encoding="utf-8"))["matches"])
    tel_out = os.path.join(args.root, "Telemetry", args.out)
    imi_out = os.path.join(args.root, "ImitationLogs", args.out)

    rows, header = [], None
    for k in range(args.n):
        shard = f"{args.out}_s{k}of{args.n}"
        nt = copy_dir(os.path.join(args.root, "Telemetry", shard), tel_out, shard)
        ni = copy_dir(os.path.join(args.root, "ImitationLogs", shard), imi_out)
        rl = os.path.join(args.root, "Telemetry", shard, f"_runlog_{sched_name}.csv")
        nr = 0
        if os.path.isfile(rl):
            with open(rl, encoding="utf-8") as f:
                r = csv.reader(f)
                h = next(r)
                header = header or h
                for line in r:
                    if line:
                        rows.append(line)
                        nr += 1
        print(f"[merge] {shard}: run log {nr} entries, copied {nt} telemetry + {ni} imitation files")

    if header is None:
        sys.exit("[merge] No run logs found.")
    rows.sort(key=lambda x: int(x[0]))
    idx = [int(x[0]) for x in rows]
    dup = sorted({i for i in idx if idx.count(i) > 1})
    missing = sorted(set(range(total)) - set(idx))
    abandoned = [x for x in rows if x[1] == "ABANDONED"]
    os.makedirs(tel_out, exist_ok=True)
    with open(os.path.join(tel_out, f"_runlog_{sched_name}.csv"), "w", encoding="utf-8", newline="") as f:
        f.write(",".join(header) + "\n")
        for x in rows:
            f.write(",".join(x) + "\n")

    st = os.path.join(args.root, "ImitationLogs", "_style_tracker")
    agent_lines, agent_header = [], None
    for k in range(args.n):
        p = os.path.join(st, f"agent_style_log_{args.out}_s{k}of{args.n}.csv")
        if os.path.isfile(p):
            with open(p, encoding="utf-8") as f:
                lines = f.read().splitlines()
            if lines:
                agent_header = agent_header or lines[0]
                agent_lines += lines[1:]
    if agent_header:
        with open(os.path.join(st, f"agent_style_log_{args.out}.csv"), "w", encoding="utf-8", newline="") as f:
            f.write(agent_header + "\n" + "\n".join(agent_lines) + "\n")
        print(f"[merge] agent style log: {len(agent_lines)} lines -> agent_style_log_{args.out}.csv")

    print(f"[merge] {args.out}: {len(rows)}/{total} schedule entries ({len(abandoned)} ABANDONED), "
          f"missing {len(missing)}{' ' + str(missing[:20]) if missing else ''}, duplicates {dup if dup else 0}")


if __name__ == "__main__":
    main()
