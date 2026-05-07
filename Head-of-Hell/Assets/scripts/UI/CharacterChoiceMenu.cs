using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterChoiceMenu : MonoBehaviour
{
    public Button[] characterButtons;

    private int currentPlayer = 1;
    private int requiredPlayers = 2;
    private int picksMade = 0;

    public TextMeshProUGUI p1characterNameText;
    public TextMeshProUGUI p2characterNameText;
    public TextMeshProUGUI p3characterNameText;
    public TextMeshProUGUI p4characterNameText;

    public GameObject p3SelectionRoot;
    public GameObject p4SelectionRoot;

    public Button startButton, randomButton;

    bool picked = false;

    public MainMenuMusic audiomanager;

    public CharacterChoiceScript cscript;

    void Start()
    {
        picked = false;
        RefreshModeUI();

        foreach (Button button in characterButtons)
        {
            button.onClick.AddListener(() => OnCharacterButtonClicked(button));
        }
    }

    void OnEnable()
    {
        RefreshModeUI();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(1))
        {
            DeselectCharacter();
        }
    }

    public void OnCharacterButtonClicked(Button button)
    {
        if (picked)
        {
            return;
        }

        AssignPick(currentPlayer, button);
        picksMade++;

        if (picksMade >= requiredPlayers)
        {
            picked = true;
            cscript.BothPicked(true);

            if (startButton != null)
                startButton.gameObject.SetActive(true);

            if (requiredPlayers == 2)
            {
                foreach (Button butt in characterButtons)
                {
                    if (!IsChosenButton(butt))
                        butt.interactable = false;
                }
            }

            return;
        }

        currentPlayer = picksMade + 1;
    }

    void DeselectCharacter()
    {
        if (picksMade <= 0)
            return;

        if (picked)
        {
            picked = false;
            cscript.BothPicked(false);

            if (startButton != null)
                startButton.gameObject.SetActive(false);
        }

        ClearPick(picksMade);

        foreach (Button button in characterButtons)
            button.interactable = true;

        picksMade--;
        currentPlayer = picksMade + 1;
    }

    public void ResetCharacterSelection()
    {
        RefreshModeUI();

        foreach (Button button in characterButtons)
        {
            button.interactable = true;
            ToggleChild(button, "P1", false);
            ToggleChild(button, "P2", false);
            ToggleChild(button, "P3", false);
            ToggleChild(button, "P4", false);
        }

        currentPlayer = 1;
        picksMade = 0;
        picked = false;

        cscript.BothPicked(false);
        cscript.ClearPlayer1Picked();
        cscript.ClearPlayer2Picked();
        cscript.ClearPlayer3Picked();
        cscript.ClearPlayer4Picked();

        if (p1characterNameText != null) p1characterNameText.text = "";
        if (p2characterNameText != null) p2characterNameText.text = "";
        if (p3characterNameText != null) p3characterNameText.text = "";
        if (p4characterNameText != null) p4characterNameText.text = "";

        PlayerPrefs.DeleteKey("Player1Choice");
        PlayerPrefs.DeleteKey("Player2Choice");
        PlayerPrefs.DeleteKey("Player3Choice");
        PlayerPrefs.DeleteKey("Player4Choice");

        if (startButton != null)
            startButton.gameObject.SetActive(false);
    }

    public void HoveringIn(string name)
    {
        if (picked)
        {
            return;
        }

        SetChoiceLabel(currentPlayer, name);
    }

    void CharacterSound(string name)
    {
        if (name == "Lazy Bigus")
        {
            Debug.Log("rizzz");
            audiomanager.PlaySFX(audiomanager.sleep, 1f);
        }
        if (name == "Steelager")
        {
            audiomanager.PlaySFX(audiomanager.roar, 1f);
        }
        if (name == "Skipler")
        {
            audiomanager.PlaySFX(audiomanager.dash, 1f);
        }
        if (name == "Vander")
        {
            audiomanager.PlaySFX(audiomanager.stab, 1f);
        }
        if (name == "Fin")
        {
            audiomanager.PlaySFX(audiomanager.counter, 1f);
        }
        if (name == "Rager")
        {
            audiomanager.PlaySFX(audiomanager.grab, 1f);
        }
        if (name == "Random")
        {
            audiomanager.PlaySFX(audiomanager.random, 1.5f);
        }
        if (name == "Lithra")
        {
            audiomanager.PlaySFX(audiomanager.bell, 1.5f);
        }
        if (name == "Chiback")
        {
            audiomanager.PlaySFX(audiomanager.fire, 1f);
        }
    }

    public void HoveringOut()
    {
        if (picked)
        {
            return;
        }

        SetChoiceLabel(currentPlayer, "");
    }

    public void ManualCharacterButtonClick(Button button)
    {
        Debug.Log("-0-");
        OnCharacterButtonClicked(button);
    }

    private Button GetRandomCharacterButton()
    {
        List<Button> availableButtons = new List<Button>(characterButtons);
        availableButtons.RemoveAll(button => button.name == "Random");

        int randomIndex = Random.Range(0, availableButtons.Count);
        return availableButtons[randomIndex];
    }

    private void RefreshModeUI()
    {
        requiredPlayers = GameModeSelectionState.PlayerCount;
        EnsureP4SelectionRoot();
        EnsureP4ChoiceMarkers();
        p3characterNameText = ResolveChoiceLabel(p3SelectionRoot, p3characterNameText, "p3Choice");
        p4characterNameText = ResolveChoiceLabel(p4SelectionRoot, p4characterNameText, "p4Choice");

        if (p3SelectionRoot != null)
            p3SelectionRoot.SetActive(requiredPlayers >= 3);

        if (p4SelectionRoot != null)
            p4SelectionRoot.SetActive(requiredPlayers >= 4);

        if (requiredPlayers < 3)
        {
            if (p3characterNameText != null)
                p3characterNameText.text = "";

            PlayerPrefs.DeleteKey("Player3Choice");
        }

        if (requiredPlayers < 4)
        {
            if (p4characterNameText != null)
                p4characterNameText.text = "";

            PlayerPrefs.DeleteKey("Player4Choice");
        }
    }

    private void AssignPick(int playerNumber, Button button)
    {
        SetChoiceLabel(playerNumber, button.name);
        CharacterSound(button.name);
        PlayerPrefs.SetString($"Player{playerNumber}Choice", button.name);

        switch (playerNumber)
        {
            case 1:
                ToggleChild(button, "P1", true);
                cscript.SetPlayer1Picked(button);
                break;
            case 2:
                ToggleChild(button, "P2", true);
                cscript.SetPlayer2Picked(button);
                break;
            case 3:
                ToggleChild(button, "P3", true);
                cscript.SetPlayer3Picked(button);
                break;
            case 4:
                ToggleChild(button, "P4", true);
                cscript.SetPlayer4Picked(button);
                break;
        }
    }

    private void ClearPick(int playerNumber)
    {
        foreach (Button button in characterButtons)
        {
            ToggleChild(button, $"P{playerNumber}", false);
        }

        switch (playerNumber)
        {
            case 1:
                cscript.ClearPlayer1Picked();
                break;
            case 2:
                cscript.ClearPlayer2Picked();
                break;
            case 3:
                cscript.ClearPlayer3Picked();
                break;
            case 4:
                cscript.ClearPlayer4Picked();
                break;
        }

        SetChoiceLabel(playerNumber, "");
        PlayerPrefs.DeleteKey($"Player{playerNumber}Choice");
    }

    private void SetChoiceLabel(int playerNumber, string characterName)
    {
        switch (playerNumber)
        {
            case 1:
                if (p1characterNameText != null) p1characterNameText.text = characterName;
                break;
            case 2:
                if (p2characterNameText != null) p2characterNameText.text = characterName;
                break;
            case 3:
                if (p3characterNameText != null) p3characterNameText.text = characterName;
                break;
            case 4:
                if (p4characterNameText != null) p4characterNameText.text = characterName;
                break;
        }
    }

    private bool IsChosenButton(Button button)
    {
        Transform p1 = button.transform.Find("P1");
        Transform p2 = button.transform.Find("P2");
        Transform p3 = button.transform.Find("P3");
        Transform p4 = button.transform.Find("P4");

        return (p1 != null && p1.gameObject.activeSelf)
            || (p2 != null && p2.gameObject.activeSelf)
            || (p3 != null && p3.gameObject.activeSelf)
            || (p4 != null && p4.gameObject.activeSelf);
    }

    private void ToggleChild(Button button, string childName, bool active)
    {
        Transform child = button.transform.Find(childName);
        if (child != null)
            child.gameObject.SetActive(active);
    }

    private void EnsureP4SelectionRoot()
    {
        if (p4SelectionRoot != null && p4characterNameText != null)
            return;

        if (!GameModeSelectionState.RequiresFourthPlayerSelection || p3SelectionRoot == null)
            return;

        if (p4SelectionRoot == null)
        {
            p4SelectionRoot = Instantiate(p3SelectionRoot, p3SelectionRoot.transform.parent);
            p4SelectionRoot.name = "P4SelectionRoot";

            RectTransform p3Rect = p3SelectionRoot.GetComponent<RectTransform>();
            RectTransform p4Rect = p4SelectionRoot.GetComponent<RectTransform>();
            if (p3Rect != null && p4Rect != null)
                p4Rect.anchoredPosition = p3Rect.anchoredPosition + new Vector2(0f, -70f);
        }

        p4characterNameText = ResolveChoiceLabel(p4SelectionRoot, p4characterNameText, "p4Choice");
    }

    private void EnsureP4ChoiceMarkers()
    {
        if (!GameModeSelectionState.RequiresFourthPlayerSelection)
            return;

        foreach (Button button in characterButtons)
        {
            if (button == null || button.transform.Find("P4") != null)
                continue;

            Transform p3Marker = button.transform.Find("P3");
            if (p3Marker == null)
                continue;

            Transform clone = Instantiate(p3Marker, button.transform);
            clone.name = "P4";
            clone.gameObject.SetActive(false);
        }
    }

    private TextMeshProUGUI ResolveChoiceLabel(GameObject selectionRoot, TextMeshProUGUI current, string preferredObjectName)
    {
        if (selectionRoot == null)
            return current;

        if (!string.IsNullOrEmpty(preferredObjectName))
        {
            Transform preferred = selectionRoot.transform.Find(preferredObjectName);
            if (preferred != null)
            {
                TextMeshProUGUI preferredLabel = preferred.GetComponent<TextMeshProUGUI>();
                if (preferredLabel != null)
                    return preferredLabel;
            }
        }

        if (current != null && !LooksLikePlayerHeader(current.text))
            return current;

        TextMeshProUGUI[] labels = selectionRoot.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (TextMeshProUGUI label in labels)
        {
            if (label != null && !LooksLikePlayerHeader(label.text))
                return label;
        }

        return current;
    }

    private bool LooksLikePlayerHeader(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string normalized = value.Replace("\n", " ").Trim().ToLowerInvariant();
        return normalized == "player 3" || normalized == "player 4";
    }
}
