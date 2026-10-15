using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Barracuda;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Thesis data generation (ILDataScene only, Editor only).
/// Plays a list of matchups automatically: each side is a scripted persona (SimpleBotController with fixed knobs)
/// or an RL model (FighterAgent + any NNModel). One round per match; after the telemetry JSON is saved the scene
/// is reloaded for the next match. Output goes to Telemetry/Synthetic and ImitationLogs/Synthetic.
///
/// It only does what the menus + PvE setup scripts normally do (character choice, bot setup), plus:
/// power-ups off, optional time scale, persona labels in telemetry. Normal gameplay code is not modified.
/// Remove/disable ILDataQuickStart in the same scene when this is used.
/// </summary>
[DefaultExecutionOrder(-10000)]
public class ILDataGenDirector : MonoBehaviour
{
    public enum SideKind { Scripted, RL, ILAgent, Human }

    [Serializable]
    public class Side
    {
        public SideKind kind = SideKind.Scripted;
        [Tooltip("Scripted: persona id written to telemetry/logs. RL: ignored (id = RL_<model name>).")]
        public string personaId = "REF";
        [Range(0f, 1f)] public float aggression = 0.5f;
        [Range(0f, 1f)] public float defense = 0.5f;
        [Range(0f, 1f)] public float mobility = 0.5f;
        [Range(0f, 1f)] public float patience = 0.5f;
        [Tooltip("RL only: the model to play with.")]
        public NNModel model;

        [Header("ILAgent only")]
        [Tooltip("JSON exported by RL-Agent/scripts/export_theta_unity.py (e.g. theta0_v1).")]
        public TextAsset ilModel;
        [Tooltip("Target style (raw values): attack, mobility, defense, mobility-time, block-time ratios.")]
        public float styleAttack = 0.27f, styleMobility = 0.59f, styleDefense = 0.15f, styleMobilityTime = 0.54f, styleBlockTime = 0.07f;
        [Tooltip("0 = random sampling every run.")]
        public int ilSeed = 0;
        [Tooltip("Online mirror (thesis): persona id whose style the agent copies - the EMA of that persona's saved matches " +
                 "in this output folder (neutral before the first one), clamped to the model's range. Empty = the fixed style above.")]
        public string mirrorOf = "";

        public string Id =>
            kind == SideKind.RL ? "RL_" + (model != null ? model.name : "none") :
            kind == SideKind.ILAgent ? "IL_" + personaId :
            kind == SideKind.Human ? "HUMAN" : personaId;
    }

    [Serializable]
    public class Matchup
    {
        public string label = "pilot";
        public Side p1 = new Side();
        public Side p2 = new Side();
        [Min(1)] public int matches = 10;
    }

    [Header("Run")]
    public bool run = true;
    public string character = "Skipler";
    [Tooltip("Same skill for every scripted bot (0.6 = Medium in PvE).")]
    [Range(0f, 1f)] public float scriptedSkill = 0.6f;
    [Tooltip("1 = normal speed. Use >1 only after the x1 vs accelerated pilot.")]
    [Range(1f, 5f)] public float timeScale = 1f;
    [Tooltip("Seconds (real time) between the saved match and the reload.")]
    public float delayBeforeNextMatch = 1f;
    [Tooltip("Game seconds without a saved result before the match is abandoned and restarted (stalemate).")]
    public float maxMatchSeconds = 180f;
    public string outputFolder = "Synthetic";

    [Header("Schedule file (main collection)")]
    [Tooltip("Optional JSON made by RL-Agent/scripts/make_schedule.py. When set it REPLACES the Matchups list below: one match per entry, scripted personas only.")]
    public TextAsset schedule;
    [Tooltip("Schedule only: continue after the last match written in the run log (Telemetry/<Output Folder>/_runlog_<schedule>.csv), e.g. after stopping Play or a crash.")]
    public bool resumeFromRunLog = true;
    [Tooltip("Schedule only: model used by every persona whose kind is \"ILAgent\" in the schedule file.")]
    public TextAsset scheduleIlModel;
    [Tooltip("Schedule only: RL models used by personas whose kind is \"RL\" (matched by asset name, field \"model\").")]
    public List<NNModel> scheduleRlModels = new List<NNModel>();

