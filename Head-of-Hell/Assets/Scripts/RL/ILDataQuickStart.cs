using System.Collections;
using UnityEngine;

/// <summary>
/// TEMPORARY helper for ILDataScene: lets the scene be started directly with Play (no menus).
/// Before any CharacterManager.Awake runs, it writes the same selections the menus normally write:
/// - PlayerPrefs "Player1Choice" / "Player2Choice" (read in CharacterManager.Awake)
/// - PvESelectionState (read in PvEScriptedBotRuntimeSetup / PvEMLBotRuntimeSetup .Start)
/// Optionally gives the scripted bot a fixed persona (SimpleBotController.SetPersonality).
/// Put it ONLY in ILDataScene. Untick "apply" to disable it.
/// </summary>
[DefaultExecutionOrder(-10000)]
public class ILDataQuickStart : MonoBehaviour
{
    public bool apply = true;

    [Header("Characters (same names as the menu: Skipler, Fin, Lazy Bigus, ...)")]
    public string p1Character = "Skipler";
    public string p2Character = "Skipler";

    [Header("Opponent")]
    public bool vsBot = true;
    public PvEBotType botType = PvEBotType.ScriptedBot;
    public PvEDifficulty difficulty = PvEDifficulty.Easy;
    public PvEBotSide botSide = PvEBotSide.Player2;

    [Header("Scripted bot persona (empty id = keep the bot's normal random personality)")]
    public string personaId = "";
    [Range(0f, 1f)] public float aggression = 0.5f;
    [Range(0f, 1f)] public float defense = 0.5f;
    [Range(0f, 1f)] public float mobility = 0.5f;
    [Range(0f, 1f)] public float patience = 0.5f;

    private void Awake()
    {
        if (!apply) return;

        PlayerPrefs.SetString("Player1Choice", p1Character);
        PlayerPrefs.SetString("Player2Choice", p2Character);

        PvESelectionState.IsPvE = vsBot;
        PvESelectionState.SelectedBotType = botType;
        PvESelectionState.SelectedDifficulty = difficulty;
        PvESelectionState.SelectedBotSide = botSide;

        Debug.Log($"[ILDataQuickStart] P1={p1Character} P2={p2Character} vsBot={vsBot} ({botType}, {difficulty}, bot side {botSide})");
    }

    // The bot is created by PvEScriptedBotRuntimeSetup.Start, so wait until it exists, then set the persona.
    private IEnumerator Start()
    {
        if (!apply || !vsBot || botType != PvEBotType.ScriptedBot || string.IsNullOrEmpty(personaId)) yield break;

        for (int i = 0; i < 120; i++)   // up to ~2 s
        {
            var bot = FindObjectOfType<SimpleBotController>();
            if (bot != null)
            {
                bot.SetPersonality(aggression, defense, mobility, patience, personaId);
                Debug.Log($"[ILDataQuickStart] Persona '{personaId}': aggression={aggression} defense={defense} mobility={mobility} patience={patience}");
                yield break;
            }
            yield return null;
        }
        Debug.LogWarning("[ILDataQuickStart] No SimpleBotController found; persona not applied.");
    }
}
