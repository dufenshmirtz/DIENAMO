using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

// NOTE: We intentionally use plain serializable classes + JsonUtility for max Unity compatibility.
// JsonUtility is strict but perfect for our schema (no dictionaries, no polymorphism).

public class TelemetryManager : MonoBehaviour
{
    public static TelemetryManager Instance { get; private set; }

    [Header("Telemetry Settings")]
    [SerializeField] private bool enableTelemetry = true;
    [SerializeField] private bool debugToConsole = false;
    [SerializeField] private bool prettyPrintJson = true;
    [SerializeField] private bool autoStartOnFirstEvent = true;

    [Tooltip("If true, will write a session file on quit/destroy if recording is active.")]
    [SerializeField] private bool autoSaveOnQuit = true;

    // Session state
    private TelemetrySession currentSession;
    private bool isRecording = false;
    private float sessionStartTime;
    private string pendingEndReason = null;

    // Root telemetry folder
    private string telemetryRootFolderPath;

    private void Awake()
    {
        // Singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        telemetryRootFolderPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "My Games",
            "Head of Hell",
            "Telemetry"
        );

        #if UNITY_EDITOR
                string telemetryFolderPath = Path.Combine(telemetryRootFolderPath, "Editor");
        #else
        string telemetryFolderPath = Path.Combine(telemetryRootFolderPath, "Build");
        #endif

