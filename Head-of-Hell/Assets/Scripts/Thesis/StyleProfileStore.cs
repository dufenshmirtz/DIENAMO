using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Thesis (online IL agent): the style of every player, kept as an EMA of the per-round style axes
/// (alpha = 0.35, the first valid round is the first EMA value; same as build_ema_profiles in extractor_upgraded.py).
///
/// One entry per (player key, character). Two kinds of entries:
/// - persistent: real profiles of the game (ProfileManager) -> saved to disk
///   (ImitationLogs/_style_tracker/style_profiles.json; a separate file, profiles.json is never touched);
/// - memory-only: guests and synthetic personas -> live until the game is closed, never written to disk.
///
/// The EMA is stored unclamped (it is the measurement). Clamping to the training range happens only when a style
/// is given to the agent.
/// </summary>
public static class StyleProfileStore
{
    public const double Alpha = 0.35;          // EMA_ALPHA in extractor_upgraded.py
    public const int MaxHistoryPerEntry = 500;

    [Serializable]
    public class RoundRecord
    {
        public string matchId;
        public string utc;
        public double durationS;
        public double[] raw;        // the round's 5 axes (StyleAxes.AxisNames order)
        public double[] emaAfter;   // EMA after this round
    }

    [Serializable]
    public class Entry
    {
        public string key;          // profile id (persistent) or "guest:P1" / "persona:S07" (memory)
        public string displayName;
        public string character;    // lower case, e.g. "skipler"
        public bool persistent;
        public int rounds;          // valid rounds that updated the EMA
        public double[] ema;        // null until the first valid round
        public string updatedUtc;
        public List<RoundRecord> history = new List<RoundRecord>();
    }

    [Serializable]
    private class FileData
    {
        public int version = 1;
        public string[] axes = StyleAxes.AxisNames;
        public double alpha = Alpha;
        public List<Entry> entries = new List<Entry>();
    }

    private static FileData disk;                                   // persistent entries (lazy loaded)
    private static readonly List<Entry> memory = new List<Entry>(); // guests / personas (this game session only)

    /// <summary>Next to the other thesis data (Documents/My Games/Head of Hell/ImitationLogs/_style_tracker),
    /// not in persistentDataPath, so it can be inspected and analysed like the rest of the data.</summary>
    public static string FilePath => Path.Combine(StyleTracker.LogFolder, "style_profiles.json");

    /// <summary>Called when play starts: forget memory-only entries and re-read the file.</summary>
    public static void ResetSession()
    {
        memory.Clear();
        disk = null;
    }

    /// <summary>All saved (profile) entries, e.g. for the Ghost list of the menu. Read only.</summary>
    public static List<Entry> PersistentEntries() => Disk().entries;

    public static Entry Find(string key, string character, bool persistent)
    {
        var list = persistent ? Disk().entries : memory;
        return list.Find(e => e.key == key && e.character == character);
    }

    /// <summary>EMA update with one valid round. Persistent entries are saved to disk immediately.</summary>
    public static Entry AddRound(string key, string displayName, string character, bool persistent,
                                 double[] raw, string matchId, double durationS)
    {
        var list = persistent ? Disk().entries : memory;
        var e = list.Find(x => x.key == key && x.character == character);
        if (e == null)
        {
            e = new Entry { key = key, character = character, persistent = persistent };
            list.Add(e);
        }
        if (!string.IsNullOrEmpty(displayName)) e.displayName = displayName;

        if (e.ema == null || e.ema.Length != raw.Length)
            e.ema = (double[])raw.Clone();
        else
            for (int i = 0; i < raw.Length; i++) e.ema[i] = Alpha * raw[i] + (1 - Alpha) * e.ema[i];

        e.rounds++;
        e.updatedUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        e.history.Add(new RoundRecord
        {
            matchId = matchId, utc = e.updatedUtc, durationS = durationS,
            raw = (double[])raw.Clone(), emaAfter = (double[])e.ema.Clone(),
        });
        if (e.history.Count > MaxHistoryPerEntry) e.history.RemoveAt(0);

        if (persistent) Save();
        return e;
    }

    private static FileData Disk()
    {
        if (disk != null) return disk;
        disk = new FileData();
        try
        {
            if (File.Exists(FilePath))
            {
                var d = JsonUtility.FromJson<FileData>(File.ReadAllText(FilePath));
                if (d != null && d.entries != null) disk = d;
            }
        }
        catch (Exception ex)
        {
            // never lose the old file silently: keep a copy and start a new one
            Debug.LogWarning($"[StyleStore] cannot read {FilePath}: {ex.Message}. A copy is kept as .bad");
            try { File.Copy(FilePath, FilePath + ".bad", true); } catch { }
        }
        return disk;
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(StyleTracker.LogFolder);
            string tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(Disk(), true));
            File.Copy(tmp, FilePath, true);
            File.Delete(tmp);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[StyleStore] cannot write {FilePath}: {ex.Message}");
        }
    }
}
