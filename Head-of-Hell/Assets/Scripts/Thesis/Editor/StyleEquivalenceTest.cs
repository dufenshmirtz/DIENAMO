using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Thesis: equivalence test of the C# style axes (StyleAxes.cs) against the Python (extractor_upgraded.py).
/// Reads every stored telemetry file (Telemetry/**/match_*.json) with JsonUtility, computes the axes in C#
/// and writes one CSV row per player: Telemetry/_style_equiv_cs.csv. compare_style_cs_py.py then runs the Python
/// on the same files and compares. Editor only; reads the files, changes nothing.
/// </summary>
public static class StyleEquivalenceTest
{
    [MenuItem("Thesis/Style axes: export C# rows (equivalence test)")]
    public static void Run()
    {
        string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "My Games", "Head of Hell", "Telemetry");
        string outPath = Path.Combine(root, "_style_equiv_cs.csv");

        string[] files = Directory.GetFiles(root, "match_*.json", SearchOption.AllDirectories);
        Array.Sort(files, StringComparer.Ordinal);

        var sb = new StringBuilder();
        sb.AppendLine("file,match_id,slot_id,character,profile_id,valid,duration_s,fight_time_s,moving_s,blocking_s," +
                      "overheat_s,walk_segments,block_segments,actions_total,actions_attack,actions_mobility," +
                      "actions_defense,attack_rate,mobility_rate,defense_rate,mobility_time_ratio,block_time_ratio");

        int nFiles = 0, nRows = 0, nFailed = 0;
        try
        {
            for (int i = 0; i < files.Length; i++)
            {
                string f = files[i];
                if (i % 25 == 0)
                    EditorUtility.DisplayProgressBar("Style equivalence test", Path.GetFileName(f), (float)i / files.Length);

                TelemetrySession s = null;
                try { s = JsonUtility.FromJson<TelemetrySession>(File.ReadAllText(f)); }
                catch (Exception ex) { Debug.LogWarning($"[StyleEquiv] cannot read {f}: {ex.Message}"); }
                if (s == null) { nFailed++; continue; }
                nFiles++;

                string rel = f.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/');
                foreach (var r in StyleAxes.Compute(s))
                {
                    sb.Append(rel).Append(',').Append(s.matchId).Append(',').Append(r.slotId).Append(',')
                      .Append(r.character).Append(',').Append(r.profileId).Append(',').Append(r.valid ? 1 : 0).Append(',')
                      .Append(D(r.durationS)).Append(',').Append(D(r.fightTimeS)).Append(',')
                      .Append(D(r.movingS)).Append(',').Append(D(r.blockingS)).Append(',').Append(D(r.overheatS)).Append(',')
                      .Append(r.walkSegments).Append(',').Append(r.blockSegments).Append(',')
                      .Append(D(r.actionsTotal)).Append(',').Append(D(r.actionsAttack)).Append(',')
                      .Append(D(r.actionsMobility)).Append(',').Append(D(r.actionsDefense)).Append(',')
                      .Append(D(r.attackRate)).Append(',').Append(D(r.mobilityRate)).Append(',')
                      .Append(D(r.defenseRate)).Append(',').Append(D(r.mobilityTimeRatio)).Append(',')
                      .Append(D(r.blockTimeRatio)).AppendLine();
                    nRows++;
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        File.WriteAllText(outPath, sb.ToString());
        Debug.Log($"[StyleEquiv] files {nFiles} (unreadable {nFailed}), rows {nRows} -> {outPath}");
    }

    static string D(double v) => v.ToString("R", CultureInfo.InvariantCulture);
}
