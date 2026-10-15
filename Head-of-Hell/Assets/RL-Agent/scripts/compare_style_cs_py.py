"""
Thesis: equivalence test C# (StyleAxes.cs) vs Python (extractor_upgraded.py) for the 5 style axes.

1. In Unity: menu "Thesis/Style axes: export C# rows (equivalence test)" -> Telemetry/_style_equiv_cs.csv
2. python compare_style_cs_py.py --telemetry "<...>/My Games/Head of Hell/Telemetry"

For every stored telemetry file (Telemetry/**/match_*.json) the Python extractor is run on the same file and every
player row is compared with the C# row. The extractor skips players without a profile (bots, agents, guests); here
the profile ids are filled with placeholders before calling it, so that EVERY seat is compared (the C# tracker will
also measure guests and the IL agent). Nothing else in the file is changed.

PASS = same rows on both sides (same players kept by the 10-actions / 5-s filter) and
       |C# - Python| <= tol for the 5 axes and the counts behind them.
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
COUNTS = ["actions_total", "walk_segments", "block_segments", "moving_s", "blocking_s"]
INVALID = {"GUEST", "UNKNOWN", "NONE", ""}
ROUNDED_3 = {"moving_s", "blocking_s"}   # extract_round_features rounds them for the csv; the axes use full precision


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--telemetry", required=True, help="Telemetry root folder")
    ap.add_argument("--cs", default=None, help="C# csv (default: <telemetry>/_style_equiv_cs.csv)")
    ap.add_argument("--tol", type=float, default=1e-4)
    ap.add_argument("--out", default=None, help="optional json report")
    args = ap.parse_args()

    root = os.path.abspath(args.telemetry)
    cs_path = args.cs or os.path.join(root, "_style_equiv_cs.csv")

    cs_by_file = {}
    with open(cs_path, newline="", encoding="utf-8-sig") as f:
        for row in csv.DictReader(f):
            cs_by_file.setdefault(row["file"], {})[row["slot_id"]] = row

    files = sorted(glob.glob(os.path.join(root, "**", "match_*.json"), recursive=True))
    cols = AXES + COUNTS
    max_diff = {c: 0.0 for c in cols}
    worst = []            # (diff, file, slot, column, cs, py)
    missing_in_cs, extra_in_cs, py_errors, not_in_cs_file = [], [], [], []
    compared = 0

    for path in files:
        rel = os.path.relpath(path, root).replace("\\", "/")
        try:
            with open(path, encoding="utf-8-sig") as f:
                doc = json.load(f)
        except Exception as ex:
            py_errors.append((rel, f"json: {ex}"))
            continue
        doc = copy.deepcopy(doc)
        meta = doc.get("meta") if isinstance(doc.get("meta"), dict) else {}
        doc["meta"] = meta
        for k in ("p1ProfileId", "p2ProfileId"):
            if str(meta.get(k) or "").strip() in INVALID:
                meta[k] = "FORCED_" + k
        try:
            py_rows = X.extract_round_features(doc, path)
        except Exception as ex:
            py_errors.append((rel, f"extractor: {ex}"))
            continue
        py = {r["slot_id"]: r for r in py_rows}

        cs = cs_by_file.get(rel)
        if cs is None:
            not_in_cs_file.append(rel)
            continue

        for slot, r in py.items():
            c = cs.get(slot)
            if c is None or c["valid"] != "1":
                missing_in_cs.append((rel, slot, "no row" if c is None else "C# says invalid"))
                continue
            compared += 1
            for col in cols:
                cv = float(c[col])
                if col in ROUNDED_3:      # the extractor writes these rounded to 3 decimals (round(x, 3))
                    cv = round(cv, 3)
                d = abs(cv - float(r[col]))
                if d > max_diff[col]:
                    max_diff[col] = d
                if d > args.tol:
                    worst.append((d, rel, slot, col, float(c[col]), float(r[col])))
        for slot, c in cs.items():
            if c["valid"] == "1" and slot not in py:
                extra_in_cs.append((rel, slot))

    worst.sort(reverse=True)
    ok = not (missing_in_cs or extra_in_cs or worst or not_in_cs_file)

    print(f"telemetry files: {len(files)} | python errors: {len(py_errors)} | rows compared: {compared}")
    print(f"rows only in Python: {len(missing_in_cs)} | rows only in C#: {len(extra_in_cs)} | "
          f"files missing from C# csv: {len(not_in_cs_file)}")
    print("max |C# - Python| per column:")
    for col in cols:
        print(f"  {col:22s} {max_diff[col]:.3g}")
    print(f"values over tol {args.tol}: {len(worst)}")
    for w in worst[:15]:
        print(f"  diff {w[0]:.4g}  {w[1]} {w[2]} {w[3]}: C# {w[4]:.6g} vs Py {w[5]:.6g}")
    for m in missing_in_cs[:10]:
        print("  only in Python:", m)
    for m in extra_in_cs[:10]:
        print("  only in C#:", m)
    for m in py_errors[:10]:
        print("  python error:", m)
    for m in not_in_cs_file[:10]:
        print("  not in C# csv:", m)
    print("RESULT:", "PASS" if ok else "FAIL")

    if args.out:
        json.dump({"pass": ok, "files": len(files), "compared": compared, "max_diff": max_diff,
                   "over_tol": worst[:200], "only_python": missing_in_cs, "only_cs": extra_in_cs,
                   "python_errors": py_errors, "not_in_cs_csv": not_in_cs_file}, open(args.out, "w"), indent=1)


if __name__ == "__main__":
    main()
