"""
DAgger rollouts (thesis, 23/10/2026): schedule where the IL agent plays with the measured style of a scripted persona T
while T itself runs as a SHADOW expert on the agent's character ("expert": T; ILDataGenDirector + SimpleBotController
shadow mode + ImitationLoggerV2 expertAct). The expert never controls the character; it only labels what it would do.

- Targets: the TRAINING personas of the bot dataset (hold-outs excluded -> they stay clean for the style eval).
- Opponents: scripted personas of the same pool (random, never the target itself), sides alternated.
- --control: for the smoke test, the same matches once more with an agent WITHOUT shadow expert (prefix C_), to check
  that the shadow does not change the agent's play.
Agent persona id: <prefix><T> (telemetry IL_<prefix><T>). The IL model is the scene's / -ilmodel (θ6 for iteration 1).
"""
import argparse
import json
import os
import random

HERE = os.path.dirname(os.path.abspath(__file__))
AX = ["attack_rate", "mobility_rate", "defense_rate", "mobility_time_ratio", "block_time_ratio"]
KEYS = ["styleAttack", "styleMobility", "styleDefense", "styleMobilityTime", "styleBlockTime"]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dataset-json", required=True, help=".../_datasets/Synthetic_v6/dataset.json")
    ap.add_argument("--targets", nargs="*", default=None, help="default: every training persona except REF")
    ap.add_argument("--opponents", nargs="*", default=None, help="default: every training persona (incl. REF)")
    ap.add_argument("--matches-per-target", type=int, default=10)
    ap.add_argument("--prefix", default="D_")
    ap.add_argument("--control", action="store_true", help="also the same matches with an agent without shadow (C_)")
    ap.add_argument("--control-rounds", type=int, default=None,
                    help="with --control: only the first N rounds get the C_ twin (default: all rounds)")
    ap.add_argument("--seed", type=int, default=20261023)
    ap.add_argument("--name", required=True)
    ap.add_argument("--out-dir", default=os.path.normpath(os.path.join(HERE, "..", "schedules")))
    a = ap.parse_args()

    P = {p["id"]: p for p in json.load(open(a.dataset_json, encoding="utf-8"))["personas"]}
    train = [i for i, p in P.items() if not p["holdout"]]
    targets = a.targets if a.targets else [i for i in train if i != "REF"]
    opponents = a.opponents if a.opponents else train
    for t in targets + opponents:
        if t not in P:
            raise SystemExit(f"unknown persona {t}")
        if P[t]["holdout"]:
            raise SystemExit(f"{t} is a hold-out persona (keep it out of the DAgger data)")

    personas, seen = [], set()

    def scripted(pid):
        if pid in seen:
            return
        p = P[pid]
        personas.append({"id": pid, "kind": "Scripted", "aggression": p["aggression"], "defense": p["defense"],
                         "mobility": p["mobility"], "patience": p["patience"], "holdout": False})
        seen.add(pid)

    def agent(aid, t, expert):
        s = P[t]["style_mean"]
        q = {"id": aid, "kind": "ILAgent", "aggression": 0, "defense": 0, "mobility": 0, "patience": 0, "holdout": False}
        q.update({k: round(float(s[x]), 5) for k, x in zip(KEYS, AX)})
        if expert:
            q["expert"] = t
        personas.append(q)

    for t in targets:
        scripted(t)
    for o in opponents:
        scripted(o)
    for t in targets:
        agent(a.prefix + t, t, True)
        if a.control:
            agent("C_" + t, t, False)

    rng = random.Random(a.seed)
    matches = []
    for r in range(a.matches_per_target):
        for t in targets:
            opp = rng.choice([o for o in opponents if o != t])
            ctrl = a.control and (a.control_rounds is None or r < a.control_rounds)
            who = [a.prefix + t] + (["C_" + t] if ctrl else [])
            for w in who:
                p1, p2 = (w, opp) if r % 2 == 0 else (opp, w)
                matches.append({"index": len(matches), "round": r + 1, "p1": p1, "p2": p2})

    doc = {"name": a.name, "seed": a.seed, "purpose": "DAgger rollouts: IL agent with the style of T, T as shadow expert",
           "targets": targets, "opponents": opponents, "personas": personas, "matches": matches}
    with open(os.path.join(a.out_dir, a.name + ".json"), "w", encoding="utf-8") as f:
        json.dump(doc, f, indent=1)
    print(f"{len(targets)} targets x {a.matches_per_target}{' (+control)' if a.control else ''} = {len(matches)} matches -> {a.name}.json")


if __name__ == "__main__":
    main()
