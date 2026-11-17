#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Editor-only helper (never included in builds). Applies the agreed menu-navigation setup to the
/// open HoHMainMenu scene, with Undo, and logs every change it makes.
/// Step 1 (pilot): MainMenu, OptionsMenu, LocalOnlineMenu.
/// </summary>
public static class MenuNavigationSetup
{
    static readonly string[] PilotPanels = { "MainMenu", "OptionsMenu", "LocalOnlineMenu" };

    [MenuItem("Tools/DIENAMO/Menu Navigation/Apply Step 1 (Main, Options, Local-Online)")]
    static void ApplyStep1()
    {
        var bg = FindInScene("Canvas/StartingScreenBG");
        var canvas = FindInScene("Canvas");
        if (bg == null || canvas == null) { Debug.LogError("[NavSetup] Canvas/StartingScreenBG not found. Open HoHMainMenu first."); return; }

        Undo.SetCurrentGroupName("Menu Navigation Step 1");
        int group = Undo.GetCurrentGroup();

        // 1) MenuNavigator on the Canvas
        var nav = canvas.GetComponent<MenuNavigator>();
        if (nav == null) { nav = Undo.AddComponent<MenuNavigator>(canvas); Debug.Log("[NavSetup] Added MenuNavigator to Canvas"); }
        Undo.RecordObject(nav, "Configure MenuNavigator");

        foreach (var name in PilotPanels)
        {
            var panel = bg.transform.Find(name);
            if (panel == null) { Debug.LogWarning("[NavSetup] Panel not found: " + name); continue; }
            if (!nav.managedPanels.Contains(panel.gameObject)) { nav.managedPanels.Add(panel.gameObject); Debug.Log("[NavSetup] Managed panel: " + name); }

            // 2) Disable (not delete) the old arrow-key navigation script on this panel
            foreach (var mb in panel.GetComponents<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name == "MainMenuNavigation" && mb.enabled)
                {
                    Undo.RecordObject(mb, "Disable old navigation");
                    mb.enabled = false;
                    Debug.Log("[NavSetup] Disabled MainMenuNavigation on " + name);
                }
            }
        }

        // MainMenu opens on Play
        var main = bg.transform.Find("MainMenu");
        var play = main != null ? main.Find("PlayButton") : null;
        if (main != null && play != null && !nav.panelDefaults.Exists(d => d.panel == main.gameObject))
        {
            nav.panelDefaults.Add(new MenuNavigator.PanelDefault { panel = main.gameObject, firstSelected = play.GetComponent<Selectable>() });
            Debug.Log("[NavSetup] MainMenu default = PlayButton");
        }

        // Move sound = the same MainMenuMusic the old script used
        if (nav.sfx == null)
        {
            var audio = FindInScene("SuperiorAudioManager");
            var music = audio != null ? audio.GetComponent<MainMenuMusic>() : null;
            if (music != null) { nav.sfx = music; Debug.Log("[NavSetup] Sound = SuperiorAudioManager/MainMenuMusic"); }
            else Debug.LogWarning("[NavSetup] MainMenuMusic not found on SuperiorAudioManager; no move sound");
        }
        EditorUtility.SetDirty(nav);

        // 3) TitleButton: mouse only
        var title = main != null ? main.Find("TitleButton") : null;
        var titleSel = title != null ? title.GetComponent<Selectable>() : null;
        if (titleSel != null && titleSel.navigation.mode != Navigation.Mode.None)
        {
            Undo.RecordObject(titleSel, "TitleButton mouse only");
            var n = titleSel.navigation; n.mode = Navigation.Mode.None; titleSel.navigation = n;
            EditorUtility.SetDirty(titleSel);
            Debug.Log("[NavSetup] TitleButton Navigation = None (mouse only)");
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(canvas.scene);
        Selection.activeGameObject = canvas;
        Debug.Log("[NavSetup] Step 1 applied. Review, then save the scene (Ctrl+S). Ctrl+Z undoes it.");
    }

    [MenuItem("Tools/DIENAMO/Menu Navigation/Apply Step 2 (PvP menu)")]
    static void ApplyStep2()
    {
        var bg = FindInScene("Canvas/StartingScreenBG");
        var canvas = FindInScene("Canvas");
        var nav = canvas != null ? canvas.GetComponent<MenuNavigator>() : null;
        if (bg == null || nav == null) { Debug.LogError("[NavSetup] Run Step 1 first (MenuNavigator not found on Canvas)."); return; }

        var panel = bg.transform.Find("PvPMenu");
        if (panel == null) { Debug.LogError("[NavSetup] PvPMenu not found."); return; }

        Undo.RecordObject(nav, "Menu Navigation Step 2");
        if (!nav.managedPanels.Contains(panel.gameObject))
        {
            nav.managedPanels.Add(panel.gameObject);
            Debug.Log("[NavSetup] Managed panel: PvPMenu");
        }
        EditorUtility.SetDirty(nav);
        EditorSceneManager.MarkSceneDirty(canvas.scene);
        Selection.activeGameObject = canvas;
        Debug.Log("[NavSetup] Step 2 applied. Review, then save the scene (Ctrl+S). Ctrl+Z undoes it.");
    }

