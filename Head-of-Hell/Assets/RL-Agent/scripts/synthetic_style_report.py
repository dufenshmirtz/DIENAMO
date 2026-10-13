"""
Synthetic-player style report (thesis: IL agent, data generation pilot).

Reads the bot-vs-bot telemetry in  Documents/My Games/Head of Hell/Telemetry/Synthetic
and measures the play style of every synthetic player (scripted persona or RL model)
with EXACTLY the same feature code as the game's profile analysis (extractor_upgraded.py).

Why a separate script:
- The game's analysis only keeps rounds of real profiles (GUEST / NONE are skipped) and reads Telemetry/Build.
- Here the "player identity" is the persona id written by the data generator (meta.p1PersonaId / p2PersonaId).
- extractor_upgraded.py is NOT modified: each document is copied in memory and the persona id is placed in the
  profile fields of that copy before the shared feature extraction runs.

Outputs (in --out-dir, default: Documents/My Games/Head of Hell/Analysis/synthetic):
- synthetic_rounds.csv  : one row per player per match (all features + persona + opponent persona)
- synthetic_personas.csv: per persona -> number of matches, mean and std of every style feature
and a short table in the console.
"""

import argparse
import copy
import glob
import json
import os
import sys

import pandas as pd

HERE = os.path.dirname(os.path.abspath(__file__))
ANALYSIS_DIR = os.path.normpath(os.path.join(HERE, "..", "..", "StreamingAssets", "Analysis"))
sys.path.insert(0, ANALYSIS_DIR)
import extractor_upgraded as X  # noqa: E402  (shared feature definitions)

STYLE_CORE = ["attack_rate", "mobility_rate", "defense_rate", "mobility_time_ratio", "block_time_ratio"]
EXTRA = ["quick_rate", "heavy_rate", "special_rate", "charge_rate", "hit_rate", "dps_dealt", "duration_s",
         "block_rapid_repeats", "heavy_cancel_rate"]


def default_root() -> str:
    docs = os.path.join(os.path.expanduser("~"), "Documents")
    return os.path.join(docs, "My Games", "Head of Hell")


def load_rounds(telemetry_dir: str) -> pd.DataFrame:
    rows = []
    files = sorted(glob.glob(os.path.join(telemetry_dir, "*.json")))
    for fp in files:
        with open(fp, "r", encoding="utf-8-sig") as f:
            doc = json.load(f)
        meta = doc.get("meta") or {}
        personas = {"P1": meta.get("p1PersonaId") or "", "P2": meta.get("p2PersonaId") or ""}
        if not personas["P1"] or not personas["P2"]:
            print(f"[skip] {os.path.basename(fp)}: missing persona id")
            continue

        # Copy, then use the persona as the identity so the shared extractor keeps the rows.
        d = copy.deepcopy(doc)
        m = d["meta"]
        m["p1ProfileId"], m["p1ProfileName"] = personas["P1"], personas["P1"]
        m["p2ProfileId"], m["p2ProfileName"] = personas["P2"], personas["P2"]

        for r in X.extract_round_features(d, fp):
            slot = r.get("slot_id")
            r["persona"] = personas.get(slot, "")
            r["opponent_persona"] = personas["P2" if slot == "P1" else "P1"]
            r["file"] = os.path.basename(fp)
            rows.append(r)
    return pd.DataFrame(rows)


def summarize(df: pd.DataFrame) -> pd.DataFrame:
    cols = [c for c in STYLE_CORE + EXTRA if c in df.columns]
    g = df.groupby("persona")[cols]
    out = g.mean().add_suffix("_mean").join(g.std().add_suffix("_std"))
    out.insert(0, "matches", df.groupby("persona").size())
    return out.reset_index()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--telemetry-dir", default=os.path.join(default_root(), "Telemetry", "Synthetic"))
    ap.add_argument("--out-dir", default=os.path.join(default_root(), "Analysis", "synthetic"))
    args = ap.parse_args()

    df = load_rounds(args.telemetry_dir)
    if df.empty:
        print("No synthetic rounds found.")
        return

    os.makedirs(args.out_dir, exist_ok=True)
    df.to_csv(os.path.join(args.out_dir, "synthetic_rounds.csv"), index=False)
    summary = summarize(df)
    summary.to_csv(os.path.join(args.out_dir, "synthetic_personas.csv"), index=False)

    pd.set_option("display.width", 200)
    show = ["persona", "matches"] + [f"{c}_mean" for c in STYLE_CORE] + [f"{c}_std" for c in STYLE_CORE]
    print(summary[show].round(3).to_string(index=False))
    print(f"\nSaved: {args.out_dir}")


if __name__ == "__main__":
    main()
