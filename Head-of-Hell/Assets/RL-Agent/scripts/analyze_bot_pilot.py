"""
Compares bot-change pilot folders (make_bot_pilot_schedule.py), e.g. Botpilot_old vs Botpilot_new.
Per folder (persona sides only, REF is the fixed opponent; REF itself is also shown):
  quick whiff share (Miss / (Miss + HitAttempt)), heavy whiff share, hit rate, damage dealt/taken, win rate,
  reaction delay to the opponent's HeavyStart (human_vs_bot.py metric), actions/s,
  style means and the knob effects d (90 vs 10 on the target axes; Pilot-2 criterion |d| >= 2).
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
import human_vs_bot as HB  # noqa: E402

TARGET = {"A": ["attack_rate"], "D": ["defense_rate", "block_time_ratio"], "M": ["mobility_rate", "mobility_time_ratio"]}


def whiff(folder):
    rows = []
    for fp in glob.glob(os.path.join(folder, "match_*.json")):
        d = json.load(open(fp, encoding="utf-8-sig"))
        m, ev = d["meta"], d["events"]
        for s in ("P1", "P2"):
            r = {"persona": m.get(s.lower() + "PersonaId"), "file": os.path.basename(fp)}
            for mt in ("Quick", "Heavy"):
                a = sum(1 for e in ev if e["eventType"] == "HitAttempt" and e.get("attackerId") == s and e.get("moveType") == mt)
                mi = sum(1 for e in ev if e["eventType"] == "Miss" and e.get("attackerId") == s and e.get("moveType") == mt)
                r[mt.lower() + "_whiff"] = mi / (a + mi) if a + mi else np.nan
            rows.append(r)
    return pd.DataFrame(rows)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--folders", nargs="+", required=True)
    ap.add_argument("--root", default=SR.default_root())
    a = ap.parse_args()
    summary, effects = {}, {}
    for f in a.folders:
        tel = os.path.join(a.root, "Telemetry", f)
        st = SR.load_rounds(tel)
        st["won"] = st.won_round.astype(float)
        st["aps"] = st.actions_total / st.duration_s
        w = whiff(tel)
        hb = pd.DataFrame([r for fp in glob.glob(os.path.join(tel, "match_*.json")) for r in HB.player_rounds(fp, "bot")])
        hb = hb.rename(columns={"who": "persona"})
        for name, sel in (("personas", lambda d: d[d.persona != "REF"]), ("REF", lambda d: d[d.persona == "REF"])):
            s1, w1, h1 = sel(st), sel(w), sel(hb)
            summary[(f, name)] = {
                "rounds": len(s1), "win": s1.won.mean(), "dmg_dealt": s1.damage_dealt.mean(), "dmg_taken": s1.damage_taken.mean(),
                "hit_rate": s1.hit_rate.mean(), "quick_whiff": w1.quick_whiff.mean(), "heavy_whiff": w1.heavy_whiff.mean(),
                "react_share": h1.react_share.mean(), "react_med": h1.react_med.median(), "aps_extractor": s1.aps.mean(),
                **{ax: s1[ax].mean() for ax in SR.STYLE_CORE}}
        eff = {}
        for k, axes in TARGET.items():
            hi, lo = st[st.persona == k + "90"], st[st.persona == k + "10"]
            for ax in axes:
                ps = np.sqrt((hi[ax].var(ddof=1) + lo[ax].var(ddof=1)) / 2)
                eff[f"{k}: {ax}"] = (hi[ax].mean() - lo[ax].mean()) / ps
        effects[f] = eff
    print(pd.DataFrame(summary).round(3).to_string())
    print("\nknob effects d (90 vs 10), criterion |d| >= 2:")
    print(pd.DataFrame(effects).round(2).to_string())


if __name__ == "__main__":
    main()
