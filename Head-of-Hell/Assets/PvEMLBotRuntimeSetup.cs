using UnityEngine;
using Unity.MLAgents.Policies;
using Unity.Barracuda;

public class PvEMLBotRuntimeSetup : MonoBehaviour
{
    [Header("Difficulty Models (drag & drop)")]
    [SerializeField] private NNModel easyModel;
    [SerializeField] private NNModel mediumModel;
    [SerializeField] private NNModel hardModel;

    [Header("Behavior")]
    [SerializeField] private bool useInferenceOnly = true;

    void Start()
    {
        if (!PvESelectionState.IsPvE)
        {
            PvESelectionState.DisableAllMLAgents();
            return;
        }

        if (PvESelectionState.SelectedBotType != PvEBotType.MLAgent)
        {
            PvESelectionState.DisableAllMLAgents();
            return;
        }

        CharacterManager[] managers = FindObjectsOfType<CharacterManager>();

        CharacterManager p1 = null;
        CharacterManager p2 = null;

        foreach (var m in managers)
        {
            if (m.playerNum == 1) p1 = m;
            if (m.playerNum == 2) p2 = m;
        }

        if (p1 == null || p2 == null)
        {
            Debug.LogError("PvE ML Setup: Could not find both players.");
            return;
        }

        CharacterManager botManager =
            PvESelectionState.SelectedBotSide == PvEBotSide.Player1 ? p1 : p2;

        CharacterManager humanManager =
            PvESelectionState.SelectedBotSide == PvEBotSide.Player1 ? p2 : p1;

        SetupMLBot(botManager, humanManager);
    }

    void SetupMLBot(CharacterManager botManager, CharacterManager enemyManager)
    {
        GameObject botObj = botManager.gameObject;
        GameObject humanObj = enemyManager.gameObject;

        // BOT SIDE
        FighterAgent ml = botObj.GetComponent<FighterAgent>();
        if (ml == null)
        {
            Debug.LogError($"PvE ML Setup: No FighterAgent found on Player {botManager.playerNum}.");
            return;
        }

        Unity.MLAgents.DecisionRequester decision = botObj.GetComponent<Unity.MLAgents.DecisionRequester>();
        BehaviorParameters behavior = botObj.GetComponent<BehaviorParameters>();
        SimpleBotController scripted = botObj.GetComponent<SimpleBotController>();

        if (scripted != null)
        {
            scripted.enabled = false;
        }

        if (behavior == null)
        {
            Debug.LogError($"PvE ML Setup: No BehaviorParameters found on Player {botManager.playerNum}.");
            return;
        }

        NNModel selectedModel = GetModelFromDifficulty(PvESelectionState.SelectedDifficulty);

        if (selectedModel == null)
        {
            Debug.LogError($"PvE ML Setup: No model assigned for difficulty {PvESelectionState.SelectedDifficulty}.");
            return;
        }

        ml.selfManager = botManager;
        ml.enemyManager = enemyManager;

        behavior.Model = selectedModel;
        behavior.BehaviorType = useInferenceOnly ? BehaviorType.InferenceOnly : BehaviorType.Default;

        ml.enabled = true;

        if (decision != null)
        {
            decision.enabled = true;
        }

        ml.ForceRebind();
        ml.ClearInput();

        // HUMAN SIDE
        FighterAgent humanML = humanObj.GetComponent<FighterAgent>();
        if (humanML != null)
        {
            humanML.ClearInput();
            humanML.enabled = false;
        }

        Unity.MLAgents.DecisionRequester humanDecision = humanObj.GetComponent<Unity.MLAgents.DecisionRequester>();
        if (humanDecision != null)
        {
            humanDecision.enabled = false;
        }

        SimpleBotController humanScripted = humanObj.GetComponent<SimpleBotController>();
        if (humanScripted != null)
        {
            humanScripted.enabled = false;
        }

        Debug.Log(
            $"ML Bot Enabled on Player {botManager.playerNum} | Difficulty: {PvESelectionState.SelectedDifficulty} | Model: {selectedModel.name}"
        );
    }

    NNModel GetModelFromDifficulty(PvEDifficulty difficulty)
    {
        switch (difficulty)
        {
            case PvEDifficulty.Easy:
            {
                return easyModel;
            }

            case PvEDifficulty.Medium:
            {
                return mediumModel;
            }

            case PvEDifficulty.Hard:
            {
                return hardModel;
            }
        }

        return mediumModel;
    }
}
