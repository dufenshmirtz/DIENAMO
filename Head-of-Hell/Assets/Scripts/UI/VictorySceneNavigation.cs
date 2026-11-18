using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class VictoryScreenNavigation : MonoBehaviour
{
    public Button PlayAgainButton;
    public Button ChangeProfilesButton;
    public Button BackToMenuButton;
    public Button SaveReplayButton;

    private bool isVictoryScreenActive = false;

    void OnEnable()
    {
        isVictoryScreenActive = true;
        SetupButtonNavigation();
    }

    void OnDisable()
    {
        isVictoryScreenActive = false;
    }

    private void SetupButtonNavigation()
    {
        EventSystem.current.SetSelectedGameObject(PlayAgainButton.gameObject);

        Navigation navPlayAgain = PlayAgainButton.navigation;
        navPlayAgain.mode = Navigation.Mode.Explicit;
        navPlayAgain.selectOnRight = SaveReplayButton;
        navPlayAgain.selectOnDown = ChangeProfilesButton;
        PlayAgainButton.navigation = navPlayAgain;

        Navigation navSaveReplay = SaveReplayButton.navigation;
        navSaveReplay.mode = Navigation.Mode.Explicit;
        navSaveReplay.selectOnLeft = PlayAgainButton;
        navSaveReplay.selectOnDown = BackToMenuButton;
        SaveReplayButton.navigation = navSaveReplay;

        Navigation navChangeProfiles = ChangeProfilesButton.navigation;
        navChangeProfiles.mode = Navigation.Mode.Explicit;
        navChangeProfiles.selectOnUp = PlayAgainButton;
        navChangeProfiles.selectOnRight = BackToMenuButton;
        ChangeProfilesButton.navigation = navChangeProfiles;

        Navigation navBackToMenu = BackToMenuButton.navigation;
        navBackToMenu.mode = Navigation.Mode.Explicit;
        navBackToMenu.selectOnUp = SaveReplayButton;
        navBackToMenu.selectOnLeft = ChangeProfilesButton;
        BackToMenuButton.navigation = navBackToMenu;
    }

    // Enter / gamepad A is handled by the EventSystem (it clicks the selected button) and MenuNavigator
    // keeps a button selected. The old Update() that ALSO clicked the selected button on Enter was removed,
    // because together with the EventSystem it pressed buttons twice (e.g. Play Again / Save Replay).
}