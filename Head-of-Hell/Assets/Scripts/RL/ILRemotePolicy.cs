using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

/// <summary>
/// Thesis, RL fine-tuning (02/11, branch RLTeach): the IL agent's decision comes from a Python trainer
/// (RL-Agent/scripts/rl_finetune.py) over a local TCP socket. Only active in the parallel build with "-rlport N";
/// without it nothing here is created and the agent samples from its own model exactly as before.
///
/// The agent builds its 101 inputs exactly as in normal play (ILAgentController) and sends them; Python answers with the
/// index of an action combination of the model's vocabulary. While waiting, the game does not advance (locked step),
/// so the gameplay is identical to normal play. Reward is computed in Python from the raw HP changes sent here.
///
/// Protocol (little-endian; every message starts with an int32 type; every message gets an int32 reply):
///   1 START  int32 n, n bytes UTF-8 JSON {protocol, matchId, self, opp, model, vocab, inputDim, shard, style}  -> reply 0
///   2 STEP   float32 dealt, float32 taken (HP since the previous message), float32[inputDim] x  -> reply action index
///                                                                                                  (-1 = sample yourself)
///   3 END    float32 dealt, float32 taken, int32 outcome (1 win, -1 loss, 0 draw / time-out), int32 truncated,
///            [protocol 2, 05/11] int32 styleValid, float32[5] measured style of the agent in this round -> reply 0
///   (protocol 2: the agent's style of the round is measured with StyleAxes - the same code as the style tracker and the
///    Python analysis - when the round's telemetry is saved; the trainer compares it with the requested style.)
/// The connection is opened once per process and kept across the scene reloads between matches.
/// </summary>
public class ILRemotePolicy
{
    private static TcpClient client;
    private static BinaryWriter writer;
    private static BinaryReader reader;
    private static bool failed;
    private const int TimeoutMs = 120000;   // Python silent this long -> the run is stopped (no useless matches)

    private readonly string selfId, oppId, modelName;
    private readonly int vocab, playerNum;
    private readonly float[] requestedStyle;
    private double[] measuredStyle;   // StyleAxes of this round (null until the telemetry is saved)
    private bool measuredValid;
    private Character self, opp;
    private int lastSelfHp = -1, lastOppHp = -1;
    private bool episodeOpen, episodeClosed;

    public bool Active => !failed && client != null && client.Connected && !episodeClosed;

    private ILRemotePolicy(string selfId, string oppId, string modelName, int vocab, int playerNum, float[] style)
    {
        this.selfId = selfId; this.oppId = oppId; this.modelName = modelName; this.vocab = vocab;
        this.playerNum = playerNum; requestedStyle = style != null ? (float[])style.Clone() : new float[5];
        TelemetryManager.SessionSaved += OnSessionSaved;   // raised right before SessionClosed (-> EndEpisode)
    }

    // The round's telemetry was written: measure the agent's style exactly as the style tracker does.
    private void OnSessionSaved(TelemetrySession s)
    {
        try
        {
            if (s?.meta == null) return;
            string seat = playerNum == 1 ? (string.IsNullOrEmpty(s.meta.p1Id) ? "P1" : s.meta.p1Id)
                                         : (string.IsNullOrEmpty(s.meta.p2Id) ? "P2" : s.meta.p2Id);
            foreach (var r in StyleAxes.Compute(s))
                if (r.slotId == seat) { measuredStyle = r.ToVector(StyleAxes.AxisNames); measuredValid = r.valid; }
        }
        catch (Exception e) { Debug.LogWarning($"[ILRL] style of the round not measured: {e.Message}"); }
    }

