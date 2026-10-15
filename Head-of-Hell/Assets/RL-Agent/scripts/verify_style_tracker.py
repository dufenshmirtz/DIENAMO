"""
Thesis: check the in-game style tracker against the Python extractor.

For every round stored by the tracker (ImitationLogs/_style_tracker/style_profiles.json, persistent profiles) and every
line of agent_style_log.csv, the round's telemetry file is found by matchId, the Python extractor is run on it and:
- the tracker's raw axes must equal the Python axes of that player;
- the stored EMA must equal the EMA recomputed from the stored raw values (alpha 0.35, first round = first value);
- agent lines: measured axes must equal the Python axes of the agent's seat.
"""

import argparse
import copy
import csv
import glob
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "..", "..", "StreamingAssets", "Analysis")))
import extractor_upgraded as X  # noqa: E402

AXES = ["attack_rate", "mobility_rate", "defense_rate", "mobility_time_ratio", "block_time_ratio"]
ALPHA = 0.35


def python_rows(path):
    doc = json.load(open(path, encoding="utf-8-sig"))
    meta = doc.get("meta") if isinstance(doc.get("meta"), dict) else {}
    doc["meta"] = meta
    for k in ("p1ProfileId", "p2ProfileId"):
        if str(meta.get(k) or "").strip() in {"GUEST", "UNKNOWN", "NONE", ""}:
            meta[k] = "FORCED_" + k
    return doc, {r["slot_id"]: r for r in X.extract_round_features(doc, path)}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--telemetry", required=True)
    ap.add_argument("--tracker", required=True, help="ImitationLogs/_style_tracker")
    ap.add_argument("--tol", type=float, default=1e-6)
    args = ap.parse_args()

    by_match = {}
    for p in glob.glob(os.path.join(args.telemetry, "**", "match_*.json"), recursive=True):
        by_match[os.path.basename(p).rsplit("_", 1)[-1][:-5]] = p

    bad, checked = [], 0
    store = os.path.join(args.tracker, "style_profiles.json")
    if os.path.exists(store):
        data = json.load(open(store, encoding="utf-8-sig"))
        for e in data.get("entries", []):
            ema = None
            for rec in e.get("history", []):
                raw = rec["raw"]
                ema = list(raw) if ema is None else [ALPHA * r + (1 - ALPHA) * m for r, m in zip(raw, ema)]
                d_ema = max(abs(a - b) for a, b in zip(ema, rec["emaAfter"]))
                if d_ema > args.tol:
                    bad.append((e["key"], rec["matchId"], "EMA", d_ema))
                path = by_match.get(rec["matchId"])
                if path is None:
                    bad.append((e["key"], rec["matchId"], "telemetry file not found", None))
                    continue
                doc, py = python_rows(path)
                meta = doc["meta"]
                seat = "P1" if str(meta.get("p1ProfileId")) == e["key"] else ("P2" if str(meta.get("p2ProfileId")) == e["key"] else None)
                if seat is None or seat not in py:
                    bad.append((e["key"], rec["matchId"], "player not found in python rows", seat))
                    continue
                d = max(abs(py[seat][a] - v) for a, v in zip(AXES, raw))
                checked += 1
                if d > args.tol:
                    bad.append((e["key"], rec["matchId"], "raw axes", d))
            print(f"profile {e.get('displayName')} ({e['key'][:8]}, {e['character']}): rounds {e['rounds']}, "
                  f"EMA {[round(v, 3) for v in e['ema']]}")

    log = os.path.join(args.tracker, "agent_style_log.csv")
    if os.path.exists(log):
        for row in csv.DictReader(open(log, encoding="utf-8-sig")):
            path = by_match.get(row["match_id"])
            if path is None:
                bad.append(("agent", row["match_id"], "telemetry file not found", None))
                continue
            _, py = python_rows(path)
            seat = row["seat"]
            if row["valid"] != "1":
                if seat in py:
                    bad.append(("agent", row["match_id"], "tracker invalid but python valid", seat))
                continue
            if seat not in py:
                bad.append(("agent", row["match_id"], "python has no valid row", seat))
                continue
            d = max(abs(py[seat][a] - float(row["measured_" + a])) for a in AXES)
            checked += 1
            if d > args.tol:
                bad.append(("agent", row["match_id"], "measured axes", d))

    print(f"rounds checked: {checked}, problems: {len(bad)}")
    for b in bad[:20]:
        print("  ", b)
    print("RESULT:", "PASS" if checked and not bad else ("NOTHING TO CHECK" if not checked else "FAIL"))


if __name__ == "__main__":
    main()
