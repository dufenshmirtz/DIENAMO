import argparse
import os
import sys

import extractor_upgraded
import elo_from_telemetry
import export_profile_analysis

os.environ["PYTHONIOENCODING"] = "utf-8"

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

if hasattr(sys.stderr, "reconfigure"):
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")


def ensure_dir(path):
    if path:
        os.makedirs(path, exist_ok=True)


def print_progress(progress, message, hint):
    print(f"PROGRESS:{progress:.2f}:{message}:{hint}", flush=True)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--telemetry-dir", required=True)
    parser.add_argument("--out-dir", required=True)
    parser.add_argument("--elo-out-dir", required=True)
    parser.add_argument("--unity-output", required=True)
    parser.add_argument("--profiles-json", required=True)
    args = parser.parse_args()

    ensure_dir(args.out_dir)
    ensure_dir(args.elo_out_dir)
    ensure_dir(os.path.dirname(args.unity_output))

    print("=== RUNTIME PIPELINE START ===")
    print_progress(0.05, "Preparing analysis", "Checking telemetry files")

    print_progress(0.20, "Extracting features", "Reading match telemetry")
    print("Running extractor...")
    extractor_upgraded.main_with_args(
        telemetry_dir=args.telemetry_dir,
        out_dir=args.out_dir,
        skip_human_xlsx=True
    )
    print_progress(0.35, "Extracting features", "Feature datasets ready")

    print_progress(0.45, "Calculating Elo", "Updating player ratings")
    print("Running elo...")
    elo_from_telemetry.main_with_args(
        telemetry_dir=args.telemetry_dir,
        out_dir=args.elo_out_dir
    )
    print_progress(0.65, "Building profile summary", "Aggregating player behaviour")

    print_progress(0.82, "Exporting Unity data", "Writing profile_analysis.json")
    print("Running export...")
    export_profile_analysis.main_with_args(
        input_path=os.path.join(args.out_dir, "dataset_profile_level.csv"),
        elo_input=os.path.join(args.elo_out_dir, "elo_overall.csv"),
        output_dir=os.path.join(args.out_dir, "profile_analysis"),
        unity_output=args.unity_output,
        profiles_json=args.profiles_json
    )
    print_progress(0.95, "Finalizing", "Preparing UI refresh")

    if os.path.exists(args.unity_output):
        print_progress(1.00, "Analysis complete", "Profile data updated")

    print("=== PIPELINE DONE ===")


if __name__ == "__main__":
    main()
