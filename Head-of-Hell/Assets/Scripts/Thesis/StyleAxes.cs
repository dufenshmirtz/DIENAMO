using System;
using System.Collections.Generic;

/// <summary>
/// Thesis (online IL agent): the 5 style axes of ONE round (one TelemetrySession), computed in C#.
///
/// This is a port of extractor_upgraded.py (extract_round_features + compute_walk_segments), limited to what the
/// 5 axes need: Action, HitAttempt and Miss events (DamageApplied only affects hit/damage statistics, not style).
/// The rules are the same as the Python, in the same order. If a rule changes in the Python, it must change here too;
/// the Editor menu "Thesis/Style axes: export C# rows (equivalence test)" + compare_style_cs_py.py check that both
/// give the same numbers on all stored telemetry.
///
/// Pure computation: no scene objects, no side effects. Used by the equivalence test now and by the style tracker later.
/// </summary>
public static class StyleAxes
{
    // Same constants as extractor_upgraded.py
    public const double MinMoveSegmentS = 0.2;   // MIN_MOVE_SEGMENT_S
    public const double MinBlockSegmentS = 0.1;  // MIN_BLOCK_SEGMENT_S
    public const double MinActions = 10;         // MIN_ACTIONS
    public const double MinDurationS = 5;        // MIN_DURATION

    /// <summary>Axis names, in the order the IL model (theta3) expects them.</summary>
    public static readonly string[] AxisNames =
        { "attack_rate", "mobility_rate", "defense_rate", "mobility_time_ratio", "block_time_ratio" };

    [Serializable]
    public class Row
    {
        public string slotId;       // "P1" / "P2"
        public string character;    // lower case, as in meta
        public string profileId;    // "" when the seat has no profile (guest, bot, agent)

        /// <summary>Python keeps the round only if actions_total >= 10 and duration >= 5 s.</summary>
        public bool valid;

        public double durationS;    // fight time of the round (durationSeconds)
        public double fightTimeS;   // durationS minus Visvia overheat time
        public double movingS, blockingS, overheatS;
        public int walkSegments, blockSegments;

        public double actionsTotal, actionsAttack, actionsMobility, actionsDefense;

        public double attackRate, mobilityRate, defenseRate, mobilityTimeRatio, blockTimeRatio;

        public double Get(string axis)
        {
            switch (axis)
            {
                case "attack_rate": return attackRate;
                case "mobility_rate": return mobilityRate;
                case "defense_rate": return defenseRate;
                case "mobility_time_ratio": return mobilityTimeRatio;
                case "block_time_ratio": return blockTimeRatio;
                default: throw new ArgumentException("Unknown style axis: " + axis);
            }
        }

        /// <summary>The 5 axes in the given order (e.g. the order stored in the model file).</summary>
        public double[] ToVector(IList<string> order)
        {
            var v = new double[order.Count];
            for (int i = 0; i < order.Count; i++) v[i] = Get(order[i]);
            return v;
        }
    }

    // ---------------------------------------------------------------- rule tables (same as the Python)

    // ACTION_CATEGORY_MAP
    static readonly Dictionary<string, string> Category = new Dictionary<string, string>
    {
        { "Quick", "attack" }, { "Heavy", "attack" }, { "HeavyStart", "attack" }, { "Special", "attack" },
        { "ChargeRelease", "attack" },
        { "Dash", "mobility" }, { "BackDash", "mobility" }, { "Jump", "mobility" }, { "MoveLeft", "mobility" },
        { "MoveRight", "mobility" }, { "DropPlatform", "mobility" },
        { "BlockHold", "defense" }, { "ParryAttempt", "defense" }, { "Dodge", "defense" },
    };

    // IGNORED_ACTION_TYPES
    static readonly HashSet<string> Ignored = new HashSet<string>
    {
        "ChargeStart", "ChargeReleaseInput", "ChargeCancel", "ChargeBroken", "BombDetonate", "BombLaunch",
        "MoveStop", "BlockStart", "BlockEnd",
    };

    // PROJECTILE_QUICK_KITS
    static readonly HashSet<string> ProjectileQuickKits = new HashSet<string> { "steelager", "lazy bigus", "lazybigus" };

    // ---------------------------------------------------------------- helpers

    static string S(string x) => x ?? "";

    /// <summary>Python "a or b": the first non-empty string.</summary>
    static string First(string a, string b) => !string.IsNullOrEmpty(a) ? a : S(b);

    static double Ratio(double num, double den) => den <= 1e-9 ? 0.0 : num / den;

    class Agg { public double total, attack, mobility, defense; }

    class Walk { public int segments, blockSegments; public double movingS, blockingS, overheatS; }

    // ---------------------------------------------------------------- compute_walk_segments

