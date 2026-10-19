using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Command-line arguments of the parallel data-generation build (thesis). Read once per process.
/// Without "-ilgen" nothing here is active and the build is the normal game.
///
///   HoH.exe -ilgen [-schedule file.json] [-out Folder] [-shard k/N] [-shardmode index|pair]
///           [-ilmodel file.json] [-fullrender]
///   (+ Unity's own: -screen-fullscreen 0 -screen-width 640 -screen-height 360 -logFile path)
/// </summary>
public static class ILGenArgs
{
    public static bool Active { get; private set; }
    public static string SchedulePath { get; private set; }
    public static string OutFolder { get; private set; }
    public static int ShardIndex { get; private set; }        // k (0-based)
    public static int ShardCount { get; private set; } = 1;   // N
    public static string ShardMode { get; private set; } = "index";   // "index" or "pair"
    public static string IlModelPath { get; private set; }
    public static bool FullRender { get; private set; }

    public const string SceneName = "ILDataScene";
    public const float LockedStep = 1f / 60f;   // every frame = exactly 1/60 game-second (same structure as the x1 data)

    public static bool Sharded => ShardCount > 1;
    public static string ShardSuffix => Sharded ? $"_s{ShardIndex}of{ShardCount}" : "";

    private static bool parsed;

    public static void Parse()
    {
        if (parsed) return;
        parsed = true;
        string[] a = Environment.GetCommandLineArgs();
        for (int i = 0; i < a.Length; i++)
        {
            string k = a[i].ToLowerInvariant();
            string next = i + 1 < a.Length ? a[i + 1] : null;
            switch (k)
            {
                case "-ilgen": Active = true; break;
                case "-schedule": SchedulePath = next; i++; break;
                case "-out": OutFolder = next; i++; break;
                case "-ilmodel": IlModelPath = next; i++; break;
                case "-shardmode": ShardMode = (next ?? "index").ToLowerInvariant(); i++; break;
                case "-fullrender": FullRender = true; break;
                case "-shard":
                    var p = (next ?? "").Split('/');
                    if (p.Length == 2 && int.TryParse(p[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int sk)
                        && int.TryParse(p[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int sn) && sn >= 1 && sk >= 0 && sk < sn)
                    { ShardIndex = sk; ShardCount = sn; }
                    else Debug.LogError($"[ILGen] Bad -shard '{next}' (expected k/N, e.g. 0/4). Ignored.");
                    i++;
                    break;
            }
        }
        if (ShardMode != "index" && ShardMode != "pair")
        {
            Debug.LogError($"[ILGen] Bad -shardmode '{ShardMode}' (index|pair). Using index.");
            ShardMode = "index";
        }
    }

    /// <summary>Classic ruleset = exactly what StartButtonScript writes for "Default" (the thesis data were collected with it).</summary>
    public static string ClassicRulesetJson()
    {
        var r = new CustomRuleset
        {
            health = 100, powerupsEnabled = false, rounds = 1, playerSpeed = 4,
            quickDisabled = false, heavyDisabled = false, blockDisabled = false, chargeDisabled = false, specialDisabled = false,
            hideHealth = false, devTools = false, portals = 0, chanChan = false
        };
        return JsonUtility.ToJson(r);
    }

    public static TextAsset LoadTextAsset(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (!File.Exists(path)) { Debug.LogError($"[ILGen] File not found: {path}"); return null; }
        var t = new TextAsset(File.ReadAllText(path)) { name = Path.GetFileNameWithoutExtension(path) };
        return t;
    }
}

/// <summary>
/// Build only, only with -ilgen: turns the normal game build into a data-generation worker.
/// - mutes audio, keeps running when the window has no focus;
/// - removes the main-menu objects that would make the build differ from the Editor runs
///   (ProfileManager -> telemetry identity NONE like in the Editor; CharacterStatsManager -> menu win stats untouched);
/// - goes straight to ILDataScene (the ILDataGenDirector there does the rest);
/// - hides only the stage graphics (Renderers under Stages / Borders / the camera background). GameObjects, colliders,
///   animators and scripts stay active, characters and UI are untouched. -fullrender keeps everything;
/// - restores the player's own PlayerPrefs (character choice, stage, ruleset) when the process quits.
/// </summary>
public static class ILGenBootstrap
{
    static readonly string[] PrefKeys = { "Player1Choice", "Player2Choice", "SelectedStage", "SelectedRuleset" };
    static string[] savedValues;
    static bool[] savedHas;
    static bool jumped;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void BeforeFirstScene()
    {
        if (Application.isEditor) return;   // the Editor workflow is unchanged
        ILGenArgs.Parse();
        if (!ILGenArgs.Active) return;

        Application.runInBackground = true;   // Project Settings has it off: unfocused windows would pause
        AudioListener.volume = 0f;

        savedHas = new bool[PrefKeys.Length];
        savedValues = new string[PrefKeys.Length];
        for (int i = 0; i < PrefKeys.Length; i++)
        {
            savedHas[i] = PlayerPrefs.HasKey(PrefKeys[i]);
            savedValues[i] = savedHas[i] ? PlayerPrefs.GetString(PrefKeys[i]) : null;
        }
        Application.quitting += RestorePrefs;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Debug.Log($"[ILGen] Data-generation build. shard {ILGenArgs.ShardIndex}/{ILGenArgs.ShardCount} ({ILGenArgs.ShardMode}), " +
                  $"schedule '{ILGenArgs.SchedulePath ?? "(scene)"}', out '{ILGenArgs.OutFolder ?? "(scene)"}', " +
                  $"model '{ILGenArgs.IlModelPath ?? "(scene)"}', render {(ILGenArgs.FullRender ? "full" : "characters only")}");
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != ILGenArgs.SceneName)
        {
            if (jumped) return;
            jumped = true;
            RemoveMenuLeftovers();
            if (!Application.CanStreamedLevelBeLoaded(ILGenArgs.SceneName))
            {
                Debug.LogError($"[ILGen] '{ILGenArgs.SceneName}' is not in Build Settings. Quitting.");
                Application.Quit();
                return;
            }
            SceneManager.LoadScene(ILGenArgs.SceneName);
            return;
        }

        RemoveMenuLeftovers();   // in case something was created after the first scene
        if (!ILGenArgs.FullRender) HideStageGraphics(scene);
    }

    static void RemoveMenuLeftovers()
    {
        // DestroyImmediate, not Destroy: Destroy waits until the end of the frame, and in the pilot the first match's
        // GameManager.Start still saw the ProfileManager (telemetry identity GUEST instead of NONE on match 1 of every shard).
        var pm = UnityEngine.Object.FindObjectOfType<ProfileManager>(true);
        if (pm != null) { UnityEngine.Object.DestroyImmediate(pm.gameObject); Debug.Log("[ILGen] Main-menu ProfileManager removed (identity NONE, like the Editor)."); }
        var cs = UnityEngine.Object.FindObjectOfType<CharacterStatsManager>(true);
        if (cs != null) { UnityEngine.Object.DestroyImmediate(cs.gameObject); Debug.Log("[ILGen] Main-menu CharacterStatsManager removed (menu win stats untouched)."); }
    }

    static readonly string[] StageRoots = { "Stages", "Borders", "Main Camera" };

    static void HideStageGraphics(Scene scene)
    {
        int n = 0;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (Array.IndexOf(StageRoots, root.name) < 0) continue;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r.GetComponentInParent<CharacterManager>(true) != null) continue;   // never a character
                r.enabled = false;
                n++;
            }
        }
        Debug.Log($"[ILGen] Stage graphics hidden: {n} renderers (colliders/animators untouched).");
    }

    static void RestorePrefs()
    {
        if (savedHas == null) return;
        for (int i = 0; i < PrefKeys.Length; i++)
        {
            if (savedHas[i]) PlayerPrefs.SetString(PrefKeys[i], savedValues[i]);
            else PlayerPrefs.DeleteKey(PrefKeys[i]);
        }
        PlayerPrefs.Save();
    }
}
