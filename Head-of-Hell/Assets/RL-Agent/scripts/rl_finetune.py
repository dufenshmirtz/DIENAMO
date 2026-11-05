"""
RL fine-tuning of the IL agent (thesis, 02/11, branch RLTeach): PPO with a KL "leash" to a frozen reference policy.

The game build plays the matches (ILDataGenDirector, schedule with an ILAgent persona marked "rl": true, build argument
-rlport). At every decision the build sends the agent's 101 inputs (built exactly as in normal play by ILAgentController)
over a local TCP socket; this script answers with the index of an action combination of the model's vocabulary
(see Scripts/Thesis/ILRemotePolicy.cs for the protocol). While it waits, the game does not advance (locked step).

Pure NumPy (no torch / ML-Agents needed; Python 3.12 OK).
Policy  = the IL model's network (101 -> 256 -> 256 -> K), initialised from the IL model (e.g. theta8h_q0_e60_joint.json).
Value   = a separate network of the same size (new), trained first alone for --critic-warmup updates.
Reward  = (damage dealt - damage taken) / 100 per decision, +1 win / -1 loss at the end. Nothing else (no shaping).
          Optional (05/11, --style-weight w > 0, build protocol 2): at the end of a round, - w * mean over the 5 axes of
          |measured style - requested style| / style std (the model's normalisation): the agent must win WITH the style
          it was asked for. The measured style comes from StyleAxes in the game (same code as the analysis).
Leash   = beta * KL(pi || pi_ref) in the loss, pi_ref = a frozen IL model (default: the start model). With a reference
          that has another vocabulary (e.g. thetaH, human-only), its probabilities are mapped onto the policy's
          combinations; combinations it never uses get a small floor (--ref-floor) so the KL stays finite.
Output  = every --export-every updates a normal model file (same JSON format as export_theta_unity.py) that the existing
          ILAgentController plays in the evaluations and in PvE.

Example (Windows, from RL-Agent/scripts):
  python rl_finetune.py --exe "C:/Users/arish/Documents/DIENAMO/IL_Mult_Scene/HoH.exe" --schedule schedule_rl_train
         --init ../models/theta8h_q0_e60_joint.json --out RLrun_b005 --beta 0.05 --n 4 --hours 3
"""
import argparse
import csv
import glob
import json
import os
import socket
import struct
import subprocess
import sys
import threading
import time

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
SCHED_DIR = os.path.normpath(os.path.join(HERE, "..", "schedules"))


def default_root():
    return os.path.join(os.path.expanduser("~"), "Documents", "My Games", "Head of Hell")


# ---------------------------------------------------------------- networks (NumPy, no torch needed)
class MLP:
    """n_in -> h1 -> h2 -> n_out, ReLU; weights stored as in the Unity file (W[i, j] = input i -> unit j)."""

    def __init__(self, n_in, h1, h2, n_out, rng=None, zero_last=False):
        rng = rng or np.random.default_rng(0)
        he = lambda a, b: (rng.standard_normal((a, b)) * np.sqrt(2.0 / a)).astype(np.float32)
        self.p = {"W1": he(n_in, h1), "b1": np.zeros(h1, np.float32), "W2": he(h1, h2), "b2": np.zeros(h2, np.float32),
                  "W3": (np.zeros((h2, n_out), np.float32) if zero_last else he(h2, n_out) * 0.1), "b3": np.zeros(n_out, np.float32)}

    def forward(self, x, keep=False):
        a1 = np.maximum(x @ self.p["W1"] + self.p["b1"], 0)
        a2 = np.maximum(a1 @ self.p["W2"] + self.p["b2"], 0)
        out = a2 @ self.p["W3"] + self.p["b3"]
        if keep:
            self.cache = (x, a1, a2)
        return out

    def backward(self, dout):
        x, a1, a2 = self.cache
        g = {"W3": a2.T @ dout, "b3": dout.sum(0)}
        d2 = (dout @ self.p["W3"].T) * (a2 > 0)
        g["W2"], g["b2"] = a1.T @ d2, d2.sum(0)
        d1 = (d2 @ self.p["W2"].T) * (a1 > 0)
        g["W1"], g["b1"] = x.T @ d1, d1.sum(0)
        return g

    def copy(self):
        m = MLP.__new__(MLP); m.p = {k: v.copy() for k, v in self.p.items()}; return m


