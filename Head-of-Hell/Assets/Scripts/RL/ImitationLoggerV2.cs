using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Unity.MLAgents.Policies;

/// <summary>
/// Imitation-learning recorder v2 (thesis: style-conditioned behaviour cloning).
///
/// - Records BOTH players of every telemetry session, from FightStart until the session closes.
/// - Actions are read through each character's IInputProvider (keyboard, scripted bot, RL agent, replay),
///   plus the raw gamepad buttons exactly like Character.cs does when the character uses a controller.
/// - One record per player every <decisionPeriod> fixed steps (default 4 x 0.02 s = 80 ms, same as the
///   RL agent in GamePlayScene). One-shot buttons = "pressed at least once since the previous record";
///   held buttons (move, block, charge) = state at the record tick.
/// - Observations = FighterObservationEncoder (same 67 floats as FighterAgent).
/// - Files follow the telemetry session: written to tmp_<matchId>.jsonl, renamed to match_<matchId>.jsonl
///   when the telemetry JSON is saved, deleted when the session is discarded (unfinished round etc.).
///
/// Read-only with respect to gameplay: it never writes to inputs or characters.
/// Add this component to one object in the fight scene and tick "enableLogging".
/// </summary>
public class ImitationLoggerV2 : MonoBehaviour
{
    [Header("General")]
    public bool enableLogging = true;
    public bool logToConsole = false;

    [Header("Timing")]
    [Tooltip("Fixed steps between two records (4 x 0.02 s = 80 ms, same as the RL agent's DecisionPeriod).")]
    public int decisionPeriod = 4;

    [Header("Observation scales (must match FighterAgent)")]
    public float relXScale = 9f;
    public float relYScale = 5f;
    public float velScale = 10f;
    public int totalCharacterCount = 10;

    // ---------------- per-player state ----------------
    private class Slot
    {
        public string id;                 // "P1" / "P2"
        public Character root;            // any Character component of this player (re-resolved for Lupen forms)
        // one-shot presses since the last record
        public bool jump, drop, light, heavy, special, parry, chargeRelease;
        public bool prevAxisUp;           // for the vertical-axis jump edge (same rule as Character)
        public Slot expert;               // DAgger: presses of the shadow expert on this character (null = none)
        public void ClearPresses()
        {
            jump = drop = light = heavy = special = parry = chargeRelease = false;
            expert?.ClearPresses();
        }
    }

    private readonly Slot[] slots = { new Slot { id = "P1" }, new Slot { id = "P2" } };

    // ---------------- file / session state ----------------
    private string writingMatchId;
    private string tmpPath;
    private StreamWriter writer;
    private int fixedStepCounter;
    private int tick;
    private float firstTickTime;
    private float nextRefindTime;

    [Serializable]
    private class Record
    {
        public string matchId;
        public int tick;
        public float t;               // seconds since the first record of this match
        public string slot;           // P1 / P2
        public string profileId;
        public string playerKind;     // human / scripted / rl / replay / other
        public string model;          // RL model name (empty otherwise)
        public string persona;        // scripted persona id (empty otherwise)
        public float aggression = -1f, defense = -1f, mobility = -1f, patience = -1f, skill = -1f; // scripted bot knobs, -1 = n/a
        public string character;
        public int selfCharacterId;
        public int oppCharacterId;
        public int selfHp;
        public int oppHp;
        public float[] obs;           // 67
        public int[] act;             // 9: move(0/1/2), jump, drop, light, heavy, block, special, charge(0/1/2), parry
        // DAgger (23/10): what the shadow expert (scripted bot, not in control) would have done at this record,
        // same encoding as act. Empty when the character has no shadow expert.
        public int[] expertAct;
        public string expertPersona;
    }

    // =====================================================================

    private void OnEnable()
    {
        TelemetryManager.SessionClosed += OnSessionClosed;
    }

    private void OnDisable()
    {
        TelemetryManager.SessionClosed -= OnSessionClosed;
        AbortCurrentFile("logger disabled");
    }

    private void OnApplicationQuit()
    {
        AbortCurrentFile("application quit");
    }