    public List<Matchup> matchups = new List<Matchup>();

    // ---- schedule JSON (JsonUtility needs plain serializable classes with public fields) ----
    [Serializable] private class SchedPersona
    {
        public string id; public float aggression; public float defense; public float mobility; public float patience; public bool holdout;
        public string kind;   // "" / "Scripted" (default), "ILAgent" or "RL"
        public string model;  // RL: NNModel asset name
        public float styleAttack, styleMobility, styleDefense, styleMobilityTime, styleBlockTime;   // ILAgent target style
        public string mirrorOf;   // ILAgent: online mirror of this persona id (empty = fixed style)
    }
    [Serializable] private class SchedMatch { public int index; public int round; public string p1; public string p2; }
    [Serializable] private class SchedFile { public string name; public int seed; public List<SchedPersona> personas; public List<SchedMatch> matches; }

    // ---- progress: static, so it survives the scene reloads (reset when Play mode starts) ----
    private static int matchupIndex;
    private static int matchInMatchup;
    private static int totalSaved;
    private static int totalAbandoned;
    private static bool finished;
    private static bool resumeChecked;
    private static int stalematesOnCurrent;
    [Tooltip("After this many stalemates on the same match the entry is skipped (written to the run log as ABANDONED). 0 = never skip.")]
    public int maxStalematesPerMatch = 2;

#if UNITY_EDITOR
    [InitializeOnEnterPlayMode]
    private static void ResetProgress()
    {
        matchupIndex = 0; matchInMatchup = 0; totalSaved = 0; totalAbandoned = 0; finished = false; resumeChecked = false; stalematesOnCurrent = 0;
        TelemetryManager.OutputFolderOverride = null;   // never carry the Synthetic folder into a normal Play session
    }
#endif

    private Matchup current;
    private string personaReportedForMatch;
    private bool reloading;
    private float matchStartTime;

    // =====================================================================

    private void Awake()
    {
        if (!run) return;

#if !UNITY_EDITOR
        Debug.LogWarning("[ILDataGen] Editor only. Disabled in builds.");
        run = false;
        return;
#endif
        if (schedule != null)
        {
            // Rebuilt on every reload (the scene's Director is a new object each match); deterministic.
            if (!LoadSchedule())
            {
                run = false;
                return;
            }
            if (resumeFromRunLog && !resumeChecked)
            {
                resumeChecked = true;
                int next = LastLoggedIndex() + 1;
                if (next > 0)
                {
                    matchupIndex = next;
                    Debug.Log($"[ILDataGen] Resuming schedule '{schedule.name}' at match {next + 1}/{matchups.Count} (run log).");
                }
            }
        }

        if (finished || matchups == null || matchupIndex >= matchups.Count)
        {
            Debug.Log("[ILDataGen] Nothing to run (list finished or empty).");
            run = false;
            return;
        }

        current = matchups[matchupIndex];

        TelemetryManager.OutputFolderOverride = outputFolder;   // Telemetry/Synthetic + ImitationLogs/Synthetic
        Time.timeScale = timeScale;

        // What the menus normally write (read in CharacterManager.Awake / PvE setup Start)
        PlayerPrefs.SetString("Player1Choice", character);
        PlayerPrefs.SetString("Player2Choice", character);
        PvESelectionState.IsPvE = false;   // PvE setup scripts do nothing except disable the ML agents; we set up both sides ourselves

        // Power-ups are out of scope for the thesis
        foreach (var spawner in FindObjectsOfType<PowerUpSpawner>())
            spawner.gameObject.SetActive(false);

        matchStartTime = Time.time;
        Debug.Log($"[ILDataGen] Matchup {matchupIndex + 1}/{matchups.Count} '{current.label}' match {matchInMatchup + 1}/{current.matches}: P1={current.p1.Id} vs P2={current.p2.Id} (x{timeScale})");
    }

