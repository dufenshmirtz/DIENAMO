using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CharacterChoiceScript : MonoBehaviour
{
    private Button lastHighlightedButton = null;

    public Button[] buttons;
    public Button startButton;
    public MainMenuMusic sfx;
    public CharacterChoiceMenu characterChoiceMenu;

    private bool notSelected = true;
    public bool bothpicked = false;

    private Button p1PickedButton = null;
    private Button p2PickedButton = null;
    private Button p3PickedButton = null;
    private Button p4PickedButton = null;

    void Start()
    {
        notSelected = true;

        foreach (Button btn in buttons)
        {
            HideHoverBorder(btn);
            HideP1Border(btn);
            HideP2Border(btn);
            HideP3Border(btn);
            HideP4Border(btn);

            EventTrigger trigger = btn.gameObject.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = btn.gameObject.AddComponent<EventTrigger>();
            }

            trigger.triggers.Clear();

            EventTrigger.Entry selectEntry = new EventTrigger.Entry();
            selectEntry.eventID = EventTriggerType.Select;
            selectEntry.callback.AddListener((eventData) => { OnButtonHighlighted(btn); });
            trigger.triggers.Add(selectEntry);

            EventTrigger.Entry deselectEntry = new EventTrigger.Entry();
            deselectEntry.eventID = EventTriggerType.Deselect;
            deselectEntry.callback.AddListener((eventData) => { OnButtonUnhighlighted(btn); });
            trigger.triggers.Add(deselectEntry);

            EventTrigger.Entry enterEntry = new EventTrigger.Entry();
            enterEntry.eventID = EventTriggerType.PointerEnter;
            enterEntry.callback.AddListener((eventData) => { OnButtonPointerEnter(btn); });
            trigger.triggers.Add(enterEntry);

            EventTrigger.Entry exitEntry = new EventTrigger.Entry();
            exitEntry.eventID = EventTriggerType.PointerExit;
            exitEntry.callback.AddListener((eventData) => { OnButtonPointerExit(btn); });
            trigger.triggers.Add(exitEntry);
        }
    }

    void Update()
    {
        if ((Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.UpArrow) ||
             Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.LeftArrow)) && notSelected)
        {
            AutoSelectButton();
        }

        if (bothpicked && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
        {
            TriggerStartButton();
        }
    }

    void AutoSelectButton()
    {
        if (buttons.Length > 0)
        {
            sfx.ButtonSound();
            EventSystem.current.SetSelectedGameObject(buttons[0].gameObject);
            buttons[0].Select();
            notSelected = false;
            characterChoiceMenu.HoveringIn(buttons[0].name);
        }
    }

    public void OnButtonHighlighted(Button button)
    {
        if (!bothpicked)
        {
            sfx.ButtonSound();
            characterChoiceMenu.HoveringIn(button.name);
            ShowHoverBorder(button);
            lastHighlightedButton = button;
        }
    }

    public void OnButtonUnhighlighted(Button button)
    {
        HideHoverBorder(button);

        if (lastHighlightedButton == button)
        {
            lastHighlightedButton = null;
        }
    }

    public void OnButtonPointerEnter(Button button)
    {
        if (!bothpicked)
        {
            ShowHoverBorder(button);
            characterChoiceMenu.HoveringIn(button.name);
        }
    }

    public void OnButtonPointerExit(Button button)
    {
        if (EventSystem.current != null &&
            EventSystem.current.currentSelectedGameObject == button.gameObject)
        {
            return;
        }

        HideHoverBorder(button);
    }

    public void BothPicked(bool didThey)
    {
        bothpicked = didThey;
    }

    void TriggerStartButton()
    {
        Button targetStartButton = startButton;
        if (targetStartButton == null && characterChoiceMenu != null)
            targetStartButton = characterChoiceMenu.startButton;

        if (targetStartButton != null)
            targetStartButton.onClick.Invoke();
    }

    public void SetPlayer1Picked(Button button)
    {
        if (p1PickedButton != null)
        {
            HideP1Border(p1PickedButton);
        }

        p1PickedButton = button;

        if (p1PickedButton != null)
        {
            ShowP1Border(p1PickedButton);
        }
    }

    public void SetPlayer2Picked(Button button)
    {
        if (p2PickedButton != null)
        {
            HideP2Border(p2PickedButton);
        }

        p2PickedButton = button;

        if (p2PickedButton != null)
        {
            ShowP2Border(p2PickedButton);
        }
    }

    public void SetPlayer3Picked(Button button)
    {
        if (p3PickedButton != null)
        {
            HideP3Border(p3PickedButton);
        }

        p3PickedButton = button;

        if (p3PickedButton != null)
        {
            ShowP3Border(p3PickedButton);
        }
    }

    public void SetPlayer4Picked(Button button)
    {
        if (p4PickedButton != null)
        {
            HideP4Border(p4PickedButton);
        }

        p4PickedButton = button;

        if (p4PickedButton != null)
        {
            ShowP4Border(p4PickedButton);
        }
    }

    public void ClearPlayer1Picked()
    {
        if (p1PickedButton != null)
        {
            HideP1Border(p1PickedButton);
            p1PickedButton = null;
        }
    }

    public void ClearPlayer2Picked()
    {
        if (p2PickedButton != null)
        {
            HideP2Border(p2PickedButton);
            p2PickedButton = null;
        }
    }

    public void ClearPlayer3Picked()
    {
        if (p3PickedButton != null)
        {
            HideP3Border(p3PickedButton);
            p3PickedButton = null;
        }
    }

    public void ClearPlayer4Picked()
    {
        if (p4PickedButton != null)
        {
            HideP4Border(p4PickedButton);
            p4PickedButton = null;
        }
    }

    void ShowHoverBorder(Button button)
    {
        Transform t = FindChildByExactOrTrimmedName(button, "HoverBorder");
        if (t != null) t.gameObject.SetActive(true);
    }

    void HideHoverBorder(Button button)
    {
        Transform t = FindChildByExactOrTrimmedName(button, "HoverBorder");
        if (t != null) t.gameObject.SetActive(false);
    }

    void ShowP1Border(Button button)
    {
        Transform t = FindChildByExactOrTrimmedName(button, "P1Border");
        if (t != null) t.gameObject.SetActive(true);
    }

    void HideP1Border(Button button)
    {
        Transform t = FindChildByExactOrTrimmedName(button, "P1Border");
        if (t != null) t.gameObject.SetActive(false);
    }

    void ShowP2Border(Button button)
    {
        Transform t = FindChildByExactOrTrimmedName(button, "P2Border");
        if (t != null) t.gameObject.SetActive(true);
    }

    void HideP2Border(Button button)
    {
        Transform t = FindChildByExactOrTrimmedName(button, "P2Border");
        if (t != null) t.gameObject.SetActive(false);
    }

    void ShowP3Border(Button button)
    {
        Transform t = FindChildByExactOrTrimmedName(button, "P3Border");
        if (t != null) t.gameObject.SetActive(true);
    }

    void HideP3Border(Button button)
    {
        Transform t = FindChildByExactOrTrimmedName(button, "P3Border");
        if (t != null) t.gameObject.SetActive(false);
    }

    void ShowP4Border(Button button)
    {
        EnsureP4Border(button);
        Transform t = FindChildByExactOrTrimmedName(button, "P4Border");
        if (t != null) t.gameObject.SetActive(true);
    }

    void HideP4Border(Button button)
    {
        Transform t = FindChildByExactOrTrimmedName(button, "P4Border");
        if (t != null) t.gameObject.SetActive(false);
    }

    Transform FindChildByExactOrTrimmedName(Button button, string childName)
    {
        if (button == null)
            return null;

        Transform direct = button.transform.Find(childName);
        if (direct != null)
            return direct;

        Transform parent = button.transform;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child != null && child.name.Trim() == childName)
                return child;
        }

        return null;
    }

    private void EnsureP4Border(Button button)
    {
        if (button == null || FindChildByExactOrTrimmedName(button, "P4Border") != null)
            return;

        Transform p3Border = FindChildByExactOrTrimmedName(button, "P3Border");
        if (p3Border == null)
            return;

        Transform clone = Instantiate(p3Border, button.transform);
        clone.name = "P4Border";
        clone.gameObject.SetActive(false);
    }
}
