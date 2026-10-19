"""
Parallel data generation with the GAME BUILD (thesis: IL agent).
Starts N copies of the build in data-generation mode (-ilgen, see Scripts/Thesis/ILGenBootstrap.cs). Each copy plays
one shard of the schedule in locked step (every frame = exactly 1/60 game-second, as fast as the PC allows) and writes
to its own folders  Telemetry/<out>_s{k}of{N}  and  ImitationLogs/<out>_s{k}of{N}.  Afterwards merge_shards.py joins
them into  <out>  so that every existing analysis script works unchanged.

Shard modes:
  index : entry i -> shard i % N                (style eval, collection: entries are independent)
  pair  : every persona PAIR stays in one shard  (convergence: the online EMA needs the whole sequence of a pair)

Resume: start the same command again; every shard continues after the last match in its own run log.

Example:
  python run_parallel.py --exe "C:/Builds/HoH_ILGen/HoH.exe" --schedule schedule_parallel_pilot --out Parallel_pilot --n 4 --merge
"""
import argparse
import os
import subprocess
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
SCHED_DIR = os.path.normpath(os.path.join(HERE, "..", "schedules"))


def default_root() -> str:
    return os.path.join(os.path.expanduser("~"), "Documents", "My Games", "Head of Hell")


def resolve_schedule(s: str) -> str:
    if os.path.isfile(s):
        return os.path.abspath(s)
    p = os.path.join(SCHED_DIR, s if s.endswith(".json") else s + ".json")
    if os.path.isfile(p):
        return p
    sys.exit(f"Schedule not found: {s}")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--exe", required=True, help="the game build (HoH.exe) that contains ILDataScene")
    ap.add_argument("--schedule", required=True, help="schedule json (path, or name in ../schedules)")
    ap.add_argument("--out", required=True, help="output folder name (shards get _s{k}of{N})")
    ap.add_argument("--n", type=int, default=4, help="number of parallel game processes")
    ap.add_argument("--shardmode", choices=["index", "pair"], default="index")
    ap.add_argument("--ilmodel", default=None, help="IL model json (default: the one set in the scene)")
    ap.add_argument("--fullrender", action="store_true", help="keep the stage graphics (comparison only)")
    ap.add_argument("--width", type=int, default=640)
    ap.add_argument("--height", type=int, default=360)
    ap.add_argument("--root", default=default_root())
    ap.add_argument("--merge", action="store_true", help="run merge_shards.py when all processes have finished")
    args = ap.parse_args()

    if not os.path.isfile(args.exe):
        sys.exit(f"Build not found: {args.exe}")
    sched = resolve_schedule(args.schedule)
    log_dir = os.path.join(args.root, "Logs_ilgen")
    os.makedirs(log_dir, exist_ok=True)

    procs = []
    t0 = time.time()
    for k in range(args.n):
        cmd = [args.exe, "-ilgen", "-schedule", sched, "-out", args.out,
               "-shard", f"{k}/{args.n}", "-shardmode", args.shardmode,
               "-screen-fullscreen", "0", "-screen-width", str(args.width), "-screen-height", str(args.height),
               "-logFile", os.path.join(log_dir, f"{args.out}_s{k}of{args.n}.log")]
        if args.ilmodel:
            cmd += ["-ilmodel", os.path.abspath(args.ilmodel)]
        if args.fullrender:
            cmd += ["-fullrender"]
        procs.append(subprocess.Popen(cmd))
        print(f"[run] shard {k}/{args.n} started (pid {procs[-1].pid})")
        time.sleep(1.0)   # small gap: the processes do not start loading at exactly the same moment

    codes = [p.wait() for p in procs]
    mins = (time.time() - t0) / 60
    print(f"[run] all {args.n} processes finished in {mins:.1f} min, exit codes {codes}")
    print(f"[run] Player logs: {log_dir}")

    if args.merge:
        r = subprocess.call([sys.executable, os.path.join(HERE, "merge_shards.py"), "--out", args.out,
                             "--n", str(args.n), "--schedule", sched, "--root", args.root])
        sys.exit(r)


if __name__ == "__main__":
    main()
