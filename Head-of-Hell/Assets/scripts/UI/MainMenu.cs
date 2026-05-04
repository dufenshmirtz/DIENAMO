using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    private void Start()
    {
        Button pvpButton = GameObject.Find("PvPButton")?.GetComponent<Button>();
        if (pvpButton != null)
        {
            pvpButton.onClick.AddListener(SelectPvPMode);
        }
    }

    public void SelectPvPMode()
    {
        PvESelectionState.SelectPvPMode();
    }

    public void PlayGame()
    {
        if (!PvESelectionState.IsPvE)
        {
            PvESelectionState.SelectPvPMode();
        }

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
