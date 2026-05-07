using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    private void Start()
    {
        // Do not auto-add PvP listeners here.
        // PvP button flow is controlled from the Inspector via PvPModeSelectionMenu.
    }

    public void SelectPvPMode()
    {
        // Keep only PvP/PvE state here if this method is still used somewhere.
        // Do not force PvP_1v1 here.
        PvESelectionState.SelectPvPMode();
    }

    public void PlayGame()
    {
        Debug.Log("[Before Load Gameplay] CurrentMode = " + GameModeSelectionState.CurrentMode);
        Debug.Log("[Before Load Gameplay] PlayerCount = " + GameModeSelectionState.PlayerCount);
        Debug.Log("[Before Load Gameplay] RequiresThirdPlayerSelection = " + GameModeSelectionState.RequiresThirdPlayerSelection);

        SceneManager.LoadScene(1);
    }

    public void OpenTutorial()
    {
        SceneManager.LoadScene("TutorialScene");
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}