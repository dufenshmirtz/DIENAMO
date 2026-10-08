import os
import json
import argparse
from datetime import datetime, timezone

import numpy as np
import pandas as pd


# --------------------------------------------------
# Helpers
# --------------------------------------------------

ELO_LABELS = [
    (0, 1375, "Moron"),
    (1375, 1425, "Noob"),
    (1425, 1475, "Irrelevant"),
    (1475, 1525, "Tryhard"),
    (1525, 999999, "Virgin"),
]


def get_elo_label(elo: float) -> str:
    try:
        elo = float(elo)
    except Exception:
        return "Unranked"

    for low, high, label in ELO_LABELS:
        if low <= elo < high:
            return label

    return "Unranked"


def ensure_dir(path: str) -> None:
    os.makedirs(path, exist_ok=True)


def safe_minmax(series: pd.Series) -> pd.Series:
    s = pd.to_numeric(series, errors="coerce").fillna(0.0)
    mn = s.min()
    mx = s.max()
    if pd.isna(mn) or pd.isna(mx) or mx == mn:
        return pd.Series(np.zeros(len(s)), index=s.index, dtype=float)
    return (s - mn) / (mx - mn)


def validate_required_columns(df: pd.DataFrame, required: list[str]) -> None:
    missing = [c for c in required if c not in df.columns]
    if missing:
        raise ValueError(
            "Missing required columns in dataset_profile_level.csv:\n"
            + "\n".join(f"- {c}" for c in missing)
        )


# --------------------------------------------------
# Style labels
# --------------------------------------------------
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

    sorted_axes = sorted(scores.items(), key=lambda x: x[1], reverse=True)

    top_label, top_value = sorted_axes[0]
    second_label, second_value = sorted_axes[1]

    if top_value < 0.33:
        return "Scripted Bot"

    if top_label == "Aggressive" and second_label == "Defensive" and second_value > 0.60:
        return "Braindead"

    if top_label == "Aggressive" and second_label == "Risky" and second_value > 0.60:
        return "Degenerate"

    if top_label == "Aggressive" and second_label == "Mobile" and second_value > 0.60:
        return "Molesting"

    if top_label == "Defensive" and second_label == "Mobile" and second_value > 0.60:
        return "Pussy"

    if top_label == "Defensive" and second_label == "Risky" and second_value > 0.60:
        return "Traffic Cone"

    if top_label == "Mobile" and second_label == "Risky" and second_value > 0.60:
        return "Junkie"

    solo_map = {
        "Aggressive": "Rageaholic",
        "Defensive": "Coward",
        "Mobile": "Ballbuster",
        "Risky": "Insecure",
    }

    return solo_map.get(top_label, top_label)


# --------------------------------------------------
# Style computation
# --------------------------------------------------
def compute_style_axes_from_profile_df(df: pd.DataFrame) -> pd.DataFrame:
    out = df.copy()
    if "ema_parry_attempt_rate" not in out.columns and "ema_parry_rate" in out.columns:
        out["ema_parry_attempt_rate"] = out["ema_parry_rate"]

    required = [
        "ema_attack_rate",
        "ema_mobility_rate",
        "ema_defense_rate",
        "ema_hit_rate",
        "ema_miss_rate",
        "ema_dps_dealt",
        "ema_dps_taken",
        "ema_block_rate",
        "ema_dodge_rate",
        "ema_quick_rate",
        "ema_heavy_rate",
        "ema_special_rate",
        "ema_charge_rate",
        "ema_parry_rate",
        "ema_parry_attempt_rate",
    ]
    validate_required_columns(out, required)

    for col in required:
        out[col] = pd.to_numeric(out[col], errors="coerce").fillna(0.0)

    out["norm_ema_dps_dealt"] = safe_minmax(out["ema_dps_dealt"])
    out["norm_ema_dps_taken"] = safe_minmax(out["ema_dps_taken"])

    out["Aggression"] = (
        0.35 * out["ema_attack_rate"]
        + 0.15 * out["ema_hit_rate"]
        + 0.10 * out["ema_quick_rate"]
        + 0.15 * out["ema_heavy_rate"]
        + 0.10 * out["ema_charge_rate"]
        + 0.15 * out["norm_ema_dps_dealt"]
    )

    out["Defense"] = (
        0.35 * out["ema_defense_rate"]
        + 0.25 * out["ema_block_rate"]
        + 0.15 * out["ema_parry_rate"]
        + 0.25 * (1.0 - out["norm_ema_dps_taken"])
    )

    out["Mobility"] = (
        0.85 * out["ema_mobility_rate"]
        + 0.15 * out["ema_dodge_rate"]
    )

    out["Risk"] = (
        0.20 * out["ema_miss_rate"]
        + 0.20 * out["norm_ema_dps_taken"]
        + 0.30 * out["ema_parry_attempt_rate"]
        + 0.30 * out["ema_charge_rate"]
    )

    out["Aggression_norm"] = safe_minmax(out["Aggression"])
    out["Defense_norm"] = safe_minmax(out["Defense"])
    out["Mobility_norm"] = safe_minmax(out["Mobility"])
    out["Risk_norm"] = safe_minmax(out["Risk"])

    out["style_label"] = out.apply(classify_style, axis=1)

    return out


