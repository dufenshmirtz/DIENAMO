using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class StageChoiceManager : MonoBehaviour
{
    public Button defaultButton;
    public Button customButton;
    public Button[] stageButtons; // 0–3 (1 row or 2x2 logic is irrelevant here)
    public Button proceedButton;
    public Button randomButton;

    public GameObject gameSetupMenu;
    public GameObject characterSelectionMenu;
    public GameObject mainMenu;
    public GameObject botSelectionMenu;
    public GameObject menuBackground;

    public TextMeshProUGUI selectionText;

    private int selectedModeIndex = 0; // 0 = default, 1 = custom

    [Tooltip("Custom Settings slot buttons that pick a ruleset and return to Game Setup. " +
             "After one of them (or Default) is clicked, the cursor goes to the first stage.")]
    public Button[] customSlotButtons;

    void Start()
    {
        // Choosing a ruleset (Default here, or a slot in Custom Settings) moves the cursor to the stages.
        if (defaultButton != null)
            defaultButton.onClick.AddListener(SelectFirstStage);
        if (customSlotButtons != null)
            foreach (Button slot in customSlotButtons)
                if (slot != null) slot.onClick.AddListener(SelectFirstStage);

        HighlightCurrentSelection();
        RefreshCurrentSettingText();
        if (selectionText != null)
        {
            
        }
    }
    void Update()
    {
        // Keyboard/gamepad navigation (mode -> stage -> proceed) is now handled by Unity's EventSystem
        // + MenuNavigator: arrows move freely, Enter/A clicks the selected button (stage pick then jumps
        // to Proceed, see StageChoiceButtons), Esc/B undoes the stage pick or goes Back. The old
        // arrow/Enter state machine was removed here because it ran in parallel with the EventSystem.

        if (Input.GetMouseButtonDown(1)) // Right click to reset
        {
            ResetAll();
        }
    }

    /// <summary>Puts the keyboard/gamepad cursor on the left-most stage that can be picked
    /// (if a stage is already picked, that is the picked one, since the others are locked).</summary>
    public void SelectFirstStage()
    {
        if (!gameObject.activeInHierarchy || EventSystem.current == null || stageButtons == null)
            return;

        foreach (Button stage in stageButtons)
        {
            if (stage != null && stage.gameObject.activeInHierarchy && stage.interactable)
            {
                EventSystem.current.SetSelectedGameObject(stage.gameObject);
                return;
            }
        }
    }

    void HighlightCurrentSelection()
    {
        if (selectedModeIndex == 0)
            defaultButton.Select();
        else
            customButton.Select();
    }

    void ResetAll()
    {
        selectedModeIndex = 0;

        HighlightCurrentSelection();
    }
    void OnEnable()
    {
        RefreshCurrentSettingText();
    }

    void RefreshCurrentSettingText()
    {
        if (selectionText == null) return;

        if (CustomRulesetScreenManager.currentRuleset != null &&
            !string.IsNullOrEmpty(CustomRulesetScreenManager.currentRuleset.slotName))
        {
            selectionText.text = "Current Setting : " +
                CustomRulesetScreenManager.currentRuleset.slotName;
        }
        else
        {
            selectionText.text = "Current Setting : Default";
        }
    }

    // Call this from the Proceed Button OnClick
    public void OnProceed()
    {
        if (gameSetupMenu != null)
            gameSetupMenu.SetActive(false);

        if (characterSelectionMenu != null)
        {
            characterSelectionMenu.SetActive(true);
        }
        else
        {
            print("sakede");
        }
            
    }

    // Call this from the Back Button OnClick
    public void OnBack()
    {
        ResetAll();

        StageChoiceButtons stageChoiceButtons = GetComponentInChildren<StageChoiceButtons>(true);
        if (stageChoiceButtons != null)
        {
            stageChoiceButtons.ResetStageSelection();
        }

        if (gameSetupMenu != null)
        {
            gameSetupMenu.SetActive(false);
        }

        if (PvESelectionState.IsPvE)
        {
            if (botSelectionMenu != null)
            {
                botSelectionMenu.SetActive(true);
            }

            return;
        }

        PvESelectionState.SelectPvPMode();

        if (mainMenu != null)
        {
            mainMenu.SetActive(true);
        }

        if (menuBackground != null)
        {
            menuBackground.SetActive(true);
        }
    }

}
