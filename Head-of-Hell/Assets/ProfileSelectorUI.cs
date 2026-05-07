using System.Collections.Generic;
using UnityEngine;

public class ProfileSelectorsUI : MonoBehaviour
{
    public ProfileCarouselSelector p1Selector;
    public ProfileCarouselSelector p2Selector;
    public ProfileCarouselSelector p3Selector;
    public ProfileCarouselSelector p4Selector;
    public GameObject p3SelectionRoot;
    public GameObject p4SelectionRoot;

    // mapping: UI index -> profileIndex (-2 guest, >=0 slot index)
    private readonly List<int> optionMap = new List<int>();

    private const int GUEST = -2;

    private void OnEnable()
    {
        StartCoroutine(InitNextFrame());
    }

    private System.Collections.IEnumerator InitNextFrame()
    {
        yield return null;

        EnsureP4SelectionRoot();
        RefreshSelectors();

        p1Selector.OnIndexChanged -= OnP1Changed;
        p2Selector.OnIndexChanged -= OnP2Changed;
        if (p3Selector != null)
            p3Selector.OnIndexChanged -= OnP3Changed;
        if (p4Selector != null)
            p4Selector.OnIndexChanged -= OnP4Changed;

        p1Selector.OnIndexChanged += OnP1Changed;
        p2Selector.OnIndexChanged += OnP2Changed;
        if (p3Selector != null)
            p3Selector.OnIndexChanged += OnP3Changed;
        if (p4Selector != null)
            p4Selector.OnIndexChanged += OnP4Changed;
    }

    public void RefreshSelectors()
    {
        if (ProfileManager.I == null) return;

        bool showP3 = GameModeSelectionState.RequiresThirdPlayerSelection;
        bool showP4 = GameModeSelectionState.RequiresFourthPlayerSelection;
        if (p3SelectionRoot != null)
            p3SelectionRoot.SetActive(showP3);
        if (p4SelectionRoot != null)
            p4SelectionRoot.SetActive(showP4);

        optionMap.Clear();
        var names = new List<string>();

        // 0) Guest
        optionMap.Add(GUEST);
        names.Add("Guest");

        // profiles
        var db = ProfileManager.I.GetDatabase();
        for (int i = 0; i < ProfilesDatabase.MaxProfiles; i++)
        {
            var p = db.GetAt(i);
            if (p == null) continue;

            optionMap.Add(i);
            names.Add(p.profileName);
        }

        // Apply to selectors
        p1Selector.SetOptions(names);
        p2Selector.SetOptions(names);
        if (p3Selector != null)
            p3Selector.SetOptions(names);
        if (p4Selector != null)
            p4Selector.SetOptions(names);

        // Sync with current selection
        p1Selector.SetIndexWithoutNotify(FindOptionForPlayer(1));
        p2Selector.SetIndexWithoutNotify(FindOptionForPlayer(2));
        if (showP3 && p3Selector != null)
            p3Selector.SetIndexWithoutNotify(FindOptionForPlayer(3));
        if (showP4 && p4Selector != null)
            p4Selector.SetIndexWithoutNotify(FindOptionForPlayer(4));
    }

    private int FindOptionForPlayer(int playerNum)
    {
        if (ProfileManager.I.IsGuestSelected(playerNum)) return 0;

        var selected = ProfileManager.I.GetSelectedProfile(playerNum);
        if (selected == null) return 0;

        for (int opt = 0; opt < optionMap.Count; opt++)
        {
            int idx = optionMap[opt];
            if (idx >= 0)
            {
                var p = ProfileManager.I.GetDatabase().GetAt(idx);
                if (p != null && p.id == selected.id)
                    return opt;
            }
        }

        return 0;
    }

    private void OnP1Changed(int index) => OnChanged(1, index);
    private void OnP2Changed(int index) => OnChanged(2, index);
    private void OnP3Changed(int index) => OnChanged(3, index);
    private void OnP4Changed(int index) => OnChanged(4, index);

    private void OnChanged(int playerNum, int optionIndex)
    {
        if (ProfileManager.I == null) return;
        if (optionIndex < 0 || optionIndex >= optionMap.Count) return;

        int mapped = optionMap[optionIndex];

        if (mapped == GUEST)
            ProfileManager.I.SelectGuest(playerNum);
        else
            ProfileManager.I.SelectProfile(playerNum, mapped);
    }

    private void EnsureP4SelectionRoot()
    {
        if (p4SelectionRoot != null && p4Selector != null)
            return;

        if (!GameModeSelectionState.RequiresFourthPlayerSelection || p3SelectionRoot == null)
            return;

        if (p4SelectionRoot == null)
        {
            p4SelectionRoot = Instantiate(p3SelectionRoot, p3SelectionRoot.transform.parent);
            p4SelectionRoot.name = "P4ProfileSelectionRoot";

            RectTransform p3Rect = p3SelectionRoot.GetComponent<RectTransform>();
            RectTransform p4Rect = p4SelectionRoot.GetComponent<RectTransform>();
            if (p3Rect != null && p4Rect != null)
            {
                p4Rect.anchoredPosition = p3Rect.anchoredPosition + new UnityEngine.Vector2(0f, -70f);
            }
        }

        if (p4Selector == null && p4SelectionRoot != null)
            p4Selector = p4SelectionRoot.GetComponentInChildren<ProfileCarouselSelector>(true);
    }
}
