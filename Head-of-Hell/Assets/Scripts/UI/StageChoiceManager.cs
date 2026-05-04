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
    private int selectedStageIndex = 0;

    private bool gameModePicked = false;
    private bool stagePicked = false;
    private bool readyToProceed = false;

    void Start()
    {
        HighlightCurrentSelection();
        RefreshCurrentSettingText();
        if (selectionText != null)
        {
            
        }
    }
    void Update()
    {
        if (!gameModePicked)
        {
            HandleModeSelection();
        }
        else if (!stagePicked)
        {
            HandleStageSelection();
        }
        else if (readyToProceed)
        {
            HandleProceed();
        }

        if (Input.GetMouseButtonDown(1)) // Right click to reset
        {
            ResetAll();
        }

        // Global fallback: if proceed is selected and Enter is pressed, invoke
        if (Input.GetKeyDown(KeyCode.Return) &&
            EventSystem.current.currentSelectedGameObject == proceedButton.gameObject)
        {
            proceedButton.onClick.Invoke();
        }
    }

    void HandleModeSelection()
    {
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
        {
            selectedModeIndex = Mathf.Max(0, selectedModeIndex - 1);
            HighlightCurrentSelection();
            RefreshCurrentSettingText();
        }
        else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
        {
            selectedModeIndex = Mathf.Min(1, selectedModeIndex + 1);
            HighlightCurrentSelection();
            RefreshCurrentSettingText();
        }
        else if (Input.GetKeyDown(KeyCode.Return))
        {
            gameModePicked = true;
            HighlightStageSelection();
            RefreshCurrentSettingText();
        }
    }

    void HandleStageSelection()
    {
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
        {
            if (selectedStageIndex > 0)
                selectedStageIndex--;
        }
        else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
        {
            if (selectedStageIndex < stageButtons.Length - 1)
                selectedStageIndex++;
        }
        else if (Input.GetKeyDown(KeyCode.Return))
        {
            Button selectedButton = stageButtons[selectedStageIndex];

            if (selectedButton == randomButton)
            {
                Button randomStage = GetRandomStageButton();
                randomStage.onClick.Invoke();

                // Update selectedStageIndex to match randomly selected button
                for (int i = 0; i < stageButtons.Length; i++)
                {
                    if (stageButtons[i] == randomStage)
                    {
                        selectedStageIndex = i;
                        break;
                    }
                }

                Debug.Log("Randomly selected stage: " + randomStage.name);
                HighlightStageSelection();
            }
            else
            {
                selectedButton.onClick.Invoke();
            }

            stagePicked = true;
            readyToProceed = true;
            HighlightProceed();
            return;
        }

        HighlightStageSelection();
    }

    void HandleProceed()
    {
        HighlightProceed(); // Just highlight — Enter is handled globally in Update()
    }

    void HighlightCurrentSelection()
    {
        if (selectedModeIndex == 0)
            defaultButton.Select();
        else
            customButton.Select();
    }

    void HighlightStageSelection()
    {
        if (stageButtons != null && stageButtons.Length > selectedStageIndex)
        {
            stageButtons[selectedStageIndex].Select();
        }
    }

    void HighlightProceed()
    {
        proceedButton.Select();
    }

    void ResetAll()
    {
        gameModePicked = false;
        stagePicked = false;
        readyToProceed = false;

        selectedModeIndex = 0;
        selectedStageIndex = 0;

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

    private Button GetRandomStageButton()
    {
        List<Button> availableButtons = new List<Button>(stageButtons);
        availableButtons.Remove(randomButton); // Exclude the random button itself
        int randomIndex = Random.Range(0, availableButtons.Count);
        return availableButtons[randomIndex];
    }
}
