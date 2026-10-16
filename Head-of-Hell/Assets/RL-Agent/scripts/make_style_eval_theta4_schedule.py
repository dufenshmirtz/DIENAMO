"""
Schedule for the STYLE-FIDELITY experiment of theta4 (same design as make_style_eval_schedule.py / theta3).

Agent "T4_<persona>" (IL agent with the persona's measured style from dataset v3) and the scripted teacher, both vs
REF (all knobs 0.5), N matches each, sides alternated, interleaved.
- The 12 theta3 targets: ONLY the agent plays here. Their teachers already played exactly these matches vs REF in
  StyleEval_theta3 (the scripted bot and REF are unchanged since), so that data is reused -> theta3 and theta4 are
  compared on the same targets against the same teacher data.
- New corner targets (default: hold-outs X02 X03 + training X05 X10): agent AND teacher play.
"""

import argparse
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dataset-json", required=True)
    ap.add_argument("--old-targets", nargs="+",
                    default=["S07", "S08", "S15", "S20", "S22", "S24", "S04", "S13", "S14", "S16", "S17", "S21"])
    ap.add_argument("--new-targets", nargs="+", default=["X02", "X03", "X05", "X10"])
    ap.add_argument("--matches", type=int, default=8)
    ap.add_argument("--name", default="schedule_style_eval_theta4")
    ap.add_argument("--out-dir", default=os.path.normpath(os.path.join(HERE, "..", "schedules")))
    args = ap.parse_args()

    P = {p["id"]: p for p in json.load(open(args.dataset_json, encoding="utf-8"))["personas"]}
    personas = [{"id": "REF", "kind": "Scripted", "aggression": .5, "defense": .5, "mobility": .5, "patience": .5, "holdout": False}]
    players = []
    for t in args.old_targets + args.new_targets:
        p, s = P[t], P[t]["style_mean"]
        personas.append({"id": "T4_" + t, "kind": "ILAgent", "holdout": p["holdout"],
                         "aggression": 0, "defense": 0, "mobility": 0, "patience": 0,
                         "styleAttack": s["attack_rate"], "styleMobility": s["mobility_rate"], "styleDefense": s["defense_rate"],
                         "styleMobilityTime": s["mobility_time_ratio"], "styleBlockTime": s["block_time_ratio"]})
        players.append("T4_" + t)
        if t in args.new_targets:
            personas.append({"id": t, "kind": "Scripted", "aggression": p["aggression"], "defense": p["defense"],
                             "mobility": p["mobility"], "patience": p["patience"], "holdout": p["holdout"]})
            players.append(t)

    matches = []
    for r in range(args.matches):
        for who in players:
            a, b = (who, "REF") if r % 2 == 0 else ("REF", who)
            matches.append({"index": len(matches), "round": r + 1, "p1": a, "p2": b})
    doc = {"name": args.name, "seed": 0, "purpose": "style fidelity theta4 (teachers of old targets reused from StyleEval_theta3)",
           "targets": args.old_targets + args.new_targets, "personas": personas, "matches": matches}
    with open(os.path.join(args.out_dir, args.name + ".json"), "w", encoding="utf-8") as f:
        json.dump(doc, f, indent=1)
    print(f"{len(args.old_targets)} old + {len(args.new_targets)} new targets, {len(matches)} matches -> {args.name}.json")


if __name__ == "__main__":
    main()