class Adam:
    def __init__(self, params, lr, max_norm=None):
        self.p, self.lr, self.max_norm, self.t = params, lr, max_norm, 0
        self.m = {k: np.zeros_like(v) for k, v in params.items()}; self.v = {k: np.zeros_like(v) for k, v in params.items()}

    def step(self, g):
        if self.max_norm:
            norm = np.sqrt(sum(float((x * x).sum()) for x in g.values()))
            if norm > self.max_norm:
                g = {k: x * (self.max_norm / norm) for k, x in g.items()}
        self.t += 1
        for k in self.p:
            self.m[k] = 0.9 * self.m[k] + 0.1 * g[k]; self.v[k] = 0.999 * self.v[k] + 0.001 * g[k] * g[k]
            self.p[k] -= self.lr * (self.m[k] / (1 - 0.9 ** self.t)) / (np.sqrt(self.v[k] / (1 - 0.999 ** self.t)) + 1e-8)


def log_softmax(z):
    z = z - z.max(-1, keepdims=True)
    return z - np.log(np.exp(z).sum(-1, keepdims=True))


def load_unity_model(path):
    """Unity JSON (export_theta_unity.py): W1[i*n1+j] = input i -> hidden j."""
    d = json.load(open(path, encoding="utf-8"))
    n_in, h1, h2, k = d["inputDim"], d["hidden1"], d["hidden2"], d["vocabSize"]
    net = MLP.__new__(MLP)
    net.p = {"W1": np.array(d["W1"], np.float32).reshape(n_in, h1), "b1": np.array(d["b1"], np.float32),
             "W2": np.array(d["W2"], np.float32).reshape(h1, h2), "b2": np.array(d["b2"], np.float32),
             "W3": np.array(d["W3"], np.float32).reshape(h2, k), "b3": np.array(d["b3"], np.float32)}
    vocab = np.array(d["vocab"], np.int64).reshape(k, len(d["headSizes"]))
    got = net.forward(np.array(d["testInput"], np.float32)[None])[0]
    err = float(np.abs(got - np.array(d["testLogits"], np.float32)).max())
    if err > 1e-3:
        sys.exit(f"{path}: self-test failed (max diff {err})")
    return d, net, vocab


def export_unity_model(base, net, path, name):
    d = dict(base)
    for k in ("W1", "b1", "W2", "b2", "W3", "b3"):
        d[k] = net.p[k].reshape(-1).astype(float).tolist()
    d["testLogits"] = net.forward(np.array(d["testInput"], np.float32)[None])[0].astype(float).tolist()
    d["name"] = name
    with open(path, "w", encoding="utf-8") as f:
        json.dump(d, f)


def ref_mapping(ref_vocab, pol_vocab):
    """index of each policy combination in the reference vocabulary (-1 = the reference never uses it)."""
    idx = {tuple(v): i for i, v in enumerate(ref_vocab)}
    return np.array([idx.get(tuple(v), -1) for v in pol_vocab], np.int64)