        Directory.CreateDirectory(telemetryRootFolderPath);
        Directory.CreateDirectory(Path.Combine(telemetryRootFolderPath, "Editor"));
        Directory.CreateDirectory(Path.Combine(telemetryRootFolderPath, "Build"));
        Directory.CreateDirectory(telemetryFolderPath); Directory.CreateDirectory(Path.Combine(telemetryRootFolderPath, "Build"));
    }

    // ---------------------------
    // Public API (Lifecycle)
    // ---------------------------

    /// <summary>
    /// Starts a new telemetry session (match). Safe to call multiple times; will end the previous session if needed.
    /// </summary>
    public void StartSession(TelemetryMatchMeta meta = null)
    {
        if (!enableTelemetry) return;

        // If already recording, end previous session cleanly
        if (isRecording)
        {
            if (CurrentSessionLooksInvalidOrEmpty())
            {
                DiscardCurrentSession("StartSession called while recording, discarded empty/invalid previous session");
            }
            else
            {
                EndSession("StartSession called while recording (auto-ended previous session)");
            }
        }

        sessionStartTime = Time.time;

        TelemetryMatchMeta finalMeta = meta ?? new TelemetryMatchMeta();

        // Runtime environment is always auto-filled here
        finalMeta.runtimeEnvironment = Application.isEditor ? "Editor" : "Build";
        finalMeta.isEditorSession = Application.isEditor;

        currentSession = new TelemetrySession
        {
            schemaVersion = "1.0",
            matchId = GenerateMatchId(),
            startedAtLocal = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            startedAtUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            unityVersion = Application.unityVersion,
            appVersion = Application.version,
            platform = Application.platform.ToString(),
            meta = finalMeta,
            events = new List<TelemetryEvent>(capacity: 2048)
        };

        isRecording = true;

        if (debugToConsole)
            Debug.Log($"[Telemetry] Session START: {currentSession.matchId} | env={currentSession.meta.runtimeEnvironment}");
    }

    // HELPER FOR TELEMETRY TO THROW EMPTY MATCHES AND NOT SAVE THEM
    private bool CurrentSessionLooksInvalidOrEmpty()
    {
        if (currentSession == null)
            return true;

        float duration = Time.time - sessionStartTime;
        int eventCount = currentSession.events != null ? currentSession.events.Count : 0;

        bool emptyMeta =
            currentSession.meta == null ||
            (
                string.IsNullOrWhiteSpace(currentSession.meta.map) &&
                string.IsNullOrWhiteSpace(currentSession.meta.p1Id) &&
                string.IsNullOrWhiteSpace(currentSession.meta.p2Id) &&
                string.IsNullOrWhiteSpace(currentSession.meta.p1ProfileId) &&
                string.IsNullOrWhiteSpace(currentSession.meta.p2ProfileId)
            );

        // πολύ μικρό session + άδεια meta + ελάχιστα/καθόλου events
        return duration < 3f && emptyMeta && eventCount <= 1;
    }

    private void DiscardCurrentSession(string reason)
    {
        if (debugToConsole)
            Debug.Log($"[Telemetry] Session DISCARD: {reason}");

        currentSession = null;
        isRecording = false;
        sessionStartTime = 0f;
    }

    /// <summary>
    /// Ends current session and writes a JSON file to disk.
    /// </summary>
    public void EndSession(string reason = "MatchEnded")
    {
        if (!enableTelemetry) return;
        if (!isRecording || currentSession == null) return;

        pendingEndReason = reason;

        currentSession.endedAtLocal = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        currentSession.endedAtUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        currentSession.endReason = pendingEndReason;
        currentSession.durationSeconds = Mathf.Max(0f, Time.time - sessionStartTime);

        WriteSessionToDisk(currentSession);

        if (debugToConsole)
            Debug.Log($"[Telemetry] Session END: {currentSession.matchId} | reason={reason} | events={currentSession.events?.Count ?? 0}");

        // Reset
        currentSession = null;
        isRecording = false;
        pendingEndReason = null;
    }

    /// <summary>
    /// Optional: update metadata while session is running.
    /// </summary>
    public void SetMatchMeta(TelemetryMatchMeta meta)
    {
        if (!enableTelemetry) return;
        EnsureSessionStarted();
        if (currentSession == null) return;

        if (currentSession.meta == null)
            currentSession.meta = new TelemetryMatchMeta();

        if (meta == null) return;

        // Merge (όχι replace)
        if (!string.IsNullOrEmpty(meta.map)) currentSession.meta.map = meta.map;
        if (!string.IsNullOrEmpty(meta.mode)) currentSession.meta.mode = meta.mode;
        if (meta.roundNumber_ != 0) currentSession.meta.roundNumber_ = meta.roundNumber_;

        // bool -> πάντα assign
        currentSession.meta.trainingMode = meta.trainingMode;

        if (!string.IsNullOrEmpty(meta.p1Id)) currentSession.meta.p1Id = meta.p1Id;
        if (!string.IsNullOrEmpty(meta.p1Character)) currentSession.meta.p1Character = meta.p1Character;

        if (!string.IsNullOrEmpty(meta.p2Id)) currentSession.meta.p2Id = meta.p2Id;
        if (!string.IsNullOrEmpty(meta.p2Character)) currentSession.meta.p2Character = meta.p2Character;

        // winner μπορεί να είναι "" σε tie, οπότε assign και όταν είναι empty
        if (meta.winnerId != null) currentSession.meta.winnerId = meta.winnerId;
        if (meta.winnerCharacter != null) currentSession.meta.winnerCharacter = meta.winnerCharacter;

        // Profile identity merge
        if (!string.IsNullOrEmpty(meta.p1ProfileId)) currentSession.meta.p1ProfileId = meta.p1ProfileId;
        if (!string.IsNullOrEmpty(meta.p1ProfileName)) currentSession.meta.p1ProfileName = meta.p1ProfileName;

        if (!string.IsNullOrEmpty(meta.p2ProfileId)) currentSession.meta.p2ProfileId = meta.p2ProfileId;
        if (!string.IsNullOrEmpty(meta.p2ProfileName)) currentSession.meta.p2ProfileName = meta.p2ProfileName;

        // Keep runtime environment auto-consistent
        currentSession.meta.runtimeEnvironment = Application.isEditor ? "Editor" : "Build";
        currentSession.meta.isEditorSession = Application.isEditor;
    }

    /// <summary>
    /// Optional helper for common metadata: player ids and character names.
    /// </summary>
    public void SetPlayers(string p1Id, string p1Character, string p2Id, string p2Character)
    {
        if (!enableTelemetry) return;
        EnsureSessionStarted();
        if (currentSession == null) return;

        currentSession.meta.p1Id = p1Id;
        currentSession.meta.p1Character = p1Character;
        currentSession.meta.p2Id = p2Id;
        currentSession.meta.p2Character = p2Character;
    }

    // ---------------------------
    // Public API (Existing Hooks)
    // ---------------------------

    public void LogAction(string actorId, string actionType)
    {
        if (!enableTelemetry) return;
        EnsureSessionStarted();

        var ev = TelemetryEvent.CreateBase(EventType.Action, sessionStartTime);
        ev.actorId = actorId;
        ev.actionType = actionType;

        AddEvent(ev);

        if (debugToConsole)
            Debug.Log($"[Telemetry] Action | t={ev.t:F3} actor={actorId} action={actionType}");
    }

    public void LogHitAttempt(string attackerId, string defenderId, MoveType moveType)
    {
        if (!enableTelemetry) return;
        EnsureSessionStarted();

        var ev = TelemetryEvent.CreateBase(EventType.HitAttempt, sessionStartTime);
        ev.attackerId = attackerId;
        ev.defenderId = defenderId;
        ev.moveType = moveType.ToString();

        AddEvent(ev);

        if (debugToConsole)
            Debug.Log($"[Telemetry] HitAttempt | t={ev.t:F3} {attackerId}->{defenderId} move={ev.moveType}");
    }

    public void LogMiss(string attackerId, MoveType moveType)
    {
        if (!enableTelemetry) return;
        EnsureSessionStarted();

        var ev = TelemetryEvent.CreateBase(EventType.Miss, sessionStartTime);
        ev.attackerId = attackerId;
        ev.moveType = moveType.ToString();

        AddEvent(ev);

        if (debugToConsole)
            Debug.Log($"[Telemetry] Miss | t={ev.t:F3} attacker={attackerId} move={ev.moveType}");
    }

    public void LogDamageApplied(
        string attackerId,
        string defenderId,
        MoveType moveType,
        SourceType sourceType,
        int finalDamage,
        int hpBefore,
        int hpAfter,
        float distance,
        bool wasBlocked,
        bool wasDodged
    )
    {
        if (!enableTelemetry) return;
        EnsureSessionStarted();

        var ev = TelemetryEvent.CreateBase(EventType.DamageApplied, sessionStartTime);
        ev.attackerId = attackerId;
        ev.defenderId = defenderId;
        ev.moveType = moveType.ToString();
        ev.sourceType = sourceType.ToString();
        ev.finalDamage = finalDamage;
        ev.wasBlocked = wasBlocked;
        ev.wasDodged = wasDodged;
        ev.hpDefenderBefore = hpBefore;
        ev.hpDefenderAfter = hpAfter;
        ev.distance = distance;

        AddEvent(ev);

        if (debugToConsole)
            Debug.Log($"[Telemetry] DamageApplied | t={ev.t:F3} {attackerId}->{defenderId} move={ev.moveType} src={ev.sourceType} dmg={finalDamage} blocked={wasBlocked} dodged={wasDodged}");
    }

    // ---------------------------
    // Internals
    // ---------------------------

    private void EnsureSessionStarted()
    {
        if (isRecording) return;

        if (!autoStartOnFirstEvent)
            return;

        StartSession(); // empty meta by default
    }

    private void AddEvent(TelemetryEvent ev)
    {
        if (currentSession == null) return;
        currentSession.events.Add(ev);
    }

    private void WriteSessionToDisk(TelemetrySession session)
    {
        try
        {
            string runtimeFolder =
                session.meta != null && !string.IsNullOrWhiteSpace(session.meta.runtimeEnvironment)
                    ? session.meta.runtimeEnvironment
                    : (Application.isEditor ? "Editor" : "Build");

            string finalFolder = Path.Combine(telemetryRootFolderPath, runtimeFolder);
            Directory.CreateDirectory(finalFolder);

            // Safe filename
            string safeStart = session.startedAtLocal.Replace(":", "-").Replace(" ", "_");
            string fileName = $"match_{safeStart}_{session.matchId}.json";
            string fullPath = Path.Combine(finalFolder, fileName);

            string json = JsonUtility.ToJson(session, prettyPrintJson);
            File.WriteAllText(fullPath, json);

            if (debugToConsole)
                Debug.Log($"[Telemetry] Wrote session to: {fullPath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Telemetry] Failed to write session JSON: {e}");
        }
    }

    private string GenerateMatchId()
    {
        // Short, unique enough for local datasets
        return Guid.NewGuid().ToString("N").Substring(0, 12);
    }

    private void OnApplicationQuit()
    {
        if (!autoSaveOnQuit) return;
        if (isRecording) EndSession("ApplicationQuit");
    }

    private void OnDestroy()
    {
        if (!autoSaveOnQuit) return;
        if (isRecording) EndSession("Destroyed");
    }
}

