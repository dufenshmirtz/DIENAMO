using System;
using UnityEngine;

/// <summary>
/// Style-conditioned imitation-learning agent (thesis, theta0).
///
/// Every <decisionPeriod> fixed steps (4 x 0.02 s = 80 ms, the same rate as the recorded data):
///   1. builds the 67 observation floats with FighterObservationEncoder (same code and scales as the recorder),
///   2. appends the style vector, normalised with the training statistics stored in the model file,
///   3. runs the MLP (72 -> 256 -> 256 -> 9 heads),
///   4. SAMPLES one action per head from its softmax (temperature 1 = the probabilities learned from the data;
///      argmax would almost never press a rare button such as heavy).
/// The sampled actions are then played through ILAgentInputProvider with the same meaning they had in the data:
///   move/block/charge-hold = held until the next decision; jump/drop/light/heavy/special/parry = one key-down frame;
///   charge 2 = release (key-up).
/// </summary>
[DefaultExecutionOrder(-50)]   // Update before Character.Update -> presses are read in the same frame
public class ILAgentController : MonoBehaviour
{
    [Header("Model (JSON from RL-Agent/scripts/export_theta_unity.py)")]
    public TextAsset model;

    [Header("Style (raw values, same axes as the telemetry analysis)")]
    public float attackRate = 0.27f;
    public float mobilityRate = 0.59f;
    public float defenseRate = 0.15f;
    public float mobilityTimeRatio = 0.54f;
    public float blockTimeRatio = 0.07f;

    [Header("Sampling")]
    [Tooltip("1 = sample from the learned probabilities. <1 sharper, >1 more random. 0 = argmax (debug only).")]
    [Min(0f)] public float temperature = 1f;
    [Tooltip("0 = random seed every run; otherwise reproducible sampling.")]
    public int seed = 0;
    [Tooltip("Fixed steps between decisions (must match the recorded data: 4).")]
    public int decisionPeriod = 4;
    [Tooltip("Label written to telemetry / logs for this agent.")]
    public string label = "IL";
    public bool logDecisions = false;

    [Header("Safety rule (OFF by default; hand-written, report it if used)")]
    [Tooltip("> 0: after the agent presses heavy, block new attack/defence presses for this many seconds (heavy hits at ~0.57 s), like the scripted bots. 0 = pure learned policy.")]
    [Min(0f)] public float heavyCommitSeconds = 0f;

    // observation scales: must match FighterAgent / ImitationLoggerV2
    private const float RelXScale = 9f, RelYScale = 5f, VelScale = 10f;
    private const int TotalCharacterCount = 10;

    [Serializable]
    private class ThetaFile
    {
        public string name;
        public int obsDim, quickDim, styleDim, historyDim, historyCap, inputDim, hidden1, hidden2;
        public bool ego;
        public float[] styleMean, styleStd;
        public int[] headSizes;
        public string outputType;      // "heads" (one softmax per head) or "joint" (one softmax over action combinations)
        public int vocabSize;
        public int[] vocab;            // joint: vocabSize rows x headSizes.Length values
        public float[] W1, b1, W2, b2, W3, b3, testInput, testLogits;
        public float[] styleMin, styleMax;          // style range of the training personas (online clamp); may be empty
        public string[] trainedSelf, trainedOpp;    // characters seen in training; may be empty
    }

    private ThetaFile net;
    private float[] x, h1, h2, logits;
    private System.Random rng;

    private Character self, enemy;
    private ILAgentInputProvider input;
    private int fixedCounter;

    private static readonly int[] EgoXColumns = { 0, 4, 6, 64, 65 };
    private const int EgoDirIndex = 64;

    // Quick ready = the quick-attack indicator under the health bar is shown (Skipler hides it during the Blink cooldown).
    private CharacterSetup setupCache;
    private bool QuickReady(Character me)
    {
        if (setupCache == null) setupCache = me.GetComponentInParent<CharacterSetup>();
        return setupCache == null || setupCache.quickAttackIndicator == null || setupCache.quickAttackIndicator.activeSelf;
    }