# ---------------------------------------------------------------- one game process
class Env(threading.Thread):
    def __init__(self, k, port, trainer):
        super().__init__(daemon=True)
        self.k, self.port, self.tr = k, port, trainer
        self.buf = []            # transitions of the running episode / since the last update: dicts
        self.alive = True
        self.connected = False

    def run(self):
        srv = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        srv.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        srv.bind(("127.0.0.1", self.port)); srv.listen(1)
        conn, _ = srv.accept()
        conn.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
        self.connected = True
        print(f"[env {self.k}] game connected on port {self.port}", flush=True)
        f = conn.makefile("rwb", buffering=0)
        tr = self.tr
        ep = None
        try:
            while True:
                (t,) = struct.unpack("<i", self._read(f, 4))
                if t == 1:                                   # START
                    (n,) = struct.unpack("<i", self._read(f, 4))
                    ep = json.loads(self._read(f, n).decode("utf-8"))
                    ep.update(ret=0.0, dealt=0.0, taken=0.0, steps=0)
                    f.write(struct.pack("<i", 0))
                elif t == 2:                                 # STEP
                    dealt, taken = struct.unpack("<ff", self._read(f, 8))
                    x = np.frombuffer(self._read(f, 4 * tr.n_in), dtype="<f4").astype(np.float32)
                    r = tr.step_reward(dealt, taken)
                    with tr.lock:                            # blocks while the trainer updates the networks
                        if self.buf and self.buf[-1]["r"] is None:
                            self.buf[-1]["r"] = r
                        a, logp, v = tr.act(x)
                        self.buf.append(dict(x=x, a=a, logp=logp, v=v, r=None, done=False))
                    ep["ret"] += r; ep["dealt"] += dealt; ep["taken"] += taken; ep["steps"] += 1
                    f.write(struct.pack("<i", a))
                elif t == 3:                                 # END
                    dealt, taken, outcome, trunc = struct.unpack("<ffii", self._read(f, 16))
                    style_ok, measured = 0, None
                    if ep is not None and ep.get("protocol", 1) >= 2:
                        style_ok, *measured = struct.unpack("<i5f", self._read(f, 24))
                    r = tr.step_reward(dealt, taken) + (tr.args.win_reward * outcome)
                    sdist = tr.style_distance(ep, measured) if (style_ok and ep is not None) else None
                    if sdist is not None:
                        r -= tr.args.style_weight * sdist
                    with tr.lock:
                        if self.buf and self.buf[-1]["r"] is None:
                            self.buf[-1]["r"] = r
                            self.buf[-1]["done"] = True
                    if ep is not None:
                        ep["ret"] += r; ep["dealt"] += dealt; ep["taken"] += taken
                        ep.update(outcome=outcome, truncated=trunc, env=self.k, style_dist=sdist)
                        tr.episode_done(ep)
                    ep = None
                    f.write(struct.pack("<i", 0))
                else:
                    raise IOError(f"unknown message type {t}")
        except Exception as e:
            print(f"[env {self.k}] connection closed: {e}", flush=True)
        self.alive = False

    @staticmethod
    def _read(f, n):
        b = b""
        while len(b) < n:
            c = f.read(n - len(b))
            if not c:
                raise IOError("socket closed")
            b += c
        return b


