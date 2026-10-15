using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PvEBotCarouselController : MonoBehaviour
{
    [Header("Selectors")]
    public ProfileCarouselSelector botSelector;
    public ProfileCarouselSelector difficultySelector;   // IL Agent: style source (Mirror / Ghost: <profile>)
    public ProfileCarouselSelector sideSelector;

    // Thesis: options of the second carousel when the IL agent is selected
    private readonly List<string> ilGhostIds = new List<string>();     // "" = Mirror
    private readonly List<string> ilGhostNames = new List<string>();

    private void Start()
    {
        PvESelectionState.IsPvE = true;
        GameModeSelectionState.SelectPvE();

        // index == (int)PvEBotType
        botSelector.SetOptions(new List<string>
        {
            "ML Agent",
            "Scripted Bot",
            "IL Agent"
        });

        // index 0 = bot on Player 1, index 1 = bot on Player 2 (same mapping everywhere below)
        sideSelector.SetOptions(new List<string>
        {
            "Player 1",
            "Player 2"
        });

        botSelector.OnIndexChanged += OnBotChanged;
        difficultySelector.OnIndexChanged += OnDifficultyChanged;
        sideSelector.OnIndexChanged += OnSideChanged;

        // Show the current selection. (Before: the bot and side carousels started on the opposite option and
        // ApplyCurrentSelections then silently changed the selection, e.g. Scripted Bot -> ML Agent.)
        botSelector.SetIndexWithoutNotify((int)PvESelectionState.SelectedBotType);
        sideSelector.SetIndexWithoutNotify(PvESelectionState.SelectedBotSide == PvEBotSide.Player1 ? 0 : 1);
        RefreshSecondSelector();

        ApplyCurrentSelections();
        //RefreshDifficultyAvailability();
    }

    private void ApplyCurrentSelections()
    {
        PvESelectionState.SelectedBotType = (PvEBotType)botSelector.GetCurrentIndex();

        ApplySecondSelector(difficultySelector.GetCurrentIndex());

        PvESelectionState.SelectedBotSide =
            sideSelector.GetCurrentIndex() == 0 ? PvEBotSide.Player1 : PvEBotSide.Player2;
    }

    private void OnBotChanged(int index)
    {
        PvESelectionState.SelectedBotType = (PvEBotType)index;
        RefreshSecondSelector();

        //RefreshDifficultyAvailability();
    }

    private void OnDifficultyChanged(int index)
    {
        ApplySecondSelector(index);
    }

    private void OnSideChanged(int index)
    {
        PvESelectionState.SelectedBotSide =
            index == 0 ? PvEBotSide.Player1 : PvEBotSide.Player2;
    }

    // ---------------------------------------------------------------- second carousel: difficulty or style source

    private void RefreshSecondSelector()
    {
        if (PvESelectionState.SelectedBotType == PvEBotType.ILAgent)
        {
            BuildILStyleOptions();
            var labels = new List<string>();
            for (int i = 0; i < ilGhostIds.Count; i++)
                labels.Add(ilGhostIds[i] == "" ? "Style: Mirror" : "Style: Ghost " + ilGhostNames[i]);
            difficultySelector.SetOptions(labels);

            int idx = 0;
            if (PvESelectionState.SelectedILStyleSource == ILStyleSource.Ghost)
                idx = Mathf.Max(0, ilGhostIds.IndexOf(PvESelectionState.SelectedGhostProfileId));
            difficultySelector.SetIndexWithoutNotify(idx);
            ApplySecondSelector(idx);
        }
        else
        {
            difficultySelector.SetOptions(new List<string>
            {
                "Beginner",
                "Intermediate",
                "Expert"
            });
            difficultySelector.SetIndexWithoutNotify((int)PvESelectionState.SelectedDifficulty);
        }
    }

    private void ApplySecondSelector(int index)
    {
        if (PvESelectionState.SelectedBotType != PvEBotType.ILAgent)
        {
            PvESelectionState.SelectedDifficulty = (PvEDifficulty)Mathf.Clamp(index, 0, 2);
            return;
        }

        if (index <= 0 || index >= ilGhostIds.Count)
        {
            PvESelectionState.SelectedILStyleSource = ILStyleSource.Mirror;
            PvESelectionState.SelectedGhostProfileId = "";
            PvESelectionState.SelectedGhostName = "";
        }
        else
        {
            PvESelectionState.SelectedILStyleSource = ILStyleSource.Ghost;
            PvESelectionState.SelectedGhostProfileId = ilGhostIds[index];
            PvESelectionState.SelectedGhostName = ilGhostNames[index];
        }
    }

    /// <summary>Mirror + one Ghost option per saved profile that has a style for a character the model allows.</summary>
    private void BuildILStyleOptions()
    {
        ilGhostIds.Clear();
        ilGhostNames.Clear();
        ilGhostIds.Add("");
        ilGhostNames.Add("");

        var settings = ILAgentSettings.Load();
        foreach (var e in StyleProfileStore.PersistentEntries())
        {
            if (e == null || e.rounds <= 0 || e.ema == null || e.ema.Length == 0) continue;
            if (settings != null && !settings.AllowsMirrorCharacter(e.character)) continue;
            if (!StyleTracker.ProfileExists(e.key)) continue;          // deleted profiles are not offered
            if (ilGhostIds.Contains(e.key)) continue;
            ilGhostIds.Add(e.key);
            ilGhostNames.Add(string.IsNullOrEmpty(e.displayName) ? e.key.Substring(0, Mathf.Min(8, e.key.Length)) : e.displayName);
        }
    }

    private void RefreshDifficultyAvailability()
    {
        bool scriptedSelected = PvESelectionState.SelectedBotType == PvEBotType.ScriptedBot;

        foreach (Button btn in difficultySelector.GetComponentsInChildren<Button>(true))
        {
            btn.interactable = scriptedSelected;
        }
    }
}
