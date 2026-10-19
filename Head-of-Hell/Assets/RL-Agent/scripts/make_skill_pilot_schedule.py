"""
θ5 skill pilot (thesis): do stronger scripted teachers stay the SAME style and stay human-like?
For every target persona T two versions play vs REF (knobs 0.5, the scene's skill 0.6):
  T_k6 : T's knobs, skill 0.6 (= the current teachers)
  T_k9 : T's knobs, skill 0.9
N matches each, sides alternated, interleaved round robin (a stopped run stays balanced).
Analysis (analyze_skill_pilot.py): competence (win rate, damage, hit rate) up? style shift k9 vs k6 (z)? human-likeness
(actions per second, heavy cancel) unchanged?
Needs the Director with per-persona "skill" in the schedule (19/10).
"""
import argparse
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))
SCHED = os.path.normpath(os.path.join(HERE, "..", "schedules"))
TARGETS = ["S07", "S08", "S15", "S20", "S22", "S24", "S04", "S13", "S14", "S16", "S17", "S21", "X02", "X03", "X05", "X10"]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--targets", nargs="+", default=TARGETS)
    ap.add_argument("--skills", nargs="+", type=float, default=[0.6, 0.9])
    ap.add_argument("--matches", type=int, default=6)
    ap.add_argument("--name", default="schedule_skill_pilot")
    a = ap.parse_args()

    knobs = {}
    for f in ("schedule_main.json", "schedule_ext.json"):
        for p in json.load(open(os.path.join(SCHED, f), encoding="utf-8"))["personas"]:
            knobs.setdefault(p["id"], p)

    personas = [{"id": "REF", "kind": "Scripted", "aggression": 0.5, "defense": 0.5, "mobility": 0.5, "patience": 0.5,
                 "holdout": False}]
    versions = []
    for t in a.targets:
        k = knobs[t]
        for s in a.skills:
            pid = f"{t}_k{int(round(s * 10))}"
            personas.append({"id": pid, "kind": "Scripted", "aggression": k["aggression"], "defense": k["defense"],
                             "mobility": k["mobility"], "patience": k.get("patience", 0.5), "holdout": k.get("holdout", False),
                             "skill": s})
            versions.append(pid)
    matches = []
    for r in range(a.matches):
        for v in versions:
            p1, p2 = (v, "REF") if r % 2 == 0 else ("REF", v)
            matches.append({"index": len(matches), "round": r + 1, "p1": p1, "p2": p2})
    doc = {"name": a.name, "seed": 0, "purpose": "theta5 skill pilot: same knobs, skill 0.6 vs 0.9, vs REF",
           "targets": a.targets, "skills": a.skills, "personas": personas, "matches": matches}
    with open(os.path.join(SCHED, a.name + ".json"), "w", encoding="utf-8") as f:
        json.dump(doc, f, indent=1)
    print(f"{len(a.targets)} targets x {len(a.skills)} skills x {a.matches} = {len(matches)} matches -> {a.name}.json")


if __name__ == "__main__":
    main()