# --------------------------------------------------
# Aggregation
# --------------------------------------------------
def build_profile_summary(df: pd.DataFrame) -> pd.DataFrame:
    work = df.copy()
    if "parry_attempt_rate" not in work.columns and "parry_rate" in work.columns:
        work["parry_attempt_rate"] = work["parry_rate"]

    required = [
        "match_id",
        "profile_id",
        "profile_name",
        "won_round",
        "hit_rate",
        "miss_rate",
        "damage_dealt",
        "damage_taken",
        "attack_rate",
        "mobility_rate",
        "defense_rate",
        "dps_dealt",
        "dps_taken",
        "block_rate",
        "dodge_rate",
        "quick_rate",
        "heavy_rate",
        "special_rate",
        "charge_rate",
        "parry_rate",
        "parry_attempt_rate",
    ]
    validate_required_columns(work, required)

    work["profile_id"] = work["profile_id"].fillna("UNKNOWN_PROFILE").astype(str)
    work["profile_name"] = work["profile_name"].fillna("UNKNOWN").astype(str)

    numeric_cols = [
        "won_round",
        "hit_rate",
        "miss_rate",
        "damage_dealt",
        "damage_taken",
        "attack_rate",
        "mobility_rate",
        "defense_rate",
        "dps_dealt",
        "dps_taken",
        "block_rate",
        "dodge_rate",
        "quick_rate",
        "heavy_rate",
        "special_rate",
        "charge_rate",
        "parry_rate",
        "parry_attempt_rate",
    ]
    for col in numeric_cols:
        work[col] = pd.to_numeric(work[col], errors="coerce").fillna(0.0)

    work = work[
        (work["profile_id"] != "GUEST") &
        (work["profile_id"] != "UNKNOWN_PROFILE")
    ].copy()

    grouped = (
        work.groupby("profile_id", dropna=False)
        .agg(
            matches_count=("match_id", "nunique"),
            win_rate=("won_round", "mean"),
            hit_rate=("hit_rate", "mean"),
            miss_rate=("miss_rate", "mean"),
            avg_damage_dealt=("damage_dealt", "mean"),
            avg_damage_taken=("damage_taken", "mean"),
            # Keep these column names so the rest of the pipeline and Unity UI
            # stay unchanged, but feed the chart from lifetime profile averages.
            ema_attack_rate=("attack_rate", "mean"),
            ema_mobility_rate=("mobility_rate", "mean"),
            ema_defense_rate=("defense_rate", "mean"),
            ema_hit_rate=("hit_rate", "mean"),
            ema_miss_rate=("miss_rate", "mean"),
            ema_dps_dealt=("dps_dealt", "mean"),
            ema_dps_taken=("dps_taken", "mean"),
            ema_block_rate=("block_rate", "mean"),
            ema_dodge_rate=("dodge_rate", "mean"),
            ema_quick_rate=("quick_rate", "mean"),
            ema_heavy_rate=("heavy_rate", "mean"),
            ema_special_rate=("special_rate", "mean"),
            ema_charge_rate=("charge_rate", "mean"),
            ema_parry_rate=("parry_rate", "mean"),
            ema_parry_attempt_rate=("parry_attempt_rate", "mean"),
        )
        .reset_index()
    )

    latest_names = (
        work.groupby("profile_id", as_index=False)["profile_name"]
        .last()
    )

    grouped = grouped.merge(latest_names, on="profile_id", how="left")

    return grouped


