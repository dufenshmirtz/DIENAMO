"""
Bot-change pilot (θ5 teachers): the Pilot-2 knob personas vs REF, to check a SimpleBotController change.
A90/A10 (aggression), D90/D10 (defense), M90/M10 (mobility): one knob at 0.9 / 0.1, the others 0.5; REF all 0.5.
Run it twice into two folders: once with the OLD build, once with the NEW build -> analyze_bot_pilot.py compares them
(quick whiff share, hit rate, damage, reaction delay, knob effects |d| >= 2 must stay).
"""
import argparse
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))
SCHED = os.path.normpath(os.path.join(HERE, "..", "schedules"))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--matches", type=int, default=15)
    ap.add_argument("--name", default="schedule_bot_pilot")
    a = ap.parse_args()
    base = {"aggression": 0.5, "defense": 0.5, "mobility": 0.5, "patience": 0.5}
    personas = [dict(id="REF", kind="Scripted", holdout=False, **base)]
    ids = []
    for key, ax in (("A", "aggression"), ("D", "defense"), ("M", "mobility")):
        for lvl in (90, 10):
            p = dict(id=f"{key}{lvl}", kind="Scripted", holdout=False, **base)
            p[ax] = lvl / 100
            personas.append(p)
            ids.append(p["id"])
    matches = []
    for r in range(a.matches):
        for i in ids:
            p1, p2 = (i, "REF") if r % 2 == 0 else ("REF", i)
            matches.append({"index": len(matches), "round": r + 1, "p1": p1, "p2": p2})
    doc = {"name": a.name, "seed": 0, "purpose": "bot-change pilot: Pilot-2 knob personas vs REF", "personas": personas,
           "matches": matches}
    with open(os.path.join(SCHED, a.name + ".json"), "w", encoding="utf-8") as f:
        json.dump(doc, f, indent=1)
    print(f"{len(ids)} personas x {a.matches} = {len(matches)} matches -> {a.name}.json")


if __name__ == "__main__":
    main()
