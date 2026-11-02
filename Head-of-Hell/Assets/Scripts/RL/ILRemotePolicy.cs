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
///   1 START  int32 n, n bytes UTF-8 JSON {matchId, self, opp, model, vocab, inputDim, shard}   -> reply 0
///   2 STEP   float32 dealt, float32 taken (HP since the previous message), float32[inputDim] x  -> reply action index
///                                                                                                  (-1 = sample yourself)
///   3 END    float32 dealt, float32 taken, int32 outcome (1 win, -1 loss, 0 draw / time-out), int32 truncated -> reply 0
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
    private readonly int vocab;
    private Character self, opp;
    private int lastSelfHp = -1, lastOppHp = -1;
    private bool episodeOpen, episodeClosed;

    public bool Active => !failed && client != null && client.Connected && !episodeClosed;

    private ILRemotePolicy(string selfId, string oppId, string modelName, int vocab)
    {
        this.selfId = selfId; this.oppId = oppId; this.modelName = modelName; this.vocab = vocab;
    }

    /// <summary>One instance per match (the scene is reloaded between matches); the socket is shared.</summary>
    public static ILRemotePolicy ForMatch(int port, string selfId, string oppId, string modelName, int vocab)
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
        return new ILRemotePolicy(selfId, oppId, modelName, vocab);
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
                string json = $"{{\"matchId\":\"{matchId}\",\"self\":\"{selfId}\",\"opp\":\"{oppId}\",\"model\":\"{modelName}\"," +
                              $"\"vocab\":{vocab},\"inputDim\":{x.Length},\"shard\":{ILGenArgs.ShardIndex}}}";
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
        if (!Active || !episodeOpen) { episodeClosed = true; return; }
        episodeClosed = true;
        try
        {
            HpDelta(out float dealt, out float taken);
            int selfHp = self != null ? self.GetCurrentHealth() : 1, oppHp = opp != null ? opp.GetCurrentHealth() : 1;
            int outcome = truncated ? 0 : (oppHp <= 0 && selfHp > 0 ? 1 : (selfHp <= 0 && oppHp > 0 ? -1 : 0));
            writer.Write(3); writer.Write(dealt); writer.Write(taken); writer.Write(outcome); writer.Write(truncated ? 1 : 0);
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
