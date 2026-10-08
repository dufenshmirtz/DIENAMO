import os
import json
import argparse
import warnings

import numpy as np
import pandas as pd
import matplotlib.pyplot as plt


def ensure_dir(path: str) -> None:
    os.makedirs(path, exist_ok=True)


def safe_minmax(series: pd.Series) -> pd.Series:
    s = pd.to_numeric(series, errors="coerce").fillna(0.0)
    mn, mx = s.min(), s.max()
    if pd.isna(mn) or pd.isna(mx) or mn == mx:
        return pd.Series(np.zeros(len(s)), index=s.index, dtype=float)
    return (s - mn) / (mx - mn)


def find_first_existing(df: pd.DataFrame, candidates: list[str], fallback: str) -> str:
    for c in candidates:
        if c in df.columns:
            return c
    return fallback


def classify_style(row: pd.Series) -> str:
    a = row["Aggression_norm"]
    d = row["Defense_norm"]
    m = row["Mobility_norm"]
    r = row["Risk_norm"]

    scores = {
        "Aggressive": a,
        "Defensive": d,
        "Mobile": m,
        "Risky": r,
    }

    # sort axes
    sorted_axes = sorted(scores.items(), key=lambda x: x[1], reverse=True)

    top_label, top_value = sorted_axes[0]
    second_label, second_value = sorted_axes[1]

    # BALANCED
    if top_value < 0.55 or (top_value - second_value) < 0.08:
        return "Wildcard"

    # COMBO styles
    if top_label == "Aggressive" and second_label == "Defensive" and second_value > 0.60:
        return "Calculated Aggressor"

    if top_label == "Aggressive" and second_label == "Risky" and second_value > 0.60:
        return "Reckless Brawler"

    if top_label == "Aggressive" and second_label == "Mobile" and second_value > 0.60:
        return "Rushdown"

    if top_label == "Defensive" and second_label == "Mobile" and second_value > 0.60:
        return "Evasive Defender"

    if top_label == "Defensive" and second_label == "Risky" and second_value > 0.60:
        return "Counter Puncher"

    if top_label == "Mobile" and second_label == "Risky" and second_value > 0.60:
        return "Daredevil"

    # SOLO styles
    solo_map = {
        "Aggressive": "Aggressor",
        "Defensive": "Guardian",
        "Mobile": "Runner",
        "Risky": "Gambler",
    }

    return solo_map.get(top_label, top_label)

def scatter_plot(df: pd.DataFrame, x: str, y: str, label_col: str, title: str, out_path: str) -> None:
    plt.figure(figsize=(10, 7))

    labels = df[label_col].astype(str)
    unique_labels = list(pd.unique(labels))

    for label in unique_labels:
        mask = labels == label
        plt.scatter(
            df.loc[mask, x],
            df.loc[mask, y],
            label=label,
            s=120,
            alpha=0.85,
        )

    for _, row in df.iterrows():
        plt.annotate(
            str(row[label_col]),
            (row[x], row[y]),
            fontsize=8,
            xytext=(4, 4),
            textcoords="offset points",
            alpha=0.8,
        )

    plt.xlabel(x)
    plt.ylabel(y)
    plt.title(title)
    plt.grid(True, alpha=0.25)

    if len(unique_labels) <= 12:
        plt.legend(title=label_col, fontsize=8)
    else:
        plt.legend(title=label_col, fontsize=8, bbox_to_anchor=(1.02, 1), loc="upper left")

    plt.tight_layout()
    plt.savefig(out_path, dpi=200, bbox_inches="tight")
    plt.close()


def radar_chart(
    df: pd.DataFrame,
    label_col: str,
    metrics: list[str],
    metric_labels: list[str],
    title: str,
    out_path: str,
) -> None:
    num_vars = len(metrics)
    angles = np.linspace(0, 2 * np.pi, num_vars, endpoint=False).tolist()
    angles += angles[:1]

    plt.figure(figsize=(8, 8))
    ax = plt.subplot(111, polar=True)

    for _, row in df.iterrows():
        values = [float(row[m]) for m in metrics]
        values += values[:1]
        ax.plot(angles, values, linewidth=2, label=str(row[label_col]))
        ax.fill(angles, values, alpha=0.08)

    ax.set_xticks(angles[:-1])
    ax.set_xticklabels(metric_labels)
    ax.set_yticklabels([])
    ax.set_title(title, pad=20)

    if len(df) <= 10:
        ax.legend(loc="upper right", bbox_to_anchor=(1.25, 1.10), fontsize=8)

    plt.tight_layout()
    plt.savefig(out_path, dpi=200, bbox_inches="tight")
    plt.close()


