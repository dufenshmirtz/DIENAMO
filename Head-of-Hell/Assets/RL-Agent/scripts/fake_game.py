"""Fake game for testing rl_finetune.py without Unity: speaks exactly the ILRemotePolicy.cs protocol (version 2, 05/11)
(BinaryWriter little-endian). Reward rule (toy): the action index `good` deals 10 damage, every other action takes 1.
Style (toy): the "measured" attack rate of a round = 0.1 + share of `good` actions; requested style = [0.2, ...]."""
import json, socket, struct, sys, threading, numpy as np
def game(port, n_in, good, episodes, steps, seed):
    rng = np.random.default_rng(seed)
    s = socket.create_connection(("127.0.0.1", port)); f = s.makefile("rwb", buffering=0)
    rd = lambda: struct.unpack("<i", f.read(4))[0]
    hits = []
    for ep in range(episodes):
        j = json.dumps({"matchId": f"m{ep}", "self": "IL_X", "opp": "S01", "model": "fake", "vocab": 0, "inputDim": n_in, "shard": 0,
                        "protocol": 2, "style": [0.2, 0.7, 0.06, 0.78, 0.04]}).encode()
        f.write(struct.pack("<ii", 1, len(j)) + j); rd()
        dealt = taken = 0.0; nh = 0
        for t in range(steps):
            x = rng.standard_normal(n_in).astype("<f4") * 0.3
            f.write(struct.pack("<iff", 2, dealt, taken) + x.tobytes()); a = rd()
            dealt, taken = (10.0, 0.0) if a == good else (0.0, 1.0); nh += a == good
        measured = [0.1 + nh / steps, 0.7, 0.06, 0.78, 0.04]
        f.write(struct.pack("<iffii", 3, dealt, taken, 1 if nh > steps * 0.05 else -1, 0) + struct.pack("<i5f", 1, *measured)); rd()
        hits.append(nh / steps)
        if ep % 10 == 0: print(f"[fake {port}] episode {ep} good-action share {np.mean(hits[-10:]):.3f}", flush=True)
    s.close()
if __name__ == "__main__":
    port, n, n_in, good, eps, steps = map(int, sys.argv[1:7])
    th = [threading.Thread(target=game, args=(port + k, n_in, good, eps, steps, k)) for k in range(n)]
    [t.start() for t in th]; [t.join() for t in th]