    // action history (same definition as RL-Agent/scripts/history_features.py)
    private static readonly int[] PressHeads = { 1, 2, 3, 4, 6, 8 };   // jump, drop, light, heavy, special, parry
    private int[] prevAction;          // null before the first decision of a fight
    private int[] since = new int[6];
    private int chargeRun, blockRun;
    private float heavyCommitUntil = -1f;

    // last decision (applied in Update)
    private int move = 1, charge;
    private bool block, pendingJump, pendingDrop, pendingLight, pendingHeavy, pendingSpecial, pendingParry;

    public bool Ready => net != null && self != null && enemy != null;
    private bool IsJoint => net != null && net.outputType == "joint";
    public string Label => label;
    public float[] Style => new[] { attackRate, mobilityRate, defenseRate, mobilityTimeRatio, blockTimeRatio };

    // Thesis (online): where the current style came from, the style asked for before the clamp, and if it was clamped.
    // Written to agent_style_log.csv by the StyleTracker after every round.
    [NonSerialized] public string styleSource = "fixed";
    [NonSerialized] public float[] styleRequested;
    [NonSerialized] public bool styleClamped;

    /// <summary>The model's neutral style: the mean style of the training personas (null before a model is loaded).</summary>
    public float[] NeutralStyle => net != null && net.styleMean != null ? (float[])net.styleMean.Clone() : null;

    /// <summary>
    /// Online: set the style, clamped to the training range of the loaded model (outside it the agent would be asked for
    /// a style it never saw). Returns true if any axis was clamped. Without a range in the model file: no clamp.
    /// </summary>
    public bool ApplyStyle(float[] requested, string source)
    {
        if (requested == null || requested.Length != 5) throw new ArgumentException("style must have 5 values");
        styleSource = source;
        styleRequested = (float[])requested.Clone();
        var v = (float[])requested.Clone();
        bool clamped = false;
        if (net != null && net.styleMin != null && net.styleMax != null && net.styleMin.Length == 5 && net.styleMax.Length == 5)
        {
            for (int i = 0; i < 5; i++)
            {
                float c = Mathf.Clamp(v[i], net.styleMin[i], net.styleMax[i]);
                if (c != v[i]) clamped = true;
                v[i] = c;
            }
        }
        styleClamped = clamped;
        SetStyle(v[0], v[1], v[2], v[3], v[4]);
        return clamped;
    }

    // ---------------------------------------------------------------- setup
    public void SetStyle(float attack, float mobility, float defense, float mobTime, float blockTime)
    {
        attackRate = attack; mobilityRate = mobility; defenseRate = defense; mobilityTimeRatio = mobTime; blockTimeRatio = blockTime;
    }

    public void Rebind(Character selfCharacter, Character enemyCharacter)
    {
        self = selfCharacter;
        enemy = enemyCharacter;
        if (input == null) input = new ILAgentInputProvider();
        if (self != null) self.SetInput(input);
        fixedCounter = 0;
        ResetHistory();
        rng = seed != 0 ? new System.Random(seed) : new System.Random();   // seed may have been set after Awake
    }

    private void Awake()
    {
        rng = seed != 0 ? new System.Random(seed) : new System.Random();
        if (model != null) LoadModel(model);
    }

    public bool LoadModel(TextAsset asset)
    {
        model = asset;
        try { net = JsonUtility.FromJson<ThetaFile>(asset.text); }
        catch (Exception e) { Debug.LogError($"[ILAgent] Model '{asset.name}' could not be read: {e.Message}"); net = null; return false; }

        int nOut = 0;
        foreach (int k in net.headSizes) nOut += k;
        if (IsJoint) nOut = net.vocabSize;
        if ((IsJoint && (net.vocab == null || net.vocab.Length != net.vocabSize * net.headSizes.Length)) ||
            net.W1.Length != net.inputDim * net.hidden1 || net.W2.Length != net.hidden1 * net.hidden2 ||
            net.W3.Length != net.hidden2 * nOut || net.inputDim != net.obsDim + net.quickDim + net.historyDim + net.styleDim)
        {
            Debug.LogError($"[ILAgent] Model '{asset.name}' has inconsistent sizes.");
            net = null;
            return false;
        }

        x = new float[net.inputDim]; h1 = new float[net.hidden1]; h2 = new float[net.hidden2]; logits = new float[nOut];

        // self-test against the NumPy forward pass stored in the file
        Array.Copy(net.testInput, x, x.Length);
        Forward();
        float maxDiff = 0f;
        for (int i = 0; i < nOut; i++) maxDiff = Mathf.Max(maxDiff, Mathf.Abs(logits[i] - net.testLogits[i]));
        if (maxDiff < 1e-3f) Debug.Log($"[ILAgent] Model '{net.name}' loaded ({net.obsDim} obs{(net.ego ? " (egocentric)" : "")} + {net.quickDim} quick + {net.historyDim} history + {net.styleDim} style = {net.inputDim} inputs, {nOut} outputs). Self-test OK (max diff {maxDiff:E1}).");
        else Debug.LogError($"[ILAgent] Model '{net.name}' self-test FAILED (max diff {maxDiff}). Do not use it.");
        return maxDiff < 1e-3f;
    }