    [MenuItem("Tools/DIENAMO/Menu Navigation/Apply Step 3 (Game setup, Character select)")]
    static void ApplyStep3()
    {
        var bg = FindInScene("Canvas/StartingScreenBG");
        var canvas = FindInScene("Canvas");
        var nav = canvas != null ? canvas.GetComponent<MenuNavigator>() : null;
        if (bg == null || nav == null) { Debug.LogError("[NavSetup] Run Step 1 first (MenuNavigator not found on Canvas)."); return; }

        var setup = bg.transform.Find("GameSetupMenu");
        var chars = bg.transform.Find("CharacterChoiceMenu");
        var stages = setup != null ? setup.Find("Canvas") : null;
        var defaultBtn = setup != null ? setup.Find("DefaultButton") : null;
        var fin = chars != null ? chars.Find("CharacterButtons/Fin") : null;
        if (setup == null || chars == null || stages == null || defaultBtn == null || fin == null)
        { Debug.LogError("[NavSetup] GameSetupMenu / CharacterChoiceMenu objects not found."); return; }

        Undo.RecordObject(nav, "Menu Navigation Step 3");

        foreach (var p in new[] { setup.gameObject, chars.gameObject })
            if (!nav.managedPanels.Contains(p)) { nav.managedPanels.Add(p); Debug.Log("[NavSetup] Managed panel: " + p.name); }

        if (!nav.panelDefaults.Exists(d => d.panel == setup.gameObject))
        {
            nav.panelDefaults.Add(new MenuNavigator.PanelDefault { panel = setup.gameObject, firstSelected = defaultBtn.GetComponent<Selectable>() });
            Debug.Log("[NavSetup] GameSetupMenu default = DefaultButton");
        }
        if (!nav.panelDefaults.Exists(d => d.panel == chars.gameObject))
        {
            nav.panelDefaults.Add(new MenuNavigator.PanelDefault { panel = chars.gameObject, firstSelected = fin.GetComponent<Selectable>() });
            Debug.Log("[NavSetup] CharacterChoiceMenu default = Fin (same as the old first-arrow auto-select)");
        }

        if (!nav.selectOnFirstInput.Contains(chars.gameObject))
        {
            nav.selectOnFirstInput.Add(chars.gameObject);
            Debug.Log("[NavSetup] CharacterChoiceMenu selects nothing until the first direction press (as before)");
        }

        foreach (var root in new[] { chars.gameObject, stages.gameObject })
            if (!nav.noIndicatorUnder.Contains(root))
            {
                nav.noIndicatorUnder.Add(root);
                Debug.Log("[NavSetup] No new indicator/sound under: " + root.name + " (keeps its own look)");
            }

        EditorUtility.SetDirty(nav);
        EditorSceneManager.MarkSceneDirty(canvas.scene);
        Selection.activeGameObject = canvas;
        Debug.Log("[NavSetup] Step 3 applied. Review, then save the scene (Ctrl+S). Ctrl+Z undoes it.");
    }

