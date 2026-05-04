using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class StartButtonScript : MonoBehaviour
{
    public TMP_Text setting; // Assign in Inspector (for Default ruleset check)

    void Update()
    {
        //print(setting.text);
        // Check for Enter key press (both Return and Keypad Enter)
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            StartGame();
        }
    }

    // Called when the UI Button is clicked (assign in Inspector)
    public void OnStartButtonClicked()
    {
        StartGame();
    }

    private void StartGame()
    {
        // Check if the ruleset is "Default" and apply settings
        if (setting.text == "Default")
        {
            RulesetManager.Instance.SetRulesetNum(-1);
            RulesetSelectionState.SelectDefault();
            CustomRulesetScreenManager.selectedSlot = -1;
            CustomRulesetScreenManager.currentRuleset = null;

            CustomRuleset ruleset = new CustomRuleset();
            ruleset.health = 100;
            ruleset.powerupsEnabled = false;
            ruleset.rounds = 1;
            ruleset.playerSpeed = 4;
            ruleset.quickDisabled = false;
            ruleset.heavyDisabled = false;
            ruleset.blockDisabled = false;
            ruleset.chargeDisabled = false;
            ruleset.specialDisabled = false;
            ruleset.hideHealth = false;
            ruleset.devTools = false;
            ruleset.portals = 0;
            ruleset.chanChan = false;

            // Save to PlayerPrefs
            string json = JsonUtility.ToJson(ruleset);
            PlayerPrefs.SetString("SelectedRuleset", json);
            PlayerPrefs.Save();
        }

        // Load the game scene (replace "GameScene" with your actual scene name)
        SceneManager.LoadScene(1);
    }
}
