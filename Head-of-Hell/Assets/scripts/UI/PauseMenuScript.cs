using UnityEngine;
using UnityEngine.UI;

public class PauseMenuScript : MonoBehaviour
{
    public GameObject menu;  // Assign your menu panel in the Inspector
    public GameObject soundMenu;
    public GameObject baseMenu;
    public CharacterManager cChoice;
    public AudioManager sfx;
    public GameManager manager;


    void Start()
    {
        // Ensure the menu is initially closed
        menu.SetActive(false);
        
    }

    void Update()
    {
        // Check if the ESC key is pressed
        if (Input.GetKeyDown(KeyCode.Escape))
        {
        if (manager.trainingMode)
        {
            manager.SoftResetRound(0);
            return;
        }
            ToggleMenu();
        }
    }

    // Method to toggle the menu
    public void ToggleMenu()
    {
        baseMenu.SetActive(true);
        sfx.ButtonSound();
        menu.SetActive(!menu.activeSelf);
        soundMenu.SetActive(false);
        if (!menu.activeSelf)
        {
            manager.EnableGamePlay();
        }
        else
        {
            manager.DisableGamePlay();
        }
    }

    // Method to be called on button click
    public void OnButtonClick()
    {
        ToggleMenu();
    }

    public void Resume()
    {
        manager.EnableGamePlay();
    }
}
