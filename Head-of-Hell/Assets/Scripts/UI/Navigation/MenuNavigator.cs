using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// One component on the menu Canvas. Unity's EventSystem does the actual moving (keyboard + gamepad);
/// this component only makes sure there is always something sensible selected in the menu panels
/// listed in <see cref="managedPanels"/>:
///  - when a managed panel opens (or the selection gets lost, e.g. mouse click on empty space),
///    it selects the button you were last on in that panel, or the panel's first button;
///  - Cancel (Esc / gamepad B) first lets the panel undo something (IMenuCancelHandler, e.g. last pick);
///    otherwise it presses the open panel's own Back button (NoButton first, then BackButton),
///    so going back always uses the logic already wired on that button;
///  - it adds a SelectionIndicator to every Selectable in the managed panels;
///  - it plays the menu button sound when the selection is moved with keyboard / gamepad.
/// Panels NOT in the list are left completely alone.
/// </summary>
public class MenuNavigator : MonoBehaviour
{
    [System.Serializable]
    public class PanelDefault
    {
        public GameObject panel;
        public Selectable firstSelected;
    }

    [Header("Panels handled by this navigator")]
    public List<GameObject> managedPanels = new List<GameObject>();

    [Tooltip("Optional: which button a panel selects when it opens for the first time. " +
             "Panels not listed here use their first navigable button.")]
    public List<PanelDefault> panelDefaults = new List<PanelDefault>();

    [Tooltip("Panels that select nothing until the player first presses a direction or confirm " +
             "(keeps the panel looking exactly as before until then, like the old character select).")]
    public List<GameObject> selectOnFirstInput = new List<GameObject>();

    [Tooltip("Selectables under these objects get no SelectionIndicator and no move sound: " +
             "their own scripts already handle how they look and sound when selected.")]
    public List<GameObject> noIndicatorUnder = new List<GameObject>();

    [Tooltip("Names of the buttons Cancel (Esc / gamepad B) presses, in priority order.")]
    public string[] backButtonNames = { "NoButton", "BackButton" };

    [Header("Sound")]
    [Tooltip("Same MainMenuMusic the old menu navigation script used. Leave empty for no move sound.")]
    public MainMenuMusic sfx;
    [Tooltip("In gameplay scenes: the AudioManager whose ButtonSound() the old pause menu navigation used.")]
    public AudioManager gameplaySfx;

    [Header("Indicator look (shared by every SelectionIndicator)")]
    public Color glowColor = new Color(0.85f, 0.30f, 0.42f, 0.20f);
    public Color underlineColor = new Color(1.00f, 0.62f, 0.72f, 0.90f);
    [Range(1f, 1.1f)] public float selectedScale = 1.035f;
    [Min(0.01f)] public float fadeTime = 0.12f;
    [Tooltip("How far the glow extends outside the button (x = left/right, y = top/bottom).")]
    public Vector2 glowPadding = new Vector2(10f, 6f);
    [Tooltip("Underline width as a fraction of the button width.")]
    [Range(0.1f, 1f)] public float underlineWidth = 0.7f;
    public float underlineHeight = 3f;

    // Frame in which a SelectionIndicator selected something because the mouse hovered it (no sound for that).
    internal static int lastPointerSelectFrame = -10;
    // Frame of the last direction press (arrows / D-pad / stick): the move sound only plays for real moves,
    // not when a script moves the cursor (e.g. to Start or Proceed after a pick).
    int lastMoveInputFrame = -10;
    // Frame in which a managed panel became the open panel: Cancel is ignored in that frame and the next,
    // so the same Esc press that OPENED a menu (e.g. the pause menu) doesn't immediately close it again.
    int lastPanelOpenedFrame = -10;

    readonly Dictionary<GameObject, GameObject> lastSelectedInPanel = new Dictionary<GameObject, GameObject>();
    readonly List<GameObject> openOrder = new List<GameObject>();   // managed panels, most recently opened last
    readonly HashSet<GameObject> activeLastFrame = new HashSet<GameObject>();
    GameObject previousSelection;
    InputSystemUIInputModule uiModule;

