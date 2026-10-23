"""
Export a trained policy (train_bc_v2.py: model.npz + model.json) to ONE JSON file that Unity reads as a
TextAsset (ILAgentController). Weights are stored row-major: W[i, j] -> W[i * cols + j].

A test vector (input + expected logits from NumPy) is included; ILAgentController runs it once when the
model is loaded and logs the largest difference, so a broken export is caught immediately.
"""

import argparse
import json
import os

import numpy as np


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--model-dir", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--data-dir", default=None,
                    help="dataset the model was trained on: its TRAIN personas give styleMin/styleMax (online clamp)")
    ap.add_argument("--extra-data-dir", nargs="*", default=[],
                    help="further datasets (e.g. human fine-tune data): the style range becomes the UNION with their train personas")
    ap.add_argument("--trained-self", nargs="+", default=["Skipler"], help="characters the agent was trained to play")
    ap.add_argument("--name", default=None, help="model name stored in the file (default: model folder name)")
    ap.add_argument("--trained-opp", nargs="+", default=["Skipler"], help="opponent characters seen in training")
    args = ap.parse_args()

    w = dict(np.load(os.path.join(args.model_dir, "model.npz")))
    info = json.load(open(os.path.join(args.model_dir, "model.json")))
    n_in = w["W1"].shape[0]

    joint = info.get("output", {}).get("type") == "joint"
    if joint:
        info["heads"] = {"names": info["output"]["head_names"], "sizes": info["output"]["head_sizes"]}

    # Style range of the training personas (split 0). Online, the style given to the agent is clamped to it:
    # outside this range the agent would be asked for a style it never saw.
    style_min, style_max = [], []
    if args.data_dir:
        d = np.load(os.path.join(args.data_dir, "dataset.npz"))
        dmeta = json.load(open(os.path.join(args.data_dir, "dataset.json")))
        axes = dmeta.get("style_axes", info["input"]["style_axes"])
        assert list(axes) == list(info["input"]["style_axes"]), f"style axes differ: {axes}"
        tr = d["style_raw"][d["split"] == 0]
        style_min = [float(v) for v in tr.min(axis=0)]
        style_max = [float(v) for v in tr.max(axis=0)]
        print("style range (train personas):", [f"{a}: {lo:.3f}-{hi:.3f}" for a, lo, hi in zip(axes, style_min, style_max)])
        for extra in args.extra_data_dir:   # without --extra-data-dir nothing changes
            de = np.load(os.path.join(extra, "dataset.npz"))
            te = de["style_raw"][de["split"] == 0]
            if len(te):
                style_min = [min(a, float(b)) for a, b in zip(style_min, te.min(axis=0))]
                style_max = [max(a, float(b)) for a, b in zip(style_max, te.max(axis=0))]
        if args.extra_data_dir:
            print("style range (union with extra data):", [f"{a}: {lo:.3f}-{hi:.3f}" for a, lo, hi in zip(axes, style_min, style_max)])

    rng = np.random.default_rng(0)
    x = rng.uniform(-1, 1, n_in).astype(np.float32)
    h1 = np.maximum(x @ w["W1"] + w["b1"], 0)
    h2 = np.maximum(h1 @ w["W2"] + w["b2"], 0)
    logits = h2 @ w["W3"] + w["b3"]

    doc = {
        "name": args.name or os.path.basename(os.path.normpath(args.model_dir)),
        "obsDim": int(info["input"]["obs_dim"]),
        "styleDim": len(info["input"]["style_axes"]),
        "styleAxes": info["input"]["style_axes"],
        "styleMean": [float(v) for v in info["input"]["style_mean"]],
        "styleStd": [float(v) for v in info["input"]["style_std"]],
        "styleMin": style_min,
        "styleMax": style_max,
        "trainedSelf": args.trained_self,
        "trainedOpp": args.trained_opp,
        "ego": bool(info["input"].get("ego", False)),
        "quickDim": 1 if info["input"].get("quick_ready", False) else 0,
        "historyDim": int(info["input"].get("history_dim", 0)),
        "historyCap": int(info["input"].get("history_cap", 25)),
        "inputDim": int(n_in),
        "hidden1": int(w["W1"].shape[1]),
        "hidden2": int(w["W2"].shape[1]),
        "headNames": info["heads"]["names"],
        "headSizes": info["heads"]["sizes"],
        # joint output: one softmax over action combinations; vocab = K rows x 9 head values (egocentric move)
        "outputType": "joint" if joint else "heads",
        "vocabSize": int(len(w["vocab"])) if joint else 0,
        "vocab": w["vocab"].ravel().astype(int).tolist() if joint else [],
        "W1": w["W1"].ravel().tolist(), "b1": w["b1"].tolist(),
        "W2": w["W2"].ravel().tolist(), "b2": w["b2"].tolist(),
        "W3": w["W3"].ravel().tolist(), "b3": w["b3"].tolist(),
        "testInput": x.tolist(),
        "testLogits": logits.tolist(),
    }
    with open(args.out, "w") as f:
        json.dump(doc, f, separators=(",", ":"))
    print(f"saved {args.out} ({os.path.getsize(args.out) / 1e6:.2f} MB), input {n_in}")


if __name__ == "__main__":
    main()
