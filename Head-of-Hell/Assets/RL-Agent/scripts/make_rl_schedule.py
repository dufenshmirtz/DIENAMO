"""
Schedule for RL fine-tuning (thesis, 02/11, branch RLTeach; used by rl_finetune.py).

Agent personas "RL_<style>" (kind ILAgent, "rl": true = their decisions come from the trainer), one per target style:
  - the measured style of every TRAINING persona of the bot dataset (hold-outs stay unseen for the evaluations),
  - Aris's styles (default HUMAN_P00N / A / D / M; --human-styles) from the human dataset.
Opponents: every training persona of the bot dataset as bot v6.1 ("bot": "v6", its own knobs) + REF (bot v5, knobs 0.5).
Matches: --matches random (agent style, opponent) pairs, the agent alternately on P1 / P2 (seeded, reproducible).
The build plays the schedule in order; the trainer stops by time, so the schedule is simply long.
"""
import argparse
import json
import os

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
AXES = ["attack_rate", "mobility_rate", "defense_rate", "mobility_time_ratio", "block_time_ratio"]
FIELDS = ["styleAttack", "styleMobility", "styleDefense", "styleMobilityTime", "styleBlockTime"]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--bot-dataset-json", required=True, help=".../_datasets/Synthetic_v8/dataset.json")
    ap.add_argument("--human-dataset-json", required=True, help=".../_datasets/Human_v3/dataset.json")
    ap.add_argument("--human-styles", nargs="*", default=["P00N", "P00A", "P00D", "P00M"],
                    help="Aris's styles used as agent styles (03/11: P00D left out = forced, not natural play)")
    ap.add_argument("--matches", type=int, default=20000)
    ap.add_argument("--ref-share", type=float, default=0.15, help="share of matches against REF (bot v5)")
    ap.add_argument("--seed", type=int, default=20261102)
    ap.add_argument("--name", default="schedule_rl_train")
    ap.add_argument("--out-dir", default=os.path.normpath(os.path.join(HERE, "..", "schedules")))
    a = ap.parse_args()

    bot = json.load(open(a.bot_dataset_json, encoding="utf-8"))
    hum = json.load(open(a.human_dataset_json, encoding="utf-8"))
    bp = bot["personas"] if isinstance(bot["personas"], list) else eval(bot["personas"])
    hp = hum["personas"] if isinstance(hum["personas"], list) else eval(hum["personas"])
    train = [p for p in bp if not p["holdout"]]

    personas, agents, opps = [], [], []
    for p in train:
        personas.append({"id": p["id"], "kind": "Scripted", "aggression": p["aggression"], "defense": p["defense"],
                         "mobility": p["mobility"], "patience": p.get("patience", 0.5), "holdout": False, "bot": "v6"})
        opps.append(p["id"])
    personas.append({"id": "REF", "kind": "Scripted", "aggression": 0.5, "defense": 0.5, "mobility": 0.5, "patience": 0.5,
                     "holdout": False})
    styles = [(p["id"], p["style_mean"]) for p in train] + \
             [(p["id"].replace("HUMAN_", ""), p["style_mean"]) for p in hp if p["id"] in ["HUMAN_" + h for h in a.human_styles]]
    for sid, sm in styles:
        q = {"id": "RL_" + sid, "kind": "ILAgent", "aggression": 0, "defense": 0, "mobility": 0, "patience": 0,
             "holdout": False, "rl": True}
        for f, ax in zip(FIELDS, AXES):
            q[f] = round(float(sm[ax]), 5)
        personas.append(q); agents.append(q["id"])

    rng = np.random.default_rng(a.seed)
    matches = []
    for i in range(a.matches):
        ag = agents[rng.integers(len(agents))]
        op = "REF" if rng.random() < a.ref_share else opps[rng.integers(len(opps))]
        p1, p2 = (ag, op) if i % 2 == 0 else (op, ag)
        matches.append({"index": i, "round": 1, "p1": p1, "p2": p2})

    out = {"name": a.name, "seed": a.seed,
           "purpose": "RL fine-tuning (02/11): agent RL_<style> (trained by rl_finetune.py) vs bot v6.1 training personas "
                      "and REF (bot v5); styles = training personas + Aris's 4 styles.",
           "personas": personas, "matches": matches}
    path = os.path.join(a.out_dir, a.name + ".json")
    json.dump(out, open(path, "w"), indent=1)
    print(f"{len(agents)} agent styles, {len(opps)} v6 opponents + REF, {len(matches)} matches -> {path}")


if __name__ == "__main__":
    main()