    // ---------------- input sampling (every frame) ----------------
    private void LateUpdate()
    {
        if (!enableLogging || !IsFightRunning()) return;

        foreach (var s in slots)
        {
            Character c = ResolveSlot(s);
            if (c == null) continue;
            IInputProvider input = c.GetInputProvider();
            if (input == null) continue;

            bool pad = c.UsesController;
            float v = VerticalInput(c, input);
            bool axisUp = v > 0.5f;

            if (input.GetKeyDown(c.up) || (axisUp && !s.prevAxisUp)) s.jump = true;
            s.prevAxisUp = axisUp;

            if (input.GetKeyDown(c.down) || (pad && v < -0.5f)) s.drop = true;
            if (input.GetKeyDown(c.lightAttack) || (pad && Input.GetKeyDown(c.JoystickButtonName(0)))) s.light = true;
            if (input.GetKeyDown(c.heavyAttack) || (pad && Input.GetKeyDown(c.JoystickButtonName(2)))) s.heavy = true;
            if (input.GetKeyDown(c.ability) || (pad && Input.GetKeyDown(c.JoystickButtonName(3)))) s.special = true;
            if (input.GetKeyDown(c.parry) || (pad && Input.GetKeyDown(c.JoystickButtonName(4)))) s.parry = true;
            if (input.GetKeyUp(c.charge) || (pad && Input.GetKeyUp(c.JoystickButtonName(1)))) s.chargeRelease = true;

            // DAgger: the shadow expert's virtual keys (never a gamepad)
            BotInputProvider ex = ShadowExpertInput(c);
            if (ex != null)
            {
                if (s.expert == null) s.expert = new Slot { id = s.id };
                var e = s.expert;
                bool exUp = VerticalInput(c, ex) > 0.5f;
                if (ex.GetKeyDown(c.up) || (exUp && !e.prevAxisUp)) e.jump = true;
                e.prevAxisUp = exUp;
                if (ex.GetKeyDown(c.down)) e.drop = true;
                if (ex.GetKeyDown(c.lightAttack)) e.light = true;
                if (ex.GetKeyDown(c.heavyAttack)) e.heavy = true;
                if (ex.GetKeyDown(c.ability)) e.special = true;
                if (ex.GetKeyDown(c.parry)) e.parry = true;
                if (ex.GetKeyUp(c.charge)) e.chargeRelease = true;
            }
        }
    }

    // DAgger: virtual keyboard of a SimpleBotController running in shadow mode on this character (null = none).
    private static BotInputProvider ShadowExpertInput(Character c)
    {
        var bot = c.GetComponentInParent<SimpleBotController>();
        return bot != null && bot.isActiveAndEnabled && bot.ShadowMode ? bot.ShadowInput : null;
    }

    // ---------------- records (every decisionPeriod fixed steps) ----------------
    private void FixedUpdate()
    {
        if (!enableLogging || !IsFightRunning()) { fixedStepCounter = 0; return; }

        fixedStepCounter++;
        if (fixedStepCounter < Mathf.Max(1, decisionPeriod)) return;
        fixedStepCounter = 0;

        string matchId = TelemetryManager.Instance.CurrentMatchId;
        if (matchId != writingMatchId)
        {
            AbortCurrentFile("new match started before the previous one closed");
            OpenFile(matchId);
            if (writer == null) return;
        }

        Character p1 = ResolveSlot(slots[0]);
        Character p2 = ResolveSlot(slots[1]);
        if (p1 == null || p2 == null) return;

        if (tick == 0) firstTickTime = Time.time;
        float t = Time.time - firstTickTime;

        WriteRecord(matchId, slots[0], p1, p2, t);
        WriteRecord(matchId, slots[1], p2, p1, t);
        slots[0].ClearPresses();
        slots[1].ClearPresses();
        tick++;
    }