# --------------------------------------------------
# Identity / legacy IDs
# --------------------------------------------------
def load_profile_identity_map(path: str) -> tuple[pd.DataFrame, dict]:
    if not path or not os.path.exists(path):
        print(f"[WARN] profiles.json not found: {path}")
        empty_df = pd.DataFrame(columns=["profile_id", "profile_name_current"])
        return empty_df, {}

    with open(path, "r", encoding="utf-8") as f:
        data = json.load(f)

    profiles = data.get("profiles", [])
    rows = []
    legacy_to_current = {}

    for p in profiles:
        current_id = str(p.get("id", "")).strip()
        current_name = str(p.get("profileName", "")).strip()
        legacy_ids = p.get("legacyIds", []) or []

        if not current_id:
            continue

        rows.append({
            "profile_id": current_id,
            "profile_name_current": current_name
        })

        legacy_to_current[current_id] = current_id

        for legacy_id in legacy_ids:
            legacy_id = str(legacy_id).strip()
            if legacy_id:
                legacy_to_current[legacy_id] = current_id

    current_profiles_df = pd.DataFrame(rows)
    return current_profiles_df, legacy_to_current


def load_elo_table(path: str) -> pd.DataFrame:
    if not os.path.exists(path):
        print(f"[WARN] Elo CSV not found: {path}")
        return pd.DataFrame(columns=["profile_id", "elo_rating"])

    elo = pd.read_csv(path)
    if elo.empty:
        return pd.DataFrame(columns=["profile_id", "elo_rating"])

    cols = {c.lower(): c for c in elo.columns}

    profile_col = None
    for candidate in ["profile_id", "player_id", "id"]:
        if candidate in cols:
            profile_col = cols[candidate]
            break

    rating_col = None
    for candidate in ["elo_rating", "overall_elo", "rating", "elo"]:
        if candidate in cols:
            rating_col = cols[candidate]
            break

    if profile_col is None or rating_col is None:
        print("[WARN] Elo CSV missing expected columns. Expected profile_id + elo/rating column.")
        return pd.DataFrame(columns=["profile_id", "elo_rating"])

    out = elo[[profile_col, rating_col]].copy()
    out.columns = ["profile_id", "elo_rating"]
    out["profile_id"] = out["profile_id"].astype(str)
    out["elo_rating"] = pd.to_numeric(out["elo_rating"], errors="coerce").fillna(1500.0)

    return out


# --------------------------------------------------
# JSON export
# --------------------------------------------------
def to_json_payload(df: pd.DataFrame) -> dict:
    profiles = []

    sort_df = df.sort_values(
        by=["profile_name", "profile_id"],
        ascending=[True, True]
    ).reset_index(drop=True)

    for _, row in sort_df.iterrows():
        profiles.append({
            "profile_id": str(row["profile_id"]),
            "profile_name": str(row["profile_name"]),
            "style_label": str(row["style_label"]),
            "elo_label": get_elo_label(row["elo_rating"]),
            "matches_count": int(row["matches_count"]),
            "elo_rating": round(float(row["elo_rating"]), 2),
            "win_rate": round(float(row["win_rate"]), 4),
            "hit_rate": round(float(row["hit_rate"]), 4),
            "miss_rate": round(float(row["miss_rate"]), 4),
            "avg_damage_dealt": round(float(row["avg_damage_dealt"]), 4),
            "avg_damage_taken": round(float(row["avg_damage_taken"]), 4),
            "aggression_raw": round(float(row["Aggression"]), 4),
            "defense_raw": round(float(row["Defense"]), 4),
            "mobility_raw": round(float(row["Mobility"]), 4),
            "risk_raw": round(float(row["Risk"]), 4),
            "aggression": round(float(row["Aggression_norm"]), 4),
            "defense": round(float(row["Defense_norm"]), 4),
            "mobility": round(float(row["Mobility_norm"]), 4),
            "risk": round(float(row["Risk_norm"]), 4),
        })

    return {
        "version": "1.0",
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "profiles": profiles
    }


def save_json(payload: dict, out_path: str) -> None:
    parent = os.path.dirname(out_path)
    if parent:
        ensure_dir(parent)

    with open(out_path, "w", encoding="utf-8") as f:
        json.dump(payload, f, ensure_ascii=False, indent=2)


