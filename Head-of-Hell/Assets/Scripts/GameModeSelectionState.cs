using UnityEngine;
public enum SelectedGameMode
{
    PvP_1v1,
    PvP_1v1v1,
    PvP_2v2,
    PvE
}

public static class GameModeSelectionState
{
    public static SelectedGameMode CurrentMode { get; private set; } = SelectedGameMode.PvP_1v1;

    public static int PlayerCount
    {
        get
        {
            if (CurrentMode == SelectedGameMode.PvP_1v1v1)
                return 3;

            if (CurrentMode == SelectedGameMode.PvP_2v2)
                return 4;

            return 2;
        }
    }

    public static bool RequiresThirdPlayerSelection => PlayerCount >= 3;
    public static bool RequiresFourthPlayerSelection => PlayerCount >= 4;
    public static bool IsTwoVersusTwoMode => CurrentMode == SelectedGameMode.PvP_2v2;

    public static void SelectPvP1v1()
    {
        CurrentMode = SelectedGameMode.PvP_1v1;
        Debug.Log("[GameModeSelectionState] SelectPvP1v1 called");
    }

    public static void SelectPvP1v1v1()
    {
        CurrentMode = SelectedGameMode.PvP_1v1v1;
        Debug.Log("[GameModeSelectionState] SelectPvP1v1v1 called");
    }

    public static void SelectPvP2v2()
    {
        CurrentMode = SelectedGameMode.PvP_2v2;
        Debug.Log("[GameModeSelectionState] SelectPvP2v2 called");
    }

    public static void SelectPvE()
    {
        CurrentMode = SelectedGameMode.PvE;
        Debug.Log("[GameModeSelectionState] SelectPvE called");
    }
}