    void Start()
    {
        foreach (var panel in managedPanels)
        {
            if (panel == null) continue;
            foreach (var s in panel.GetComponentsInChildren<Selectable>(true))
            {
                if (IsUnderAny(s.transform, noIndicatorUnder)) continue;
                var ind = s.GetComponent<SelectionIndicator>();
                if (ind == null) ind = s.gameObject.AddComponent<SelectionIndicator>();
                ind.Init(this);
            }
        }
    }

    void Update()
    {
        var es = EventSystem.current;
        if (es == null) return;

        TrackOpenPanels();
        if (MoveInputThisFrame(es)) lastMoveInputFrame = Time.frameCount;

        GameObject current = es.currentSelectedGameObject;
        GameObject panel = CurrentPanel();

        // Remember where we are in each panel, so coming back restores it.
        if (IsUsable(current))
        {
            var p = PanelOf(current);
            if (p != null) lastSelectedInPanel[p] = current;
        }

        // Move sound: selection changed between two usable buttons, not because of the mouse,
        // and only for elements with our indicator (the others play their own sounds).
        if (current != previousSelection && IsUsable(current) && previousSelection != null &&
            lastPointerSelectFrame < Time.frameCount - 1 && lastMoveInputFrame >= Time.frameCount - 1 &&
            current.GetComponent<SelectionIndicator>() != null)
        {
            if (sfx != null) sfx.ButtonSound();
            else if (gameplaySfx != null) gameplaySfx.ButtonSound();
        }
        previousSelection = current;

        if (panel == null || IsTyping(current)) return;
        MenuInputGuard.MarkOpen();

        // Cancel (Esc / gamepad B) -> let the panel undo something first, otherwise press its own back button.
        if (CancelPressed(es) && Time.frameCount - lastPanelOpenedFrame > 1 && !IsRebinding())
        {
            foreach (var handler in panel.GetComponentsInChildren<IMenuCancelHandler>(false))
                if (handler.TryCancel()) return;

            var back = FindBackButton(panel);
            if (back != null) { back.onClick.Invoke(); return; }
        }
    }

    // Selecting is done in LateUpdate, i.e. AFTER the EventSystem has processed this frame's input.
    // In a "select on first input" panel the first selection happens when that first key/button is
    // RELEASED: a press lasts several frames, so selecting on the press would let the EventSystem
    // immediately move one step further while the key is still held.
    void LateUpdate()
    {
        var es = EventSystem.current;
        if (es == null) return;

        GameObject panel = CurrentPanel();
        GameObject current = es.currentSelectedGameObject;
        if (panel == null || IsTyping(current)) return;

        // Nothing usable selected in the open panel -> select something.
        if ((!IsUsable(current) || PanelOf(current) == null) &&
            (!selectOnFirstInput.Contains(panel) || NavigationReleasedThisFrame(es)))
        {
            var target = PickSelection(panel);
            if (target != null)
            {
                es.SetSelectedGameObject(target);
                previousSelection = target;   // selecting on our own is not a "move": no sound
            }
        }
    }

    // ---------- panels ----------

    void TrackOpenPanels()
    {
        foreach (var p in managedPanels)
        {
            if (p == null) continue;
            bool active = p.activeInHierarchy;
            if (active && !activeLastFrame.Contains(p))
            {
                openOrder.Remove(p);
                openOrder.Add(p);
                lastPanelOpenedFrame = Time.frameCount;
            }
            if (active) activeLastFrame.Add(p); else activeLastFrame.Remove(p);
        }
    }

    GameObject CurrentPanel()
    {
        for (int i = openOrder.Count - 1; i >= 0; i--)
            if (openOrder[i] != null && openOrder[i].activeInHierarchy) return openOrder[i];
        return null;
    }

    GameObject PanelOf(GameObject go)
    {
        if (go == null) return null;
        for (var t = go.transform; t != null; t = t.parent)
            if (managedPanels.Contains(t.gameObject)) return t.gameObject;
        return null;
    }