    private void WriteRecord(string matchId, Slot s, Character self, Character opp, float t)
    {
        IInputProvider input = self.GetInputProvider();
        if (input == null) return;

        var meta = TelemetryManager.Instance.CurrentMeta;
        string profileId = meta == null ? "" : (s.id == "P1" ? meta.p1ProfileId : meta.p2ProfileId);

        var rec = new Record
        {
            matchId = matchId,
            tick = tick,
            t = t,
            slot = s.id,
            profileId = profileId ?? "",
            playerKind = PlayerKind(input),
            model = ModelName(self, input),
            character = meta == null ? "" : (s.id == "P1" ? meta.p1Character : meta.p2Character),
            selfCharacterId = self.characterID,
            oppCharacterId = opp.characterID,
            selfHp = self.GetCurrentHealth(),
            oppHp = opp.GetCurrentHealth(),
            obs = FighterObservationEncoder.Encode(self, opp, relXScale, relYScale, velScale, totalCharacterCount),
            act = EncodeActions(s, self, input)
        };

        BotInputProvider ex = ShadowExpertInput(self);
        if (ex != null && s.expert != null)
        {
            rec.expertAct = EncodeExpertActions(s.expert, self, ex);
            rec.expertPersona = self.GetComponentInParent<SimpleBotController>().PersonaId;
        }

        if (input is ILAgentInputProvider)
        {
            var ilc = self.GetComponentInParent<ILAgentController>();
            if (ilc != null) rec.persona = ilc.Label;
        }

        var bot = (input is BotInputProvider) ? self.GetComponentInParent<SimpleBotController>() : null;
        if (bot != null)
        {
            rec.persona = bot.PersonaId;
            rec.aggression = bot.Aggression;
            rec.defense = bot.DefenseBias;
            rec.mobility = bot.MobilityBias;
            rec.patience = bot.Patience;
            rec.skill = bot.Skill;
        }

        writer.WriteLine(JsonUtility.ToJson(rec));
    }

    private int[] EncodeActions(Slot s, Character c, IInputProvider input)
    {
        bool pad = c.UsesController;

        float h = HorizontalInput(c, input);
        int move = h < -0.5f ? 0 : (h > 0.5f ? 2 : 1);

        bool blockHeld = input.GetKey(c.block) || (pad && Input.GetKey(c.JoystickButtonName(5)));
        bool chargeHeld = input.GetKey(c.charge) || (pad && Input.GetKey(c.JoystickButtonName(1)));
        int charge = s.chargeRelease ? 2 : (chargeHeld ? 1 : 0);

        return new[]
        {
            move,
            s.jump ? 1 : 0,
            s.drop ? 1 : 0,
            s.light ? 1 : 0,
            s.heavy ? 1 : 0,
            blockHeld ? 1 : 0,
            s.special ? 1 : 0,
            charge,
            s.parry ? 1 : 0
        };
    }

    // DAgger: same encoding as EncodeActions, for the shadow expert's virtual keys (no gamepad).
    private static int[] EncodeExpertActions(Slot e, Character c, BotInputProvider ex)
    {
        float h = HorizontalInput(c, ex);
        int move = h < -0.5f ? 0 : (h > 0.5f ? 2 : 1);
        int charge = e.chargeRelease ? 2 : (ex.GetKey(c.charge) ? 1 : 0);
        return new[]
        {
            move, e.jump ? 1 : 0, e.drop ? 1 : 0, e.light ? 1 : 0, e.heavy ? 1 : 0,
            ex.GetKey(c.block) ? 1 : 0, e.special ? 1 : 0, charge, e.parry ? 1 : 0
        };
    }

    // ---------------- helpers: input exactly like Character.GetHorizontal/VerticalInput ----------------
    private static float HorizontalInput(Character c, IInputProvider input)
    {
        if (!c.UsesController)
        {
            float m = 0f;
            if (input.GetKey(c.left)) m -= 1f;
            if (input.GetKey(c.right)) m += 1f;
            return m;
        }
        return input.GetAxis("Horizontal" + c.playerString);
    }

    private static float VerticalInput(Character c, IInputProvider input)
    {
        if (!c.UsesController)
        {
            float m = 0f;
            if (input.GetKey(c.down)) m -= 1f;
            if (input.GetKey(c.up)) m += 1f;
            return m;
        }
        return input.GetAxis("Vertical" + c.playerString);
    }

