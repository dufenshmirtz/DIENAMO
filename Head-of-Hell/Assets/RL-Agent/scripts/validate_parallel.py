"""
Validation of the parallel build runner (thesis). Question: does a match played by the BUILD in locked step
(run_parallel.py, several processes) produce the same data as a match played in the EDITOR at x1?

Compared, parallel folder vs reference folder (default Synthetic_main), same personas:
  1. time structure : game frames per game-second (telemetry 'frame' vs 't'); x1 data = 60. x3 had 20.
                      imitation-log samples per game-second (FixedUpdate, decision period).
  2. environment    : stage, runtime, profile identity (must be NONE like the Editor), _environment files.
  3. style          : per persona and axis z = (mean_par - mean_ref) / (std_ref * sqrt(1/n_par + 1/n_ref)).
                      Same distribution -> mean z^2 ~ 1 and ~5% of |z| > 2.
  4. competence     : hit rate, damage per second, match duration, P1 win share.
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

AXES = SR.STYLE_CORE
EXTRA = ["hit_rate", "dps_dealt", "duration_s", "quick_rate", "heavy_rate", "charge_rate"]


def time_structure(tel_dir, imi_dir):
    fps, sps, meta = [], [], []
    for fp in sorted(glob.glob(os.path.join(tel_dir, "*.json"))):
        d = json.load(open(fp, encoding="utf-8-sig"))
        ev = [e for e in d.get("events", []) if e.get("frame", -1) >= 0 and e.get("t", -1) >= 0]
        if len(ev) >= 2 and ev[-1]["t"] - ev[0]["t"] > 5:
            fps.append((ev[-1]["frame"] - ev[0]["frame"]) / (ev[-1]["t"] - ev[0]["t"]))
        m = d.get("meta", {})
        meta.append((m.get("map"), m.get("runtimeEnvironment"), m.get("p1ProfileId"), m.get("p2ProfileId")))
        lp = os.path.join(imi_dir, f"match_{d.get('matchId')}.jsonl")
        if os.path.isfile(lp):
            ts = {}
            with open(lp, encoding="utf-8") as f:
                for line in f:
                    r = json.loads(line)
                    ts.setdefault(r["slot"], []).append(r["t"])
            for t in ts.values():
                if len(t) > 10 and t[-1] - t[0] > 5:
                    sps.append((len(t) - 1) / (t[-1] - t[0]))
    return np.array(fps), np.array(sps), pd.Series(meta).value_counts()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--parallel", required=True, help="merged run folder, e.g. Parallel_pilot")
    ap.add_argument("--reference", default="Synthetic_main")
    ap.add_argument("--root", default=SR.default_root())
    args = ap.parse_args()
    T = lambda f: os.path.join(args.root, "Telemetry", f)
    I = lambda f: os.path.join(args.root, "ImitationLogs", f)

    print("=== 1-2. time structure / environment ===")
    for name in (args.reference, args.parallel):
        fps, sps, meta = time_structure(T(name), I(name))
        print(f"{name:>18}: matches {len(fps)}, frames/game-s {fps.mean():.2f} (min {fps.min():.2f}, max {fps.max():.2f}), "
              f"log samples/game-s {sps.mean():.2f} (min {sps.min():.2f}, max {sps.max():.2f})" if len(fps) and len(sps)
              else f"{name}: no data")
        for k, v in meta.items():
            print(f"{'':>20}(map, env, p1Profile, p2Profile) {k}: {v}")
    for fp in sorted(glob.glob(os.path.join(T(args.parallel), "_environment_*.json"))):
        e = json.load(open(fp, encoding="utf-8"))
        print(f"  env {os.path.basename(fp)}: {e.get('runtime')} stage={e.get('stage')} captureDt={e.get('captureDeltaTime')} "
              f"timeScale={e.get('timeScale')} fps={e.get('targetFrameRate')} shard {e.get('shardIndex')}/{e.get('shardCount')} "
              f"fullRender={e.get('fullRender')} ruleset={e.get('ruleset')}")

    ref = SR.load_rounds(T(args.reference))
    par = SR.load_rounds(T(args.parallel))
    ref = ref[ref.persona.isin(set(par.persona))]

    print("\n=== 3. style per persona (z of the mean difference) ===")
    zs = []
    rows = []
    for p, g in par.groupby("persona"):
        r = ref[ref.persona == p]
        if len(r) < 3 or len(g) < 2:
            continue
        line = {"persona": p, "n_par": len(g), "n_ref": len(r)}
        for a in AXES:
            sd = r[a].std(ddof=1)
            if not sd or np.isnan(sd):
                continue
            z = (g[a].mean() - r[a].mean()) / (sd * np.sqrt(1 / len(g) + 1 / len(r)))
            zs.append(z)
            line[a] = round(z, 2)
        rows.append(line)
    print(pd.DataFrame(rows).to_string(index=False))
    zs = np.array(zs)
    print(f"\n{len(zs)} persona-axis tests: mean z = {zs.mean():+.2f}, mean z^2 = {np.mean(zs ** 2):.2f} (same data ~1.0), "
          f"|z|>2: {np.mean(np.abs(zs) > 2) * 100:.1f}% (same data ~5%)")
    for a in AXES:
        za = [r[a] for r in rows if a in r]
        print(f"  {a:>20}: mean z {np.mean(za):+.2f}  (a systematic shift shows up here)")

    print("\n=== 4. competence (all rounds, same personas) ===")
    for c in EXTRA:
        if c in par and c in ref:
            print(f"  {c:>14}: editor {ref[c].mean():.3f}  build {par[c].mean():.3f}")
    for name, df in (("editor", ref), ("build", par)):
        if "is_winner" in df:
            print(f"  P1 win share {name}: {df[(df.slot_id == 'P1')]['is_winner'].mean():.2f}")


if __name__ == "__main__":
    main()
