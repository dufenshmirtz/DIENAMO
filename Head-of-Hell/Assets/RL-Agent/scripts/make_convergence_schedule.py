"""
Schedule for the ONLINE CONVERGENCE experiment (thesis: online IL agent).

For every target persona T a fresh pair is created:
  - "C_<T>[_tag]": the scripted persona with T's knobs (the "player" whose style is learned online),
  - "M_<T>[_tag]": the IL agent in online-mirror mode (kind ILAgent, mirrorOf = the C_ id).
They play N matches in a row (sides alternated). Before each match the Director rebuilds the EMA of C_'s saved matches
in the output folder (OnlineMirrorStyle, alpha 0.35): match 1 = neutral style, then the agent follows the player.
Measured: how fast the agent's MEASURED style approaches the player's style (agent_style_log.csv + telemetry).

Targets are interleaved (round robin), so a stopped run is still balanced; every target's matches stay in order.
Use a new --tag (and output folder) for every run: the ids must be new, or old matches would be part of the EMA.
"""

import argparse
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--targets", nargs="+", required=True, help="persona ids of the main/extension schedules")
    ap.add_argument("--schedules", nargs="+", default=[os.path.join(HERE, "..", "schedules", "schedule_main.json"),
                                                       os.path.join(HERE, "..", "schedules", "schedule_ext.json")])
    ap.add_argument("--matches", type=int, default=10, help="matches per target")
    ap.add_argument("--tag", default="", help="suffix that makes the ids unique for this run")
    ap.add_argument("--name", default="schedule_convergence")
    ap.add_argument("--out-dir", default=os.path.normpath(os.path.join(HERE, "..", "schedules")))
    args = ap.parse_args()

    knobs = {}
    for s in args.schedules:
        if os.path.exists(s):
            for p in json.load(open(s, encoding="utf-8"))["personas"]:
                knobs.setdefault(p["id"], p)
    personas, pairs = [], []
    sfx = ("_" + args.tag) if args.tag else ""
    for t in args.targets:
        k = knobs[t]
        c, m = f"C_{t}{sfx}", f"M_{t}{sfx}"
        personas.append({"id": c, "kind": "Scripted", "aggression": k["aggression"], "defense": k["defense"],
                         "mobility": k["mobility"], "patience": k.get("patience", 0.5), "holdout": k.get("holdout", False)})
        personas.append({"id": m, "kind": "ILAgent", "mirrorOf": c, "aggression": 0, "defense": 0, "mobility": 0,
                         "patience": 0, "holdout": k.get("holdout", False)})
        pairs.append((c, m))

    matches = []
    for r in range(args.matches):
        for c, m in pairs:
            a, b = (c, m) if r % 2 == 0 else (m, c)
            matches.append({"index": len(matches), "round": r + 1, "p1": a, "p2": b})

    doc = {"name": args.name, "seed": 0, "purpose": "online convergence: scripted persona vs IL agent mirroring it",
           "targets": args.targets, "personas": personas, "matches": matches}
    os.makedirs(args.out_dir, exist_ok=True)
    with open(os.path.join(args.out_dir, args.name + ".json"), "w", encoding="utf-8") as f:
        json.dump(doc, f, indent=1)
    print(f"{len(args.targets)} targets x {args.matches} = {len(matches)} matches -> {args.name}.json")


if __name__ == "__main__":
    main()