    static Dictionary<string, Walk> ComputeWalk(List<TelemetryEvent> events, double fightStart, double fightEnd)
    {
        var segStart = new Dictionary<string, double?>();
        var ohStart = new Dictionary<string, double?>();
        var blkStart = new Dictionary<string, double?>();
        var raw = new Dictionary<string, List<(double a, double b)>>();
        var oh = new Dictionary<string, List<(double a, double b)>>();
        var blk = new Dictionary<string, List<(double a, double b)>>();
        var order = new List<string>();   // pids in first-seen order (only used to iterate deterministically)

        void Add(Dictionary<string, List<(double a, double b)>> d, string pid, double a, double b)
        {
            if (!d.TryGetValue(pid, out var list)) { list = new List<(double a, double b)>(); d[pid] = list; }
            list.Add((a, b));
            if (!order.Contains(pid)) order.Add(pid);
        }

        double Clip(double t) => Math.Min(Math.Max(t, fightStart), fightEnd);

        double? Get(Dictionary<string, double?> d, string pid) => d.TryGetValue(pid, out var v) ? v : null;

        foreach (var e in events)
        {
            if (e == null || S(e.eventType) != "Action") continue;
            string pid = First(e.actorId, e.attackerId);
            if (pid.Length == 0) continue;
            string a = S(e.actionType);
            double t = Clip(e.t);

            if (a == "OverheatStart")
                ohStart[pid] = t;
            else if (a == "OverheatEnd" && Get(ohStart, pid) != null)
            {
                Add(oh, pid, Get(ohStart, pid).Value, t);
                ohStart[pid] = null;
            }

            if (a == "BlockStart")
            {
                if (Get(blkStart, pid) == null) blkStart[pid] = t;
            }
            else if (a == "BlockEnd" && Get(blkStart, pid) != null)
            {
                Add(blk, pid, Get(blkStart, pid).Value, t);
                blkStart[pid] = null;
            }

            bool walkAction = a == "MoveLeft" || a == "MoveRight";
            if (walkAction || a == "MoveStop")
            {
                if (Get(segStart, pid) != null) Add(raw, pid, Get(segStart, pid).Value, t);
                segStart[pid] = walkAction ? t : (double?)null;
            }
        }

        // segments still open at the end of the round end at fightEnd
        foreach (var kv in segStart) if (kv.Value != null) Add(raw, kv.Key, kv.Value.Value, fightEnd);
        foreach (var kv in blkStart) if (kv.Value != null) Add(blk, kv.Key, kv.Value.Value, fightEnd);
        foreach (var kv in ohStart) if (kv.Value != null) Add(oh, kv.Key, kv.Value.Value, fightEnd);

        var outW = new Dictionary<string, Walk>();
        var empty = new List<(double a, double b)>();
        foreach (var pid in order)
        {
            var ohs = oh.TryGetValue(pid, out var o) ? o : empty;
            var w = new Walk();

            foreach (var seg in raw.TryGetValue(pid, out var r) ? r : empty)
            {
                double length = Math.Max(0.0, seg.b - seg.a);
                foreach (var ov in ohs)
                    length -= Math.Max(0.0, Math.Min(seg.b, ov.b) - Math.Max(seg.a, ov.a));
                length = Math.Max(0.0, length);
                w.movingS += length;
                if (length >= MinMoveSegmentS) w.segments++;
            }

            var blocks = new List<(double a, double b)>(blk.TryGetValue(pid, out var bl) ? bl : empty);
            blocks.Sort((x, y) => x.a != y.a ? x.a.CompareTo(y.a) : x.b.CompareTo(y.b));   // Python sorted() on tuples
            foreach (var b in blocks)
            {
                double len = Math.Max(0.0, b.b - b.a);
                w.blockingS += len;                              // all blocks count for the time ratio
                if (len >= MinBlockSegmentS) w.blockSegments++;  // only >= 0.1 s count as a defense action
            }

            foreach (var ov in ohs) w.overheatS += Math.Max(0.0, ov.b - ov.a);
            outW[pid] = w;
        }
        return outW;
    }

    // ---------------------------------------------------------------- extract_round_features (style part)