// ---------------------------
// Serializable data models
// ---------------------------

public enum EventType
{
    Action,
    HitAttempt,
    Miss,
    DamageApplied
}

[Serializable]
public class TelemetryMatchMeta
{
    // Existing
    public string map = "";
    public string mode = "1v1";

    // Round-level metadata
    public int roundNumber_ = 0;

    // Players
    public string p1Id = "";
    public string p1Character = "";

    public string p2Id = "";
    public string p2Character = "";
    
    public string p3Id = "";
    public string p3Character = "";

    // Outcome
    public string winnerId = "";          // "P1" / "P2" / empty for tie
    public string winnerCharacter = "";   // character name / empty for tie

    // Existing training flag
    public bool trainingMode = false;

    // Profile identity
    public string p1ProfileId = "";
    public string p1ProfileName = "";

    public string p2ProfileId = "";
    public string p2ProfileName = "";

    public string p3ProfileId = "";
    public string p3ProfileName = "";

    // NEW: runtime separation
    public string runtimeEnvironment = "";   // "Editor" / "Build"
    public bool isEditorSession = false;
}

[Serializable]
public class TelemetrySession
{
    public string schemaVersion;

    public string matchId;

    public string startedAtLocal;
    public string startedAtUtc;

    public string endedAtLocal;
    public string endedAtUtc;

