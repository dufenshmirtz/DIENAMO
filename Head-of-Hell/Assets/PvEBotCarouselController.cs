using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PvEBotCarouselController : MonoBehaviour
{
    [Header("Selectors")]
    public ProfileCarouselSelector botSelector;
    public ProfileCarouselSelector difficultySelector;
    public ProfileCarouselSelector sideSelector;

    private void Start()
    {
        PvESelectionState.IsPvE = true;
        GameModeSelectionState.SelectPvE();

        botSelector.SetOptions(new List<string>
        {
            "ML Agent",
            "Scripted Bot"
        });

        difficultySelector.SetOptions(new List<string>
        {
            "Beginner",
            "Intermediate",
            "Expert"
        });

        sideSelector.SetOptions(new List<string>
        {
            "Player 1",
            "Player 2"
        });

        botSelector.OnIndexChanged += OnBotChanged;
        difficultySelector.OnIndexChanged += OnDifficultyChanged;
        sideSelector.OnIndexChanged += OnSideChanged;

        botSelector.SetIndexWithoutNotify(
            PvESelectionState.SelectedBotType == PvEBotType.MLAgent ? 0 : 1
        );

        difficultySelector.SetIndexWithoutNotify((int)PvESelectionState.SelectedDifficulty);

        sideSelector.SetIndexWithoutNotify(
            PvESelectionState.SelectedBotSide == PvEBotSide.Player1 ? 0 : 1
        );

        ApplyCurrentSelections();
        //RefreshDifficultyAvailability();
    }

    private void ApplyCurrentSelections()
    {
        PvESelectionState.SelectedBotType =
            botSelector.GetCurrentIndex() == 0 ? PvEBotType.MLAgent : PvEBotType.ScriptedBot;

        PvESelectionState.SelectedDifficulty =
            (PvEDifficulty)difficultySelector.GetCurrentIndex();

        PvESelectionState.SelectedBotSide =
            sideSelector.GetCurrentIndex() == 0 ? PvEBotSide.Player1 : PvEBotSide.Player2;
    }

    private void OnBotChanged(int index)
    {
        PvESelectionState.SelectedBotType =
            index == 0 ? PvEBotType.MLAgent : PvEBotType.ScriptedBot;

        //RefreshDifficultyAvailability();
    }

    private void OnDifficultyChanged(int index)
    {
        PvESelectionState.SelectedDifficulty = (PvEDifficulty)index;
    }

    private void OnSideChanged(int index)
    {
        PvESelectionState.SelectedBotSide =
            index == 0 ? PvEBotSide.Player1 : PvEBotSide.Player2;
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
