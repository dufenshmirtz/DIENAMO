"""
Blind human-likeness test (thesis, 30/10/2026). One participant plays N rounds against 4 opponents in random order,
without knowing which is which, and after every round writes down (ratings CSV in the output folder):
  how human-like the opponent played (1-5) and whether it was the scripted bot or the IL agent.
With "rating": true the Director asks the two questions on screen after every match and writes
Telemetry/<folder>/_ratings_<schedule>.csv (needs the 30/10 Director). Opponents (same character, Classic):
  REF      scripted bot, knobs 0.5 (the teacher family)
  GHOST_B  IL agent, bot-only model (default theta6_joint), fixed style = the participant's natural style
  GHOST_H  IL agent, human-tuned model (default theta6h2_joint), same fixed style
  MIRROR_H IL agent, human-tuned model, online mirror of the participant (EMA of his rounds in this output folder)
Persona ids are NEUTRAL random codes (K..); the key is stored in the schedule ("blind_key") and must not be opened
before the test. Per-persona models need ILDataGenDirector "ilModel" support (30/10).
Constraints: the first two rounds are not the mirror (it needs the participant's rounds), no condition 3x in a row.
"""
import argparse, json, os, random

HERE = os.path.dirname(os.path.abspath(__file__))
SCHED = os.path.normpath(os.path.join(HERE, "..", "schedules"))
AX = ["attack_rate", "mobility_rate", "defense_rate", "mobility_time_ratio", "block_time_ratio"]
KEYS = ["styleAttack", "styleMobility", "styleDefense", "styleMobilityTime", "styleBlockTime"]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--participant", required=True, help="e.g. P00B (telemetry HUMAN_P00B)")
    ap.add_argument("--style-json", required=True, help="human dataset.json with the participant's natural persona")
    ap.add_argument("--style-persona", required=True, help="e.g. HUMAN_P00N")
    ap.add_argument("--bot-model", default="theta6_joint")
    ap.add_argument("--human-model", default="theta6h2_joint")
    ap.add_argument("--per-condition", type=int, default=4)
    ap.add_argument("--seed", type=int, default=20261030)
    ap.add_argument("--out-folder", default=None)
    ap.add_argument("--name", default=None, help="schedule name (default schedule_blind_<participant>)")
    a = ap.parse_args()

    per = {p["id"]: p for p in json.load(open(a.style_json, encoding="utf-8"))["personas"]}
    s = per[a.style_persona]["style_mean"]
    style = {k: round(float(s[x]), 5) for k, x in zip(KEYS, AX)}
    rng = random.Random(a.seed)
    codes = rng.sample([f"K{i}" for i in range(10, 100)], 4)
    cond = ["REF", "GHOST_B", "GHOST_H", "MIRROR_H"]
    key = dict(zip(cond, codes))
    hum = {"id": a.participant, "kind": "Human", "aggression": 0, "defense": 0, "mobility": 0, "patience": 0, "holdout": False}
    base = {"aggression": 0, "defense": 0, "mobility": 0, "patience": 0, "holdout": False}
    personas = [hum,
                {"id": key["REF"], "kind": "Scripted", "aggression": .5, "defense": .5, "mobility": .5, "patience": .5, "holdout": False},
                {"id": key["GHOST_B"], "kind": "ILAgent", "ilModel": a.bot_model, **base, **style},
                {"id": key["GHOST_H"], "kind": "ILAgent", "ilModel": a.human_model, **base, **style},
                {"id": key["MIRROR_H"], "kind": "ILAgent", "ilModel": a.human_model, "mirrorOf": "HUMAN_" + a.participant, **base}]
    while True:
        order = [c for c in cond for _ in range(a.per_condition)]
        rng.shuffle(order)
        if "MIRROR_H" in order[:2]:
            continue
        if any(order[i] == order[i + 1] == order[i + 2] for i in range(len(order) - 2)):
            continue
        break
    folder = a.out_folder or "HV_blind_" + a.participant
    name = a.name or "schedule_blind_" + a.participant
    doc = {"name": name, "seed": a.seed, "participant": a.participant, "outputFolder": folder,
           "rating": True,   # ILDataGenDirector shows the rating panel after every match (30/10)
           "purpose": "blind human-likeness test: rate each opponent 1-5 and guess bot / agent (DO NOT OPEN before the test)",
           "blind_key": key, "ghost_style_from": a.style_persona, "ghost_style": style,
           "personas": personas,
           "matches": [{"index": i, "round": i + 1, "p1": a.participant, "p2": key[c]} for i, c in enumerate(order)]}
    json.dump(doc, open(os.path.join(SCHED, name + ".json"), "w", encoding="utf-8"), indent=1)
    print(f"{name}.json: {len(order)} rounds -> Output Folder {folder}")
    return folder, len(order)


if __name__ == "__main__":
    main()
