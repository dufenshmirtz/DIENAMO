"""
Diagnosis for the θ5 teachers (thesis): how does the scripted bot differ from HUMAN players?
Only telemetry events are used (humans have no imitation logs, no positions), so the metrics are event based:
  tempo      : actions per second by type, inter-action intervals (median, p10, coefficient of variation)
  segments   : how long a walk / a block lasts
  attack     : hit rate, heavy cancel, attacks per second, damage by move type, distance at damage
  reaction   : after the opponent's HeavyStart, does the player react defensively (BlockStart/ParryAttempt/Roll/Jump)
               within 0.62 s (heavy commit time), and how fast
  follow-up  : time from landing a hit to the next own attack; from blocking a hit to the next own attack (punish)
Groups: human rounds (Telemetry/Build human vs human, any character; Skipler-only subset) vs bots (Synthetic_main).
"""
import argparse
import glob
import json
import os
import sys

import numpy as np
import pandas as pd

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import synthetic_style_report as SR  # noqa: E402

ATTACKS = {"Quick", "HeavyStart", "Special", "ChargeStart"}
DEF = {"BlockStart", "ParryAttempt", "Roll", "Jump"}
MOVES = {"MoveLeft", "MoveRight"}
COUNT = ["MoveLeft", "MoveRight", "MoveStop", "Jump", "DropPlatform", "Quick", "HeavyStart", "Special", "ChargeStart",
         "BlockStart", "ParryAttempt", "Roll"]


def collapse_repeats(acts):
    """Human keyboards log a held / mashed key as the same action on many consecutive frames (e.g. DropPlatform while
    'down' is held, HeavyStart mashing); the bot presses once. Same action again within 0.10 s = one action, and a
    MoveStop followed by a move within 2 frames = one direction change. Without this, human rhythm/drop metrics are
    inflated (19/10: 13% of all human transitions were DropPlatform->DropPlatform 1 frame apart)."""
    out = []
    for e in acts:
        if out and e["actionType"] == out[-1]["actionType"] and e["t"] - out[-1]["t"] < 0.10:
            continue
        if out and out[-1]["actionType"] == "MoveStop" and e["actionType"] in MOVES and e["t"] - out[-1]["t"] < 0.034:
            out[-1] = e
            continue
        out.append(e)
    return out