    private void ResetHistory()
    {
        prevAction = null;
        for (int j = 0; j < since.Length; j++) since[j] = net != null ? net.historyCap : 25;
        chargeRun = blockRun = 0;
        heavyCommitUntil = -1f;
    }

    private static bool FightRunning()
    {
        var tm = TelemetryManager.Instance;
        return tm == null || tm.FightStarted;   // the recorded data starts at FightStart
    }

    private void OnDisable()
    {
        input?.ReleaseAll();
    }

    // ---------------------------------------------------------------- decide (fixed steps)
    private void FixedUpdate()
    {
        if (!Ready) return;
        if (!FightRunning())
        {
            fixedCounter = 0;
            if (prevAction != null) ResetHistory();
            move = 1; block = false; charge = 0;
            return;
        }
        if (++fixedCounter < Mathf.Max(1, decisionPeriod)) return;
        fixedCounter = 0;

        Character me = Character.ResolveActiveCharacter(self) ?? self;
        Character opp = Character.ResolveActiveCharacter(enemy) ?? enemy;

        float[] obs = FighterObservationEncoder.Encode(me, opp, RelXScale, RelYScale, VelScale, TotalCharacterCount);
        // egocentric frame (RL-Agent/scripts/egocentric.py): opponent always on the right
        float dir = obs[EgoDirIndex] < 0f ? -1f : 1f;
        if (net.ego)
            foreach (int c in EgoXColumns) obs[c] *= dir;
        Array.Copy(obs, x, net.obsDim);
        int o2 = net.obsDim;
        if (net.quickDim > 0) x[o2++] = QuickReady(me) ? 1f : 0f;
        if (net.historyDim > 0) WriteHistory(o2);
        float[] s = Style;
        int so = o2 + net.historyDim;
        for (int i = 0; i < net.styleDim; i++)
            x[so + i] = (s[i] - net.styleMean[i]) / net.styleStd[i];

        Forward();

        int[] a = new int[net.headSizes.Length];
        if (IsJoint)
        {
            // one sample over the action combinations the teachers used
            int k = Sample(0, net.vocabSize);
            Array.Copy(net.vocab, k * a.Length, a, 0, a.Length);
        }
        else
        {
            int o = 0;
            for (int h = 0; h < a.Length; h++)
            {
                a[h] = Sample(o, net.headSizes[h]);
                o += net.headSizes[h];
            }
        }

        // optional hand-written safety rule (off by default)
        if (heavyCommitSeconds > 0f && Time.time < heavyCommitUntil)
        {
            a[3] = 0; a[4] = 0; a[6] = 0; a[8] = 0;
            if (!block) a[5] = 0;
            if (charge != 1) a[7] = 0;
        }
        if (heavyCommitSeconds > 0f && a[4] == 1) heavyCommitUntil = Time.time + heavyCommitSeconds;

        UpdateHistory(a);   // history is kept in the model's (egocentric) frame

        // ego move (0 away, 1 none, 2 towards) -> world move (0 left, 1 none, 2 right)
        move = (net.ego && dir < 0f) ? 2 - a[0] : a[0];
        pendingJump |= a[1] == 1;
        pendingDrop |= a[2] == 1;
        pendingLight |= a[3] == 1;
        pendingHeavy |= a[4] == 1;
        block = a[5] == 1;
        pendingSpecial |= a[6] == 1;
        charge = a[7];
        pendingParry |= a[8] == 1;

        if (logDecisions) Debug.Log($"[ILAgent] {label} act=[{string.Join(",", a)}]");
    }

