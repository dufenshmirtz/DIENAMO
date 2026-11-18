using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PauseMenuScript : MonoBehaviour
{
    public GameObject menu;  // Assign your menu panel in the Inspector
    public GameObject soundMenu;
    public GameObject baseMenu;
    public CharacterManager cChoice;
    public AudioManager sfx;
    public GameManager manager;

    // Menu visibility as of the previous frame. Used so that the frame in which a menu button (or
    // MenuNavigator's Esc = Resume) closes the menu is not mistaken for "menu closed, Esc = open it".
    private bool wasOpen;


    void Start()
    {
        // Ensure the menu is initially closed
        menu.SetActive(false);
        
    }

    void Update()
    {
        bool open = menu.activeSelf;

        // Opened by something other than ToggleMenu (e.g. clicking the pause icon with the mouse,
        // which only does SetActive): freeze the fighters too, like Esc always did.
        if (open && !wasOpen)
            manager.DisableGamePlay();

        bool esc = Input.GetKeyDown(KeyCode.Escape);
        bool start = Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame;

        if (esc && manager.trainingMode)
        {
            manager.SoftResetRound(0);
            wasOpen = menu.activeSelf;
            return;
        }

        // Victory screen is up: Esc / Start must not open the pause menu on top of it.
        bool victoryShown = manager.victoryScreenNavigation != null && manager.victoryScreenNavigation.activeInHierarchy;

        if (!wasOpen && !open && !victoryShown)
        {
            // Closed: Esc or gamepad Start opens the pause menu.
            if (esc || start) ToggleMenu();
        }
        else if (wasOpen && open && start)
        {
            // Open: gamepad Start closes it again. Esc / gamepad B are handled by MenuNavigator
            // (back one level: Sound Settings -> pause list -> Resume).
            ToggleMenu();
        }

        wasOpen = menu.activeSelf;
        if (wasOpen) MenuInputGuard.MarkOpen();
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