    private static string PlayerKind(IInputProvider input)
    {
        if (input is KeyboardInputProvider) return "human";
        if (input is BotInputProvider) return "scripted";
        if (input is AIInputProvider) return "rl";
        if (input is ILAgentInputProvider) return "il_agent";
        if (input is ReplayInputProvider) return "replay";
        return input.GetType().Name;
    }

    private static string ModelName(Character c, IInputProvider input)
    {
        if (!(input is AIInputProvider)) return "";
        var bp = c.GetComponentInParent<BehaviorParameters>();
        return (bp != null && bp.Model != null) ? bp.Model.name : "";
    }

    // ---------------- helpers: players / session ----------------
    private static bool IsFightRunning()
    {
        var tm = TelemetryManager.Instance;
        return tm != null && tm.FightStarted && !string.IsNullOrEmpty(tm.CurrentMatchId);
    }

    private Character ResolveSlot(Slot s)
    {
        if (s.root != null)
        {
            Character active = Character.ResolveActiveCharacter(s.root);   // follows Lupen's active form
            if (active != null && active.isActiveAndEnabled && active.PlayerId == s.id) return active;
        }

        if (Time.unscaledTime < nextRefindTime) return null;
        nextRefindTime = Time.unscaledTime + 0.5f;

        // One search fills BOTH slots (a shared throttle per slot delayed P2 by 0.5 s at fight start).
        foreach (var c in FindObjectsOfType<Character>())
        {
            if (!c.isActiveAndEnabled) continue;
            foreach (var other in slots)
                if (c.PlayerId == other.id) other.root = c;
        }
        return s.root != null ? Character.ResolveActiveCharacter(s.root) : null;
    }

    private void OpenFile(string matchId)
    {
        try
        {
            var meta = TelemetryManager.Instance.CurrentMeta;
            string env = !string.IsNullOrEmpty(TelemetryManager.OutputFolderOverride)
                ? TelemetryManager.OutputFolderOverride          // data generation -> ImitationLogs/Synthetic
                : ((meta != null && !string.IsNullOrEmpty(meta.runtimeEnvironment))
                    ? meta.runtimeEnvironment
                    : (Application.isEditor ? "Editor" : "Build"));

            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "My Games", "Head of Hell", "ImitationLogs", env);
            Directory.CreateDirectory(dir);

            tmpPath = Path.Combine(dir, $"tmp_{matchId}.jsonl");
            writer = new StreamWriter(tmpPath, false, new UTF8Encoding(false));
            writingMatchId = matchId;
            tick = 0;
            foreach (var s in slots) { s.ClearPresses(); s.prevAxisUp = false; }

            if (logToConsole) Debug.Log($"[ImitationLoggerV2] Recording {matchId} -> {tmpPath}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[ImitationLoggerV2] Could not open log file: " + ex.Message);
            writer = null;
            writingMatchId = null;
        }
    }

    private void OnSessionClosed(string matchId, bool saved)
    {
        if (writer == null || matchId != writingMatchId) return;

        string path = tmpPath;
        CloseWriter();

        try
        {
            if (saved)
            {
                string final = Path.Combine(Path.GetDirectoryName(path), $"match_{matchId}.jsonl");
                if (File.Exists(final)) File.Delete(final);
                File.Move(path, final);
                if (logToConsole) Debug.Log($"[ImitationLoggerV2] Saved {final} ({tick} ticks)");
            }
            else
            {
                if (File.Exists(path)) File.Delete(path);
                if (logToConsole) Debug.Log($"[ImitationLoggerV2] Discarded {matchId}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[ImitationLoggerV2] Finalizing log failed: " + ex.Message);
        }
    }

    private void AbortCurrentFile(string reason)
    {
        if (writer == null) return;
        string path = tmpPath;
        CloseWriter();
        try { if (File.Exists(path)) File.Delete(path); } catch { }
        if (logToConsole) Debug.Log($"[ImitationLoggerV2] Dropped unfinished log ({reason})");
    }

    private void CloseWriter()
    {
        try { writer?.Flush(); writer?.Dispose(); } catch { }
        writer = null;
        writingMatchId = null;
        tmpPath = null;
    }
}
