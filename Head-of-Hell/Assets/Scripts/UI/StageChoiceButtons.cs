using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class StageChoiceButtons : MonoBehaviour, IMenuCancelHandler
{
    public Button[] stageButtons;  // Buttons representing stages
    public TextMeshProUGUI stageNameText;  // Text to display the selected stage name
    public Button startButton;  // Button to confirm the stage selection
    private bool stagePicked = false;
    public MainMenuMusic audiomanager;

    void Start()
    {
        stagePicked = false;
        foreach (Button button in stageButtons)
        {
            button.onClick.AddListener(() => OnStageButtonClicked(button));
        }
        startButton.gameObject.SetActive(false);  // Hide the start button initially
    }

    void Update()
    {
        // Detect right mouse button click
        if (Input.GetMouseButtonDown(1)) // 1 means right mouse button
        {
            ResetStageSelection();
        }
    }

    public void OnStageButtonClicked(Button button)
    {
        if (stagePicked) return;

        // Get the stage index or identifier from the button
        int stageIndex = System.Array.IndexOf(stageButtons, button);

        // Update the UI to show the selected stage
        stageNameText.text = button.name;

        // Store the selected stage name for later use (e.g., in PlayerPrefs)
        PlayerPrefs.SetString("SelectedStage", button.name);

        // Disable other buttons to prevent changing the selection
        foreach (Button stageButton in stageButtons)
        {
            if (stageButton != button)
            {
                stageButton.interactable = false;
            }
        }

        // Play sound associated with the selected stage
        PlayStageSound(button.name);

        stagePicked = true;

        // Show the start button once a stage is selected
        startButton.gameObject.SetActive(true);

        // Keyboard/gamepad: put the cursor on Proceed, so Enter/A continues to character select.
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(startButton.gameObject);
    }

    /// <summary>Esc / gamepad B (via MenuNavigator): undo the stage pick, exactly like right-click,
    /// and put the cursor back on that stage. Returns false when no stage is picked, so Back is pressed.</summary>
    public bool TryCancel()
    {
        if (!stagePicked)
            return false;

        // While a stage is picked, only that stage button is still interactable.
        Button picked = null;
        foreach (Button button in stageButtons)
            if (button != null && button.interactable) { picked = button; break; }

        ResetStageSelection();

        if (picked != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(picked.gameObject);
        return true;
    }

    public void ResetStageSelection()
    {
        if (!stagePicked) return; // If no stage was picked, don't do anything

        // Reactivate all stage buttons
        foreach (Button button in stageButtons)
        {
            button.interactable = true;
        }

        // Reset stage selection
        stagePicked = false;
        stageNameText.text = "";

        // Hide the start button
        startButton.gameObject.SetActive(false);

        Debug.Log("Stage selection reset!");
    }

    public void HoveringIn(string name)
    {
        if (stagePicked) return;
        stageNameText.text = name;
    }

    public void HoveringOut()
    {
        if (stagePicked) return;
        stageNameText.text = "";
    }

    void PlayStageSound(string name)
    {
        audiomanager.PlaySFX(audiomanager.stageSound, 1f);
    }
}