# ---------------------------------------------------------------- PPO
class Trainer:
    def __init__(self, args):
        self.args = args
        self.rng = np.random.default_rng(args.seed)
        self.base, self.pi, self.vocab = load_unity_model(args.init)
        self.n_in, self.k = self.base["inputDim"], self.base["vocabSize"]
        ref_path = args.ref or args.init
        rd, self.ref, ref_vocab = load_unity_model(ref_path)
        self.ref_map = ref_mapping(ref_vocab, self.vocab)
        print(f"policy {os.path.basename(args.init)} (K={self.k}), reference {os.path.basename(ref_path)} "
              f"(K={rd['vocabSize']}; policy combinations unknown to the reference: {int((self.ref_map < 0).sum())})")
        self.vf = MLP(self.n_in, 256, 256, 1, rng=self.rng, zero_last=True)
        self.opt_pi = Adam(self.pi.p, args.lr, max_norm=0.5)
        self.opt_vf = Adam(self.vf.p, args.vf_lr, max_norm=1.0)
        self.lock = threading.RLock()
        self.episodes, self.updates, self.total_steps = [], 0, 0

    def step_reward(self, dealt, taken):
        return (dealt - taken) / 100.0

    def style_distance(self, ep, measured):
        """mean over the 5 axes of |measured - requested| / std (the model's style normalisation)."""
        req = ep.get("style")
        if not req or measured is None or len(req) != 5:
            return None
        sd = np.array(self.base["styleStd"], np.float64)
        return float(np.mean(np.abs(np.array(measured, np.float64) - np.array(req, np.float64)) / sd))

    def act(self, x):
        lp = log_softmax(self.pi.forward(x[None])[0])
        p = np.exp(lp.astype(np.float64)); p /= p.sum()
        a = int(self.rng.choice(self.k, p=p))
        v = float(self.vf.forward(x[None])[0, 0])
        return a, float(lp[a]), v

    def episode_done(self, ep):
        self.episodes.append(ep)

    def ref_logprobs(self, x):
        """log pi_ref over the POLICY's combinations (floor for combinations the reference does not have)."""
        out = []
        for i in range(0, len(x), 8192):
            pr = np.exp(log_softmax(self.ref.forward(x[i:i + 8192])))
            m = self.ref_map
            p = np.where(m[None] >= 0, pr[:, np.clip(m, 0, None)], self.args.ref_floor)   # floor only where unknown
            out.append(np.log(p / p.sum(-1, keepdims=True)).astype(np.float32))
        return np.concatenate(out)

    def collect(self, envs):
        """Take every transition whose reward is known; keep the pending last one of a running episode."""
        X, A, LP, ADV, RET = [], [], [], [], []
        g, lam = self.args.gamma, self.args.lam
        for e in envs:
            buf = e.buf
            n_done = len(buf) if (not buf or buf[-1]["r"] is not None) else len(buf) - 1
            if n_done == 0:
                continue
            seg, rest = buf[:n_done], buf[n_done:]
            boot = rest[0]["v"] if rest else 0.0
            adv = np.zeros(len(seg), np.float32); last = 0.0
            for i in reversed(range(len(seg))):
                if seg[i]["done"]:
                    nv, nonterm = 0.0, 0.0          # episode end: nothing after it
                else:
                    nv, nonterm = (boot if i == len(seg) - 1 else seg[i + 1]["v"]), 1.0
                delta = seg[i]["r"] + g * nv - seg[i]["v"]
                last = delta + g * lam * nonterm * last
                adv[i] = last
            X += [t["x"] for t in seg]; A += [t["a"] for t in seg]; LP += [t["logp"] for t in seg]
            ADV.append(adv); RET.append(adv + np.array([t["v"] for t in seg], np.float32))
            e.buf = rest
        if not X:
            return None
        return (np.stack(X).astype(np.float32), np.array(A, np.int64), np.array(LP, np.float32),
                np.concatenate(ADV), np.concatenate(RET))

    def losses_and_grads(self, x, a, old_lp, adv, ref_lp, ret, need_pi=True):
        """PPO-clip + beta * KL(pi || pi_ref) for the policy, MSE for the value. Returns grads and stats."""
        a_ = self.args; n = len(x)
        v = self.vf.forward(x, keep=True)[:, 0]
        dv = (2.0 * (v - ret) / n)[:, None]
        g_vf = self.vf.backward(dv.astype(np.float32))
        z = self.pi.forward(x, keep=True)
        lp_all = log_softmax(z); p_all = np.exp(lp_all)
        l = lp_all - ref_lp
        kl_each = (p_all * l).sum(-1)
        lp = lp_all[np.arange(n), a]
        ratio = np.exp(lp - old_lp)
        s1, s2 = ratio * adv, np.clip(ratio, 1 - a_.clip, 1 + a_.clip) * adv
        active = (s1 <= s2).astype(np.float32)            # the unclipped term is the minimum -> it has a gradient
        onehot = np.zeros_like(p_all); onehot[np.arange(n), a] = 1.0
        dz = (-(active * ratio * adv)[:, None] * (onehot - p_all)) / n          # clip surrogate
        dz += a_.beta * p_all * (l - kl_each[:, None]) / n                       # KL leash
        g_pi = self.pi.backward(dz.astype(np.float32)) if need_pi else None
        stats = dict(ploss=float(-np.minimum(s1, s2).mean() + a_.beta * kl_each.mean()), kl_ref=float(kl_each.mean()),
                     ent=float(-(p_all * lp_all).sum(-1).mean()), clip=float((np.abs(ratio - 1) > a_.clip).mean()),
                     approx_kl=float((old_lp - lp).mean()), vloss=float(((v - ret) ** 2).mean()))
        return g_pi, g_vf, stats

    def update(self, data):
        a_ = self.args
        X, A, LP, ADV, RET = data
        n = len(X)
        ADVn = ((ADV - ADV.mean()) / (ADV.std() + 1e-8)).astype(np.float32)
        ref_lp = self.ref_logprobs(X)
        critic_only = self.updates < a_.critic_warmup
        acc = {}; nb = 0
        for _ in range(a_.epochs):
            perm = self.rng.permutation(n)
            for i in range(0, n, a_.minibatch):
                idx = perm[i:i + a_.minibatch]
                g_pi, g_vf, st = self.losses_and_grads(X[idx], A[idx], LP[idx], ADVn[idx], ref_lp[idx], RET[idx],
                                                       need_pi=not critic_only)
                self.opt_vf.step(g_vf)
                if not critic_only:
                    self.opt_pi.step(g_pi)
                for k, v in st.items():
                    acc[k] = acc.get(k, 0.0) + v
                nb += 1
        self.updates += 1
        return {k: v / max(nb, 1) for k, v in acc.items()} | {"critic_only": int(critic_only), "n": n}


