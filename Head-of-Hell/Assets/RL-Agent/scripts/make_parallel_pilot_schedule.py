"""
Validation pilot of the parallel build runner (thesis): the FIRST N entries of schedule_main (same personas, same
order), played again by the build in locked step. validate_parallel.py compares them with Synthetic_main (Editor, x1).
"""
import argparse
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))
SCHED = os.path.normpath(os.path.join(HERE, "..", "schedules"))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--n", type=int, default=80)
    ap.add_argument("--name", default="schedule_parallel_pilot")
    args = ap.parse_args()
    src = json.load(open(os.path.join(SCHED, "schedule_main.json"), encoding="utf-8"))
    matches = src["matches"][:args.n]
    used = {m["p1"] for m in matches} | {m["p2"] for m in matches}
    doc = {"name": args.name, "seed": src.get("seed", 0),
           "purpose": f"parallel-build validation: first {args.n} entries of schedule_main, compare with Synthetic_main",
           "personas": [p for p in src["personas"] if p["id"] in used], "matches": matches}
    with open(os.path.join(SCHED, args.name + ".json"), "w", encoding="utf-8") as f:
        json.dump(doc, f, indent=1)
    print(f"{len(matches)} matches, {len(doc['personas'])} personas -> {args.name}.json")


if __name__ == "__main__":
    main()
