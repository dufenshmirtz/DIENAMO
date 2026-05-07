using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PvPModeSelectionMenu : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject localOnlineMenu;
    [SerializeField] private GameObject pvpModeSelectionMenu;
    [SerializeField] private GameObject gameSetupMenu;
    [SerializeField] private Button button1v1;
    [SerializeField] private Button button1v1v1;

    [SerializeField] private Button button2v2;

    private void Awake()
    {
        Ensure2v2Button();
    }

    public void OpenFromPvPButton()
    {
        PvESelectionState.SelectPvPMode();
        GameModeSelectionState.SelectPvP1v1();

        if (localOnlineMenu != null)
            localOnlineMenu.SetActive(false);

        if (pvpModeSelectionMenu != null)
            pvpModeSelectionMenu.SetActive(true);
    }

    public void Select1v1()
    {
        PvESelectionState.SelectPvPMode();
        GameModeSelectionState.SelectPvP1v1();
        OpenGameSetup();
    }

    public void Select1v1v1()
    {
        PvESelectionState.SelectPvPMode();
        GameModeSelectionState.SelectPvP1v1v1();
        OpenGameSetup();
    }

    public void Select2v2()
    {
        PvESelectionState.SelectPvPMode();
        GameModeSelectionState.SelectPvP2v2();
        OpenGameSetup();
    }

    public void Back()
    {
        GameModeSelectionState.SelectPvP1v1();

        if (pvpModeSelectionMenu != null)
            pvpModeSelectionMenu.SetActive(false);

        if (localOnlineMenu != null)
            localOnlineMenu.SetActive(true);
    }

    private void OpenGameSetup()
    {
        if (pvpModeSelectionMenu != null)
            pvpModeSelectionMenu.SetActive(false);

        if (gameSetupMenu != null)
            gameSetupMenu.SetActive(true);
    }

    private void Ensure2v2Button()
    {
        if (button2v2 != null)
            return;

        if (button1v1v1 == null)
            button1v1v1 = FindButtonByName("Button1v1v1");

        if (button1v1 == null)
            button1v1 = FindButtonByName("Button1v1");

        if (button1v1v1 == null)
            return;

        Transform existing = button1v1v1.transform.parent.Find("Button2v2");
        if (existing != null)
        {
            button2v2 = existing.GetComponent<Button>();
            return;
        }

        GameObject clone = Instantiate(button1v1v1.gameObject, button1v1v1.transform.parent);
        clone.name = "Button2v2";
        button2v2 = clone.GetComponent<Button>();

        RectTransform cloneRect = clone.GetComponent<RectTransform>();
        RectTransform sourceRect = button1v1v1.GetComponent<RectTransform>();
        RectTransform anchorRect = button1v1 != null ? button1v1.GetComponent<RectTransform>() : null;
        if (cloneRect != null && sourceRect != null)
        {
            Vector2 anchoredPos = sourceRect.anchoredPosition;
            if (anchorRect != null)
            {
                Vector2 spacing = sourceRect.anchoredPosition - anchorRect.anchoredPosition;
                anchoredPos = sourceRect.anchoredPosition + spacing;
            }

            cloneRect.anchoredPosition = anchoredPos;
        }

        TMP_Text label = clone.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.text = "2v2";

        if (button2v2 != null)
        {
            button2v2.onClick.RemoveAllListeners();
            button2v2.onClick.AddListener(Select2v2);
        }
    }

    private Button FindButtonByName(string objectName)
    {
        Transform root = pvpModeSelectionMenu != null ? pvpModeSelectionMenu.transform : transform;
        Transform found = root.Find(objectName);
        return found != null ? found.GetComponent<Button>() : null;
    }
}
