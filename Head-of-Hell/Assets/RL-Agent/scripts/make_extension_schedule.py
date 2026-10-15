"""
Extension of the synthetic collection (thesis, 15/10/2026): new personas in the high-mobility / low-defense corner.

Why: the first human played outside the style range of the 30 main personas (more movement, almost no defense).
The Latin hypercube of the main collection covers every knob on its own but never sampled that corner
(low defense + high mobility). schedule_extreme_pilot (60 matches vs REF) showed the scripted bot reaches it with
knobs inside [0, 1] (no change to the bot).

Personas: N new ones "X01".. with a Latin hypercube inside the corner box (default A 0.3-0.9, D 0.0-0.3, M 0.8-1.0);
2 hold-out (never trained on; tests generalisation inside the corner), chosen as in make_schedule.py.

Matches: R rounds. In every round each new persona plays exactly once:
- `--new-pairs` pairs of new personas play each other (corner vs corner; two hold-outs are never paired),
- every other new persona plays a random TRAINING persona of the main collection (the main hold-outs are not used:
  their side would be unusable), balanced so the old opponents are used evenly.
Side: the persona with fewer P1 matches so far is P1. Listed round by round (a stopped run stays balanced).
"""

import argparse
import csv
import json
import os
from datetime import datetime, timezone

import numpy as np

from make_schedule import latin_hypercube, pick_holdout

HERE = os.path.dirname(os.path.abspath(__file__))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--name", default="schedule_ext")
    ap.add_argument("--main-schedule", default=os.path.normpath(os.path.join(HERE, "..", "schedules", "schedule_main.json")))
    ap.add_argument("--personas", type=int, default=10)
    ap.add_argument("--rounds", type=int, default=20, help="matches per new persona")
    ap.add_argument("--new-pairs", type=int, default=2, help="new-vs-new matches per round")
    ap.add_argument("--holdout", type=int, default=2)
    ap.add_argument("--a", type=float, nargs=2, default=[0.3, 0.9])
    ap.add_argument("--d", type=float, nargs=2, default=[0.0, 0.3])
    ap.add_argument("--m", type=float, nargs=2, default=[0.8, 1.0])
    ap.add_argument("--seed", type=int, default=20261015)
    ap.add_argument("--out-dir", default=os.path.normpath(os.path.join(HERE, "..", "schedules")))
    args = ap.parse_args()

    rng = np.random.default_rng(args.seed)
    lo = np.array([args.a[0], args.d[0], args.m[0]])
    hi = np.array([args.a[1], args.d[1], args.m[1]])
    unit = latin_hypercube(args.personas, 3, rng)
    knobs = np.round(lo + unit * (hi - lo), 3)
    ids = [f"X{i + 1:02d}" for i in range(args.personas)]
    # hold-outs: interior of the box (in box coordinates), spread out
    hold = {ids[i] for i in pick_holdout(unit, args.holdout, 0.2, 0.8, rng)}

    main = json.load(open(args.main_schedule, encoding="utf-8"))
    old = [p for p in main["personas"] if not p["holdout"]]
    old_ids = [p["id"] for p in old]
    old_use = {i: 0 for i in old_ids}

    p1 = {i: 0 for i in ids + old_ids}
    played = {i: 0 for i in ids}
    matches = []

    def add(a, b, r):
        # side balance is kept for the NEW personas (the old ones already have 20 balanced matches)
        if b in played:   # new vs new: the one with the lower P1 share is P1
            sa, sb = p1[a] - played[a] / 2, p1[b] - played[b] / 2
            if sa > sb or (sa == sb and rng.random() < 0.5):
                a, b = b, a
        elif p1[a] > played[a] / 2 or (p1[a] == played[a] / 2 and rng.random() < 0.5):   # new vs old
            a, b = b, a
        for x in (a, b):
            if x in played:
                played[x] += 1
        p1[a] += 1
        matches.append({"index": len(matches), "round": r + 1, "p1": a, "p2": b})

    for r in range(args.rounds):
        for _ in range(1000):
            order = list(rng.permutation(ids))
            pairs = [(order[2 * k], order[2 * k + 1]) for k in range(args.new_pairs)]
            if not any(a in hold and b in hold for a, b in pairs):
                break
        rest = order[2 * args.new_pairs:]
        for a, b in pairs:
            add(a, b, r)
        for x in rest:
            least = min(old_use.values())
            cand = [o for o in old_ids if old_use[o] == least]
            o = cand[rng.integers(len(cand))]
            old_use[o] += 1
            add(x, o, r)

    personas = [{"id": ids[i], "aggression": float(knobs[i, 0]), "defense": float(knobs[i, 1]),
                 "mobility": float(knobs[i, 2]), "patience": 0.5, "holdout": ids[i] in hold}
                for i in range(args.personas)]
    personas += [{"id": p["id"], "aggression": p["aggression"], "defense": p["defense"], "mobility": p["mobility"],
                  "patience": p["patience"], "holdout": False} for p in old]

    doc = {"name": args.name, "seed": args.seed, "createdUtc": datetime.now(timezone.utc).isoformat(timespec="seconds"),
           "purpose": "extension: personas in the high-mobility / low-defense corner",
           "cornerBox": {"aggression": args.a, "defense": args.d, "mobility": args.m},
           "matchesPerPersona": args.rounds, "personas": personas, "matches": matches}
    os.makedirs(args.out_dir, exist_ok=True)
    with open(os.path.join(args.out_dir, args.name + ".json"), "w", encoding="utf-8") as f:
        json.dump(doc, f, indent=1)
    with open(os.path.join(args.out_dir, args.name + "_personas.csv"), "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(personas[0].keys()) + ["matches", "p1_matches"])
        w.writeheader()
        for p in personas[:args.personas]:
            w.writerow({**p, "matches": args.rounds, "p1_matches": p1[p["id"]]})

    print(f"{args.personas} new personas (hold-out {sorted(hold)}), {len(matches)} matches")
    for p in personas[:args.personas]:
        print(f"  {p['id']}  A {p['aggression']:.2f}  D {p['defense']:.2f}  M {p['mobility']:.2f}"
              f"{'  HOLD-OUT' if p['holdout'] else ''}  P1 {p1[p['id']]}/{args.rounds}")
    print(f"old opponents used: {len(old_ids)} personas, {min(old_use.values())}-{max(old_use.values())} matches each")


if __name__ == "__main__":
    main()
