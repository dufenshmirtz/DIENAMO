using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Thesis (online IL agent): sets up the IL agent in the normal game when "IL Agent" was chosen in the PvE menu.
///
/// The gameplay scene is reloaded for every round, so this runs at the start of EVERY round and gives the agent the
/// latest style:
/// - Mirror: the EMA of its opponent (the human), for this character;
/// - Ghost : the EMA of the profile chosen in the menu;
/// - no EMA yet (first round, new profile, or ghost without data): the neutral style of the model (persona mean).
/// The style is clamped to the training range (ILAgentController.ApplyStyle). After the round the StyleTracker
/// updates the human's EMA and logs given vs measured style of the agent.
///
/// Created automatically (no scene changes): it listens to scene loads and acts only in a scene with two fighters
/// while PvE + IL Agent is selected. Safety: if a character is outside what the model was trained on, or the
/// settings/model are missing, the scripted bot plays instead (warning in the Console).
/// </summary>
public class PvEILAgentRuntimeSetup : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);   // the scene that was already loaded
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!PvESelectionState.IsPvE || PvESelectionState.SelectedBotType != PvEBotType.ILAgent) return;
        if (FindObjectOfType<PvEILAgentRuntimeSetup>() != null) return;
        if (FindObjectOfType<ILDataGenDirector>() != null) return;                    // data generation: the Director decides the sides
        if (FindObjectsOfType<CharacterManager>().Length < 2) return;                 // menus etc.
        new GameObject("IL_Agent_RuntimeSetup").AddComponent<PvEILAgentRuntimeSetup>();
    }

    private IEnumerator Start()
    {
        yield return null;   // one frame: the other setups (ML / scripted) have run their Start by now

        CharacterManager p1 = null, p2 = null;
        foreach (var m in FindObjectsOfType<CharacterManager>())
        {
            if (m.playerNum == 1) p1 = m;
            if (m.playerNum == 2) p2 = m;
        }
        if (p1 == null || p2 == null)
        {
            Debug.LogError("[IL PvE] Could not find both players.");
            yield break;
        }

        CharacterManager bot = PvESelectionState.SelectedBotSide == PvEBotSide.Player1 ? p1 : p2;
        CharacterManager human = bot == p1 ? p2 : p1;
        string botChar = bot.GetCharacterName(1);
        string humanChar = human.GetCharacterName(1);

        var settings = ILAgentSettings.Load();
        if (settings == null || settings.model == null)
        {
            Debug.LogError("[IL PvE] No Resources/ILAgentSettings asset with a model. The scripted bot plays instead.");
            FallbackScripted(bot, human);
            yield break;
        }
        if (!settings.AllowsSelf(botChar) || !settings.AllowsOpponent(humanChar))
        {
            Debug.LogWarning($"[IL PvE] The IL agent was trained on {string.Join("/", settings.Meta?.trainedSelf ?? new string[0])} " +
                             $"vs {string.Join("/", settings.Meta?.trainedOpp ?? new string[0])} only; this round is " +
                             $"{botChar} vs {humanChar}. The scripted bot plays instead.");
            FallbackScripted(bot, human);
            yield break;
        }

        GameObject obj = bot.gameObject;
        var ml = obj.GetComponent<FighterAgent>();
        if (ml != null) ml.enabled = false;
        var decision = obj.GetComponent<Unity.MLAgents.DecisionRequester>();
        if (decision != null) decision.enabled = false;
        var scripted = obj.GetComponent<SimpleBotController>();
        if (scripted != null) scripted.enabled = false;

        var il = obj.GetComponent<ILAgentController>();
        if (il == null) il = obj.AddComponent<ILAgentController>();
        il.seed = settings.seed;
        il.temperature = settings.temperature;
        il.enabled = true;
        if (!il.LoadModel(settings.model))
        {
            il.enabled = false;
            Debug.LogError("[IL PvE] Model failed to load. The scripted bot plays instead.");
            FallbackScripted(bot, human);
            yield break;
        }

        // ---- the style for this round
        string character = (botChar ?? "").Trim().ToLowerInvariant();   // same key as StyleAxes (meta character, lower case)
        string source;
        StyleProfileStore.Entry entry;
        if (PvESelectionState.SelectedILStyleSource == ILStyleSource.Ghost)
        {
            source = "ghost";
            entry = StyleProfileStore.Find(PvESelectionState.SelectedGhostProfileId, character, true);
        }
        else
        {
            source = "mirror";
            var id = ProfileManager.I != null ? ProfileManager.I.GetTelemetryIdentity(human.playerNum) : ("NONE", "None");
            var key = StyleTracker.PlayerKey(id.Item1, id.Item2, "", "P" + human.playerNum);
            string humanCharacter = (humanChar ?? "").Trim().ToLowerInvariant();   // the human's style on his own character
            entry = StyleProfileStore.Find(key.key, humanCharacter, key.persistent);
        }

        float[] requested;
        if (entry != null && entry.rounds > 0 && entry.ema != null && entry.ema.Length == 5)
        {
            requested = new float[5];
            for (int i = 0; i < 5; i++) requested[i] = (float)entry.ema[i];
        }
        else
        {
            requested = il.NeutralStyle;
            source = source == "ghost" ? "neutral(ghost has no style)" : "neutral";
        }

        il.label = source == "ghost" ? "IL:ghost:" + PvESelectionState.SelectedGhostName : "IL:" + source;
        bool clamped = il.ApplyStyle(requested, source);
        il.Rebind(bot.GetCurrentCharacter(), human.GetCurrentCharacter());

        Debug.Log($"[IL PvE] P{bot.playerNum} = IL agent ({source}{(entry != null ? $", {entry.rounds} rounds of {entry.displayName}" : "")}) " +
                  $"style {Fmt(il.Style)}{(clamped ? $" (clamped from {Fmt(requested)})" : "")}");

        // name the agent in the telemetry of this round (persona id), like the Director does
        float waitUntil = Time.realtimeSinceStartup + 30f;
        while (Time.realtimeSinceStartup < waitUntil &&
               (TelemetryManager.Instance == null || string.IsNullOrEmpty(TelemetryManager.Instance.CurrentMatchId)))
            yield return null;
        TelemetryManager.Instance?.SetPersona("P" + bot.playerNum, il.label);
    }

    /// <summary>Same steps as PvEScriptedBotRuntimeSetup (skill of the "Intermediate" difficulty).</summary>
    private static void FallbackScripted(CharacterManager bot, CharacterManager human)
    {
        GameObject obj = bot.gameObject;
        var ml = obj.GetComponent<FighterAgent>();
        if (ml != null) ml.enabled = false;
        var il = obj.GetComponent<ILAgentController>();
        if (il != null) il.enabled = false;
        var sb = obj.GetComponent<SimpleBotController>();
        if (sb == null) sb = obj.AddComponent<SimpleBotController>();
        sb.enabled = true;
        sb.Rebind(bot.GetCurrentCharacter(), human.GetCurrentCharacter());
        sb.SetSkill(0.60f);
    }

    static string Fmt(float[] v)
    {
        var p = new string[v.Length];
        for (int i = 0; i < v.Length; i++) p[i] = v[i].ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);
        return "[" + string.Join(" ", p) + "]";
    }
}
