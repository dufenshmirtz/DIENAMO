"""
RL-teacher pilot schedule (thesis: can RL models be better teachers than the scripted personas?).

Every RL model plays N matches against the scripted REF (all knobs 0.5), sides alternated, round-robin order.
Telemetry then gives, per model: competence (wins, damage, hit rate, heavy completion), style (5 axes) and
"human-likeness" indicators (facing, movement towards the opponent, action rate) to compare with the personas.

The model names must match the NNModel assets dragged into ILDataGenDirector.scheduleRlModels.
"""

import argparse
import glob
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))
ASSETS = os.path.normpath(os.path.join(HERE, "..", ".."))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--matches", type=int, default=4)
    ap.add_argument("--name", default="schedule_rl_pilot")
    ap.add_argument("--out-dir", default=os.path.normpath(os.path.join(HERE, "..", "schedules")))
    args = ap.parse_args()

    paths = sorted(glob.glob(os.path.join(ASSETS, "SuccessfulAgents", "**", "*.onnx"), recursive=True))
    paths.append(os.path.join(ASSETS, "results", "hoh_run1", "Fighter.onnx"))
    models = [os.path.splitext(os.path.basename(p))[0] for p in paths]
    assert len(models) == len(set(models)), "duplicate model names"

    personas = [{"id": "REF", "kind": "Scripted", "aggression": .5, "defense": .5, "mobility": .5, "patience": .5, "holdout": False}]
    personas += [{"id": "RL_" + m, "kind": "RL", "model": m, "aggression": 0, "defense": 0, "mobility": 0, "patience": 0,
                  "holdout": False} for m in models]
    matches = []
    for r in range(args.matches):
        for m in models:
            a, b = ("RL_" + m, "REF") if r % 2 == 0 else ("REF", "RL_" + m)
            matches.append({"index": len(matches), "round": r + 1, "p1": a, "p2": b})

    doc = {"name": args.name, "seed": 0, "purpose": "RL models vs REF: competence, style, human-likeness",
           "models": models, "model_paths": [os.path.relpath(p, ASSETS) for p in paths],
           "personas": personas, "matches": matches}
    os.makedirs(args.out_dir, exist_ok=True)
    with open(os.path.join(args.out_dir, args.name + ".json"), "w", encoding="utf-8") as f:
        json.dump(doc, f, indent=1)
    print(f"{len(models)} models, {len(matches)} matches -> {args.name}.json")
    for p in doc["model_paths"]:
        print("  ", p)


if __name__ == "__main__":
    main()
