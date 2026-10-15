using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Thesis (convergence experiment): the online-mirror style of a persona, rebuilt from the telemetry already saved
/// in a folder. Same rules as the in-game tracker: StyleAxes per round (identical to the Python), only valid rounds,
/// EMA alpha 0.35 with the first valid round as the first value, rounds in the order they were played (startedAtUtc).
///
/// Rebuilding from the saved files (instead of keeping the EMA in memory) makes a long Director run safe to stop and
/// resume: the agent always gets exactly the EMA of the matches that exist on disk.
/// </summary>
public static class OnlineMirrorStyle
{
    /// <returns>EMA in StyleAxes.AxisNames order, or null if the persona has no valid round yet.</returns>
    public static double[] FromTelemetryFolder(string folder, string personaId, out int rounds)
    {
        rounds = 0;
        if (string.IsNullOrEmpty(personaId) || !Directory.Exists(folder)) return null;

        var sessions = new List<TelemetrySession>();
        foreach (var f in Directory.GetFiles(folder, "match_*.json"))
        {
            TelemetrySession s = null;
            try { s = JsonUtility.FromJson<TelemetrySession>(File.ReadAllText(f)); }
            catch (Exception ex) { Debug.LogWarning($"[OnlineMirror] cannot read {f}: {ex.Message}"); }
            if (s?.meta == null) continue;
            if (s.meta.p1PersonaId == personaId || s.meta.p2PersonaId == personaId) sessions.Add(s);
        }
        sessions.Sort((a, b) =>
        {
            int c = string.CompareOrdinal(a.startedAtUtc, b.startedAtUtc);
            return c != 0 ? c : string.CompareOrdinal(a.matchId, b.matchId);
        });

        double[] ema = null;
        foreach (var s in sessions)
        {
            string seat = s.meta.p1PersonaId == personaId
                ? (string.IsNullOrEmpty(s.meta.p1Id) ? "P1" : s.meta.p1Id)
                : (string.IsNullOrEmpty(s.meta.p2Id) ? "P2" : s.meta.p2Id);
            foreach (var r in StyleAxes.Compute(s))
            {
                if (r.slotId != seat || !r.valid) continue;
                double[] x = r.ToVector(StyleAxes.AxisNames);
                if (ema == null) ema = x;
                else for (int i = 0; i < x.Length; i++) ema[i] = StyleProfileStore.Alpha * x[i] + (1 - StyleProfileStore.Alpha) * ema[i];
                rounds++;
            }
        }
        return ema;
    }
}