    GameObject PickSelection(GameObject panel)
    {
        if (lastSelectedInPanel.TryGetValue(panel, out var last) && IsUsable(last) && IsNavigable(last))
            return last;

        foreach (var d in panelDefaults)
            if (d != null && d.panel == panel && d.firstSelected != null && IsUsable(d.firstSelected.gameObject))
                return d.firstSelected.gameObject;

        // First choice: the first navigable button that is not a Back/No button; otherwise any navigable one.
        GameObject fallback = null;
        foreach (var s in panel.GetComponentsInChildren<Selectable>(false))
        {
            if (!IsUsable(s.gameObject) || !IsNavigable(s.gameObject)) continue;
            if (System.Array.IndexOf(backButtonNames, s.name) < 0) return s.gameObject;
            if (fallback == null) fallback = s.gameObject;
        }
        return fallback;
    }

    Button FindBackButton(GameObject panel)
    {
        var buttons = panel.GetComponentsInChildren<Button>(false);
        foreach (var name in backButtonNames)
            foreach (var b in buttons)
                if (b.name == name && b.IsInteractable()) return b;
        return null;
    }

    // ---------- helpers ----------

    static bool IsUsable(GameObject go)
    {
        if (go == null || !go.activeInHierarchy) return false;
        var s = go.GetComponent<Selectable>();
        return s != null && s.enabled && s.IsInteractable();
    }

    static bool IsNavigable(GameObject go)
    {
        var s = go.GetComponent<Selectable>();
        return s != null && s.navigation.mode != Navigation.Mode.None;
    }

    // While a key is being rebound (Controls menu), Esc belongs to RebindListener (it cancels the rebind).
    static bool IsRebinding()
    {
        foreach (var r in FindObjectsOfType<RebindListener>())
            if (r != null && r.IsListening) return true;
        return false;
    }

    static bool IsUnderAny(Transform t, List<GameObject> roots)
    {
        for (; t != null; t = t.parent)
            if (roots.Contains(t.gameObject)) return true;
        return false;
    }

    bool NavigationReleasedThisFrame(EventSystem es)
    {
        if (uiModule == null) uiModule = es.currentInputModule as InputSystemUIInputModule;
        // Directions only: the Enter/A that opened this panel is usually released after it opened,
        // and must not count as the "first input" here.
        var move = uiModule != null && uiModule.move != null ? uiModule.move.action : null;
        if (move != null && move.WasReleasedThisFrame())
            return true;
        var kb = Keyboard.current;
        if (kb != null && (kb.upArrowKey.wasReleasedThisFrame || kb.downArrowKey.wasReleasedThisFrame ||
                           kb.leftArrowKey.wasReleasedThisFrame || kb.rightArrowKey.wasReleasedThisFrame))
            return true;
        var pad = Gamepad.current;
        return pad != null && (pad.dpad.up.wasReleasedThisFrame || pad.dpad.down.wasReleasedThisFrame ||
                               pad.dpad.left.wasReleasedThisFrame || pad.dpad.right.wasReleasedThisFrame);
    }

    bool MoveInputThisFrame(EventSystem es)
    {
        if (uiModule == null) uiModule = es.currentInputModule as InputSystemUIInputModule;
        var move = uiModule != null && uiModule.move != null ? uiModule.move.action : null;
        if (move != null && move.WasPressedThisFrame())
            return true;
        var kb = Keyboard.current;
        if (kb != null && (kb.upArrowKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame ||
                           kb.leftArrowKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame))
            return true;
        var pad = Gamepad.current;
        return pad != null && (pad.dpad.up.wasPressedThisFrame || pad.dpad.down.wasPressedThisFrame ||
                               pad.dpad.left.wasPressedThisFrame || pad.dpad.right.wasPressedThisFrame);
    }

    static bool IsTyping(GameObject go)
    {
        if (go == null) return false;
        var tmp = go.GetComponent<TMP_InputField>();
        if (tmp != null && tmp.isFocused) return true;
        var legacy = go.GetComponent<InputField>();
        return legacy != null && legacy.isFocused;
    }

    bool CancelPressed(EventSystem es)
    {
        if (uiModule == null) uiModule = es.currentInputModule as InputSystemUIInputModule;
        var action = uiModule != null && uiModule.cancel != null ? uiModule.cancel.action : null;
        if (action != null) return action.WasPressedThisFrame();

        // Fallback if no Input System UI module is found.
        bool esc = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        bool padB = Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame;
        return esc || padB;
    }
}
