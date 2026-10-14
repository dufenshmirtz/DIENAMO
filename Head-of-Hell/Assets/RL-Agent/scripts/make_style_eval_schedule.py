"""
Schedule for the main STYLE-FIDELITY experiment (thesis: IL agent evaluation).

For every target persona T:
  - the IL agent plays with T's measured style vector   (id "T_<persona>", kind ILAgent -> telemetry "IL_T_<persona>")
  - the scripted teacher T itself plays                  (id "<persona>",   kind Scripted, T's knobs)
both against the same fixed opponent REF (scripted, all knobs 0.5), N matches each, sides alternated.
Comparing the agent with its teacher under the SAME opponent removes the opponent effect from the comparison.

Targets: all hold-out personas (never seen in training) + an equal number of training personas chosen to be as
different from each other as possible in measured style (greedy farthest point, z-scored 5 axes).
Matches are interleaved (round robin over targets) so a run stopped early is still balanced.
"""

import argparse
import json
import os

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
AXES = ["attack_rate", "mobility_rate", "defense_rate", "mobility_time_ratio", "block_time_ratio"]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dataset-json", required=True, help="dataset.json of the training dataset (persona styles, hold-out flags)")
    ap.add_argument("--schedule", default=os.path.normpath(os.path.join(HERE, "..", "schedules", "schedule_main.json")))
    ap.add_argument("--n-train", type=int, default=6)
    ap.add_argument("--matches", type=int, default=8, help="matches per (target, player kind)")
    ap.add_argument("--name", default="schedule_style_eval")
    ap.add_argument("--out-dir", default=os.path.normpath(os.path.join(HERE, "..", "schedules")))
    args = ap.parse_args()

    ds = json.load(open(args.dataset_json, encoding="utf-8"))
    knobs = {p["id"]: p for p in json.load(open(args.schedule, encoding="utf-8"))["personas"]}
    P = {p["id"]: p for p in ds["personas"]}
    hold = [pid for pid, p in P.items() if p["holdout"]]
    train = [pid for pid, p in P.items() if not p["holdout"]]

    S = np.array([[P[i]["style_mean"][a] for a in AXES] for i in train])
    Z = (S - S.mean(0)) / S.std(0)
    chosen = [int(np.argmax(np.linalg.norm(Z, axis=1)))]            # most extreme first
    while len(chosen) < args.n_train:
        d = np.array([min(np.linalg.norm(Z[i] - Z[j]) for j in chosen) for i in range(len(train))])
        d[chosen] = -1
        chosen.append(int(np.argmax(d)))
    targets = sorted(hold) + sorted(train[i] for i in chosen)

    personas = [{"id": "REF", "kind": "Scripted", "aggression": .5, "defense": .5, "mobility": .5, "patience": .5, "holdout": False}]
    for t in targets:
        k = knobs[t]
        s = P[t]["style_mean"]
        personas.append({"id": t, "kind": "Scripted", "aggression": k["aggression"], "defense": k["defense"],
                         "mobility": k["mobility"], "patience": k["patience"], "holdout": P[t]["holdout"]})
        personas.append({"id": "T_" + t, "kind": "ILAgent", "holdout": P[t]["holdout"],
                         "aggression": 0, "defense": 0, "mobility": 0, "patience": 0,
                         "styleAttack": s["attack_rate"], "styleMobility": s["mobility_rate"], "styleDefense": s["defense_rate"],
                         "styleMobilityTime": s["mobility_time_ratio"], "styleBlockTime": s["block_time_ratio"]})

    matches = []
    for r in range(args.matches):
        for t in targets:
            for who in ("T_" + t, t):
                a, b = (who, "REF") if (r % 2 == 0) else ("REF", who)
                matches.append({"index": len(matches), "round": r + 1, "p1": a, "p2": b})

    doc = {"name": args.name, "seed": 0, "purpose": "style fidelity: IL agent vs its scripted teacher, both vs REF",
           "targets": targets, "personas": personas, "matches": matches}
    os.makedirs(args.out_dir, exist_ok=True)
    with open(os.path.join(args.out_dir, args.name + ".json"), "w", encoding="utf-8") as f:
        json.dump(doc, f, indent=1)
    print(f"targets {targets} (hold-out {sorted(hold)}), {len(matches)} matches -> {args.name}.json")


if __name__ == "__main__":
    main()