    /// <summary>One row per player that appears in the round (both seats always included).</summary>
    public static List<Row> Compute(TelemetrySession s)
    {
        var rows = new List<Row>();
        if (s == null) return rows;

        var meta = s.meta ?? new TelemetryMatchMeta();
        var events = s.events ?? new List<TelemetryEvent>();

        string p1Seat = First(meta.p1Id, "P1");
        string p2Seat = First(meta.p2Id, "P2");
        string p1Char = S(meta.p1Character).Trim().ToLowerInvariant();
        string p2Char = S(meta.p2Character).Trim().ToLowerInvariant();
        string p1Prof = S(meta.p1ProfileId).Trim();
        string p2Prof = S(meta.p2ProfileId).Trim();

        double duration = s.durationSeconds;

        var lupenForm = new Dictionary<string, string>();  // pid -> character Lupen is transformed into
        var overheated = new Dictionary<string, bool>();
        var agg = new Dictionary<string, Agg>();
        var pidOrder = new List<string>();

        Agg Ensure(string pid)
        {
            if (!agg.TryGetValue(pid, out var a)) { a = new Agg(); agg[pid] = a; pidOrder.Add(pid); }
            return a;
        }

        string CharacterForSlot(string pid) => pid == p1Seat ? p1Char : (pid == p2Seat ? p2Char : "");

        string KitFor(string pid) =>
            lupenForm.TryGetValue(pid, out var k) && !string.IsNullOrEmpty(k) ? k : CharacterForSlot(pid);

        // Melee Quick/Special count when executed (HitAttempt / Miss); projectile Quick counts at spawn;
        // Lupen's own Special is not an attack.
        bool CountedAtOutcome(string pid, string moveType)
        {
            if (moveType == "Special") return KitFor(pid) != "lupen";
            if (moveType == "Quick") return !ProjectileQuickKits.Contains(KitFor(pid));
            return false;
        }

        // Heavy: players with HeavyStart events count HeavyStart (the decision); "Heavy" is only its outcome.
        // Schema 1.x files: HeavyStart had another meaning -> not used.
        var heavyStartPlayers = new HashSet<string>();
        if (!S(s.schemaVersion).StartsWith("1", StringComparison.Ordinal))
            foreach (var e in events)
                if (e != null && S(e.eventType) == "Action" && S(e.actionType) == "HeavyStart")
                    heavyStartPlayers.Add(First(e.actorId, e.attackerId));

        double fightStart = s.fightStartT;
        double fightEnd = s.sessionSeconds;
        if (fightEnd <= fightStart)
        {
            fightEnd = fightStart;
            foreach (var e in events) if (e != null) fightEnd = Math.Max(fightEnd, e.t);
        }
        var walk = ComputeWalk(events, fightStart, fightEnd);

        foreach (var e in events)
        {
            if (e == null) continue;
            string etype = S(e.eventType);

            if (etype == "Action")
            {
                string pid = First(e.actorId, e.attackerId);
                if (pid.Length == 0) continue;
                var a = Ensure(pid);
                string at = S(e.actionType);

                if (at == "Transform") { lupenForm[pid] = S(e.detail).Trim().ToLowerInvariant(); continue; }
                if (at == "TransformEnd") { lupenForm.Remove(pid); continue; }

                // Visvia overheat: forced movement-only state -> movement inside it is kit, not style.
                if (at == "OverheatStart") { overheated[pid] = true; continue; }
                if (at == "OverheatEnd") { overheated[pid] = false; continue; }
                Category.TryGetValue(at, out string cat);
                if (overheated.TryGetValue(pid, out bool oh) && oh && cat == "mobility") continue;

                if (Ignored.Contains(at)) continue;
                if (at == "MoveLeft" || at == "MoveRight") continue;          // walking: from segments
                if (at == "Heavy" && heavyStartPlayers.Contains(pid)) continue; // outcome of a HeavyStart
                if ((at == "Quick" || at == "Special") && CountedAtOutcome(pid, at)) continue;

                a.total += 1;

                if (at == "Special" && KitFor(pid) == "lupen") { a.defense += 1; continue; }  // push away + transform
                if (at == "Roll") { a.defense += 0.5; a.mobility += 0.5; continue; }        // Fin's quick slot
                if (at == "Parry") { a.total -= 1; continue; }                                // outcome of ParryAttempt

                if (cat == "attack") a.attack += 1;
                else if (cat == "mobility") a.mobility += 1;
                else if (cat == "defense") a.defense += 1;
                // any other action type: counts in the total only (same as the Python)
            }
            else if (etype == "HitAttempt" || etype == "Miss")
            {
                string attacker = S(e.attackerId);
                if (attacker.Length == 0) continue;
                var a = Ensure(attacker);
                if (CountedAtOutcome(attacker, S(e.moveType).Trim())) { a.total += 1; a.attack += 1; }
            }
        }

        Ensure(p1Seat);
        Ensure(p2Seat);

        foreach (var pid in pidOrder)
        {
            var a = agg[pid];
            var w = walk.TryGetValue(pid, out var ww) ? ww : new Walk();

            var r = new Row
            {
                slotId = pid,
                character = CharacterForSlot(pid),
                profileId = pid == p1Seat ? p1Prof : (pid == p2Seat ? p2Prof : ""),
                durationS = duration,
                movingS = w.movingS,
                blockingS = w.blockingS,
                overheatS = w.overheatS,
                walkSegments = w.segments,
                blockSegments = w.blockSegments,
                actionsTotal = a.total + w.segments + w.blockSegments,
                actionsAttack = a.attack,
                actionsMobility = a.mobility + w.segments,
                actionsDefense = a.defense + w.blockSegments,
            };
            r.fightTimeS = Math.Max(0.0, duration - w.overheatS);
            r.mobilityTimeRatio = Math.Min(1.0, Ratio(w.movingS, r.fightTimeS));
            r.blockTimeRatio = Math.Min(1.0, Ratio(w.blockingS, r.fightTimeS));
            r.valid = !(r.actionsTotal < MinActions || duration < MinDurationS);
            r.attackRate = Ratio(r.actionsAttack, r.actionsTotal);
            r.mobilityRate = Ratio(r.actionsMobility, r.actionsTotal);
            r.defenseRate = Ratio(r.actionsDefense, r.actionsTotal);
            rows.Add(r);
        }
        return rows;
    }
}