    public string endReason;
    public float durationSeconds;

    public string unityVersion;
    public string appVersion;
    public string platform;

    public TelemetryMatchMeta meta;

    public List<TelemetryEvent> events;
}

[Serializable]
public class TelemetryEvent
{
    public string eventType;     // Action / HitAttempt / Miss / DamageApplied

    // Time info
    public float t;              // seconds since session start
    public int frame;            // Time.frameCount

    // Actor fields (varies by event)
    public string actorId;       // used by Action
    public string actionType;    // "Quick", "Heavy", "Special", "ChargeStart", etc.

    // Combat fields (varies by event)
    public string attackerId;
    public string defenderId;

    public string moveType;      // MoveType enum as string
    public string sourceType;    // SourceType enum as string

    public int finalDamage;      // DamageApplied only
    public bool wasBlocked;      // DamageApplied only
    public bool wasDodged;       // DamageApplied only

    public float distance;       // -1 if unknown
    public int hpDefenderBefore; // -1 if unknown
    public int hpDefenderAfter;  // -1 if unknown

    public static TelemetryEvent CreateBase(EventType type, float sessionStartTime)
    {
        return new TelemetryEvent
        {
            eventType = type.ToString(),
            t = Mathf.Max(0f, Time.time - sessionStartTime),
            frame = Time.frameCount,

            // defaults
            actorId = "",
            actionType = "",

            attackerId = "",
            defenderId = "",
            moveType = "",
            sourceType = "",

            finalDamage = 0,
            wasBlocked = false,
            wasDodged = false,

            distance = -1f,
            hpDefenderBefore = -1,
            hpDefenderAfter = -1
        };
    }
}

// These enums already exist in your project; keep them where they are.
// Included here only as reference if you want this file standalone.
// public enum MoveType { Quick, Heavy, Special, Charge, Projectile, ParryCounter, PoisonTick }
// public enum SourceType { Melee, Spell, Projectile, Dot, Parry }