    /// <summary>One instance per match (the scene is reloaded between matches); the socket is shared.</summary>
    public static ILRemotePolicy ForMatch(int port, string selfId, string oppId, string modelName, int vocab,
                                          int playerNum, float[] requestedStyle)
    {
        if (failed) return null;
        if (client == null || !client.Connected)
        {
            try
            {
                client = new TcpClient { NoDelay = true, ReceiveTimeout = TimeoutMs, SendTimeout = TimeoutMs };
                client.Connect("127.0.0.1", port);
                var s = client.GetStream();
                writer = new BinaryWriter(s);
                reader = new BinaryReader(s);
                Debug.Log($"[ILRL] Connected to the trainer on port {port}.");
            }
            catch (Exception e) { Fail($"cannot connect to the trainer on port {port}: {e.Message}"); return null; }
        }
        return new ILRemotePolicy(selfId, oppId, modelName, vocab, playerNum, requestedStyle);
    }

    /// <summary>Called by ILAgentController at every decision (fight running). Returns an action index or -1.</summary>
    public int Decide(float[] x, Character me, Character enemy)
    {
        if (!Active) return -1;
        self = me; opp = enemy;
        try
        {
            if (!episodeOpen)
            {
                episodeOpen = true;
                lastSelfHp = me.GetCurrentHealth(); lastOppHp = enemy.GetCurrentHealth();
                string matchId = TelemetryManager.Instance != null ? TelemetryManager.Instance.CurrentMatchId : "";
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                string style = string.Join(",", Array.ConvertAll(requestedStyle, v => v.ToString("R", inv)));
                string json = $"{{\"protocol\":2,\"matchId\":\"{matchId}\",\"self\":\"{selfId}\",\"opp\":\"{oppId}\",\"model\":\"{modelName}\"," +
                              $"\"vocab\":{vocab},\"inputDim\":{x.Length},\"shard\":{ILGenArgs.ShardIndex},\"style\":[{style}]}}";
                byte[] b = Encoding.UTF8.GetBytes(json);
                writer.Write(1); writer.Write(b.Length); writer.Write(b); writer.Flush();
                reader.ReadInt32();
            }
            HpDelta(out float dealt, out float taken);
            writer.Write(2); writer.Write(dealt); writer.Write(taken);
            for (int i = 0; i < x.Length; i++) writer.Write(x[i]);
            writer.Flush();
            return reader.ReadInt32();
        }
        catch (Exception e) { Fail($"step failed: {e.Message}"); return -1; }
    }

    /// <summary>Director: the match is over (saved = normal end; truncated = stalemate restart). Sends the last HP
    /// changes (the final blow happens after the last decision) and the outcome.</summary>
    public void EndEpisode(bool truncated)
    {
        TelemetryManager.SessionSaved -= OnSessionSaved;
        if (!Active || !episodeOpen) { episodeClosed = true; return; }
        episodeClosed = true;
        try
        {
            HpDelta(out float dealt, out float taken);
            int selfHp = self != null ? self.GetCurrentHealth() : 1, oppHp = opp != null ? opp.GetCurrentHealth() : 1;
            int outcome = truncated ? 0 : (oppHp <= 0 && selfHp > 0 ? 1 : (selfHp <= 0 && oppHp > 0 ? -1 : 0));
            writer.Write(3); writer.Write(dealt); writer.Write(taken); writer.Write(outcome); writer.Write(truncated ? 1 : 0);
            bool styleOk = !truncated && measuredValid && measuredStyle != null && measuredStyle.Length == 5;
            writer.Write(styleOk ? 1 : 0);
            for (int i = 0; i < 5; i++) writer.Write(styleOk ? (float)measuredStyle[i] : 0f);
            writer.Flush();
            reader.ReadInt32();
        }
        catch (Exception e) { Fail($"end failed: {e.Message}"); }
    }

    private void HpDelta(out float dealt, out float taken)
    {
        int s = self != null ? self.GetCurrentHealth() : lastSelfHp, o = opp != null ? opp.GetCurrentHealth() : lastOppHp;
        dealt = Mathf.Max(0, lastOppHp - o);
        taken = Mathf.Max(0, lastSelfHp - s);
        lastSelfHp = s; lastOppHp = o;
    }

    private static void Fail(string why)
    {
        failed = true;
        Debug.LogError($"[ILRL] {why}. RL training stopped; the build quits.");
        try { client?.Close(); } catch { }
#if !UNITY_EDITOR
        Application.Quit();
#endif
    }
}
