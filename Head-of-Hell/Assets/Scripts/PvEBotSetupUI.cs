using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PvEBotSetupUI : MonoBehaviour
{
    [Header("Scene Navigation")]
    public string nextSceneName = "GameSetup";
    public string backSceneName = "MainMenu";

    [Header("Bot Type Buttons")]
    public Button mlAgentButton;
    public Button scriptedBotButton;

    [Header("Difficulty Buttons")]
    public Button easyButton;
    public Button mediumButton;
    public Button hardButton;

    [Header("Navigation Buttons")]
    public Button proceedButton;
    public Button backButton;

    [Header("Labels")]
    public TMP_Text selectedBotText;
    public TMP_Text selectedDifficultyText;
    public TMP_Text instructionText;

    private bool botSelected;
    private bool difficultySelected;

    private void Start()
    {
        if (!PvESelectionState.IsPvE)
            PvESelectionState.IsPvE = true;

        GameModeSelectionState.SelectPvE();

        // Δεν θεωρούμε preselected τα defaults.
        botSelected = false;
        difficultySelected = false;

        RefreshUI();
    }

    public void SelectMLAgent()
    {
        PvESelectionState.SelectedBotType = PvEBotType.MLAgent;
        botSelected = true;

        // κάθε αλλαγή bot type απαιτεί νέο confirm difficulty
        difficultySelected = false;

        RefreshUI();
    }

    public void SelectScriptedBot()
    {
        PvESelectionState.SelectedBotType = PvEBotType.ScriptedBot;
        botSelected = true;

        difficultySelected = false;

        RefreshUI();
    }

    public void SelectEasy()
    {
        if (!botSelected) return;

        PvESelectionState.SelectedDifficulty = PvEDifficulty.Easy;
        difficultySelected = true;

        RefreshUI();
    }

    public void SelectMedium()
    {
        if (!botSelected) return;

        PvESelectionState.SelectedDifficulty = PvEDifficulty.Medium;
        difficultySelected = true;

        RefreshUI();
    }

    public void SelectHard()
    {
        if (!botSelected) return;

        PvESelectionState.SelectedDifficulty = PvEDifficulty.Hard;
        difficultySelected = true;

        RefreshUI();
    }

    public void ContinueToGameSetup()
    {
        if (!botSelected || !difficultySelected)
        {
            Debug.LogWarning("Select bot type first, then difficulty.");
            return;
        }

        SceneManager.LoadScene(nextSceneName);
    }

    public void BackToMainMenu()
    {
        PvESelectionState.ResetToDefaults();
        SceneManager.LoadScene(backSceneName);
    }

    private void RefreshUI()
    {
        // Labels
        if (selectedBotText != null)
        {
            selectedBotText.text = botSelected
                ? "Bot: " + GetBotTypeLabel(PvESelectionState.SelectedBotType)
                : "Bot: Not Selected";
        }

        if (selectedDifficultyText != null)
        {
            selectedDifficultyText.text = difficultySelected
                ? "Difficulty: " + GetDifficultyLabel(PvESelectionState.SelectedDifficulty)
                : "Difficulty: Not Selected";
        }

        if (instructionText != null)
        {
            if (!botSelected)
                instructionText.text = "Select bot type";
            else if (!difficultySelected)
                instructionText.text = "Select difficulty";
            else
                instructionText.text = "Ready";
        }

        // Bot selection visuals
        SetButtonVisual(mlAgentButton, botSelected && PvESelectionState.SelectedBotType == PvEBotType.MLAgent);
        SetButtonVisual(scriptedBotButton, botSelected && PvESelectionState.SelectedBotType == PvEBotType.ScriptedBot);

        // Difficulty buttons enabled only after bot type
        if (easyButton != null) easyButton.interactable = botSelected;
        if (mediumButton != null) mediumButton.interactable = botSelected;
        if (hardButton != null) hardButton.interactable = botSelected;

        SetButtonVisual(easyButton, difficultySelected && PvESelectionState.SelectedDifficulty == PvEDifficulty.Easy);
        SetButtonVisual(mediumButton, difficultySelected && PvESelectionState.SelectedDifficulty == PvEDifficulty.Medium);
        SetButtonVisual(hardButton, difficultySelected && PvESelectionState.SelectedDifficulty == PvEDifficulty.Hard);

        // Proceed only when both chosen
        if (proceedButton != null)
            proceedButton.interactable = botSelected && difficultySelected;
    }

    private string GetBotTypeLabel(PvEBotType type)
    {
        switch (type)
        {
            case PvEBotType.MLAgent: return "ML Agent";
            case PvEBotType.ScriptedBot: return "Scripted Bot";
            default: return "Unknown";
        }
    }

    private string GetDifficultyLabel(PvEDifficulty difficulty)
    {
        switch (difficulty)
        {
            case PvEDifficulty.Easy: return "Easy";
            case PvEDifficulty.Medium: return "Medium";
            case PvEDifficulty.Hard: return "Hard";
            default: return "Unknown";
        }
    }

    private void SetButtonVisual(Button button, bool selected)
    {
        if (button == null) return;

        ColorBlock colors = button.colors;
        colors.normalColor = selected ? new Color(0.75f, 0.75f, 0.75f, 1f) : Color.white;
        colors.selectedColor = colors.normalColor;
        button.colors = colors;
    }
}