    // ---- history features: prev action one-hot, decisions since each press, charge/block hold runs ----
    private void WriteHistory(int o)
    {
        int cap = net.historyCap;
        int k = 0;
        for (int h = 0; h < net.headSizes.Length; h++)
            for (int c = 0; c < net.headSizes[h]; c++)
                x[o + k++] = (prevAction != null && prevAction[h] == c) ? 1f : 0f;
        for (int j = 0; j < PressHeads.Length; j++) x[o + k++] = Mathf.Min(since[j], cap) / (float)cap;
        x[o + k++] = Mathf.Min(chargeRun, cap) / (float)cap;
        x[o + k++] = Mathf.Min(blockRun, cap) / (float)cap;
        if (k != net.historyDim) Debug.LogError($"[ILAgent] history size {k} != model {net.historyDim}");
    }

    private void UpdateHistory(int[] a)
    {
        for (int j = 0; j < PressHeads.Length; j++) since[j] = a[PressHeads[j]] == 1 ? 1 : since[j] + 1;
        chargeRun = a[7] == 1 ? chargeRun + 1 : 0;
        blockRun = a[5] == 1 ? blockRun + 1 : 0;
        prevAction = (int[])a.Clone();
    }

    private void Forward()
    {
        int nIn = net.inputDim, n1 = net.hidden1, n2 = net.hidden2, nOut = logits.Length;
        for (int j = 0; j < n1; j++)
        {
            float v = net.b1[j];
            for (int i = 0; i < nIn; i++) v += x[i] * net.W1[i * n1 + j];
            h1[j] = v > 0f ? v : 0f;
        }
        for (int j = 0; j < n2; j++)
        {
            float v = net.b2[j];
            for (int i = 0; i < n1; i++) v += h1[i] * net.W2[i * n2 + j];
            h2[j] = v > 0f ? v : 0f;
        }
        for (int j = 0; j < nOut; j++)
        {
            float v = net.b3[j];
            for (int i = 0; i < n2; i++) v += h2[i] * net.W3[i * nOut + j];
            logits[j] = v;
        }
    }

    private int Sample(int offset, int k)
    {
        int best = 0;
        for (int c = 1; c < k; c++) if (logits[offset + c] > logits[offset + best]) best = c;
        if (temperature <= 0f) return best;

        double max = logits[offset + best], sum = 0;
        double[] p = new double[k];
        for (int c = 0; c < k; c++) { p[c] = Math.Exp((logits[offset + c] - max) / temperature); sum += p[c]; }
        double r = rng.NextDouble() * sum;
        for (int c = 0; c < k; c++) { r -= p[c]; if (r <= 0) return c; }
        return k - 1;
    }

    // ---------------------------------------------------------------- apply (frames)
    private void Update()
    {
        if (!Ready || input == null) return;
        Character me = Character.ResolveActiveCharacter(self) ?? self;
        if (me.GetInputProvider() != input) me.SetInput(input);   // e.g. a Lupen form became active

        input.ClearFrameState();

        // held
        float h = move == 0 ? -1f : (move == 2 ? 1f : 0f);
        input.SetAxis("Horizontal" + me.playerString, h);
        input.SetAxis("Horizontal", h);
        input.SetKey(me.left, h < -0.1f);
        input.SetKey(me.right, h > 0.1f);
        input.SetKey(me.block, block);
        input.SetKey(me.charge, charge == 1);   // 2 (release) and 0 -> key up if it was held

        // one-frame presses
        if (pendingJump) input.PressKeyOneFrame(me.up);
        if (pendingDrop) input.PressKeyOneFrame(me.down);
        if (pendingLight) input.PressKeyOneFrame(me.lightAttack);
        if (pendingHeavy) input.PressKeyOneFrame(me.heavyAttack);
        if (pendingSpecial) input.PressKeyOneFrame(me.ability);
        if (pendingParry) input.PressKeyOneFrame(me.parry);
        pendingJump = pendingDrop = pendingLight = pendingHeavy = pendingSpecial = pendingParry = false;
    }
}
