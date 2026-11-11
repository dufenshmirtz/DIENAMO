"""
Blind human-likeness test for friends (thesis, 11/11/2026). Same idea as make_blind_schedule.py (30/10), but every
IL opponent is an ONLINE MIRROR of the participant (no ghost: friends have no earlier rounds), and the three IL
opponents are the three stages of the thesis:
  REF     scripted bot, knobs 0.5 (the teacher family)
  M_IL    IL agent, theta8h_q0_e60_joint     (best imitation-learning model)
  M_RL    IL agent, thetaR_RLp_b02_u60       (IL + RL, KL leash beta 0.2)
  M_RLS   IL agent, thetaR_RLc3_w2_u40       (IL + RL + style reward w=2)
The mirrors take the EMA of the participant's rounds already saved in this output folder (any opponent).
Optional --warmup practice rounds (default 0) vs a scripted bot with the visible id WARMUP; the rating panel also
appears after them, they are not analysed. Without warm-up, a mirror that plays round 1 has no rounds of the
participant yet and plays the neutral style (same as the in-game Mirror). Persona ids of the 4 opponents are neutral random codes (K..); the key is stored in the schedule
("blind_key") and must not be opened before the test. Constraint: no opponent 3x in a row.
Needs ILDataGenDirector with "ilModel", "mirrorOf" and "rating" (30/10). Writes only a new schedule file.
"""
import argparse, json, os, random, zlib

HERE = os.path.dirname(os.path.abspath(__file__))
SCHED = os.path.normpath(os.path.join(HERE, "..", "schedules"))
BASE = {"aggression": 0, "defense": 0, "mobility": 0, "patience": 0, "holdout": False}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--participant", required=True, help="e.g. P01 (telemetry HUMAN_P01)")
    ap.add_argument("--il-model", default="theta8h_q0_e60_joint")
    ap.add_argument("--rl-model", default="thetaR_RLp_b02_u60")
    ap.add_argument("--rls-model", default="thetaR_RLc3_w2_u40")
    ap.add_argument("--per-condition", type=int, default=4)
    ap.add_argument("--warmup", type=int, default=0, help="practice rounds vs WARMUP before the rated ones (default none)")
    ap.add_argument("--seed", type=int, default=None, help="default: derived from the participant id")
    ap.add_argument("--out-folder", default=None)
    ap.add_argument("--name", default=None, help="schedule name (default schedule_blind_<participant>)")
    a = ap.parse_args()

    seed = a.seed if a.seed is not None else 20261111 + zlib.crc32(a.participant.encode()) % 100000
    rng = random.Random(seed)
    cond = ["REF", "M_IL", "M_RL", "M_RLS"]
    key = dict(zip(cond, rng.sample([f"K{i}" for i in range(10, 100)], 4)))
    me = "HUMAN_" + a.participant
    personas = [{"id": a.participant, "kind": "Human", **BASE},
                {"id": "WARMUP", "kind": "Scripted", "aggression": .5, "defense": .5, "mobility": .5, "patience": .5, "holdout": False},
                {"id": key["REF"], "kind": "Scripted", "aggression": .5, "defense": .5, "mobility": .5, "patience": .5, "holdout": False},
                {"id": key["M_IL"], "kind": "ILAgent", "ilModel": a.il_model, "mirrorOf": me, **BASE},
                {"id": key["M_RL"], "kind": "ILAgent", "ilModel": a.rl_model, "mirrorOf": me, **BASE},
                {"id": key["M_RLS"], "kind": "ILAgent", "ilModel": a.rls_model, "mirrorOf": me, **BASE}]
    while True:
        order = [c for c in cond for _ in range(a.per_condition)]
        rng.shuffle(order)
        if not any(order[i] == order[i + 1] == order[i + 2] for i in range(len(order) - 2)):
            break
    opp = ["WARMUP"] * a.warmup + [key[c] for c in order]
    folder = a.out_folder or "HV_blind_" + a.participant
    name = a.name or "schedule_blind_" + a.participant
    path = os.path.join(SCHED, name + ".json")
    if os.path.exists(path):
        raise SystemExit(f"{path} already exists: choose another --participant or --name")
    doc = {"name": name, "seed": seed, "participant": a.participant, "outputFolder": folder,
           "rating": True,
           "purpose": "blind human-likeness test (friends, mirror only): rate each opponent 1-5 and guess bot / agent; "
                      f"first {a.warmup} rounds = WARMUP, not analysed (DO NOT OPEN before the test)",
           "warmup_rounds": a.warmup,
           "blind_key": {**key, "models": {"M_IL": a.il_model, "M_RL": a.rl_model, "M_RLS": a.rls_model}},
           "personas": personas,
           "matches": [{"index": i, "round": i + 1, "p1": a.participant, "p2": o} for i, o in enumerate(opp)]}
    json.dump(doc, open(path, "w", encoding="utf-8"), indent=1)
    print(f"{name}.json: {a.warmup} warm-up + {len(order)} rated rounds -> Output Folder {folder}")


if __name__ == "__main__":
    main()
