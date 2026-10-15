using System;
using UnityEngine;

/// <summary>
/// Thesis: settings of the IL agent in the normal game (PvE). One asset at Assets/Resources/ILAgentSettings.asset,
/// read both by the menu (which characters may be picked) and by the round setup (which model to load).
/// Create it with Assets > Create > Thesis > IL Agent Settings and drag the model (theta3_joint) into it.
/// </summary>
[CreateAssetMenu(fileName = "ILAgentSettings", menuName = "Thesis/IL Agent Settings")]
public class ILAgentSettings : ScriptableObject
{
    [Tooltip("Model exported by export_theta_unity.py (e.g. RL-Agent/models/theta3_joint.json)")]
    public TextAsset model;

    [Tooltip("Sampling temperature (1 = the probabilities as learned; used in all evaluations)")]
    [Min(0f)] public float temperature = 1f;

    [Tooltip("0 = random seed every round")]
    public int seed = 0;

    /// <summary>The few fields of the model file the menu needs (the weights are ignored here).</summary>
    [Serializable]
    public class ModelMeta
    {
        public string name;
        public string[] styleAxes;
        public float[] styleMean, styleMin, styleMax;
        public string[] trainedSelf, trainedOpp;
    }

    private static ILAgentSettings cached;
    private ModelMeta meta;
    private TextAsset metaFor;

    public static ILAgentSettings Load()
    {
        if (cached == null) cached = Resources.Load<ILAgentSettings>("ILAgentSettings");
        return cached;
    }

    public ModelMeta Meta
    {
        get
        {
            if (model == null) return null;
            if (meta == null || metaFor != model)
            {
                try { meta = JsonUtility.FromJson<ModelMeta>(model.text); }
                catch (Exception e) { Debug.LogError("[ILAgentSettings] model cannot be read: " + e.Message); meta = null; }
                metaFor = model;
            }
            return meta;
        }
    }

    static bool Contains(string[] list, string character)
    {
        if (list == null || list.Length == 0 || string.IsNullOrEmpty(character)) return false;
        string c = character.Trim();
        foreach (var x in list)
            if (!string.IsNullOrEmpty(x) && string.Equals(x.Trim(), c, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public bool AllowsSelf(string character) => Meta != null && Contains(Meta.trainedSelf, character);
    public bool AllowsOpponent(string character) => Meta != null && Contains(Meta.trainedOpp, character);

    /// <summary>Mirror scope: a character both seats may use (agent and opponent).</summary>
    public bool AllowsMirrorCharacter(string character) => AllowsSelf(character) && AllowsOpponent(character);
}