def player_rounds(fp, group):
    d = json.load(open(fp, encoding="utf-8-sig"))
    m = d.get("meta") or {}
    ev = d.get("events") or []
    fs = [e["t"] for e in ev if e["eventType"] == "FightStart"]
    t0 = fs[0] if fs else (ev[0]["t"] if ev else 0)
    t1 = ev[-1]["t"] if ev else t0
    dur = max(t1 - t0, 1e-6)
    out = []
    for seat, opp in (("P1", "P2"), ("P2", "P1")):
        name = m.get(f"{seat.lower()}ProfileName") or ""
        persona = m.get(f"{seat.lower()}PersonaId") or ""
        if group == "human" and (persona or name in ("", "None")):
            continue
        act = collapse_repeats([e for e in ev if e["eventType"] == "Action" and e.get("actorId") == seat and e["t"] >= t0])
        oppact = [e for e in ev if e["eventType"] == "Action" and e.get("actorId") == opp and e["t"] >= t0]
        if len(act) < 10 or dur < 10:
            continue
        r = {"group": group, "who": persona or name, "character": m.get(f"{seat.lower()}Character"),
             "file": os.path.basename(fp), "dur": dur, "aps": len(act) / dur}
        for c in COUNT:
            r["r_" + c] = sum(1 for e in act if e["actionType"] == c) / dur
        r["r_attack"] = sum(1 for e in act if e["actionType"] in ATTACKS) / dur
        ts = np.array([e["t"] for e in act])
        iai = np.diff(ts)
        iai = iai[iai > 1e-4]
        if len(iai) > 5:
            r["iai_med"] = float(np.median(iai)); r["iai_p10"] = float(np.percentile(iai, 10))
            r["iai_cv"] = float(np.std(iai) / np.mean(iai))
        # walk / block segment lengths
        walks, blocks, wstart, bstart = [], [], None, None
        for e in act:
            a = e["actionType"]
            if a in MOVES:
                if wstart is not None: walks.append(e["t"] - wstart)
                wstart = e["t"]
            elif a == "MoveStop" and wstart is not None:
                walks.append(e["t"] - wstart); wstart = None
            if a == "BlockStart": bstart = e["t"]
            elif a == "BlockEnd" and bstart is not None:
                blocks.append(e["t"] - bstart); bstart = None
        r["walk_med"] = float(np.median(walks)) if walks else np.nan
        r["block_med"] = float(np.median(blocks)) if blocks else np.nan
        # reaction to the opponent's heavy
        my_def = np.array([e["t"] for e in act if e["actionType"] in DEF])
        lat = []
        heavies = [e["t"] for e in oppact if e["actionType"] == "HeavyStart"]
        for th in heavies:
            nxt = my_def[(my_def > th) & (my_def <= th + 0.62)]
            if len(nxt): lat.append(nxt[0] - th)
        r["react_share"] = len(lat) / len(heavies) if heavies else np.nan
        r["react_med"] = float(np.median(lat)) if lat else np.nan
        # follow-ups
        my_att = np.array([e["t"] for e in act if e["actionType"] in ATTACKS])
        hits = [e["t"] for e in ev if e["eventType"] == "DamageApplied" and e.get("attackerId") == seat]
        blocked = [e["t"] for e in ev if e["eventType"] == "DamageApplied" and e.get("defenderId") == seat and e.get("wasBlocked")]
        def nxt_att(times):
            v = [my_att[my_att > t][0] - t for t in times if (my_att > t).any()]
            return float(np.median(v)) if v else np.nan
        r["after_hit"] = nxt_att(hits)
        r["after_block"] = nxt_att(blocked)
        dist = [e["distance"] for e in ev if e["eventType"] == "DamageApplied" and e.get("attackerId") == seat
                and e.get("sourceType") == "Melee" and e.get("distance", -1) > 0]
        r["hit_dist_med"] = float(np.median(dist)) if dist else np.nan
        dmg = [e for e in ev if e["eventType"] == "DamageApplied" and e.get("attackerId") == seat]
        tot = sum(e.get("finalDamage", 0) for e in dmg) or 1
        for mt in ("Quick", "Heavy", "Special", "Charge"):
            r["dmg_" + mt] = sum(e.get("finalDamage", 0) for e in dmg if (e.get("moveType") or "").startswith(mt)) / tot
        r["dmg_ps"] = sum(e.get("finalDamage", 0) for e in dmg) / dur
        out.append(r)
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=SR.default_root())
    ap.add_argument("--human", nargs="+", default=["Build"])
    ap.add_argument("--bots", nargs="+", default=["Synthetic_main"])
    ap.add_argument("--csv-out", default=None)
    a = ap.parse_args()
    rows = []
    for f in a.human:
        for fp in glob.glob(os.path.join(a.root, "Telemetry", f, "*.json")):
            rows += player_rounds(fp, "human")
    for f in a.bots:
        for fp in glob.glob(os.path.join(a.root, "Telemetry", f, "match_*.json")):
            rows += player_rounds(fp, "bot")
    df = pd.DataFrame(rows)
    df.loc[(df.group == "human") & (df.character == "Skipler"), "group2"] = "human_skipler"
    if a.csv_out: df.to_csv(a.csv_out, index=False)

    cols = [c for c in df.columns if c not in ("group", "who", "character", "file", "group2")]
    hs = df[df.group2 == "human_skipler"]
    summ = pd.DataFrame({"human (all chars)": df[df.group == "human"][cols].median(),
                         "human Skipler": hs[cols].median(),
                         "bot": df[df.group == "bot"][cols].median()})
    hum, bot = df[df.group == "human"], df[df.group == "bot"]
    pooled = np.sqrt((hum[cols].var() + bot[cols].var()) / 2)
    summ["d (bot-human)"] = (bot[cols].mean() - hum[cols].mean()) / pooled
    print(f"player-rounds: human {len(hum)} (Skipler {len(hs)}; players {sorted(hum.who.unique())}), bot {len(bot)}\n")
    print("medians, and Cohen's d of bot vs human (|d| > 0.8 = large difference):")
    print(summ.round(3).to_string())
    print("\nper human player (medians):")
    print(hum.groupby("who")[["aps", "r_attack", "r_Jump", "r_DropPlatform", "iai_med", "react_share", "dmg_ps"]].median().round(3).to_string())


if __name__ == "__main__":
    main()