# ---------------------------------------------------------------- main
def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--exe", default=None, help="game build; omitted = the games are started by hand (or fake_game.py)")
    ap.add_argument("--schedule", default="schedule_rl_train")
    ap.add_argument("--init", required=True, help="IL model to start from (Unity JSON)")
    ap.add_argument("--ref", default=None, help="frozen reference policy for the KL leash (default: --init)")
    ap.add_argument("--out", required=True, help="run name: telemetry folder + <root>/RL_runs/<out>/")
    ap.add_argument("--n", type=int, default=4)
    ap.add_argument("--port", type=int, default=5600)
    ap.add_argument("--beta", type=float, default=0.05, help="KL leash strength")
    ap.add_argument("--ref-floor", type=float, default=1e-4)
    ap.add_argument("--win-reward", type=float, default=1.0)
    ap.add_argument("--style-weight", type=float, default=0.0,
                    help="style reward (05/11): - w * mean |measured - requested| / std at the end of a round (0 = off)")
    ap.add_argument("--gamma", type=float, default=0.995)
    ap.add_argument("--lam", type=float, default=0.95)
    ap.add_argument("--clip", type=float, default=0.1)
    ap.add_argument("--lr", type=float, default=3e-5)
    ap.add_argument("--vf-lr", type=float, default=3e-4)
    ap.add_argument("--epochs", type=int, default=4)
    ap.add_argument("--minibatch", type=int, default=1024)
    ap.add_argument("--rollout", type=int, default=16384, help="decisions per update (all games together)")
    ap.add_argument("--critic-warmup", type=int, default=3, help="first updates train only the value network")
    ap.add_argument("--export-every", type=int, default=10)
    ap.add_argument("--hours", type=float, default=3.0)
    ap.add_argument("--max-updates", type=int, default=100000)
    ap.add_argument("--keep-telemetry", type=int, default=30, help="match files kept per game folder (the rest deleted)")
    ap.add_argument("--root", default=default_root())
    ap.add_argument("--seed", type=int, default=1)
    args = ap.parse_args()

    run_dir = os.path.join(args.root, "RL_runs", args.out)   # outside the repo (next to Telemetry), not imported by Unity
    os.makedirs(run_dir, exist_ok=True)
    json.dump(vars(args), open(os.path.join(run_dir, "args.json"), "w"), indent=1)
    tr = Trainer(args)

    envs = [Env(k, args.port + k, tr) for k in range(args.n)]
    for e in envs:
        e.start()
    procs = []
    if args.exe:
        sched = args.schedule if os.path.isfile(args.schedule) else os.path.join(SCHED_DIR, args.schedule.removesuffix(".json") + ".json")
        log_dir = os.path.join(args.root, "Logs_ilgen"); os.makedirs(log_dir, exist_ok=True)
        for k in range(args.n):
            cmd = [args.exe, "-ilgen", "-schedule", sched, "-out", args.out, "-shard", f"{k}/{args.n}",
                   "-ilmodel", os.path.abspath(args.init), "-rlport", str(args.port),
                   "-screen-fullscreen", "0", "-screen-width", "320", "-screen-height", "180",
                   "-logFile", os.path.join(log_dir, f"{args.out}_s{k}of{args.n}.log")]
            procs.append(subprocess.Popen(cmd)); time.sleep(1.0)
        print(f"[run] {args.n} games started")

    log_path = os.path.join(run_dir, "progress.csv")
    new = not os.path.exists(log_path)
    logf = open(log_path, "a", newline=""); log = csv.writer(logf)
    if new:
        log.writerow(["update", "minutes", "decisions", "episodes", "win", "loss", "dealt", "taken", "return",
                      "kl_ref", "entropy", "clipfrac", "approx_kl", "vloss", "critic_only", "style_dist"])
    t0 = time.time(); seen_eps = 0
    try:
        while tr.updates < args.max_updates and (time.time() - t0) < args.hours * 3600:
            time.sleep(0.5)
            if not any(e.alive for e in envs):
                print("[run] no game connected any more: stop"); break
            pending = sum(len(e.buf) for e in envs)
            if pending < args.rollout:
                continue
            with tr.lock:                     # the games wait while the networks change
                data = tr.collect(envs)
                if data is None:
                    continue
                st = tr.update(data)
            tr.total_steps += st["n"]
            eps = tr.episodes[seen_eps:]; seen_eps = len(tr.episodes)
            win = np.mean([e["outcome"] == 1 for e in eps]) if eps else float("nan")
            loss = np.mean([e["outcome"] == -1 for e in eps]) if eps else float("nan")
            row = [tr.updates, round((time.time() - t0) / 60, 1), tr.total_steps, len(eps), round(win, 3), round(loss, 3),
                   round(np.mean([e["dealt"] for e in eps]), 1) if eps else "", round(np.mean([e["taken"] for e in eps]), 1) if eps else "",
                   round(np.mean([e["ret"] for e in eps]), 3) if eps else "", round(st["kl_ref"], 4), round(st["ent"], 3),
                   round(st["clip"], 3), round(st["approx_kl"], 5), round(st["vloss"], 4), st["critic_only"]]
            sd = [e["style_dist"] for e in eps if e.get("style_dist") is not None]
            row.append(round(float(np.mean(sd)), 3) if sd else "")
            log.writerow(row); logf.flush()
            print("[upd] " + " ".join(f"{h}={v}" for h, v in zip(
                ["u", "min", "dec", "eps", "win", "loss", "dealt", "taken", "ret", "kl_ref", "ent", "clip", "akl", "vl", "co", "style"], row)), flush=True)
            if tr.updates % args.export_every == 0:
                name = f"thetaR_{args.out}_u{tr.updates}"
                export_unity_model(tr.base, tr.pi, os.path.join(run_dir, name + ".json"), name)
                np.savez(os.path.join(run_dir, "checkpoint.npz"), **{"pi_" + k: v for k, v in tr.pi.p.items()},
                         **{"vf_" + k: v for k, v in tr.vf.p.items()}, updates=tr.updates)
                print(f"[run] exported {name}.json")
            # telemetry of the training matches is not needed: keep only the newest files
            for k in range(args.n):
                folder = os.path.join(args.root, "Telemetry", f"{args.out}_s{k}of{args.n}" if args.n > 1 else args.out)
                files = sorted(glob.glob(os.path.join(folder, "match_*.json")), key=os.path.getmtime)
                for fp in files[:-args.keep_telemetry]:
                    try: os.remove(fp)
                    except OSError: pass
    finally:
        name = f"thetaR_{args.out}_final"
        export_unity_model(tr.base, tr.pi, os.path.join(run_dir, name + ".json"), name)
        print(f"[run] exported {name}.json after {tr.updates} updates")
        for p in procs:
            p.terminate()


if __name__ == "__main__":
    main()
