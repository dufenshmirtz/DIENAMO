"""
Evaluation schedules for θ6h (θ6 fine-tuned on Human_v1), thesis 22/10/2026. Writes three NEW schedules:

1. schedule_style_eval_theta6h  (parallel build, -ilmodel theta6h_joint.json)
   Agent T6H_<T> with the v6 measured style of the 16 style-eval targets vs REF, 8 each, sides alternated.
   ONLY the agent plays: the bot-v5 teachers already played exactly these matches vs REF in StyleEval_theta6
   (bot v5 and REF unchanged since) -> θ6 and θ6h are compared against the same teacher data.
2. schedule_ghost_human  (parallel build, run TWICE: -ilmodel theta6_joint.json and theta6h_joint.json)
   Agent G_<P> with the measured style of Aris's personas (Human_v1: P00N, P00A, P00D, P00M), clamped to the
   θ6h style range, vs REF, 10 each. Compared with Aris's own rounds vs REF (Telemetry/HV_P00).
3. schedule_hv2b_P00  (Editor, scene "Schedule Il Model" = theta6h_joint)
   Aris vs the θ6h online mirror of HUMAN_P00, 10 rounds, Output Folder HV_mirror6h_P00 (own folder -> EMA from
   neutral, same as the θ5 mirror session schedule_hv2_P00 / HV_mirror_P00).
"""
import argparse
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))
SCHED = os.path.normpath(os.path.join(HERE, "..", "schedules"))
MODEL = os.path.normpath(os.path.join(HERE, "..", "models", "theta6h_joint.json"))
REF = {"id": "REF", "kind": "Scripted", "aggression": .5, "defense": .5, "mobility": .5, "patience": .5, "holdout": False}
AX = ["attack_rate", "mobility_rate", "defense_rate", "mobility_time_ratio", "block_time_ratio"]
KEYS = ["styleAttack", "styleMobility", "styleDefense", "styleMobilityTime", "styleBlockTime"]
TARGETS = ["S07", "S08", "S15", "S20", "S22", "S24", "S04", "S13", "S14", "S16", "S17", "S21", "X02", "X03", "X05", "X10"]
HUMANS = ["HUMAN_P00N", "HUMAN_P00A", "HUMAN_P00D", "HUMAN_P00M"]


def agent(pid, style, holdout=False):
    p = {"id": pid, "kind": "ILAgent", "aggression": 0, "defense": 0, "mobility": 0, "patience": 0, "holdout": holdout}
    p.update({k: round(float(v), 5) for k, v in zip(KEYS, style)})
    return p


def vs_ref(players, n):
    out = []
    for r in range(n):
        for who in players:
            a, b = (who, "REF") if r % 2 == 0 else ("REF", who)
            out.append({"index": len(out), "round": r + 1, "p1": a, "p2": b})
    return out


def write(doc):
    with open(os.path.join(SCHED, doc["name"] + ".json"), "w", encoding="utf-8") as f:
        json.dump(doc, f, indent=1)
    print(f"{doc['name']}.json: {len(doc['matches'])} matches, Output Folder {doc['outputFolder']}")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--bot-dataset-json", required=True, help=".../_datasets/Synthetic_v6/dataset.json")
    ap.add_argument("--human-dataset-json", required=True, help=".../_datasets/Human_v1/dataset.json")
    ap.add_argument("--style-matches", type=int, default=8)
    ap.add_argument("--ghost-matches", type=int, default=10)
    ap.add_argument("--mirror-rounds", type=int, default=10)
    a = ap.parse_args()
    m = json.load(open(MODEL, encoding="utf-8"))
    lo, hi = m["styleMin"], m["styleMax"]

    # 1. style eval θ6h (agent only)
    P = {p["id"]: p for p in json.load(open(a.bot_dataset_json, encoding="utf-8"))["personas"]}
    per = [REF] + [agent("T6H_" + t, [P[t]["style_mean"][x] for x in AX], P[t]["holdout"]) for t in TARGETS]
    write({"name": "schedule_style_eval_theta6h", "seed": 0, "outputFolder": "StyleEval_theta6h",
           "purpose": "style fidelity theta6h (agent only; teacher data reused from StyleEval_theta6)",
           "targets": TARGETS, "personas": per, "matches": vs_ref([p["id"] for p in per[1:]], a.style_matches)})

    # 2. ghost of Aris's personas (run with θ6 and with θ6h)
    H = {p["id"]: p for p in json.load(open(a.human_dataset_json, encoding="utf-8"))["personas"]}
    per, req, clamped = [REF], {}, {}
    for h in HUMANS:
        s = [H[h]["style_mean"][x] for x in AX]
        c = [min(max(v, l), u) for v, l, u in zip(s, lo, hi)]
        req[h], clamped[h] = [round(v, 5) for v in s], [round(v, 5) for v in c]
        per.append(agent("G_" + h[6:], c))
    write({"name": "schedule_ghost_human", "seed": 0, "outputFolder": "GhostHuman_theta6h (and GhostHuman_theta6)",
           "purpose": "ghost of Aris's personas vs REF; run once per model (-ilmodel), compare with HV_P00",
           "requested_style": req, "clamped_style": clamped, "clamp_range": {"min": lo, "max": hi},
           "personas": per, "matches": vs_ref([p["id"] for p in per[1:]], a.ghost_matches)})

    # 3. Aris vs θ6h mirror (Editor)
    hum = {"id": "P00", "kind": "Human", "aggression": 0, "defense": 0, "mobility": 0, "patience": 0, "holdout": False}
    mir = {"id": "M_P00", "kind": "ILAgent", "mirrorOf": "HUMAN_P00", "aggression": 0, "defense": 0, "mobility": 0,
           "patience": 0, "holdout": False}
    write({"name": "schedule_hv2b_P00", "seed": 0, "participant": "P00", "session": 2, "outputFolder": "HV_mirror6h_P00",
           "purpose": "human validation: online mirror (theta6h)", "personas": [hum, mir],
           "matches": [{"index": i, "round": i + 1, "p1": "P00", "p2": "M_P00"} for i in range(a.mirror_rounds)]})


if __name__ == "__main__":
    main()