    private void OnEnable() { TelemetryManager.SessionClosed += OnSessionClosed; }
    private void OnDisable() { TelemetryManager.SessionClosed -= OnSessionClosed; }

    private IEnumerator Start()
    {
        if (!run) yield break;

        // Wait until both players exist and the PvE setup scripts (Start) have run.
        CharacterManager p1 = null, p2 = null;
        for (int i = 0; i < 300 && (p1 == null || p2 == null || p1.GetCurrentCharacter() == null || p2.GetCurrentCharacter() == null); i++)
        {
            yield return null;
            foreach (var m in FindObjectsOfType<CharacterManager>())
            {
                if (m.playerNum == 1) p1 = m;
                if (m.playerNum == 2) p2 = m;
            }
        }
        yield return null;   // one more frame: PvEMLBotRuntimeSetup.Start has disabled all ML agents by now

        if (p1 == null || p2 == null)
        {
            Debug.LogError("[ILDataGen] Could not find both players. Stopping.");
            run = false;
            yield break;
        }

        SetupSide(p1, p2, current.p1);
        SetupSide(p2, p1, current.p2);
    }

    private void Update()
    {
        if (!run || reloading || current == null) return;

        // RL sides have no SimpleBotController to report their id -> report here, once per match.
        var tm = TelemetryManager.Instance;
        if (tm != null && !string.IsNullOrEmpty(tm.CurrentMatchId) && tm.CurrentMatchId != personaReportedForMatch)
        {
            if (current.p1.kind != SideKind.Scripted) tm.SetPersona("P1", current.p1.Id);
            if (current.p2.kind != SideKind.Scripted) tm.SetPersona("P2", current.p2.Id);
            personaReportedForMatch = tm.CurrentMatchId;
        }

        // Stalemate guard
        if (Time.time - matchStartTime > maxMatchSeconds)
        {
            totalAbandoned++;
            stalematesOnCurrent++;
            if (maxStalematesPerMatch > 0 && stalematesOnCurrent >= maxStalematesPerMatch)
            {
                Debug.LogWarning($"[ILDataGen] {stalematesOnCurrent} stalemates on match {matchupIndex + 1}: skipped.");
                if (schedule != null) AppendRunLog(matchupIndex, "ABANDONED", current);
                stalematesOnCurrent = 0;
                matchInMatchup++;
                if (matchInMatchup >= current.matches) { matchInMatchup = 0; matchupIndex++; }
                if (matchupIndex >= matchups.Count)
                {
                    finished = true;
                    Debug.Log($"[ILDataGen] FINISHED. Saved matches: {totalSaved}, abandoned: {totalAbandoned}.");
                    StartCoroutine(FinishRun());
                    return;
                }
            }
            else
                Debug.LogWarning($"[ILDataGen] No result after {maxMatchSeconds}s, restarting the match (not counted).");
            StartCoroutine(Reload());
        }
    }

