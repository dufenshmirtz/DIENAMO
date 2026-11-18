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

    [MenuItem("Tools/DIENAMO/Menu Navigation/Apply Step 6 (Pause menu in GamePlayScene + TutorialScene)")]
    static void ApplyStep6()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string back = EditorSceneManager.GetActiveScene().path;

        foreach (var scenePath in new[] { "Assets/Scenes/GamePlayScene.unity", "Assets/Scenes/TutorialScene.unity" })
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Debug.Log("[NavSetup] ---- " + scenePath);

            var canvas = FindInScene("Canvas");
            var menuImage = FindInScene("Canvas/MenuImage");
            var pauseList = FindInScene("Canvas/MenuImage/PauseMenu");
            var sound = FindInScene("Canvas/MenuImage/SoundSettings");
            var resume = FindInScene("Canvas/MenuImage/PauseMenu/ResumeButton");
            var music = FindInScene("Canvas/MenuImage/SoundSettings/MusicSlider");
            var pauseIcon = FindInScene("Canvas/PauseButton");
            var audioGo = FindInScene("AudioManager");
            if (canvas == null || menuImage == null || pauseList == null || sound == null || resume == null || music == null)
            { Debug.LogError("[NavSetup] Pause menu objects not found in " + scenePath + " - nothing changed there."); continue; }

            // 1) MenuNavigator on the Canvas, handling the pause list and Sound Settings
            var nav = canvas.GetComponent<MenuNavigator>();
            if (nav == null) { nav = canvas.AddComponent<MenuNavigator>(); Debug.Log("[NavSetup] Added MenuNavigator to Canvas"); }
            nav.managedPanels = new List<GameObject> { pauseList, sound };
            nav.panelDefaults = new List<MenuNavigator.PanelDefault>
            {
                new MenuNavigator.PanelDefault { panel = pauseList, firstSelected = resume.GetComponent<Selectable>() },
                new MenuNavigator.PanelDefault { panel = sound, firstSelected = music.GetComponent<Selectable>() },
            };
            nav.backButtonNames = new[] { "Back", "ResumeButton" };   // Esc/B: Sound Settings -> list, list -> Resume
            nav.gameplaySfx = audioGo != null ? audioGo.GetComponent<AudioManager>() : null;
            EditorUtility.SetDirty(nav);
            Debug.Log("[NavSetup] Managed: PauseMenu (default ResumeButton), SoundSettings (default MusicSlider); Esc/B = Back / Resume; sound = " +
                      (nav.gameplaySfx != null ? "AudioManager" : "none (AudioManager not found)"));

            // 2) Disable (not delete) the old arrow-key navigation on MenuImage
            foreach (var mb in menuImage.GetComponents<MonoBehaviour>())
                if (mb != null && mb.GetType().Name == "MenuNavigation" && mb.enabled)
                { mb.enabled = false; EditorUtility.SetDirty(mb); Debug.Log("[NavSetup] Disabled MenuNavigation on MenuImage"); }

            // 3) Pause icon: mouse only (keyboard/gamepad cursor never lands on it)
            var iconSel = pauseIcon != null ? pauseIcon.GetComponent<Selectable>() : null;
            if (iconSel != null && iconSel.navigation.mode != Navigation.Mode.None)
            {
                var n = iconSel.navigation; n.mode = Navigation.Mode.None; iconSel.navigation = n;
                EditorUtility.SetDirty(iconSel);
                Debug.Log("[NavSetup] PauseButton (pause icon) Navigation = None (mouse only)");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[NavSetup] Saved " + scenePath);
        }

        if (!string.IsNullOrEmpty(back)) EditorSceneManager.OpenScene(back, OpenSceneMode.Single);
        Debug.Log("[NavSetup] Step 6 applied to both scenes (already saved). To undo: revert those two scene files in GitHub Desktop.");
    }

    [MenuItem("Tools/DIENAMO/Menu Navigation/Apply Step 7 (Pause menu button links)")]
    static void ApplyStep7()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string back = EditorSceneManager.GetActiveScene().path;

        foreach (var scenePath in new[] { "Assets/Scenes/GamePlayScene.unity", "Assets/Scenes/TutorialScene.unity" })
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Debug.Log("[NavSetup] ---- " + scenePath);

            Selectable S(string path) { var go = FindInScene(path); return go != null ? go.GetComponent<Selectable>() : null; }
            var restart = S("Canvas/MenuImage/PauseMenu/RestartButton");
            var resume = S("Canvas/MenuImage/PauseMenu/ResumeButton");
            var sound = S("Canvas/MenuImage/PauseMenu/SoundSettings");
            var mainMenu = S("Canvas/MenuImage/PauseMenu/MainMenuButton");
            var music = S("Canvas/MenuImage/SoundSettings/MusicSlider") as Slider;
            var sfx = S("Canvas/MenuImage/SoundSettings/SFXSlider") as Slider;
            var backBtn = S("Canvas/MenuImage/SoundSettings/Back");
            if (restart == null || resume == null || sound == null || mainMenu == null || music == null || sfx == null || backBtn == null)
            { Debug.LogError("[NavSetup] Pause menu objects not found in " + scenePath + " - nothing changed there."); continue; }

            // Pause list, top to bottom on screen: Restart, Resume, Sound, Main Menu (wraps around).
            Link(restart, up: mainMenu, down: resume);
            Link(resume, up: restart, down: sound);
            Link(sound, up: resume, down: mainMenu);
            Link(mainMenu, up: sound, down: restart);
            Debug.Log("[NavSetup] Pause list links: Restart <-> Resume <-> Sound <-> Main Menu (wraps)");

            // Sound Settings: the two sliders sit side by side, Back below them.
            bool horizontal = music.direction == Slider.Direction.LeftToRight || music.direction == Slider.Direction.RightToLeft;
            if (horizontal)
            {
                // Left/Right change the value, so Up/Down move: Music -> SFX -> Back (wraps).
                Link(music, up: backBtn, down: sfx);
                Link(sfx, up: music, down: backBtn);
                Link(backBtn, up: sfx, down: music);
                Debug.Log("[NavSetup] Sound Settings (horizontal sliders): Up/Down = Music -> SFX -> Back, Left/Right = change value");
            }
            else
            {
                // Up/Down change the value, so Left/Right move between the sliders; Down from Back goes nowhere.
                Link(music, right: sfx, down: backBtn, left: sfx);
                Link(sfx, left: music, down: backBtn, right: music);
                Link(backBtn, up: music, left: music, right: sfx);
                Debug.Log("[NavSetup] Sound Settings (vertical sliders): Left/Right = between sliders, Up/Down = change value, Back reachable from Down");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[NavSetup] Saved " + scenePath);
        }

        if (!string.IsNullOrEmpty(back)) EditorSceneManager.OpenScene(back, OpenSceneMode.Single);
        Debug.Log("[NavSetup] Step 7 applied to both scenes (already saved). To undo: revert those two scene files in GitHub Desktop.");
    }

    static void Link(Selectable s, Selectable up = null, Selectable down = null, Selectable left = null, Selectable right = null)
    {
        var n = s.navigation;
        n.mode = Navigation.Mode.Explicit;
        n.selectOnUp = up; n.selectOnDown = down; n.selectOnLeft = left; n.selectOnRight = right;
        s.navigation = n;
        EditorUtility.SetDirty(s);
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
