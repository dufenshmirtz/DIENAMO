"""
Schedule for the main synthetic data collection (thesis: IL agent).

Creates N scripted personas and a list of persona-vs-persona matches, written as JSON that
ILDataGenDirector reads (Inspector field "Schedule"). Everything is reproducible from --seed.

Personas
- Knobs aggression / defense / mobility sampled with a Latin hypercube in [lo, hi] (default 0.1-0.9,
  the range tested in pilots 1-2). Latin hypercube: each knob's range is cut into N equal strata and
  every stratum is used exactly once -> even coverage of every knob, no clusters or holes.
- patience fixed at 0.5 (decision 2026-10-13). Skill is set in the Director (0.6 for everyone).
- Hold-out personas (never used for training): chosen among personas with all knobs inside
  [0.2, 0.8] (interior -> tests interpolation), spread out with a greedy farthest-point rule.

Matches
- R rounds; in every round the personas are paired randomly (a perfect matching), so after R rounds
  every persona has played exactly R matches. A pair is not repeated more than --max-repeat times and
  two hold-out personas are not paired (that match would give no training data).
- Side: the persona with fewer P1 matches so far is P1 -> ~half of each persona's matches on each side.
- Matches are listed round by round, so a run stopped early is still balanced.

Outputs (--out-dir, default Assets/RL-Agent/schedules):
- <name>.json : read by Unity (TextAsset)
- <name>_personas.csv : the personas, for reading / the thesis
"""

import argparse
import csv
import json
import os
from datetime import datetime, timezone

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))


def latin_hypercube(n, dims, rng):
    """n points in [0,1]^dims; each dimension has exactly one point per stratum [k/n, (k+1)/n)."""
    out = np.empty((n, dims))
    for d in range(dims):
        out[:, d] = (rng.permutation(n) + rng.random(n)) / n
    return out


def pick_holdout(knobs, count, inner_lo, inner_hi, rng):
    inside = [i for i, k in enumerate(knobs) if np.all((k >= inner_lo) & (k <= inner_hi))]
    if len(inside) < count:
        raise RuntimeError(f"Only {len(inside)} interior personas; lower --holdout or change the seed.")
    centre = np.full(knobs.shape[1], 0.5)
    # start from the interior persona closest to the centre, then add the one farthest from those chosen
    chosen = [min(inside, key=lambda i: np.linalg.norm(knobs[i] - centre))]
    while len(chosen) < count:
        rest = [i for i in inside if i not in chosen]
        chosen.append(max(rest, key=lambda i: min(np.linalg.norm(knobs[i] - knobs[j]) for j in chosen)))
    return set(chosen)


def make_rounds(ids, rounds, holdout, max_repeat, rng, tries=20000):
    pair_count = {}
    p1_count = {i: 0 for i in ids}
    matches = []
    for r in range(rounds):
        for _ in range(tries):
            order = list(rng.permutation(ids))
            pairs = [(order[k], order[k + 1]) for k in range(0, len(order), 2)]
            ok = all(pair_count.get(frozenset(p), 0) < max_repeat and not (p[0] in holdout and p[1] in holdout)
                     for p in pairs)
            if ok:
                break
        else:
            raise RuntimeError(f"Could not build round {r + 1}; relax --max-repeat.")
        for a, b in pairs:
            pair_count[frozenset((a, b))] = pair_count.get(frozenset((a, b)), 0) + 1
            if p1_count[a] > p1_count[b] or (p1_count[a] == p1_count[b] and rng.random() < 0.5):
                a, b = b, a
            p1_count[a] += 1
            matches.append({"index": len(matches), "round": r + 1, "p1": a, "p2": b})
    return matches, p1_count, pair_count


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--name", default="schedule_main")
    ap.add_argument("--personas", type=int, default=30)
    ap.add_argument("--matches-per-persona", type=int, default=20)
    ap.add_argument("--holdout", type=int, default=6)
    ap.add_argument("--lo", type=float, default=0.1)
    ap.add_argument("--hi", type=float, default=0.9)
    ap.add_argument("--max-repeat", type=int, default=2)
    ap.add_argument("--seed", type=int, default=20261013)
    ap.add_argument("--out-dir", default=os.path.normpath(os.path.join(HERE, "..", "schedules")))
    args = ap.parse_args()

    if args.personas % 2:
        raise SystemExit("--personas must be even (every persona plays in every round).")

    rng = np.random.default_rng(args.seed)
    knobs = np.round(args.lo + latin_hypercube(args.personas, 3, rng) * (args.hi - args.lo), 3)
    ids = [f"S{i + 1:02d}" for i in range(args.personas)]
    holdout_idx = pick_holdout(knobs, args.holdout, 0.2, 0.8, rng)
    holdout = {ids[i] for i in holdout_idx}

    matches, p1_count, pair_count = make_rounds(ids, args.matches_per_persona, holdout, args.max_repeat, rng)

    personas = [{"id": ids[i], "aggression": float(knobs[i, 0]), "defense": float(knobs[i, 1]),
                 "mobility": float(knobs[i, 2]), "patience": 0.5, "holdout": ids[i] in holdout}
                for i in range(args.personas)]
    doc = {
        "name": args.name,
        "seed": args.seed,
        "createdUtc": datetime.now(timezone.utc).isoformat(timespec="seconds"),
        "knobRange": [args.lo, args.hi],
        "matchesPerPersona": args.matches_per_persona,
        "personas": personas,
        "matches": matches,
    }

    os.makedirs(args.out_dir, exist_ok=True)
    with open(os.path.join(args.out_dir, args.name + ".json"), "w", encoding="utf-8") as f:
        json.dump(doc, f, indent=1)
    with open(os.path.join(args.out_dir, args.name + "_personas.csv"), "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(personas[0].keys()) + ["p1_matches", "p2_matches"])
        w.writeheader()
        for p in personas:
            w.writerow({**p, "p1_matches": p1_count[p["id"]],
                        "p2_matches": args.matches_per_persona - p1_count[p["id"]]})

    print(f"{len(personas)} personas ({len(holdout)} hold-out: {sorted(holdout)}), {len(matches)} matches")
    print(f"P1 matches per persona: min {min(p1_count.values())}, max {max(p1_count.values())}")
    print(f"pair repeats: max {max(pair_count.values())}, distinct pairs {len(pair_count)}")
    print(f"saved to {args.out_dir}")


if __name__ == "__main__":
    main()