    // ---------------- side setup (same steps as PvEScriptedBotRuntimeSetup / PvEMLBotRuntimeSetup) ----------------
    private void SetupSide(CharacterManager self, CharacterManager enemy, Side side)
    {
        GameObject obj = self.gameObject;
        var agent = obj.GetComponent<FighterAgent>();
        var decision = obj.GetComponent<DecisionRequester>();
        var scripted = obj.GetComponent<SimpleBotController>();
        var il = obj.GetComponent<ILAgentController>();
        if (il != null && side.kind != SideKind.ILAgent) il.enabled = false;

        if (side.kind == SideKind.ILAgent || side.kind == SideKind.Human)
        {
            if (agent != null) agent.enabled = false;
            if (decision != null) decision.enabled = false;
            if (scripted != null) scripted.enabled = false;

            if (side.kind == SideKind.Human)
            {
                self.GetCurrentCharacter().SetInput(new KeyboardInputProvider());
            }
            else
            {
                if (side.ilModel == null)
                {
                    Debug.LogError($"[ILDataGen] ILAgent on P{self.playerNum}: no model assigned.");
                    return;
                }
                if (il == null) il = obj.AddComponent<ILAgentController>();
                il.seed = side.ilSeed;
                il.label = side.Id;
                il.enabled = true;
                il.LoadModel(side.ilModel);
                if (string.IsNullOrEmpty(side.mirrorOf))
                    il.SetStyle(side.styleAttack, side.styleMobility, side.styleDefense, side.styleMobilityTime, side.styleBlockTime);
                else
                {
                    // online mirror: EMA of the target persona's saved matches in this run's folder (resume-safe)
                    string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "My Games", "Head of Hell", "Telemetry", outputFolder);
                    double[] ema = OnlineMirrorStyle.FromTelemetryFolder(folder, side.mirrorOf, out int rounds);
                    float[] requested = il.NeutralStyle;
                    string source = "neutral";
                    if (ema != null)
                    {
                        requested = new float[ema.Length];
                        for (int i = 0; i < ema.Length; i++) requested[i] = (float)ema[i];
                        source = "mirror";
                    }
                    bool clamped = il.ApplyStyle(requested, source);
                    Debug.Log($"[ILDataGen] P{self.playerNum} online mirror of {side.mirrorOf}: {source} ({rounds} rounds)" +
                              (clamped ? " - clamped" : ""));
                }
                il.Rebind(self.GetCurrentCharacter(), enemy.GetCurrentCharacter());
            }
        }
        else if (side.kind == SideKind.Scripted)
        {
            if (agent != null) agent.enabled = false;
            if (decision != null) decision.enabled = false;

            if (scripted == null) scripted = obj.AddComponent<SimpleBotController>();
            scripted.enabled = true;
            scripted.Rebind(self.GetCurrentCharacter(), enemy.GetCurrentCharacter());
            scripted.SetSkill(scriptedSkill);
            scripted.SetPersonality(side.aggression, side.defense, side.mobility, side.patience, side.personaId);
        }
        else
        {
            var behavior = obj.GetComponent<BehaviorParameters>();
            if (agent == null || behavior == null || side.model == null)
            {
                Debug.LogError($"[ILDataGen] RL setup failed on P{self.playerNum} (agent/behavior/model missing).");
                return;
            }
            if (scripted != null) scripted.enabled = false;

            agent.selfManager = self;
            agent.enemyManager = enemy;
            behavior.Model = side.model;
            behavior.BehaviorType = BehaviorType.InferenceOnly;
            agent.enabled = true;
            if (decision != null) decision.enabled = true;
            agent.ForceRebind();
            agent.ClearInput();
        }

