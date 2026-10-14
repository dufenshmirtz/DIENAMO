"""
Egocentric frame (thesis: IL agent, theta v2). Mirrored exactly in ILAgentController.cs.

The observation has no absolute x position, so the game looks the same for a player whose opponent is on the
right and, mirrored, for one whose opponent is on the left. We therefore express everything as if the opponent
were always on the RIGHT:
  d = oppDirSign (obs[64], +1 opponent to the right, -1 to the left; Unity's Mathf.Sign never returns 0)
  x-dependent observation entries are multiplied by d:
     0 relative x, 4 own x-velocity, 6 opponent x-velocity, 64 opponent direction (-> always +1), 65 own facing
  move head: 0 = away, 1 = none, 2 = towards (world left/right swapped when d = -1)
Both sides of every match then teach the same "towards / away" behaviour (this is also a mirror augmentation).
"""

import numpy as np

X_COLUMNS = [0, 4, 6, 64, 65]
DIR_COLUMN = 64


def direction(obs):
    d = np.sign(obs[:, DIR_COLUMN]).astype(np.float32)
    d[d == 0] = 1.0
    return d


def obs_to_ego(obs):
    d = direction(obs)
    out = obs.copy()
    out[:, X_COLUMNS] *= d[:, None]
    return out, d


def act_to_ego(act, d):
    out = act.copy()
    flip = d < 0
    out[flip, 0] = 2 - out[flip, 0]
    return out