    [MenuItem("Tools/DIENAMO/Menu Navigation/Apply Step 4 (Start indicator, Default-Custom to stages)")]
    static void ApplyStep4()
    {
        var bg = FindInScene("Canvas/StartingScreenBG");
        var canvas = FindInScene("Canvas");
        var nav = canvas != null ? canvas.GetComponent<MenuNavigator>() : null;
        if (bg == null || nav == null) { Debug.LogError("[NavSetup] Run Steps 1-3 first."); return; }

        var chars = bg.transform.Find("CharacterChoiceMenu");
        var start = chars != null ? chars.Find("StartButton") : null;
        var setup = bg.transform.Find("GameSetupMenu");
        var stageMgr = setup != null ? setup.GetComponent<StageChoiceManager>() : null;
        var slots = bg.transform.Find("Custom Settings Menu/Slotbuttons");
        if (chars == null || start == null || stageMgr == null || slots == null)
        { Debug.LogError("[NavSetup] CharacterChoiceMenu/StartButton, GameSetupMenu or Custom Settings Menu/Slotbuttons not found."); return; }

        Undo.SetCurrentGroupName("Menu Navigation Step 4");
        int group = Undo.GetCurrentGroup();

        // 1) Character select: new indicator on Start only. Everything else in the panel keeps its look.
        Undo.RecordObject(nav, "Step 4 navigator");
        if (nav.noIndicatorUnder.Remove(chars.gameObject))
            Debug.Log("[NavSetup] CharacterChoiceMenu no longer excluded as a whole");
        foreach (Transform child in chars)
        {
            if (child == start) continue;
            if (child.GetComponentInChildren<Selectable>(true) == null) continue;
            if (!nav.noIndicatorUnder.Contains(child.gameObject))
            {
                nav.noIndicatorUnder.Add(child.gameObject);
                Debug.Log("[NavSetup] No new indicator under: CharacterChoiceMenu/" + child.name + " (keeps its own look)");
            }
        }
        EditorUtility.SetDirty(nav);

        //    Start's old Color Tint highlight off (Highlighted/Selected = Normal), so only the new indicator shows.
        var startSel = start.GetComponent<Selectable>();
        Undo.RecordObject(startSel, "Start old highlight off");
        var cb = startSel.colors;
        Debug.Log("[NavSetup] StartButton old colors: highlighted=" + cb.highlightedColor + " selected=" + cb.selectedColor + " (normal=" + cb.normalColor + ")");
        cb.highlightedColor = cb.normalColor;
        cb.selectedColor = cb.normalColor;
        startSel.colors = cb;
        EditorUtility.SetDirty(startSel);
        Debug.Log("[NavSetup] StartButton highlighted/selected color = normal (old highlight off)");

        // 2) Game setup: Custom Settings slot buttons that choose a ruleset -> cursor to first stage.
        var list = new List<Button>();
        foreach (var b in slots.GetComponentsInChildren<Button>(true))
            for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
                if (b.onClick.GetPersistentMethodName(i) == "SelectSlot") { list.Add(b); break; }
        Undo.RecordObject(stageMgr, "Step 4 stage manager");
        stageMgr.customSlotButtons = list.ToArray();
        EditorUtility.SetDirty(stageMgr);
        Debug.Log("[NavSetup] StageChoiceManager.customSlotButtons = " + list.Count + " slot buttons");

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(canvas.scene);
        Debug.Log("[NavSetup] Step 4 applied. Review, then save the scene (Ctrl+S). Ctrl+Z undoes it.");
    }

    [MenuItem("Tools/DIENAMO/Menu Navigation/Apply Step 5 (Character select Back indicator)")]
    static void ApplyStep5()
    {
        var bg = FindInScene("Canvas/StartingScreenBG");
        var canvas = FindInScene("Canvas");
        var nav = canvas != null ? canvas.GetComponent<MenuNavigator>() : null;
        var back = bg != null ? bg.transform.Find("CharacterChoiceMenu/BackButton") : null;
        if (nav == null || back == null) { Debug.LogError("[NavSetup] MenuNavigator or CharacterChoiceMenu/BackButton not found."); return; }

        Undo.SetCurrentGroupName("Menu Navigation Step 5");
        int group = Undo.GetCurrentGroup();

        Undo.RecordObject(nav, "Step 5 navigator");
        if (nav.noIndicatorUnder.Remove(back.gameObject))
            Debug.Log("[NavSetup] CharacterChoiceMenu/BackButton now gets the new indicator");
        EditorUtility.SetDirty(nav);

        // Old Color Tint highlight off (Highlighted/Selected = Normal), so only the new indicator shows.
        var sel = back.GetComponent<Selectable>();
        Undo.RecordObject(sel, "Back old highlight off");
        var cb = sel.colors;
        Debug.Log("[NavSetup] Character select BackButton old colors: highlighted=" + cb.highlightedColor + " selected=" + cb.selectedColor + " (normal=" + cb.normalColor + ")");
        cb.highlightedColor = cb.normalColor;
        cb.selectedColor = cb.normalColor;
        sel.colors = cb;
        EditorUtility.SetDirty(sel);
        Debug.Log("[NavSetup] Character select BackButton highlighted/selected color = normal (old highlight off)");

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(canvas.scene);
        Debug.Log("[NavSetup] Step 5 applied. Review, then save the scene (Ctrl+S). Ctrl+Z undoes it.");
    }

    static GameObject FindInScene(string path)
    {
        var parts = path.Split('/');
        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name != parts[0]) continue;
            if (parts.Length == 1) return root;
            var t = root.transform.Find(string.Join("/", parts, 1, parts.Length - 1));
            if (t != null) return t.gameObject;
        }
        return null;
    }
}
#endif
