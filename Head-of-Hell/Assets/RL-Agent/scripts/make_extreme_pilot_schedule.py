"""
Pilot for extending the persona range (thesis, 15/10/2026).

The first human (online test) played outside the style range of the 30 training personas on 4 of 5 axes:
more movement (mobility_rate ~0.72, mobility_time ~0.84) and almost no defense (defense ~0.02, block ~0.01).
A linear fit style ~ knobs over the 30 personas (R^2 0.94-0.97) predicts that corner at low defense + max
mobility knobs - a corner the Latin hypercube never sampled (the high-mobility personas all had high defense).

This pilot checks whether the scripted bot really reaches it (or saturates): a grid of corner personas,
each vs REF (all knobs 0.5), N matches each, sides alternated, interleaved so a stopped run stays balanced.
Knobs stay inside [0, 1] (SetPersonality clamps): no change to the bot.
"""

import argparse
import itertools
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--aggression", type=float, nargs="+", default=[0.2, 0.45, 0.7])
    ap.add_argument("--defense", type=float, nargs="+", default=[0.0, 0.15])
    ap.add_argument("--mobility", type=float, nargs="+", default=[0.9, 1.0])
    ap.add_argument("--matches", type=int, default=5, help="matches per persona")
    ap.add_argument("--name", default="schedule_extreme_pilot")
    ap.add_argument("--out-dir", default=os.path.normpath(os.path.join(HERE, "..", "schedules")))
    args = ap.parse_args()

    personas = [{"id": "REF", "kind": "Scripted", "aggression": .5, "defense": .5, "mobility": .5, "patience": .5,
                 "holdout": False}]
    ids = []
    for a, d, m in itertools.product(args.aggression, args.defense, args.mobility):
        pid = f"E_A{round(a * 100):02d}_D{round(d * 100):02d}_M{round(m * 100):02d}"
        ids.append(pid)
        personas.append({"id": pid, "kind": "Scripted", "aggression": a, "defense": d, "mobility": m,
                         "patience": .5, "holdout": False})

    matches = []
    for r in range(args.matches):
        for pid in ids:
            p1, p2 = (pid, "REF") if r % 2 == 0 else ("REF", pid)
            matches.append({"index": len(matches), "round": r + 1, "p1": p1, "p2": p2})

    doc = {"name": args.name, "seed": 0, "purpose": "pilot: can the scripted bot reach the high-mobility / no-defense corner",
           "targets": ids, "personas": personas, "matches": matches}
    os.makedirs(args.out_dir, exist_ok=True)
    with open(os.path.join(args.out_dir, args.name + ".json"), "w", encoding="utf-8") as f:
        json.dump(doc, f, indent=1)
    print(f"{len(ids)} personas x {args.matches} = {len(matches)} matches -> {args.name}.json")


if __name__ == "__main__":
    main()