def run_export(
    input_path: str,
    elo_input: str,
    output_dir: str,
    output_name: str = "profile_analysis.json",
    unity_output: str = None,
    profiles_json: str = None,
    skip_unity_copy: bool = False,
) -> dict:
    if not os.path.exists(input_path):
        raise FileNotFoundError(f"Input CSV not found: {input_path}")

    ensure_dir(output_dir)

    df = pd.read_csv(input_path)
    if df.empty:
        raise ValueError("Input CSV is empty.")

    elo_df = load_elo_table(elo_input)
    current_profiles_df, legacy_to_current = load_profile_identity_map(profiles_json)

    if "profile_id" in df.columns:
        df["profile_id"] = df["profile_id"].astype(str)

    if legacy_to_current:
        df["profile_id"] = df["profile_id"].map(
            lambda pid: legacy_to_current.get(str(pid), str(pid))
        )

    if not elo_df.empty and legacy_to_current:
        elo_df["profile_id"] = elo_df["profile_id"].astype(str).map(
            lambda pid: legacy_to_current.get(str(pid), str(pid))
        )

        elo_df = (
            elo_df.groupby("profile_id", dropna=False)["elo_rating"]
            .last()
            .reset_index()
        )

    profile_summary = build_profile_summary(df)

    profile_summary["profile_id"] = profile_summary["profile_id"].astype(str)
    profile_summary = profile_summary.merge(elo_df, on="profile_id", how="left")
    profile_summary["elo_rating"] = pd.to_numeric(
        profile_summary["elo_rating"], errors="coerce"
    ).fillna(1500.0)

    if not current_profiles_df.empty:
        profile_summary = profile_summary.merge(current_profiles_df, on="profile_id", how="left")
        profile_summary["profile_name"] = profile_summary["profile_name_current"].fillna(profile_summary["profile_name"])
        profile_summary = profile_summary.drop(columns=["profile_name_current"], errors="ignore")

    scored_profiles = compute_style_axes_from_profile_df(profile_summary)
    payload = to_json_payload(scored_profiles)

    out_path = os.path.join(output_dir, output_name)
    save_json(payload, out_path)
    print(f"[OK] Saved JSON: {out_path}")

    if not skip_unity_copy and unity_output:
        save_json(payload, unity_output)
        print(f"[OK] Exported to Unity: {unity_output}")

    print(f"[OK] Profiles exported: {len(payload['profiles'])}")
    return payload


def main_with_args(
    input_path: str,
    elo_input: str,
    output_dir: str,
    unity_output: str = None,
    profiles_json: str = None,
    output_name: str = "profile_analysis.json",
    skip_unity_copy: bool = False,
) -> dict:
    return run_export(
        input_path=input_path,
        elo_input=elo_input,
        output_dir=output_dir,
        output_name=output_name,
        unity_output=unity_output,
        profiles_json=profiles_json,
        skip_unity_copy=skip_unity_copy,
    )


# --------------------------------------------------
# Main
# --------------------------------------------------
def main() -> None:
    parser = argparse.ArgumentParser(
        description="Export profile_analysis.json for Unity Profile Analysis screen."
    )
    parser.add_argument(
        "--elo-input",
        default=os.path.join("out_elo", "elo_overall.csv"),
        help="Path to elo_overall.csv",
    )
    parser.add_argument(
        "--input",
        default=os.path.join("out", "dataset_profile_level.csv"),
        help="Path to dataset_profile_level.csv",
    )
    parser.add_argument(
        "--output-dir",
        default=os.path.join("out", "profile_analysis"),
        help="Directory to save profile_analysis.json",
    )
    parser.add_argument(
        "--profiles-json",
        default=None,
        help="Path to Unity profiles.json for current display names"
    )
    parser.add_argument(
        "--output-name",
        default="profile_analysis.json",
        help="Output JSON filename",
    )
    parser.add_argument(
        "--unity-output",
        default=None,
        help="Path to Unity profile_analysis.json"
    )
    parser.add_argument(
        "--skip-unity-copy",
        action="store_true",
        help="If set, do not export/copy JSON to Unity path",
    )
    args = parser.parse_args()

    run_export(
        input_path=args.input,
        elo_input=args.elo_input,
        output_dir=args.output_dir,
        output_name=args.output_name,
        unity_output=args.unity_output,
        profiles_json=args.profiles_json,
        skip_unity_copy=args.skip_unity_copy,
    )


if __name__ == "__main__":
    main()
