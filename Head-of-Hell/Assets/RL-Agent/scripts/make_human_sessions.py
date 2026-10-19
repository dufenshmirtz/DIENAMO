"""
Human validation of θ5 (thesis): schedules for ONE participant (Editor, ILDataScene, real time, human = P1, keyboard).
Persona kind "Human" -> telemetry persona id HUMAN_<id> (Director, 19/10).

  --session 1  Baseline: participant vs REF (knobs 0.5, skill 0.6), N rounds.     Output Folder: HV_baseline
  --session 2  Mirror:   participant vs θ5 online mirror of HUMAN_<id> (EMA α 0.35, neutral in round 1).
                         Output Folder: HV_mirror_<id>  (own folder, so the EMA starts from neutral)
  --session 3  Blind:    3x ghost of self (θ5 + baseline style of the participant), 3x ghost of --other participant,
                         3x REF, random order (seed from the id). Output Folder: HV_blind
               Ghost styles = mean of the participant's baseline rounds vs REF, CLAMPED to the model's training range
               (requested and clamped values are written into the schedule file).
The IL model is the scene's "Schedule Il Model" (must be theta5_joint for these sessions).
"""
import argparse
import json
import os
import random
import sys
import zlib

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
SCHED = os.path.normpath(os.path.join(HERE, "..", "schedules"))
MODEL = os.path.normpath(os.path.join(HERE, "..", "models", "theta5_joint.json"))
REF = {"id": "REF", "kind": "Scripted", "aggression": 0.5, "defense": 0.5, "mobility": 0.5, "patience": 0.5, "holdout": False}
AX = ["attack_rate", "mobility_rate", "defense_rate", "mobility_time_ratio", "block_time_ratio"]
KEYS = ["styleAttack", "styleMobility", "styleDefense", "styleMobilityTime", "styleBlockTime"]


def human(pid):
    return {"id": pid, "kind": "Human", "aggression": 0, "defense": 0, "mobility": 0, "patience": 0, "holdout": False}


def baseline_style(root, folder, pid):
    import synthetic_style_report as SR
    df = SR.load_rounds(os.path.join(root, "Telemetry", folder))
    d = df[df.persona == "HUMAN_" + pid]
    if len(d) == 0:
        sys.exit(f"No baseline rounds of HUMAN_{pid} in Telemetry/{folder}. Run session 1 first.")
    return [float(d[a].mean()) for a in AX], len(d)


def ghost(gid, style, lo, hi):
    c = [min(max(v, l), h) for v, l, h in zip(style, lo, hi)]
    p = {"id": gid, "kind": "ILAgent", "aggression": 0, "defense": 0, "mobility": 0, "patience": 0, "holdout": False}
    p.update({k: round(v, 5) for k, v in zip(KEYS, c)})
    return p, c


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--participant", required=True, help="pseudonym, e.g. P01")
    ap.add_argument("--session", type=int, choices=[1, 2, 3], required=True)
    ap.add_argument("--other", help="session 3: the other participant whose ghost is shown")
    ap.add_argument("--rounds", type=int, default=10, help="sessions 1/2")
    ap.add_argument("--blind-each", type=int, default=3, help="session 3: rounds per condition")
    ap.add_argument("--baseline-folder", default="HV_baseline")
    ap.add_argument("--root", default=os.path.join(os.path.expanduser("~"), "Documents", "My Games", "Head of Hell"))
    a = ap.parse_args()
    pid = a.participant
    doc = {"seed": 0, "participant": pid, "session": a.session}

    if a.session == 1:
        personas = [human(pid), REF]
        matches = [(pid, "REF")] * a.rounds
        doc.update(purpose="human validation: baseline vs REF", outputFolder=a.baseline_folder)
    elif a.session == 2:
        m = {"id": "M_" + pid, "kind": "ILAgent", "mirrorOf": "HUMAN_" + pid, "aggression": 0, "defense": 0, "mobility": 0,
             "patience": 0, "holdout": False}
        personas = [human(pid), m]
        matches = [(pid, "M_" + pid)] * a.rounds
        doc.update(purpose="human validation: online mirror (theta5)", outputFolder="HV_mirror_" + pid)
    else:
        if not a.other:
            sys.exit("--other is required for session 3")
        mj = json.load(open(MODEL, encoding="utf-8"))
        lo, hi = mj["styleMin"], mj["styleMax"]
        bs, ns = baseline_style(a.root, a.baseline_folder, pid)
        bo, no = baseline_style(a.root, a.baseline_folder, a.other)
        gs, cs = ghost("GS_" + pid, bs, lo, hi)
        go, co = ghost("GO_" + pid, bo, lo, hi)
        personas = [human(pid), gs, go, REF]
        conds = [gs["id"]] * a.blind_each + [go["id"]] * a.blind_each + ["REF"] * a.blind_each
        random.Random(zlib.crc32(pid.encode())).shuffle(conds)
        matches = [(pid, c) for c in conds]
        doc.update(purpose="human validation: blind ghost-self / ghost-other / REF", outputFolder="HV_blind", other=a.other,
                   ghost_self={"baseline_rounds": ns, "requested": bs, "clamped": cs},
                   ghost_other={"baseline_rounds": no, "requested": bo, "clamped": co},
                   order=conds)
        print(f"ghost self  ({ns} rounds) requested {[round(v, 3) for v in bs]} -> {[round(v, 3) for v in cs]}")
        print(f"ghost other ({no} rounds) requested {[round(v, 3) for v in bo]} -> {[round(v, 3) for v in co]}")
        print("order (keep secret from the participant):", conds)

    name = f"schedule_hv{a.session}_{pid}"
    doc.update(name=name, personas=personas,
               matches=[{"index": i, "round": i + 1, "p1": p1, "p2": p2} for i, (p1, p2) in enumerate(matches)])
    with open(os.path.join(SCHED, name + ".json"), "w", encoding="utf-8") as f:
        json.dump(doc, f, indent=1)
    print(f"{len(matches)} rounds -> {name}.json  (Output Folder in the scene: {doc['outputFolder']})")


if __name__ == "__main__":
    main()