        Debug.Log($"[ILDataGen] P{self.playerNum} = {side.kind} '{side.Id}'");
    }

    // ---------------- next match ----------------
    private void OnSessionClosed(string matchId, bool saved)
    {
        if (!run || reloading) return;

        // Discarded sessions (e.g. the old session closed by StartSession after a stalemate reload) are ignored:
        // only a saved match moves the run forward and triggers the next reload.
        if (!saved) return;

        if (schedule != null)
            AppendRunLog(matchupIndex, matchId, current);
        stalematesOnCurrent = 0;

        {
            totalSaved++;
            matchInMatchup++;
            if (matchInMatchup >= current.matches)
            {
                matchInMatchup = 0;
                matchupIndex++;
            }
        }

        if (matchupIndex >= matchups.Count)
        {
            finished = true;
            Debug.Log($"[ILDataGen] FINISHED. Saved matches: {totalSaved}, abandoned: {totalAbandoned}.");
            // Do NOT stop Play here: we are inside the SessionClosed notification and the logger still has to
            // rename its file (stopping immediately lost the last match's imitation log).
            StartCoroutine(FinishRun());
            return;
        }

        StartCoroutine(Reload());
    }

    // ---------------- schedule file ----------------
    private bool LoadSchedule()
    {
        SchedFile file;
        try { file = JsonUtility.FromJson<SchedFile>(schedule.text); }
        catch (Exception e) { Debug.LogError($"[ILDataGen] Schedule '{schedule.name}' could not be read: {e.Message}"); return false; }

        if (file == null || file.personas == null || file.matches == null || file.matches.Count == 0)
        {
            Debug.LogError($"[ILDataGen] Schedule '{schedule.name}' has no personas or matches.");
            return false;
        }

        var byId = new Dictionary<string, SchedPersona>();
        foreach (var p in file.personas) byId[p.id] = p;

        var list = new List<Matchup>(file.matches.Count);
        foreach (var m in file.matches)
        {
            if (!byId.TryGetValue(m.p1, out var a) || !byId.TryGetValue(m.p2, out var b))
            {
                Debug.LogError($"[ILDataGen] Schedule match {m.index}: unknown persona '{m.p1}' or '{m.p2}'.");
                return false;
            }
            list.Add(new Matchup { label = $"#{m.index} r{m.round}", p1 = ToSide(a), p2 = ToSide(b), matches = 1 });
        }
        matchups = list;
        return true;
    }

    private Side ToSide(SchedPersona p)
    {
        if (p.kind == "RL")
        {
            NNModel m = scheduleRlModels.Find(x => x != null && x.name == p.model);
            if (m == null) Debug.LogError($"[ILDataGen] Schedule persona '{p.id}': RL model '{p.model}' not in Schedule Rl Models.");
            return new Side { kind = SideKind.RL, personaId = p.id, model = m };
        }
        if (p.kind == "ILAgent")
            return new Side
            {
                kind = SideKind.ILAgent, personaId = p.id, ilModel = scheduleIlModel,
                styleAttack = p.styleAttack, styleMobility = p.styleMobility, styleDefense = p.styleDefense,
                styleMobilityTime = p.styleMobilityTime, styleBlockTime = p.styleBlockTime,
                mirrorOf = p.mirrorOf ?? ""
            };
        return new Side
        {
            kind = SideKind.Scripted, personaId = p.id,
            aggression = p.aggression, defense = p.defense, mobility = p.mobility, patience = p.patience
        };
    }

    private string RunLogPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "My Games", "Head of Hell", "Telemetry", outputFolder, "_runlog_" + schedule.name + ".csv");

    // Highest schedule index already saved (-1 if none). Lines: index,matchId,p1,p2,savedLocal
    private int LastLoggedIndex()
    {
        string path = RunLogPath();
        if (!File.Exists(path)) return -1;
        int last = -1;
        foreach (string line in File.ReadAllLines(path))
        {
            string[] parts = line.Split(',');
            if (parts.Length > 0 && int.TryParse(parts[0], out int idx) && idx > last) last = idx;
        }
        return last;
    }

    // One line per saved match: links the schedule entry to the telemetry / imitation log (same matchId).
    private void AppendRunLog(int index, string matchId, Matchup m)
    {
        try
        {
            string path = RunLogPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (!File.Exists(path)) File.WriteAllText(path, "index,matchId,p1,p2,savedLocal\n");
            File.AppendAllText(path, $"{index},{matchId},{m.p1.Id},{m.p2.Id},{DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ILDataGen] Run log not written: {e.Message}");
        }
    }

    private IEnumerator FinishRun()
    {
        reloading = true;
        yield return new WaitForSecondsRealtime(delayBeforeNextMatch);

        Time.timeScale = 1f;
        TelemetryManager.OutputFolderOverride = null;
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#endif
    }

    private IEnumerator Reload()
    {
        reloading = true;
        yield return new WaitForSecondsRealtime(delayBeforeNextMatch);

        FindObjectOfType<GameManager>()?.ResetStatics();   // round/win counters are static in GameManager

#if UNITY_EDITOR
        EditorSceneManager.LoadSceneInPlayMode(SceneManager.GetActiveScene().path,
            new LoadSceneParameters(LoadSceneMode.Single));
#endif
    }

    private void OnDestroy()
    {
        // Leaving the data-generation scene must never redirect normal telemetry.
        if (finished || !run) TelemetryManager.OutputFolderOverride = null;
    }

    private void OnApplicationQuit()
    {
        TelemetryManager.OutputFolderOverride = null;
        Time.timeScale = 1f;
    }
}
