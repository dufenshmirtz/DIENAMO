using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Thesis (online IL agent): measures the style of both players after every completed round.
///
/// Listens to TelemetryManager.SessionSaved (raised right after the round's JSON is written), computes the 5 axes
/// with StyleAxes (proven identical to the Python extractor) and:
/// - human / scripted player, valid round -> EMA update in StyleProfileStore
///     real profile -> saved (style_profiles.json); guest -> "guest:P1" (memory only); persona -> "persona:S07" (memory only);
/// - IL agent seat -> one line in agent_style_log.csv: the style it was GIVEN vs the style MEASURED in that round
///   (online style fidelity). The agent never gets an EMA entry of its own.
///
/// Only reads; never changes the game. Any error is logged and swallowed.
/// </summary>
public static class StyleTracker
{
    public static bool Enabled = true;
    public static bool LogToConsole = true;

    /// <summary>Raised after a human/scripted player's EMA was updated (seat, entry). For the online agent (next step).</summary>
    public static event Action<string, StyleProfileStore.Entry> StyleUpdated;

    static readonly string[] InvalidProfiles = { "", "GUEST", "UNKNOWN", "NONE" };

    public static string LogFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Head of Hell", "ImitationLogs", "_style_tracker");
    /// <summary>One shared file; in the parallel build (-ilgen) one file per output folder (= per shard), so that
    /// several game processes never append to the same file at the same time.</summary>
    public static string AgentLogPath => Path.Combine(LogFolder,
        ILGenArgs.Active && !string.IsNullOrEmpty(TelemetryManager.OutputFolderOverride)
            ? "agent_style_log_" + TelemetryManager.OutputFolderOverride + ".csv"
            : "agent_style_log.csv");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Init()
    {
        // safe also when domain reload is disabled (no double subscription)
        TelemetryManager.SessionSaved -= OnSessionSaved;
        TelemetryManager.SessionSaved += OnSessionSaved;
        StyleProfileStore.ResetSession();
    }

    static void OnSessionSaved(TelemetrySession s)
    {
        if (!Enabled || s == null) return;
        try { Process(s); }
        catch (Exception ex) { Debug.LogWarning("[StyleTracker] " + ex); }
    }

    static void Process(TelemetrySession s)
    {
        var meta = s.meta ?? new TelemetryMatchMeta();
        string p1Seat = string.IsNullOrEmpty(meta.p1Id) ? "P1" : meta.p1Id;
        string p2Seat = string.IsNullOrEmpty(meta.p2Id) ? "P2" : meta.p2Id;

        // which seats are played by an IL agent right now (the round just ended, the scene is not reloaded yet)
        var agents = new Dictionary<string, ILAgentController>();
        foreach (var il in UnityEngine.Object.FindObjectsOfType<ILAgentController>())
        {
            if (il == null || !il.enabled) continue;
            var cm = il.GetComponent<CharacterManager>();
            if (cm != null) agents["P" + cm.playerNum] = il;
        }

        var rows = StyleAxes.Compute(s);
        var bySeat = new Dictionary<string, StyleAxes.Row>();
        foreach (var r in rows) if (r.slotId == p1Seat || r.slotId == p2Seat) bySeat[r.slotId] = r;

        foreach (var seat in new[] { p1Seat, p2Seat })
        {
            if (!bySeat.TryGetValue(seat, out var row)) continue;
            string opp = seat == p1Seat ? p2Seat : p1Seat;
            double[] measured = row.ToVector(StyleAxes.AxisNames);

            if (agents.TryGetValue(seat, out var agent))
            {
                bool oppP1 = opp == p1Seat;
                WriteAgentLine(s, seat, row, measured, agent, opp,
                    PlayerKey(oppP1 ? meta.p1ProfileId : meta.p2ProfileId, "", oppP1 ? meta.p1PersonaId : meta.p2PersonaId, opp).key);
                continue;
            }

            if (!row.valid)
            {
                if (LogToConsole)
                    Debug.Log($"[StyleTracker] {seat}: round not counted (actions {row.actionsTotal}, {row.durationS:0.0}s)");
                continue;
            }

            bool isP1 = seat == p1Seat;
            var (key, name, persistent) = PlayerKey(isP1 ? meta.p1ProfileId : meta.p2ProfileId,
                                                    isP1 ? meta.p1ProfileName : meta.p2ProfileName,
                                                    isP1 ? meta.p1PersonaId : meta.p2PersonaId, seat);
            // data generation (Editor or -ilgen build): never write into the saved profiles (style_profiles.json)
            if (!string.IsNullOrEmpty(TelemetryManager.OutputFolderOverride)) persistent = false;
            var entry = StyleProfileStore.AddRound(key, name, row.character, persistent, measured, s.matchId, row.durationS);
            if (LogToConsole)
                Debug.Log($"[StyleTracker] {seat} {key} ({row.character}) round {entry.rounds}: " +
                          $"raw {Fmt(measured)} -> EMA {Fmt(entry.ema)}{(persistent ? " [saved]" : " [memory]")}");
            StyleUpdated?.Invoke(seat, entry);
        }
    }

