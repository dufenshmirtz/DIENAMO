"""
Action-history features (thesis: IL agent, theta v2). The SAME definition is implemented in
ILAgentController.cs, which computes them from the agent's own previous decisions.

For every row (one player, one 80 ms decision) the features use ONLY earlier decisions of the same player
in the same match:
  - prev_action one-hot (21): the 9 heads of the previous decision (all zeros at the first decision)
  - since_<press> (6): decisions since the last jump / drop / light / heavy / special / parry press,
                       min(n, CAP) / CAP  (1.0 = long ago or never)
  - charge_hold, block_hold (2): consecutive previous decisions with charge held (head 7 == 1) / block held,
                       min(n, CAP) / CAP
Total 29 floats. CAP = 25 decisions = 2 s.
"""

import numpy as np

HEAD_SIZES = [3, 2, 2, 2, 2, 2, 2, 3, 2]
PRESS_HEADS = [1, 2, 3, 4, 6, 8]          # jump, drop, light, heavy, special, parry
CAP = 25
N_FEATURES = sum(HEAD_SIZES) + len(PRESS_HEADS) + 2
OFFSETS = np.cumsum([0] + HEAD_SIZES[:-1])


def compute(act, match, slot, tick):
    n = len(act)
    out = np.zeros((n, N_FEATURES), dtype=np.float32)
    key = match.astype(np.int64) * 2 + slot.astype(np.int64)
    order = np.lexsort((tick, key))
    prev_key = None
    for idx in order:
        k = key[idx]
        if k != prev_key:                                  # new player stream
            prev = None
            since = [CAP] * len(PRESS_HEADS)
            charge_run = block_run = 0
            prev_key = k
        f = out[idx]
        if prev is not None:
            for h in range(len(HEAD_SIZES)):
                f[OFFSETS[h] + prev[h]] = 1.0
        base = sum(HEAD_SIZES)
        for j in range(len(PRESS_HEADS)):
            f[base + j] = min(since[j], CAP) / CAP
        f[base + len(PRESS_HEADS)] = min(charge_run, CAP) / CAP
        f[base + len(PRESS_HEADS) + 1] = min(block_run, CAP) / CAP
        # update with THIS decision (used by the next one)
        a = act[idx]
        for j, h in enumerate(PRESS_HEADS):
            since[j] = 1 if a[h] == 1 else since[j] + 1
        charge_run = charge_run + 1 if a[7] == 1 else 0
        block_run = block_run + 1 if a[5] == 1 else 0
        prev = a
    return out