SUBSTYLE_AXES = {
    "aggression": [
        "quick_rate",
        "heavy_rate",
        "special_rate",
        "charge_rate",
        "projectile_rate",
    ],
    "mobility": [
        "mobility_rate",
        "dodge_rate",
    ],
    "defense": [
        "block_rate",
        "dodge_rate",
        "dps_taken",
    ],
    "risk": [
        "miss_rate",
        "charge_rate",
        "special_rate",
        "dps_taken",
    ],
}


def get_available_substyle_axes(df: pd.DataFrame) -> dict[str, list[str]]:
    available: dict[str, list[str]] = {}
    for axis_name, metrics in SUBSTYLE_AXES.items():
        existing_metrics = [m for m in metrics if m in df.columns]
        if existing_metrics:
            available[axis_name] = existing_metrics
    return available


def main() -> None:
    parser = argparse.ArgumentParser(description="Create player-level style map from exported profile_analysis.json.")
    parser.add_argument(
        "--input",
        default=os.path.join("out", "profile_analysis", "profile_analysis.json"),
        help="Input profile_analysis.json path",
    )
    parser.add_argument(
        "--output-dir",
        default="player_style_maps",
        help="Output directory",
    )
    args = parser.parse_args()

    if not os.path.exists(args.input):
        raise FileNotFoundError(f"Input JSON not found: {args.input}")

    ensure_dir(args.output_dir)

    with open(args.input, "r", encoding="utf-8") as f:
        payload = json.load(f)

    profiles = payload.get("profiles", [])
    if not profiles:
        raise ValueError("No profiles found in profile_analysis.json")

    df = pd.DataFrame(profiles)
    if df.empty:
        raise ValueError("Input JSON produced empty DataFrame.")

    id_col = find_first_existing(
        df,
        ["profile_name", "profile_id", "player_id", "player_slot"],
        df.columns[0],
    )

    invalid_labels = {"UNKNOWN", "GUEST", "", "NONE", "NAN", "UNKNOWN_PROFILE", "UNKNOWN_PROFILE_NAME"}
    df[id_col] = df[id_col].astype(str).str.strip()
    df = df[~df[id_col].str.upper().isin(invalid_labels)]

    if df.empty:
        raise ValueError("No valid profiles left after UNKNOWN/GUEST cleanup.")

    required_json_cols = [
        "aggression_raw", "defense_raw", "mobility_raw", "risk_raw",
        "aggression", "defense", "mobility", "risk"
    ]
    missing_json = [c for c in required_json_cols if c not in df.columns]
    if missing_json:
        raise ValueError(f"Missing required JSON columns: {missing_json}")

    for c in required_json_cols:
        df[c] = pd.to_numeric(df[c], errors="coerce").fillna(0.0)

    # Keep existing player_style_map plotting/classification logic unchanged
    # by adapting exported JSON field names to the names this script already expects.
    df["Aggression"] = df["aggression_raw"]
    df["Defense"] = df["defense_raw"]
    df["Mobility"] = df["mobility_raw"]
    df["Risk"] = df["risk_raw"]

    df["Aggression_norm"] = df["aggression"]
    df["Defense_norm"] = df["defense"]
    df["Mobility_norm"] = df["mobility"]
    df["Risk_norm"] = df["risk"]

    available_substyle_axes = get_available_substyle_axes(df)
    missing_substyle_cols = sorted({m for metrics in SUBSTYLE_AXES.values() for m in metrics if m not in df.columns})
    if missing_substyle_cols:
        print("[WARN] Missing substyle columns in input summary, skipping them:", ", ".join(missing_substyle_cols))

    for axis_metrics in available_substyle_axes.values():
        for col in axis_metrics:
            df[col] = pd.to_numeric(df[col], errors="coerce").fillna(0.0)
            df[f"{col}_norm"] = safe_minmax(df[col])

    # Keep existing logic; this will match export_profile_analysis.py labels
    # as long as the normalized values in JSON are the same source of truth.
    df["style_label"] = df.apply(classify_style, axis=1)

    out_csv = os.path.join(args.output_dir, "player_style_map.csv")
    df.to_csv(out_csv, index=False)

    scatter_plot(
        df,
        x="Aggression_norm",
        y="Defense_norm",
        label_col=id_col,
        title="Player Style Map: Aggression vs Defense",
        out_path=os.path.join(args.output_dir, "player_aggression_vs_defense.png"),
    )

    scatter_plot(
        df,
        x="Mobility_norm",
        y="Risk_norm",
        label_col=id_col,
        title="Player Style Map: Mobility vs Risk",
        out_path=os.path.join(args.output_dir, "player_mobility_vs_risk.png"),
    )

    radar_chart(
        df,
        label_col=id_col,
        metrics=["Aggression", "Defense", "Mobility", "Risk"],
        metric_labels=["Aggression", "Defense", "Mobility", "Risk"],
        title="Player Style Radar Chart (Raw Metrics)",
        out_path=os.path.join(args.output_dir, "player_style_radar_raw.png"),
    )

    radar_chart(
        df,
        label_col=id_col,
        metrics=["Aggression_norm", "Defense_norm", "Mobility_norm", "Risk_norm"],
        metric_labels=["Aggression", "Defense", "Mobility", "Risk"],
        title="Player Style Radar Chart (Normalized Metrics)",
        out_path=os.path.join(args.output_dir, "player_style_radar_normalized.png"),
    )

    radar_chart(
        df,
        label_col=id_col,
        metrics=["Aggression_norm", "Defense_norm", "Mobility_norm", "Risk_norm"],
        metric_labels=["Aggression", "Defense", "Mobility", "Risk"],
        title="Player Style Radar Chart",
        out_path=os.path.join(args.output_dir, "player_style_radar.png"),
    )

    for axis_name, metrics in available_substyle_axes.items():
        radar_chart(
            df,
            label_col=id_col,
            metrics=metrics,
            metric_labels=metrics,
            title=f"{axis_name.capitalize()} Breakdown Radar (Raw Metrics)",
            out_path=os.path.join(args.output_dir, f"{axis_name}_breakdown_radar_raw.png"),
        )

        radar_chart(
            df,
            label_col=id_col,
            metrics=[f"{m}_norm" for m in metrics],
            metric_labels=metrics,
            title=f"{axis_name.capitalize()} Breakdown Radar (Normalized Metrics)",
            out_path=os.path.join(args.output_dir, f"{axis_name}_breakdown_radar_normalized.png"),
        )

    summary_cols = [
        id_col,
        "Aggression",
        "Defense",
        "Mobility",
        "Risk",
        "Aggression_norm",
        "Defense_norm",
        "Mobility_norm",
        "Risk_norm",
        "style_label",
    ]
    df[summary_cols].to_csv(
        os.path.join(args.output_dir, "player_style_summary.csv"),
        index=False,
    )

    print("Saved outputs to:", args.output_dir)
    print("-", out_csv)
    print("-", os.path.join(args.output_dir, "player_style_summary.csv"))
    print("-", os.path.join(args.output_dir, "player_aggression_vs_defense.png"))
    print("-", os.path.join(args.output_dir, "player_mobility_vs_risk.png"))
    print("-", os.path.join(args.output_dir, "player_style_radar_raw.png"))
    print("-", os.path.join(args.output_dir, "player_style_radar_normalized.png"))
    print("-", os.path.join(args.output_dir, "player_style_radar.png"))

    if available_substyle_axes:
        print("Generated substyle radar charts for axes:", ", ".join(sorted(available_substyle_axes.keys())))
    else:
        print("No substyle radar charts generated because no substyle columns were available in the input summary.")


if __name__ == "__main__":
    with warnings.catch_warnings():
        warnings.simplefilter("ignore")
        main()