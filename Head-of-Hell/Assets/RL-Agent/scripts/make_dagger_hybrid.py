"""
DAgger option A (thesis, 23/10/2026): "press-only" teacher labels.

Iteration 1 (θ7) showed that the scripted teacher is not Markov in the agent's state: its HELD actions (walk
commitment, block hold, charge hold) depend on hidden timers that the agent's history does not show, so BC learned a
per-decision average and the agent flickered (walks / blocks twice as many, half as long). Here the label keeps the
teacher's choice only for the ONE-SHOT decisions and takes the held branches from the agent itself:
  teacher : jump, drop, light, heavy, special, parry, charge RELEASE (charge == 2)
  agent   : move, block, charge HOLD (charge == 0 / 1)
  charge  : 2 if the teacher releases; else, if the agent released but the teacher did not, the teacher's 0/1;
            else the agent's 0/1.
Input: a DAgger dataset (build_dataset_dagger.py: act = expertAct, hist_act = the agent's own act).
Output: the same dataset with act replaced (hist_act unchanged), plus counts of what changed.
"""
import argparse
import json
import os
import shutil

import numpy as np

PRESS = [1, 2, 3, 4, 6, 8]       # jump, drop, light, heavy, special, parry
HELD = [0, 5]                    # move, block
CHARGE = 7


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--in-dir", required=True)
    ap.add_argument("--out-dir", required=True)
    a = ap.parse_args()
    d = dict(np.load(os.path.join(a.in_dir, "dataset.npz")))
    ex, ag = d["act"].astype(np.int16), d["hist_act"].astype(np.int16)
    y = ex.copy()
    y[:, HELD] = ag[:, HELD]
    c = np.where(ex[:, CHARGE] == 2, 2, np.where(ag[:, CHARGE] == 2, ex[:, CHARGE], ag[:, CHARGE]))
    y[:, CHARGE] = c
    d["act"] = y.astype(np.int8)
    os.makedirs(a.out_dir, exist_ok=True)
    np.savez_compressed(os.path.join(a.out_dir, "dataset.npz"), **d)
    meta = json.load(open(os.path.join(a.in_dir, "dataset.json"), encoding="utf-8"))
    names = meta["action_branches"]
    stats = {"rows": int(len(y)),
             "label_equals_agent_all9": round(float((y == ag).all(1).mean()), 4),
             "label_equals_teacher_all9": round(float((y == ex).all(1).mean()), 4),
             "per_branch_label_differs_from_agent": {names[b]: round(float((y[:, b] != ag[:, b]).mean()), 4) for b in range(9)}}
    meta.update(version=meta["version"] + " + press-only labels (option A)", label="expertAct for presses, agent for move/block/charge hold",
                hybrid_stats=stats, hybrid_from=os.path.basename(os.path.normpath(a.in_dir)))
    json.dump(meta, open(os.path.join(a.out_dir, "dataset.json"), "w", encoding="utf-8"), indent=1)
    print(json.dumps(stats, indent=1))


if __name__ == "__main__":
    main()
