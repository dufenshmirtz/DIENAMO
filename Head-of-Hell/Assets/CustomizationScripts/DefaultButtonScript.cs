using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class DefaultButtonScript : MonoBehaviour
{
    public TMP_Text choice;

    public void Defaultify()
    {
        choice.text = "Default";
        RulesetManager.Instance.SetRulesetNum(-1);
        RulesetSelectionState.SelectDefault();
        CustomRulesetScreenManager.selectedSlot = -1;
        CustomRulesetScreenManager.currentRuleset = null;
    }
}