    /// <summary>Real profile (in ProfileManager) -> persistent; otherwise persona id or guest seat -> memory only.</summary>
    /// <summary>The store key of a player. Used by the tracker (after a round) and by the IL agent setup (before a
    /// round), so both always point to the same entry.</summary>
    public static (string key, string name, bool persistent) PlayerKey(string profileId, string profileName,
                                                                        string personaId, string seat)
    {
        string pid = (profileId ?? "").Trim();
        string pname = (profileName ?? "").Trim();
        string persona = (personaId ?? "").Trim();

        if (Array.IndexOf(InvalidProfiles, pid) < 0 && ProfileExists(pid)) return (pid, pname, true);
        if (persona.Length > 0) return ("persona:" + persona, persona, false);
        return ("guest:" + seat, "Guest " + seat, false);
    }

    public static bool ProfileExists(string id)
    {
        var pm = ProfileManager.I;
        var db = pm != null ? pm.GetDatabase() : null;
        return db != null && db.profiles != null && db.profiles.Exists(p => p != null && p.id == id);
    }

    static void WriteAgentLine(TelemetrySession s, string seat, StyleAxes.Row row, double[] measured,
                               ILAgentController agent, string oppSeat, string oppKey)
    {
        float[] given = agent.Style;
        bool newFile = !File.Exists(AgentLogPath);
        var sb = new StringBuilder();
        if (newFile)
        {
            sb.Append("utc,output_folder,match_id,seat,agent_label,character,opponent_seat,opponent_key,style_source," +
                      "style_clamped,valid,duration_s,actions_total");
            foreach (var a in StyleAxes.AxisNames) sb.Append(",requested_").Append(a);
            foreach (var a in StyleAxes.AxisNames) sb.Append(",given_").Append(a);
            foreach (var a in StyleAxes.AxisNames) sb.Append(",measured_").Append(a);
            sb.AppendLine();
        }
        string folder = !string.IsNullOrEmpty(TelemetryManager.OutputFolderOverride)
            ? TelemetryManager.OutputFolderOverride : (Application.isEditor ? "Editor" : "Build");
        sb.Append(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")).Append(',').Append(Csv(folder)).Append(',')
          .Append(s.matchId).Append(',').Append(seat).Append(',').Append(Csv(agent.Label)).Append(',')
          .Append(row.character).Append(',').Append(oppSeat).Append(',').Append(Csv(oppKey)).Append(',')
          .Append(Csv(agent.styleSource)).Append(',').Append(agent.styleClamped ? 1 : 0)
          .Append(',').Append(row.valid ? 1 : 0).Append(',').Append(D(row.durationS)).Append(',').Append(D(row.actionsTotal));
        float[] requested = agent.styleRequested != null && agent.styleRequested.Length == given.Length
            ? agent.styleRequested : given;   // before the clamp
        for (int i = 0; i < requested.Length; i++) sb.Append(',').Append(D(requested[i]));
        for (int i = 0; i < given.Length; i++) sb.Append(',').Append(D(given[i]));
        for (int i = 0; i < measured.Length; i++) sb.Append(',').Append(D(measured[i]));
        sb.AppendLine();

        Directory.CreateDirectory(LogFolder);
        File.AppendAllText(AgentLogPath, sb.ToString());
        if (LogToConsole)
            Debug.Log($"[StyleTracker] agent {seat} ({agent.Label}) given {Fmt(given)} measured {Fmt(measured)}" +
                      (row.valid ? "" : " (round not valid)"));
    }

    static string Csv(string x) => (x ?? "").Replace(",", ";");
    static string D(double v) => v.ToString("R", CultureInfo.InvariantCulture);
    static string Fmt(IList<double> v)
    {
        var parts = new string[v.Count];
        for (int i = 0; i < v.Count; i++) parts[i] = v[i].ToString("0.000", CultureInfo.InvariantCulture);
        return "[" + string.Join(" ", parts) + "]";
    }
    static string Fmt(float[] v)
    {
        var d = new double[v.Length];
        for (int i = 0; i < v.Length; i++) d[i] = v[i];
        return Fmt(d);
    }
}
