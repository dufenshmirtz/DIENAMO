"""
Does the trained policy actually USE the style vector? (thesis: IL agent, offline evaluation)

Two checks, on rows the model never trained on:
1. Persona fidelity: for every persona of a split, the model's mean predicted probability of each
   action ("how often would a sampling agent press it") vs the persona's real frequency in the data.
   Reported as the correlation across personas and the mean absolute error, per action.
2. Counterfactual control: take the SAME game states (all unseen rows) and give the model the style of
   each training persona in turn. If the policy listens to the style, the predicted action rates must follow
   those personas' real action rates (correlation across the 24 personas).
   A model without style input cannot do this by construction (its output does not change).
"""

import argparse
import json
import os

import numpy as np

import egocentric as EGO
import history_features as HF
import train_bc_v2 as T

NONZERO = {"move": "moving", "jump": "jump", "drop": "drop", "light": "light", "heavy": "heavy",
           "block": "block", "special": "special", "charge": "charge(any)", "parry": "parry"}


def action_rates_true(y):
    out = {}
    for h, name in enumerate(T.HEAD_NAMES):
        idle = 1 if name == "move" else 0
        out[name] = float((y[:, h] != idle).mean())
    return out


def action_rates_pred(probs):
    out = {}
    for h, name in enumerate(T.HEAD_NAMES):
        idle = 1 if name == "move" else 0
        out[name] = float(1.0 - probs[:, T.SL[h].start + idle].mean())
    return out


def load_model(model_dir):
    w = dict(np.load(os.path.join(model_dir, "model.npz")))
    info = json.load(open(os.path.join(model_dir, "model.json")))
    return w, info


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--data-dir", required=True)
    ap.add_argument("--models", nargs="+", required=True)
    ap.add_argument("--out", required=True)
    args = ap.parse_args()

    d = np.load(os.path.join(args.data_dir, "dataset.npz"))
    meta = json.load(open(os.path.join(args.data_dir, "dataset.json")))
    obs_w, act_w, split, persona, sraw = d["obs"], d["act"].astype(np.int64), d["split"], d["persona"], d["style_raw"]
    obs_e, dirs = EGO.obs_to_ego(obs_w)
    act_e = EGO.act_to_ego(act_w, dirs)
    train_personas = sorted(set(persona[split == 0].tolist()))
    hist_all = None

    report = {}
    for md in args.models:
        w, info = load_model(md)
        use_style = info["input"]["style"]
        use_hist = info["input"].get("history", False)
        ego = info["input"].get("ego", False)
        quick = info["input"].get("quick_ready", False)
        obs = obs_e if ego else obs_w
        act = act_e if ego else act_w
        hist_all = HF.compute(act, d["match"], d["slot"], d["tick"]) if use_hist else None
        mu = np.array(info["input"]["style_mean"] or [0] * 5, np.float32)
        sd = np.array(info["input"]["style_std"] or [1] * 5, np.float32)

        def run(rows, style_override=None):
            x = obs[rows]
            if quick:
                x = np.concatenate([x, d["quick_ready"][rows][:, None]], axis=1)
            if use_hist:
                x = np.concatenate([x, hist_all[rows]], axis=1)
            if use_style:
                s = sraw[rows] if style_override is None else np.tile(style_override, (len(x), 1))
                x = np.concatenate([x, (s - mu) / sd], axis=1).astype(np.float32)
            return T.predict(w, x)[1]

        res = {}
        # 1. persona fidelity on test (seen personas, unseen matches) and unseen (unseen personas)
        for sname, code in (("test", 2), ("unseen", 3)):
            per = sorted(set(persona[split == code].tolist()))
            t_rates, p_rates = [], []
            for pid in per:
                rows = (split == code) & (persona == pid)
                t_rates.append(action_rates_true(act[rows]))
                p_rates.append(action_rates_pred(run(rows)))
            fid = {}
            for name in T.HEAD_NAMES:
                t = np.array([r[name] for r in t_rates]); pr = np.array([r[name] for r in p_rates])
                fid[name] = {"corr": round(float(np.corrcoef(t, pr)[0, 1]), 3) if t.std() > 0 and pr.std() > 0 else None,
                             "mae": round(float(np.abs(t - pr).mean()), 4),
                             "true_mean": round(float(t.mean()), 4)}
            res[f"fidelity_{sname}"] = {"personas": len(per), "per_action": fid}

        # 2. counterfactual control: unseen states, style of every training persona
        if use_style:
            rows = split == 3
            sub = np.flatnonzero(rows)
            t_rates, p_rates = [], []
            for pid in train_personas:
                style = sraw[persona == pid][0]
                t_rates.append(action_rates_true(act[(split == 0) & (persona == pid)]))
                p_rates.append(action_rates_pred(run(sub, style_override=style)))
            cf = {}
            for name in T.HEAD_NAMES:
                t = np.array([r[name] for r in t_rates]); pr = np.array([r[name] for r in p_rates])
                cf[name] = {"corr": round(float(np.corrcoef(t, pr)[0, 1]), 3),
                            "pred_min": round(float(pr.min()), 4), "pred_max": round(float(pr.max()), 4),
                            "true_min": round(float(t.min()), 4), "true_max": round(float(t.max()), 4)}
            res["counterfactual_unseen_states"] = cf
        report[os.path.basename(md.rstrip("/"))] = res

    json.dump(report, open(args.out, "w"), indent=1)
    for m, r in report.items():
        print(f"== {m}")
        for k in ("fidelity_test", "fidelity_unseen"):
            print(f"  {k}: corr " + " ".join(f"{n}={v['corr']}" for n, v in r[k]["per_action"].items()))
        if "counterfactual_unseen_states" in r:
            print("  counterfactual corr " + " ".join(f"{n}={v['corr']}" for n, v in r["counterfactual_unseen_states"].items()))


if __name__ == "__main__":
    main()
