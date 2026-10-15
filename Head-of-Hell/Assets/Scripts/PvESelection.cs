using Unity.MLAgents;
using UnityEngine;

public enum PvEBotType
{
    MLAgent,
    ScriptedBot,
    ILAgent       // thesis: style-conditioned imitation agent (order = carousel index)
}

/// <summary>Thesis: where the IL agent takes its style from.</summary>
public enum ILStyleSource
{
    Mirror,       // the style of its opponent (EMA, updated every round)
    Ghost         // the stored style of another profile
}

public enum PvEDifficulty
{
    Easy,
    Medium,
    Hard
}

public enum PvEBotSide
{
    Player1,
    Player2
}

public static class PvESelectionState
{
    public static bool IsPvE = false;
    public static PvEBotType SelectedBotType = PvEBotType.ScriptedBot;
    public static PvEDifficulty SelectedDifficulty = PvEDifficulty.Easy;
    public static PvEBotSide SelectedBotSide = PvEBotSide.Player1;

    // Thesis (IL agent)
    public static ILStyleSource SelectedILStyleSource = ILStyleSource.Mirror;
    public static string SelectedGhostProfileId = "";
    public static string SelectedGhostName = "";

    public static void ResetToDefaults()
    {
        ResetPvEOptions();
        GameModeSelectionState.SelectPvP1v1();
    }

    public static void ResetPvEOptions()
    {
        IsPvE = false;
        SelectedBotType = PvEBotType.ScriptedBot;
        SelectedDifficulty = PvEDifficulty.Easy;
        SelectedBotSide = PvEBotSide.Player1;
        SelectedILStyleSource = ILStyleSource.Mirror;
        SelectedGhostProfileId = "";
        SelectedGhostName = "";
    }
    public static void SelectPvPMode()
    {
        ResetPvEOptions();
        DisableAllMLAgents();
    }

    public static void DisableAllMLAgents()
    {
        FighterAgent[] agents = Object.FindObjectsOfType<FighterAgent>(true);

        foreach (FighterAgent agent in agents)
        {
            agent.ClearInput();
            agent.enabled = false;

            DecisionRequester decisionRequester = agent.GetComponent<DecisionRequester>();
            if (decisionRequester != null)
            {
                decisionRequester.enabled = false;
            }

            CharacterManager characterManager = agent.GetComponent<CharacterManager>();
            Character currentCharacter = characterManager != null ? characterManager.GetCurrentCharacter() : null;
            if (currentCharacter != null && currentCharacter.GetInputProvider() is AIInputProvider)
            {
                currentCharacter.SetInput(new KeyboardInputProvider());
            }
        }
    }
}
