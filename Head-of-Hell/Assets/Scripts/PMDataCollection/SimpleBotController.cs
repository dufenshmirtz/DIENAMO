using UnityEngine;

public class SimpleBotController : MonoBehaviour
{
    [Header("Runtime References")]
    [SerializeField] private Character character;
    [SerializeField] private CharacterSetup setup;
    [SerializeField] private Character target;

    [Header("Skill")]
    [Range(0f, 1f)]
    [SerializeField] private float skill = 0.6f;
    [SerializeField] private bool randomizePersonality = true;

    [Header("Movement")]
    [SerializeField] private float preferredRange = 1.25f;
    [SerializeField] private float tooCloseRange = 0.55f;
    [SerializeField] private float moveCommitMin = 0.10f;
    [SerializeField] private float moveCommitMax = 0.24f;
    [SerializeField] private float idleShimmyChance = 0.10f;

    [Header("Timing")]
    [SerializeField] private float thinkIntervalMin = 0.18f;
    [SerializeField] private float thinkIntervalMax = 0.28f;
    [SerializeField] private float actionCooldownMin = 0.35f;
    [SerializeField] private float actionCooldownMax = 0.80f;

    [Header("Base Action Chances")]
    [SerializeField] private float quickChance = 0.62f;
    [SerializeField] private float heavyChance = 0.20f;
    [SerializeField] private float specialChance = 0.08f;
    [SerializeField] private float jumpChance = 0.04f;
    [SerializeField] private float blockChance = 0.05f;

    [Header("Level")]
    [SerializeField] private float sameLevelTolerance = 1.5f;
    [SerializeField] private float engageRange = 1.35f;

    [Header("Platform Drop")]
    [SerializeField] private float dropHoldDuration = 0.20f;

    [Header("Human-like timing (theta5 teachers, 19/10)")]
    [Tooltip("Share of quick attacks for which the bot holds the direction TOWARD the opponent at the press. " +
             "Skipler's quick is a dash in the held direction (no key = right), before this ~50% dashed away. 1 - value = human-like mistakes.")]
    [Range(0f, 1f)] [SerializeField] private float quickAimChance = 0.9f;
    [Tooltip("Frames-worth of time the aimed direction is held after a quick press (covers script execution order).")]
    [SerializeField] private float quickAimHold = 0.05f;
    [Tooltip("Reactions to a rush / a charge happen after a perception delay instead of on the same frame the danger appears.")]
    [SerializeField] private bool humanReactionDelay = true;
    [Tooltip("Median reaction delay at skill 0 and at skill 1 (log-normal; skill 0.6 -> ~0.31 s, 0.9 -> ~0.27 s).")]
    [SerializeField] private float reactionMedianSkill0 = 0.38f;
    [SerializeField] private float reactionMedianSkill1 = 0.26f;
    [SerializeField] private float reactionSigma = 0.35f;
    [SerializeField] private float reactionMin = 0.15f;
    [SerializeField] private float reactionMax = 0.60f;
    [Tooltip("Walk commitment: a new walk (move != 0) is kept this long (humans ~0.5 s per walk; before 0.10-0.24 s). Stops keep moveCommitMin/Max.")]
    [SerializeField] private float walkCommitMin = 0.30f;
    [SerializeField] private float walkCommitMax = 0.60f;
    [Tooltip("Scales every random 'defend' block chance (humans block ~3.5x less often than the old bot). The defense knob still multiplies it.")]
    [SerializeField] private float blockFreqScale = 0.40f;
    [Tooltip("Scales every block hold (and the pause after it): fewer but longer blocks, like humans (~0.38 s vs 0.22 s).")]
    [SerializeField] private float blockHoldScale = 1.80f;
    [Tooltip("Scales parry choices (old bot parried ~2x more than humans).")]
    [SerializeField] private float parryScale = 0.90f;   // v2 pilot: 0.50 overshot (parries ~0 vs humans 0.038/s)
    [Tooltip("Scales charge starts (old bot charged ~4x more than humans).")]
    [SerializeField] private float chargeScale = 0.35f;
    [Tooltip("Human rhythm: decisions come in short bursts (2-4 quick decisions) separated by irregular pauses, instead of one every 0.18-0.28 s.")]
    [SerializeField] private bool humanRhythm = false;   // bot v4: off - no measurable effect in the v3 pilot (gaps identical to v2)
    [Tooltip("Chance that a normal decision starts a burst.")]
    [Range(0f, 1f)] [SerializeField] private float burstStartChance = 0.25f;
    [SerializeField] private int burstExtraMin = 1;
    [SerializeField] private int burstExtraMax = 3;
    [SerializeField] private float burstIntervalMin = 0.04f;
    [SerializeField] private float burstIntervalMax = 0.10f;
    [Tooltip("Inside a burst a decision may come this much before the previous action's cooldown ends (never during a heavy commit or a block).")]
    [SerializeField] private float burstCooldownSlack = 0.12f;
    [Tooltip("Outside bursts: log-normal pause between decisions (median, sigma, clamp).")]
    [SerializeField] private float thinkMedian = 0.22f;
    [SerializeField] private float thinkSigma = 0.45f;
    [SerializeField] private float thinkMinHuman = 0.08f;
    [SerializeField] private float thinkMaxHuman = 0.80f;
    [Tooltip("Free platform use: chance per decision to drop through the platform the bot stands on (mobility knob scales it).")]
    [SerializeField] private float platformDropChance = 0.15f;   // bot v4 (v3: 0.06 -> 0.08 drops/s, humans ~0.23)

    [Header("Bot v5 (19-20/10): special aim, charge awareness, human answer to heavies")]
    [Tooltip("Share of specials aimed at the opponent (Skipler's special is a dash in the held direction; no key = right).")]
    [Range(0f, 1f)] [SerializeField] private float specialAimChance = 0.9f;
    [Tooltip("Movement knows that a charging opponent hits only in FRONT: never walk into the front of a charge; from behind is fine.")]
    [SerializeField] private bool chargeAwareness = true;
    [Tooltip("Distance kept from the front of a charging opponent.")]
    [SerializeField] private float chargeSafeRange = 1.8f;
    [Tooltip("Perception delay before the movement reacts to a charge start (a fully charged opponent is always known).")]
    [SerializeField] private float chargeAwareDelay = 0.25f;
    [Tooltip("Chance per decision to dash (special) through a charging opponent in front (breaks the charge).")]
    [Range(0f, 1f)] [SerializeField] private float chargeDashChance = 0.5f;
    [Tooltip("Human answer to an opponent's heavy start nearby (after the perception delay): block / step back / parry / nothing. " +
             "A blocked heavy still deals 5 of 14 damage, so stepping out of range is preferred when not too close.")]
    [SerializeField] private bool heavyReaction = true;
    [SerializeField] private float heavyThreatRange = 1.6f;
    [SerializeField] private float heavyCloseRange = 1.0f;
    [SerializeField] private float heavyBlockWeight = 0.45f;
    [SerializeField] private float heavyStepWeight = 0.30f;
    [SerializeField] private float heavyParryWeight = 0.10f;
    [SerializeField] private float heavyNoneWeight = 0.35f;
    [SerializeField] private float heavyReactCooldown = 0.70f;

    [Header("Anti-Charge Reaction")]
    [SerializeField] private bool antiChargeEnabled = true;
    [SerializeField] private float antiChargeRange = 2.0f;
    [SerializeField] private float antiChargeHeightTolerance = 1.6f;
    [SerializeField] private float antiChargeReactionCooldown = 0.45f;
    [SerializeField] private float antiChargeBaseChance = 0.35f;
    [SerializeField] private float antiChargeSkillBonus = 0.50f;
    [SerializeField] private float antiChargeParryWeight = 0.42f;
    [SerializeField] private float antiChargeJumpWeight = 0.26f;
    [SerializeField] private float antiChargeRetreatWeight = 0.22f;
    [SerializeField] private float antiChargeBlockWeight = 0f;   // bot v5: was 0.10 - a block does NOT reduce charge damage
    [SerializeField] private float retreatDuration = 0.22f;
    [SerializeField] private float antiChargeSpecialChance = 0.92f;
    [SerializeField] private float antiChargeSpecialRange = 2f;
    [SerializeField] private float antiChargeSpecialCooldown = 0.30f;
    [SerializeField] private float antiChargeParryDelay = 0.5f;

    [Header("Post Action Reposition")]
    [SerializeField] private float repositionChance = 0.28f;
    [SerializeField] private float repositionMinDuration = 0.08f;
    [SerializeField] private float repositionMaxDuration = 0.18f;

    [Header("Vertical / Head-Stack Avoidance")]
    [SerializeField] private float headStackXThreshold = 0.55f;
    [SerializeField] private float headStackMinY = 0.65f;
    [SerializeField] private float headStackMaxY = 2.2f;
    [SerializeField] private float headStackEscapeMin = 0.18f;
    [SerializeField] private float headStackEscapeMax = 0.32f;
    [SerializeField] private float crossLevelFollowXThreshold = 1.6f;   // (no longer used by HandleMovement, kept for the Inspector)

    [Header("Cross-Level Pursuit")]
    [Tooltip("Target below: drop through the platform when closer than this in X (was 0.6).")]
    [SerializeField] private float crossLevelDropRange = 1.2f;
    [Tooltip("Target above: jump towards it when closer than this in X.")]
    [SerializeField] private float crossLevelJumpRange = 1.2f;
    [Tooltip("Chance to jump up per movement update, scaled by mobility (min..max).")]
    [SerializeField] private float jumpUpChanceMin = 0.35f;
    [SerializeField] private float jumpUpChanceMax = 0.80f;
    [Tooltip("The bot never holds a charge longer than this (s): release if the target is in range, else cancel.")]
    [SerializeField] private float maxBotChargeHold = 1.5f;
    [Tooltip("Character still charging although the bot already released/stopped (key-up lost) -> after this many seconds the bot takes the charge back and releases or cancels it.")]
    [SerializeField] private float orphanChargeTimeout = 0.3f;
    [Tooltip("Release a charged attack only when the target is this close in X (the character cannot move while charging; charge hits ~0.8, heavy up to ~1.3 in telemetry).")]
    [SerializeField] private float chargeHitRange = 1.1f;
    private float orphanChargeTimer;

    [Header("Heavy = main attack (2026-10-13)")]
    [Tooltip("After pressing heavy the bot makes no new decision for this long. The heavy hits at ~0.57 s and ANY other action (block, quick, special, parry, charge, heavy) cancels it; before this most bot heavies were cancelled by the bot itself.")]
    [SerializeField] private float heavyCommitTime = 0.62f;
    private float heavyCommitTimer;

    [Header("Persona strength (thesis; only after SetPersonality, knob 0.5 = x1)")]
    [Tooltip("Multiplier at knob 1.0 (knob 0.0 = 1/strength, 0.9 ~ strength^0.8). 1 = knobs behave as before.")]
    [SerializeField] private float personaStrengthAggression = 4f;
    [SerializeField] private float personaStrengthDefense = 4f;
    [SerializeField] private float personaStrengthMobility = 4f;
    [Tooltip("Preferred distance shift at aggression 0 (+) / 1 (-).")]
    [SerializeField] private float personaRangeShift = 0.4f;
    [Tooltip("Mobility 0: waits up to this long before chasing an opponent out of range (mobility >= 0.5: no waiting).")]
    [SerializeField] private float holdGroundMaxTime = 1.5f;
    private float outOfRangeSince = -1f;

    [Header("Anti-Charge Danger")]
    [SerializeField] private float antiChargeDangerRange = 1.35f;
    [SerializeField] private float antiChargeDangerHeightTolerance = 1.4f;
    [SerializeField] private float antiChargeEmergencyRetreatTime = 0.30f;
    [SerializeField] private float antiChargeEmergencyJumpChance = 0.75f;
    [SerializeField] private float antiChargeEmergencyParryChance = 0.55f;

    [Header("Rush Punish")]
    [SerializeField] private bool rushPunishEnabled = true;
    [SerializeField] private float rushPunishRangeMultiplier = 1.35f;
    [SerializeField] private float rushPunishVelocityThreshold = 0.45f;
    [SerializeField] private float rushPunishBaseChance = 0.82f;
    [SerializeField] private float rushPunishSkillBonus = 0.15f;
    [SerializeField] private float rushPunishQuickWeight = 1.55f;
    [SerializeField] private float rushPunishHeavyWeight = 1.35f;
    [SerializeField] private float rushPunishSpecialWeight = 1.10f;
    [SerializeField] private float rushPunishRetreatWeight = 0.05f;
    [SerializeField] private float rushPunishWaitWeight = 0.01f;
    [SerializeField] private float rushPunishLightCooldownMin = 0.12f;
    [SerializeField] private float rushPunishLightCooldownMax = 0.22f;
    [SerializeField] private float rushPunishHeavyCooldownMin = 0.18f;
    [SerializeField] private float rushPunishHeavyCooldownMax = 0.34f;
    [SerializeField] private float rushPunishSpecialCooldownMin = 0.22f;
    [SerializeField] private float rushPunishSpecialCooldownMax = 0.40f;
    [SerializeField] private float rushPunishRetreatDuration = 0.12f;

    [Header("Anti-Rush Defense")]
    [SerializeField] private bool antiRushDefenseEnabled = true;
    [SerializeField] private float antiRushDangerRange = 1.15f;
    [SerializeField] private float antiRushCriticalRange = 0.78f;
    [SerializeField] private float antiRushHeightTolerance = 1.25f;
    [SerializeField] private float antiRushApproachVelocity = 0.55f;
    [SerializeField] private float antiRushReactionCooldown = 0.20f;
    [SerializeField] private float antiRushBlockWeight = 1.20f;
    [SerializeField] private float antiRushParryWeight = 1.10f;
    [SerializeField] private float antiRushLightWeight = 1.45f;
    [SerializeField] private float antiRushHeavyWeight = 1.20f;
    [SerializeField] private float antiRushSpecialWeight = 0.85f;
    [SerializeField] private float antiRushRetreatWeight = 0.22f;
    [SerializeField] private float antiRushChargeWeight = 0.65f;
    [SerializeField] private float antiRushBlockHoldMin = 0.16f;
    [SerializeField] private float antiRushBlockHoldMax = 0.34f;
    [SerializeField] private float antiRushRetreatDuration = 0.18f;
    [SerializeField] private float antiRushParryWindowMin = 0.08f;
    [SerializeField] private float antiRushParryWindowMax = 0.20f;

    [Header("Bot Charge Counterplay")]
    [SerializeField] private bool proactiveChargeEnabled = true;
    [SerializeField] private float proactiveChargeStartRangeMin = 0.95f;
    [SerializeField] private float proactiveChargeStartRangeMax = 1.85f;
    [SerializeField] private float proactiveChargeApproachVelocity = 0.45f;
    [SerializeField] private float proactiveChargeChance = 0.65f;
    [SerializeField] private float proactiveChargeReleaseTime = 0.50f;
    [SerializeField] private float proactiveChargeEarlyReleaseRange = 0.95f;
    [SerializeField] private float proactiveChargeAbortRange = 2.2f;
    [SerializeField] private float proactiveChargeCooldown = 0.30f;

    private float aggression;
    private float defenseBias;
    private float mobilityBias;
    private float patience;

    // ---- Persona (thesis): a fixed, named personality instead of the per-round random one ----
    // Only active after SetPersonality() is called; otherwise the bot behaves exactly as before.
    private bool personalityLocked;
    private string personaId = "";
    private string personaReportedForMatch;

    public string PersonaId => personaId;
    public float Aggression => aggression;
    public float DefenseBias => defenseBias;
    public float MobilityBias => mobilityBias;
    public float Patience => patience;
    public float Skill => skill;

    public void SetPersonality(float aggression, float defense, float mobility, float patience, string personaId)
    {
        this.aggression = Mathf.Clamp01(aggression);
        this.defenseBias = Mathf.Clamp01(defense);
        this.mobilityBias = Mathf.Clamp01(mobility);
        this.patience = Mathf.Clamp01(patience);
        this.personaId = personaId ?? "";
        personalityLocked = true;
        personaReportedForMatch = null;
    }

    // Persona multiplier: strength^((knob-0.5)*2). Exactly 1 at knob 0.5 and whenever no persona is set
    // (normal PvE), so those bots are unchanged; symmetric: knob 0.9 multiplies by as much as 0.1 divides.
    private float PersonaMul(float knob, float strength)
    {
        if (!personalityLocked || strength <= 1f) return 1f;
        return Mathf.Pow(strength, (knob - 0.5f) * 2f);
    }
    private float AggrMul => PersonaMul(aggression, personaStrengthAggression);
    private float DefMul => PersonaMul(defenseBias, personaStrengthDefense);
    private float MobMul => PersonaMul(mobilityBias, personaStrengthMobility);
    private float BlockHoldMul => Mathf.Sqrt(DefMul);

    // Quick attacks with a cooldown (e.g. Skipler's Blink, 3 s) hide the quick indicator while recharging.
    // Pressing quick then does nothing, so the bot only picks quick when it is ready (no indicator -> assume ready).
    private bool QuickReady => setup == null || setup.quickAttackIndicator == null || setup.quickAttackIndicator.activeSelf;
    private float QuickReadyMul => QuickReady ? 1f : 0f;

    private float HoldGroundLimit()
    {
        if (!personalityLocked || mobilityBias >= 0.5f) return 0f;
        return holdGroundMaxTime * (0.5f - mobilityBias) * 2f;
    }

    // Heavy press + commitment: no new decision until the swing has hit.
    private void PressHeavy(float cooldownMin, float cooldownMax)
    {
        PressOneFrame(setup.heavyAttack);
        heavyCommitTimer = heavyCommitTime;
        shadowOwnHeavyCooldown = Random.Range(cooldownMin, cooldownMax);   // same random draw as before
        actionTimer = Mathf.Max(shadowOwnHeavyCooldown, heavyCommitTime);
        shadowSinceOwnHeavy = 0f;
        shadowCommitIsOwn = true;
    }

    // Writes the persona id into the current telemetry session once per match (p1PersonaId / p2PersonaId).
    private void ReportPersonaToTelemetry()
    {
        if (shadowMode || !personalityLocked || string.IsNullOrEmpty(personaId) || character == null) return;   // shadow: the agent's id stays
        var tm = TelemetryManager.Instance;
        if (tm == null || string.IsNullOrEmpty(tm.CurrentMatchId) || tm.CurrentMatchId == personaReportedForMatch) return;
        tm.SetPersona(character.PlayerId, personaId);
        personaReportedForMatch = tm.CurrentMatchId;
    }

    private bool trackingEnemyCharge;
    private float enemyChargeTimer;

    private BotInputProvider botInput;
    private float thinkTimer;
    private int burstLeft;
    private float actionTimer;
    private float blockHoldTimer;

    private float forcedVerticalTimer;
    private float forcedVerticalValue;
    private bool isDroppingThroughPlatform;

    private float antiChargeReactionTimer;

    private float forcedHorizontalTimer;
    private float forcedHorizontalValue;

    private float moveCommitTimer;
    private float committedMove;

    private bool initialized;

    private Transform selfTr;
    private Transform targetTr;

    private float antiRushReactionTimer;
    private float quickAimTimer;
    private float quickAimDir;
    private float rushPerceiveTimer = -1f;     // -1 idle, >0 perceiving, 0 perceived (react if the danger still holds)
    private float chargePerceiveTimer = -1f;
    private float heavyPerceiveTimer = -1f;
    private float heavyReactTimer;

    private bool botCharging;
    private float botChargeTimer;
    private float botChargeCooldownTimer;

    private float lastTargetDistanceX = 999f;

    // ---- DAgger shadow expert (thesis, 23/10) ----
    // The bot runs on a character that the IL AGENT controls: it decides every frame from the real game state, but writes
    // into its own BotInputProvider that is NOT connected to the character. ImitationLoggerV2 records these presses as the
    // expert label next to the agent's own action. Two intentions are re-synced every frame with what really happened:
    // the charge (botCharging) and the heavy commitment (heavyCommitTimer). The bot's rhythm (actionTimer) is kept.
    private bool shadowMode;
    private float shadowRealChargeTime;        // how long the character has really been charging / charged
    private float shadowSinceOwnCharge = 99f;  // time since the shadow itself started a charge
    private float shadowSinceOwnHeavy = 99f;   // time since the shadow itself pressed heavy
    private bool shadowCommitIsOwn;            // the current heavy commitment comes from the shadow's own (unfollowed?) press
    private float shadowOwnHeavyCooldown;      // the normal cooldown of that press (without the commitment)
    private bool shadowPrevHeavy;
    [Tooltip("Shadow mode: a charge / heavy the bot suggested is kept this long; if the agent has not done it by then, " +
             "the intention is dropped (one log record = 80 ms).")]
    [SerializeField] private float shadowFollowGrace = 0.10f;

    public bool ShadowMode => shadowMode;
    public BotInputProvider ShadowInput => shadowMode ? botInput : null;

    /// <summary>DAgger: run as a shadow expert on a character that someone else controls. Call before enabling.</summary>
    public void EnableShadow(Character self, Character enemy)
    {
        shadowMode = true;
        character = self;
        target = enemy;
        targetTr = target != null ? target.transform : null;
        setup = GetComponent<CharacterSetup>();
        selfTr = transform;
        if (botInput == null) botInput = new BotInputProvider();   // never connected to the character
        initialized = (character != null && setup != null && target != null);
    }

    // Every frame in shadow mode, before the decisions: the bot's intentions follow what the character really does.
    private void SyncShadowWithCharacter(float dt)
    {
        bool realCharge = character.IsCharging || character.IsCharged;
        shadowRealChargeTime = realCharge ? shadowRealChargeTime + dt : 0f;
        shadowSinceOwnCharge += dt;
        shadowSinceOwnHeavy += dt;

        if (botCharging && !realCharge && shadowSinceOwnCharge > shadowFollowGrace)
        {
            // suggested a charge, the agent is not charging: drop the intention silently (no "release" label)
            botInput.ForceKeyState(setup.charge, false);
            botCharging = false;
            botChargeTimer = 0f;
        }
        else if (!botCharging && realCharge)
        {
            // the agent charges on its own: the expert takes the charge over now (it decides when to release / cancel)
            botCharging = true;
            botChargeTimer = shadowRealChargeTime;
            botInput.ForceKeyState(setup.charge, true);
        }

        bool heavyNow = character.HeavyAttacking;
        if (heavyNow && !shadowPrevHeavy)
        {
            // the character really started a heavy (the agent's, or the suggested one followed): no new decision until it hits
            heavyCommitTimer = heavyCommitTime;
            actionTimer = Mathf.Max(actionTimer, heavyCommitTime);
            shadowCommitIsOwn = false;
        }
        else if (shadowCommitIsOwn && !heavyNow && heavyCommitTimer > 0f && shadowSinceOwnHeavy > shadowFollowGrace)
        {
            // the shadow suggested a heavy and the agent did not swing: drop the commitment, keep the normal cooldown
            heavyCommitTimer = 0f;
            actionTimer = shadowFollowAgent ? 0f   // B: the rhythm follows what the agent really did (nothing)
                : Mathf.Min(actionTimer, Mathf.Max(0f, shadowOwnHeavyCooldown - shadowSinceOwnHeavy));
            shadowCommitIsOwn = false;
        }
        shadowPrevHeavy = heavyNow;

        if (shadowFollowAgent) SyncShadowFollowAgent(dt);
    }

    // ---- DAgger option B (23/10): the teacher becomes "Markov in the agent's history" ----
    // Iteration 1 showed that the teacher's HELD intentions (walk commitment, block hold) and its RHYTHM (actionTimer)
    // are invisible in the agent's history -> the agent learned a per-decision average and flickered (θ7), or copied the
    // rate but not the timing of presses (θ7a). Here the shadow treats what the agent really does as its OWN choice:
    //  - the agent starts a walk / stops       -> the shadow commits to it (new walk commitment), re-decides when it expires
    //  - the agent starts a block              -> the shadow holds it (new block hold); the agent releases -> it ends too
    //  - the agent presses quick/special/jump/drop/parry without the shadow's suggestion -> the shadow's cooldown starts
    //    as if it had pressed it
    //  - the shadow's own suggestion (press or block) not followed within the grace time -> dropped, cooldown = 0
    //    (it may suggest again at its next decision, like the bot itself would in that situation)
    [Tooltip("DAgger option B: the shadow adopts the agent's walk / block / presses as its own intentions and its rhythm " +
             "follows the agent. Off = the iteration-1 shadow (only charge and heavy re-synced).")]
    [SerializeField] private bool shadowFollowAgent = true;
    private float shadowAgentMovePrev;
    private bool shadowAgentBlockPrev, shadowMyBlockPrev, shadowOwnBlockPending;
    private float shadowSinceOwnBlock = 99f;
    private static readonly int ShadowKeyCount = 5;   // 0 light, 1 special, 2 jump, 3 drop, 4 parry
    private readonly float[] shadowSinceOwnPress = { 99f, 99f, 99f, 99f, 99f };
    private readonly bool[] shadowOwnPressPending = new bool[5];

    private int ShadowKeyIndex(KeyCode key)
    {
        if (setup == null) return -1;
        if (key == setup.lightAttack) return 0;
        if (key == setup.ability) return 1;
        if (key == setup.up) return 2;
        if (key == setup.down) return 3;
        if (key == setup.parry) return 4;
        return -1;
    }

    private KeyCode ShadowKey(int i) =>
        i == 0 ? setup.lightAttack : i == 1 ? setup.ability : i == 2 ? setup.up : i == 3 ? setup.down : setup.parry;

    // Cooldown the bot itself uses after that press (ranges of DecideAction, engage range).
    private float ShadowAdoptedCooldown(int i) =>
        i == 0 ? Random.Range(0.15f, 0.36f) : i == 1 ? Random.Range(0.24f, 0.56f) : i == 2 ? Random.Range(0.20f, 0.38f)
        : i == 3 ? Random.Range(0.12f, 0.24f) : Random.Range(0.08f, 0.22f);

    private void SyncShadowFollowAgent(float dt)
    {
        IInputProvider agent = character.GetInputProvider();
        if (agent == null || ReferenceEquals(agent, botInput)) return;

        // --- walk / stop ---
        float agentMove = 0f;
        if (agent.GetKey(setup.left)) agentMove -= 1f;
        if (agent.GetKey(setup.right)) agentMove += 1f;
        if (Mathf.Abs(agentMove - shadowAgentMovePrev) > 0.01f && forcedHorizontalTimer <= 0f && quickAimTimer <= 0f &&
            !botCharging && Mathf.Abs(committedMove - agentMove) > 0.01f)
        {
            committedMove = agentMove;
            moveCommitTimer = Mathf.Abs(agentMove) > 0.01f ? Random.Range(walkCommitMin, walkCommitMax)
                                                            : Random.Range(moveCommitMin, moveCommitMax);
        }
        shadowAgentMovePrev = agentMove;

        // --- block ---
        bool agentBlock = agent.GetKey(setup.block);
        bool myBlock = botInput.GetKey(setup.block);
        shadowSinceOwnBlock += dt;
        if (myBlock && !shadowMyBlockPrev && !agentBlock) { shadowOwnBlockPending = true; shadowSinceOwnBlock = 0f; }
        if (agentBlock) shadowOwnBlockPending = false;
        if (agentBlock && !shadowAgentBlockPrev && !myBlock)
        {
            // the agent starts a block on its own: the shadow holds it like its own block
            float hold = Random.Range(0.10f, 0.24f) * BlockHoldMul * blockHoldScale;
            botInput.ForceKeyState(setup.block, true);
            blockHoldTimer = hold;
            actionTimer = Mathf.Max(actionTimer, hold);
            myBlock = true;
        }
        else if (!agentBlock && shadowAgentBlockPrev && myBlock)
        {
            // the agent released its block: the shadow's (adopted or followed) block ends with it
            botInput.ForceKeyState(setup.block, false);
            blockHoldTimer = 0f;
            myBlock = false;
        }
        else if (myBlock && !agentBlock && shadowOwnBlockPending && shadowSinceOwnBlock > shadowFollowGrace)
        {
            // suggested a block, the agent did not block: drop it, the rhythm restarts
            botInput.ForceKeyState(setup.block, false);
            blockHoldTimer = 0f;
            actionTimer = 0f;
            shadowOwnBlockPending = false;
            myBlock = false;
        }
        shadowAgentBlockPrev = agentBlock;
        shadowMyBlockPrev = myBlock;

        // --- one-shot presses: quick, special, jump, drop, parry (heavy: see the heavy sync above) ---
        for (int i = 0; i < ShadowKeyCount; i++)
        {
            shadowSinceOwnPress[i] += dt;
            bool agentPressed = agent.GetKeyDown(ShadowKey(i));
            if (agentPressed)
            {
                if (shadowSinceOwnPress[i] > shadowFollowGrace)
                    actionTimer = Mathf.Max(actionTimer, ShadowAdoptedCooldown(i));   // the agent's own press: cooldown as if ours
                shadowOwnPressPending[i] = false;
            }
            else if (shadowOwnPressPending[i] && shadowSinceOwnPress[i] > shadowFollowGrace)
            {
                actionTimer = 0f;   // suggested, not followed: the rhythm restarts
                shadowOwnPressPending[i] = false;
            }
        }
    }

    private void Start()
    {
        selfTr = transform;
        TryInitialize();
        RandomizePersonality();
    }

    private void OnEnable()
    {
        thinkTimer = 0f;
        burstLeft = 0;
        actionTimer = 0f;
        blockHoldTimer = 0f;

        forcedVerticalTimer = 0f;
        forcedVerticalValue = 0f;
        isDroppingThroughPlatform = false;

        antiChargeReactionTimer = 0f;
        forcedHorizontalTimer = 0f;
        forcedHorizontalValue = 0f;

        moveCommitTimer = 0f;
        committedMove = 0f;

        initialized = false;

        trackingEnemyCharge = false;
        enemyChargeTimer = 0f;

        antiRushReactionTimer = 0f;
        quickAimTimer = 0f;
        rushPerceiveTimer = -1f;
        chargePerceiveTimer = -1f;
        heavyPerceiveTimer = -1f;
        heavyReactTimer = 0f;

        botCharging = false;
        botChargeTimer = 0f;
        botChargeCooldownTimer = 0f;

        lastTargetDistanceX = 999f;
    }

    private void OnDisable()
    {
        StopBotCharge();

        if (botInput != null)
            botInput.ClearFrameState();
    }

    private void Update()
    {
        if (!initialized)
        {
            TryInitialize();
            return;
        }

        ReportPersonaToTelemetry();

        if (character == null || setup == null || target == null)
        {
            target = FindOpponent();
            targetTr = target != null ? target.transform : null;
            if (character == null || setup == null || target == null)
                return;
        }

        botInput.ClearFrameState();

        float dt = Time.deltaTime;

        thinkTimer -= dt;
        actionTimer -= dt;
        blockHoldTimer -= dt;
        antiChargeReactionTimer -= dt;
        antiRushReactionTimer -= dt;
        quickAimTimer -= dt;
        if (rushPerceiveTimer > 0f) rushPerceiveTimer = Mathf.Max(0f, rushPerceiveTimer - dt);
        if (chargePerceiveTimer > 0f) chargePerceiveTimer = Mathf.Max(0f, chargePerceiveTimer - dt);
        if (heavyPerceiveTimer > 0f) heavyPerceiveTimer = Mathf.Max(0f, heavyPerceiveTimer - dt);
        heavyReactTimer -= dt;
        botChargeCooldownTimer -= dt;
        heavyCommitTimer -= dt;

        UpdateForcedVertical(dt);
        UpdateForcedHorizontal(dt);
        UpdateEnemyChargeTracking(dt);

        if (blockHoldTimer <= 0f)
            botInput.SetKey(setup.block, false);

        if (shadowMode) SyncShadowWithCharacter(dt);   // DAgger: intentions follow the real character
        else CheckOrphanCharge(dt);

        if (HandleBotChargeState(dt))
        {
            HandleMovement(dt);
            UpdateLastTargetDistance();
            return;
        }

        if (TryReactToCharge())
        {
            HandleMovement(dt);
            UpdateLastTargetDistance();
            return;
        }

        // Bot v5: human answer to an opponent's heavy (before the generic anti-rush, which prefers counter-attacks).
        if (heavyCommitTimer <= 0f && TryReactToHeavy())
        {
            HandleMovement(dt);
            UpdateLastTargetDistance();
            return;
        }

        // Anti-rush ignores actionTimer, so it must not cancel our own heavy swing.
        if (heavyCommitTimer <= 0f && TryReactToRush())
        {
            HandleMovement(dt);
            UpdateLastTargetDistance();
            return;
        }

        HandleMovement(dt);

        if (thinkTimer <= 0f)
        {
            thinkTimer = NextThinkInterval();
            DecideAction();
        }

        UpdateLastTargetDistance();
    }

    private void TryInitialize()
    {
        if (setup == null) setup = GetComponent<CharacterSetup>();
        if (character == null) character = GetComponent<Character>();

        if (setup == null || character == null) return;

        if (selfTr == null)
            selfTr = transform;

        if (botInput == null)
        {
            botInput = new BotInputProvider();
            if (!shadowMode) character.SetInput(botInput);   // DAgger shadow: never take control
        }

        if (target == null)
            target = FindOpponent();

        targetTr = target != null ? target.transform : null;

        if (target != null)
            initialized = true;
    }

    private void RandomizePersonality()
    {
        if (personalityLocked) return;   // persona set explicitly -> keep it

        if (!randomizePersonality)
        {
            aggression = 0.5f;
            defenseBias = 0.5f;
            mobilityBias = 0.5f;
            patience = 0.5f;
            return;
        }

        aggression = Random.Range(0.25f, 0.85f);
        defenseBias = Random.Range(0.20f, 0.80f);
        mobilityBias = Random.Range(0.20f, 0.80f);
        patience = Random.Range(0.20f, 0.80f);
    }

    private void HandleMovement(float dt)
    {
        if (targetTr == null)
        {
            ApplyMovement(0f);
            return;
        }

        Vector3 myPos = selfTr.position;
        Vector3 targetPos = targetTr.position;

        float dxSigned = targetPos.x - myPos.x;
        float dySigned = targetPos.y - myPos.y;

        float dx = Mathf.Abs(dxSigned);
        float dy = Mathf.Abs(dySigned);

        bool sameLevel = dy <= sameLevelTolerance;
        bool targetBelow = dySigned < -sameLevelTolerance;
        bool inEngageRange = dx <= engageRange && sameLevel;

        bool headStacking = IsHeadStackingTarget(dx, dySigned);

        if (headStacking)
        {
            float escapeDir = -Mathf.Sign(dxSigned);

            if (Mathf.Abs(escapeDir) < 0.01f)
                escapeDir = Random.value < 0.5f ? -1f : 1f;

            forcedHorizontalValue = escapeDir;
            forcedHorizontalTimer = Random.Range(headStackEscapeMin, headStackEscapeMax);
            moveCommitTimer = 0f;
            committedMove = 0f;

            ApplyMovement(forcedHorizontalValue);
            return;
        }

        if (forcedHorizontalTimer > 0f)
        {
            ApplyMovement(forcedHorizontalValue);
            return;
        }

        // Bot v5 (H): a charging opponent hits only in front -> keep out of its front, wait for the release; behind is fine.
        if (ChargeThreatFront(dxSigned, dx, sameLevel))
        {
            if (dx < chargeSafeRange)
            {
                float away = -Mathf.Sign(dxSigned);
                if (Mathf.Abs(away) < 0.01f) away = Random.value < 0.5f ? -1f : 1f;
                committedMove = away;
                moveCommitTimer = Random.Range(0.12f, 0.22f);
            }
            else
            {
                committedMove = 0f;
                moveCommitTimer = Random.Range(0.08f, 0.16f);
            }
            ApplyMovement(committedMove);
            return;
        }

        // A committed walk toward the opponent ends early when it would run into them.
        if (moveCommitTimer > 0f && Mathf.Abs(committedMove) > 0.01f && sameLevel &&
            Mathf.Sign(committedMove) == Mathf.Sign(dxSigned) && dx < tooCloseRange)
            moveCommitTimer = 0f;

        if (moveCommitTimer > 0f)
        {
            moveCommitTimer -= dt;
            ApplyMovement(committedMove);
            return;
        }

        float move = 0f;

        if (targetBelow && dx < crossLevelDropRange && !isDroppingThroughPlatform && !headStacking)
        {
            bool enemyChargingOrReady =
                target != null &&
                (target.IsCharging || target.IsCharged);

            if (!enemyChargingOrReady)
            {
                float dropChance = Mathf.Lerp(0.4f, 0.75f, skill);

                if (Random.value < dropChance)
                {
                    TriggerDropPlatform();
                }
                else
                {
                    move = Random.value < 0.5f ? -1f : 1f;
                }
            }
            else
            {
                move = -Mathf.Sign(dxSigned);

                if (Mathf.Abs(move) < 0.01f)
                    move = Random.value < 0.5f ? -1f : 1f;
            }
        }
        else
        {
            float desiredRange =
                preferredRange
                + Mathf.Lerp(0.22f, -0.18f, aggression)
                + Mathf.Lerp(-0.05f, 0.18f, defenseBias)
                + (personalityLocked ? (0.5f - aggression) * 2f * personaRangeShift : 0f);

            float retreatRange =
                tooCloseRange
                + Mathf.Lerp(-0.04f, 0.08f, defenseBias);

            desiredRange = Mathf.Max(retreatRange + 0.08f, desiredRange);

            if (!sameLevel)
            {
                // Cross-level pursuit (was: stand still when 0.6 < dx < 1.6 -> two bots on different levels froze).
                bool targetAbove = dySigned > sameLevelTolerance;

                if (targetAbove && dx <= crossLevelJumpRange)
                {
                    // Platforms are one-way: jump straight up through them, drifting towards the target.
                    move = dx > 0.15f ? Mathf.Sign(dxSigned) : 0f;

                    float jumpUpChance = Mathf.Lerp(jumpUpChanceMin, jumpUpChanceMax, mobilityBias);
                    if (character.IsGrounded && !character.JumpDisabled && Random.value < jumpUpChance)
                        PressOneFrame(setup.up);
                }
                else
                {
                    // Line up in X with the target (then the drop / jump rules above take over).
                    move = Mathf.Sign(dxSigned);
                }
            }
            else if (!inEngageRange)
            {
                if (outOfRangeSince < 0f) outOfRangeSince = Time.time;

                if (dx > desiredRange)
                    // Low-mobility persona: hold ground for a while before chasing (time-limited -> no stalemate).
                    move = (Time.time - outOfRangeSince < HoldGroundLimit()) ? 0f : Mathf.Sign(dxSigned);
                else if (dx < retreatRange)
                    move = -Mathf.Sign(dxSigned);
            }
            else
            {
                outOfRangeSince = -1f;
                float shimmyChance = Mathf.Min(0.9f, idleShimmyChance * Mathf.Lerp(0.7f, 1.5f, mobilityBias) * MobMul);

                if (Random.value < shimmyChance)
                {
                    if (dx < retreatRange + 0.05f)
                        move = -Mathf.Sign(dxSigned);
                    else
                        move = Random.value < 0.55f ? Mathf.Sign(dxSigned) : -Mathf.Sign(dxSigned);
                }
            }
        }

        committedMove = Mathf.Clamp(move, -1f, 1f);
        moveCommitTimer = Mathf.Abs(committedMove) > 0.01f
            ? Random.Range(walkCommitMin, walkCommitMax)      // a walk: commit like a human
            : Random.Range(moveCommitMin, moveCommitMax);     // standing: re-decide soon

        ApplyMovement(committedMove);
    }

    private void ApplyMovement(float move)
    {
        if (quickAimTimer > 0f) move = quickAimDir;   // a quick was just pressed: keep the dash direction

        string suffix = GetAxisSuffix();

        botInput.SetAxis("Horizontal" + suffix, move);
        botInput.SetAxis("Horizontal" + setup.playerNum, move);
        botInput.SetAxis("Horizontal", move);

        // Character reads GetKey(left/right) when no physical gamepad is mapped to this side,
        // so press the keys too. Otherwise the bot only moves when a gamepad happens to be plugged in.
        botInput.SetKey(setup.left, move < -0.1f);
        botInput.SetKey(setup.right, move > 0.1f);

        float vertical = forcedVerticalTimer > 0f ? forcedVerticalValue : 0f;

        botInput.SetAxis("Vertical" + suffix, vertical);
        botInput.SetAxis("Vertical" + setup.playerNum, vertical);
        botInput.SetAxis("Vertical", vertical);
    }

    private void DecideAction()
    {
        // Inside a burst the next decision may come slightly before the cooldown ends (human chains), but never while a
        // heavy is committed or a block is held (that would cancel them).
        bool burstEarly = humanRhythm && burstLeft > 0 && actionTimer <= burstCooldownSlack &&
                          heavyCommitTimer <= 0f && blockHoldTimer <= 0f;
        if ((actionTimer > 0f && !burstEarly) || isDroppingThroughPlatform || targetTr == null)
            return;

        // Free platform use (humans drop through platforms far more often than the old bot).
        if (character.CanDropPlatform && character.IsGrounded && !character.IsCasting &&
            Random.value < platformDropChance * Mathf.Lerp(0.6f, 1.6f, mobilityBias) * MobMul)
        {
            TriggerDropPlatform();
            actionTimer = Random.Range(0.12f, 0.24f);
            return;
        }

        Vector3 myPos = selfTr.position;
        Vector3 targetPos = targetTr.position;

        float dxSigned = targetPos.x - myPos.x;
        float dySigned = targetPos.y - myPos.y;

        float dx = Mathf.Abs(dxSigned);
        float dy = Mathf.Abs(dySigned);

        bool sameLevel = dy <= sameLevelTolerance;
        bool inEngageRange = dx <= engageRange && sameLevel;
        bool inCloseRange = dx <= tooCloseRange && sameLevel;
        bool inPokeRange = dx <= engageRange * 1.20f && sameLevel;

        bool rushPunishState = IsRushPunishState(dx, dy, dxSigned, sameLevel);

        // Bot v5 (H): no attacks into the front of a charge; the special dash goes through it and breaks it.
        if (ChargeThreatFront(dxSigned, dx, sameLevel) && dx < chargeSafeRange + 0.3f)
        {
            if (CanUseSpecialNow() && dx <= antiChargeSpecialRange && Random.value < chargeDashChance)
            {
                PressSpecial(dxSigned);
                actionTimer = Random.Range(0.30f, 0.50f);
            }
            else
                actionTimer = Random.Range(0.08f, 0.16f);
            return;
        }

        if (TryStartProactiveCharge(dx, dy, dxSigned, sameLevel))
            return;

        float hesitateChance = Mathf.Lerp(0.03f, 0.18f, patience);
        if (Random.value < hesitateChance && !rushPunishState)
        {
            actionTimer = Random.Range(0.06f, 0.14f);
            return;
        }

        if (rushPunishState)
        {
            if (TryRushPunish(dxSigned))
                return;
        }

        float defendChance =
            Mathf.Lerp(blockChance, blockChance + 0.28f, defenseBias) *
            Mathf.Lerp(0.75f, 1.15f, skill);
        defendChance = Mathf.Min(0.6f, defendChance * DefMul) * blockFreqScale;

        if (inEngageRange && Random.value < defendChance)
        {
            botInput.SetKey(setup.block, true);
            blockHoldTimer = Random.Range(0.10f, 0.24f) * BlockHoldMul * blockHoldScale;
            actionTimer = Random.Range(0.16f, 0.28f) * BlockHoldMul * blockHoldScale;
            MaybeRepositionAfterAction(dxSigned, false);
            return;
        }

        if (!inPokeRange)
        {
            float jumpInChance = jumpChance * Mathf.Lerp(0.7f, 2.0f, mobilityBias) * MobMul;

            if (character.IsGrounded && !character.JumpDisabled && Random.value < jumpInChance)
            {
                PressOneFrame(setup.up);
                actionTimer = Random.Range(0.22f, 0.38f);
                MaybeRepositionAfterAction(dxSigned, true);
            }

            return;
        }

        if (inCloseRange)
        {
            float quickW = quickChance * Mathf.Lerp(1.00f, 1.50f, aggression) * AggrMul * QuickReadyMul;
            float heavyW = heavyChance * Mathf.Lerp(0.95f, 1.45f, aggression) * AggrMul;
            float retreatW = Mathf.Lerp(0.04f, 0.18f, defenseBias) * DefMul / AggrMul;
            float specialW = CanUseSpecialNow() ? specialChance * Mathf.Lerp(0.75f, 1.35f, aggression) * AggrMul : 0f;
            float waitW = Mathf.Lerp(0.01f, 0.08f, patience) / AggrMul;

            float total = quickW + heavyW + retreatW + specialW + waitW;
            float roll = Random.value * total;

            if (roll < quickW)
            {
                PressQuick(dxSigned);
                actionTimer = Random.Range(0.15f, 0.26f);
                MaybeRepositionAfterAction(dxSigned, true);
                return;
            }
            roll -= quickW;

            if (roll < heavyW)
            {
                PressHeavy(0.22f, 0.36f);
                MaybeRepositionAfterAction(dxSigned, true);
                return;
            }
            roll -= heavyW;

            if (roll < retreatW)
            {
                ForceRetreatFromTarget(dxSigned, 0.14f);
                actionTimer = 0.14f;
                return;
            }
            roll -= retreatW;

            if (roll < specialW)
            {
                PressSpecial(dxSigned);
                actionTimer = Random.Range(0.24f, 0.42f);
                MaybeRepositionAfterAction(dxSigned, true);
                return;
            }

            actionTimer = Random.Range(0.06f, 0.14f);
            return;
        }

        {
            float quickW = quickChance * Mathf.Lerp(0.80f, 1.20f, aggression) * AggrMul * QuickReadyMul;
            float heavyW = heavyChance * Mathf.Lerp(0.70f, 1.15f, aggression) * Mathf.Lerp(0.85f, 1.15f, patience) * AggrMul;
            float specialW = CanUseSpecialNow() ? specialChance * Mathf.Lerp(0.75f, 1.35f, aggression) * AggrMul : 0f;
            float jumpW = jumpChance * Mathf.Lerp(0.60f, 1.80f, mobilityBias) * MobMul;
            float blockW = blockChance * Mathf.Lerp(0.80f, 1.80f, defenseBias) * DefMul * blockFreqScale;
            float waitW = Mathf.Lerp(0.05f, 0.22f, patience) / AggrMul;

            float total = quickW + heavyW + specialW + jumpW + blockW + waitW;
            float roll = Random.value * total;

            if (roll < quickW)
            {
                PressQuick(dxSigned);
                actionTimer = Random.Range(0.20f, 0.36f);
                MaybeRepositionAfterAction(dxSigned, true);
                return;
            }
            roll -= quickW;

            if (roll < heavyW)
            {
                PressHeavy(0.28f, 0.46f);
                MaybeRepositionAfterAction(dxSigned, true);
                return;
            }
            roll -= heavyW;

            if (roll < specialW)
            {
                PressSpecial(dxSigned);
                actionTimer = Random.Range(0.32f, 0.56f);
                MaybeRepositionAfterAction(dxSigned, true);
                return;
            }
            roll -= specialW;

            if (roll < jumpW)
            {
                if (character.IsGrounded && !character.JumpDisabled)
                {
                    PressOneFrame(setup.up);
                    actionTimer = Random.Range(0.20f, 0.34f);
                    MaybeRepositionAfterAction(dxSigned, true);
                    return;
                }
            }
            roll -= jumpW;

            if (roll < blockW)
            {
                botInput.SetKey(setup.block, true);
                blockHoldTimer = Random.Range(0.10f, 0.22f) * BlockHoldMul * blockHoldScale;
                actionTimer = Random.Range(0.15f, 0.26f) * BlockHoldMul * blockHoldScale;
                MaybeRepositionAfterAction(dxSigned, false);
                return;
            }

            actionTimer = Random.Range(0.08f, 0.18f);
        }
    }

    private bool TryReactToRush()
    {
        if (!antiRushDefenseEnabled) return false;
        if (antiRushReactionTimer > 0f) return false;
        if (character == null || target == null || targetTr == null) return false;
        if (character.IsCasting || character.IsStunned || character.IsKnocked) return false;

        Vector3 myPos = selfTr.position;
        Vector3 targetPos = targetTr.position;

        float dxSigned = targetPos.x - myPos.x;
        float dySigned = targetPos.y - myPos.y;
        float dx = Mathf.Abs(dxSigned);
        float dy = Mathf.Abs(dySigned);

        if (dy > antiRushHeightTolerance)
        {
            PerceptionGate(ref rushPerceiveTimer, false);
            return false;
        }

        float targetVelX = GetTargetVelocityX();
        bool approachingFast =
            Mathf.Abs(targetVelX) >= antiRushApproachVelocity &&
            Mathf.Sign(targetVelX) == Mathf.Sign(dxSigned);

        bool targetAttacking = IsTargetAttackCommitted();
        bool distanceClosingFast = dx < lastTargetDistanceX - 0.03f;

        bool dangerousRush =
            dx <= antiRushDangerRange &&
            (approachingFast || targetAttacking || distanceClosingFast);

        if (!PerceptionGate(ref rushPerceiveTimer, dangerousRush))
            return false;

        antiRushReactionTimer = antiRushReactionCooldown;

        bool critical = dx <= antiRushCriticalRange;

        float blockW = antiRushBlockWeight * Mathf.Lerp(1.0f, 1.25f, defenseBias) * DefMul;
        float parryW = antiRushParryWeight * Mathf.Lerp(0.9f, 1.2f, skill) * DefMul * parryScale;
        float lightW = antiRushLightWeight * Mathf.Lerp(1.0f, 1.25f, aggression) * AggrMul * QuickReadyMul;
        float heavyW = antiRushHeavyWeight * Mathf.Lerp(0.95f, 1.20f, aggression) * AggrMul;
        float specialW = CanUseSpecialNow() ? antiRushSpecialWeight * AggrMul : 0f;
        float retreatW = antiRushRetreatWeight;
        float chargeW = CanStartBotCharge() ? antiRushChargeWeight * AggrMul * chargeScale : 0f;

        if (critical)
        {
            lightW *= 1.20f;
            blockW *= 1.20f;
            parryW *= 1.15f;
            heavyW *= 0.95f;
        }

        float total = blockW + parryW + lightW + heavyW + specialW + retreatW + chargeW;
        float roll = Random.value * total;

        if (roll < blockW)
        {
            botInput.SetKey(setup.block, true);
            blockHoldTimer = Random.Range(antiRushBlockHoldMin, antiRushBlockHoldMax) * BlockHoldMul * blockHoldScale;
            actionTimer = Random.Range(0.12f, 0.22f) * BlockHoldMul * blockHoldScale;
            return true;
        }
        roll -= blockW;

        if (roll < parryW)
        {
            if (character.CanParry && !character.IsCasting)
            {
                PressOneFrame(setup.parry);
                actionTimer = Random.Range(antiRushParryWindowMin, antiRushParryWindowMax);
                return true;
            }
        }
        roll -= parryW;

        if (roll < lightW)
        {
            PressQuick(dxSigned);
            actionTimer = Random.Range(0.12f, 0.22f);
            return true;
        }
        roll -= lightW;

        if (roll < heavyW)
        {
            PressHeavy(0.18f, 0.32f);
            return true;
        }
        roll -= heavyW;

        if (roll < specialW)
        {
            PressSpecial(dxSigned);
            actionTimer = Random.Range(0.22f, 0.40f);
            return true;
        }
        roll -= specialW;

        if (roll < retreatW)
        {
            ForceRetreatFromTarget(dxSigned, antiRushRetreatDuration);
            actionTimer = antiRushRetreatDuration;
            return true;
        }
        roll -= retreatW;

        if (roll < chargeW)
        {
            StartBotCharge();
            actionTimer = 0.06f;
            return true;
        }

        return false;
    }

    private bool TryReactToCharge()
    {
        if (!antiChargeEnabled) return false;
        if (antiChargeReactionTimer > 0f) return false;
        if (character == null || target == null || targetTr == null) return false;

        bool emergencyChargeThreat = target.IsCharging || target.IsCharged;

        if (actionTimer > 0f && !emergencyChargeThreat)
            return false;

        if (character.IsCasting || character.IsStunned || character.IsKnocked)
            return false;

        Vector3 myPos = selfTr.position;
        Vector3 targetPos = targetTr.position;

        float dxSigned = targetPos.x - myPos.x;
        float dySigned = targetPos.y - myPos.y;

        float dx = Mathf.Abs(dxSigned);
        float dy = Mathf.Abs(dySigned);

        bool sameLevel = dy <= antiChargeHeightTolerance;
        bool closeEnough = dx <= antiChargeRange;
        bool opponentCharging = target.IsCharging;

        if (!PerceptionGate(ref chargePerceiveTimer, opponentCharging && closeEnough && sameLevel))
            return false;

        bool chargingTowardsMe = IsTargetChargingTowardsMe(dxSigned);

        bool inDangerZone =
            dx <= antiChargeDangerRange &&
            dy <= antiChargeDangerHeightTolerance &&
            chargingTowardsMe;

        if (inDangerZone)
        {
            if (dx <= antiChargeSpecialRange && CanUseSpecialNow())
            {
                PressSpecial(dxSigned);
                antiChargeReactionTimer = antiChargeSpecialCooldown;
                actionTimer = 0.16f;
                return true;
            }

            if (character.IsGrounded && !character.JumpDisabled && !character.IsCasting)
            {
                if (trackingEnemyCharge && enemyChargeTimer >= antiChargeParryDelay &&
                    character.CanParry && Random.value < antiChargeEmergencyParryChance * parryScale)
                {
                    PressOneFrame(setup.parry);
                    antiChargeReactionTimer = antiChargeReactionCooldown;
                    actionTimer = 0.18f;
                    return true;
                }

                if (Random.value < antiChargeEmergencyJumpChance)
                {
                    PressOneFrame(setup.up);
                    ForceRetreatFromTarget(dxSigned, antiChargeEmergencyRetreatTime);
                    antiChargeReactionTimer = antiChargeReactionCooldown * 0.75f;
                    actionTimer = 0.18f;
                    return true;
                }
            }

            ForceRetreatFromTarget(dxSigned, antiChargeEmergencyRetreatTime);
            antiChargeReactionTimer = antiChargeReactionCooldown * 0.75f;
            actionTimer = 0.16f;
            return true;
        }

        if (chargingTowardsMe &&
            dx <= antiChargeSpecialRange &&
            CanUseSpecialNow())
        {
            if (Random.value < antiChargeSpecialChance)
            {
                PressSpecial(dxSigned);
                antiChargeReactionTimer = antiChargeSpecialCooldown;
                actionTimer = 0.20f;
                return true;
            }
        }

        float reactChance = antiChargeBaseChance + antiChargeSkillBonus * skill;

        if (target.IsCharged)
            reactChance += 0.15f;

        reactChance = Mathf.Clamp01(reactChance);

        if (Random.value > reactChance)
            return false;

        antiChargeReactionTimer = antiChargeReactionCooldown;

        float totalWeight =
            antiChargeParryWeight * parryScale +
            antiChargeJumpWeight +
            antiChargeRetreatWeight +
            antiChargeBlockWeight;

        float roll = Random.value * totalWeight;

        if (roll < antiChargeParryWeight * parryScale)
        {
            bool parryTimingReady = trackingEnemyCharge && enemyChargeTimer >= antiChargeParryDelay;

            if (parryTimingReady && character.CanParry && !character.IsCasting)
            {
                PressOneFrame(setup.parry);
                actionTimer = 0.22f;
                return true;
            }
        }
        else
        {
            roll -= antiChargeParryWeight * parryScale;
        }

        if (roll < antiChargeJumpWeight)
        {
            if (character.IsGrounded && !character.JumpDisabled && !character.IsCasting)
            {
                PressOneFrame(setup.up);
                ForceRetreatFromTarget(dxSigned, retreatDuration * 0.75f);
                actionTimer = 0.25f;
                return true;
            }
        }
        else
        {
            roll -= antiChargeJumpWeight;
        }

        if (roll < antiChargeRetreatWeight)
        {
            ForceRetreatFromTarget(dxSigned, retreatDuration);
            actionTimer = 0.18f;
            return true;
        }
        else
        {
            roll -= antiChargeRetreatWeight;
        }

        // Bot v5: fallback was a block, but a block does not reduce charge damage -> step away instead.
        ForceRetreatFromTarget(dxSigned, retreatDuration);
        actionTimer = 0.18f;
        return true;
    }

    private bool TryRushPunish(float dxSigned)
    {
        if (!rushPunishEnabled)
            return false;

        float punishChance = rushPunishBaseChance + rushPunishSkillBonus * skill;
        punishChance = Mathf.Clamp01(punishChance);

        if (Random.value > punishChance)
            return false;

        float quickW = rushPunishQuickWeight * Mathf.Lerp(0.95f, 1.25f, aggression) * AggrMul * QuickReadyMul;
        float heavyW = rushPunishHeavyWeight * Mathf.Lerp(0.90f, 1.20f, aggression) * AggrMul;
        float specialW = CanUseSpecialNow() ? rushPunishSpecialWeight * Mathf.Lerp(0.9f, 1.15f, aggression) * AggrMul : 0f;
        float retreatW = rushPunishRetreatWeight * Mathf.Lerp(0.8f, 1.2f, defenseBias) * DefMul;
        float waitW = rushPunishWaitWeight * Mathf.Lerp(0.7f, 1.2f, patience);

        float total = quickW + heavyW + specialW + retreatW + waitW;
        float roll = Random.value * total;

        if (roll < quickW)
        {
            PressQuick(dxSigned);
            actionTimer = Random.Range(rushPunishLightCooldownMin, rushPunishLightCooldownMax);
            return true;
        }
        roll -= quickW;

        if (roll < heavyW)
        {
            PressHeavy(rushPunishHeavyCooldownMin, rushPunishHeavyCooldownMax);
            return true;
        }
        roll -= heavyW;

        if (roll < specialW)
        {
            PressSpecial(dxSigned);
            actionTimer = Random.Range(rushPunishSpecialCooldownMin, rushPunishSpecialCooldownMax);
            return true;
        }
        roll -= specialW;

        if (roll < retreatW)
        {
            ForceRetreatFromTarget(dxSigned, rushPunishRetreatDuration);
            actionTimer = rushPunishRetreatDuration;
            return true;
        }

        actionTimer = Random.Range(0.05f, 0.10f);
        return true;
    }

    private bool IsRushPunishState(float dx, float dy, float dxSigned, bool sameLevel)
    {
        if (!rushPunishEnabled || target == null || character == null)
            return false;

        if (!sameLevel)
            return false;

        float rushRange = tooCloseRange * rushPunishRangeMultiplier;
        bool veryClose = dx <= rushRange;

        if (!veryClose)
            return false;

        float targetVelX = GetTargetVelocityX();
        bool movingTowardMe =
            Mathf.Abs(targetVelX) >= rushPunishVelocityThreshold &&
            Mathf.Sign(targetVelX) == Mathf.Sign(dxSigned);

        bool opponentCommittedAction = IsTargetAttackCommitted() || target.IsCharging || target.IsCharged;

        return movingTowardMe || opponentCommittedAction;
    }

    private bool TryStartProactiveCharge(float dx, float dy, float dxSigned, bool sameLevel)
    {
        if (!proactiveChargeEnabled) return false;
        if (!CanStartBotCharge()) return false;
        if (!sameLevel) return false;
        if (dy > antiRushHeightTolerance) return false;

        float targetVelX = GetTargetVelocityX();
        bool approaching =
            Mathf.Abs(targetVelX) >= proactiveChargeApproachVelocity &&
            Mathf.Sign(targetVelX) == Mathf.Sign(dxSigned);

        bool inRange =
            dx >= proactiveChargeStartRangeMin &&
            dx <= proactiveChargeStartRangeMax;

        bool mashEntry = approaching || IsTargetAttackCommitted();

        if (!inRange || !mashEntry)
            return false;

        float chance = proactiveChargeChance * Mathf.Lerp(0.9f, 1.15f, skill) * AggrMul * chargeScale;
        chance = Mathf.Min(0.95f, chance);

        if (Random.value > chance)
            return false;

        StartBotCharge();
        actionTimer = 0.06f;
        return true;
    }

    private bool HandleBotChargeState(float dt)
    {
        if (!botCharging)
            return false;

        if (character == null || target == null || targetTr == null)
        {
            StopBotCharge();
            return false;
        }

        if (character.IsCasting || character.IsStunned || character.IsKnocked)
        {
            StopBotCharge();
            return false;
        }

        botChargeTimer += dt;
        botInput.SetKey(setup.charge, true);

        Vector3 myPos = selfTr.position;
        Vector3 targetPos = targetTr.position;

        float dxSigned = targetPos.x - myPos.x;
        float dx = Mathf.Abs(dxSigned);
        float dy = Mathf.Abs(targetPos.y - myPos.y);

        bool sameLevel = dy <= antiRushHeightTolerance;
        bool opponentCommitted = IsTargetAttackCommitted() || target.IsCharging || target.IsCharged;

        // Hold limit: between StartRangeMax (1.85) and AbortRange (2.2) neither rule below fired -> charge held forever.
        bool overHold = botChargeTimer >= maxBotChargeHold;

        // Release ONLY a fully charged attack (Character.chargeTime = 0.5 s). Releasing earlier just cancels the charge;
        // before this change most bot charges ended as ChargeCancel after 0.02-0.33 s (early release when the
        // opponent attacked / charged). Until charged the bot keeps holding (charge has armor) or aborts below.
        // (opponentCommitted and proactiveChargeReleaseTime are no longer needed for the release decision.)
        bool charged = character.IsCharged;

        // Release only when charged AND the target is within reach (was dx <= 1.85 -> every release whiffed).
        // If the target never comes in reach, the hold limit below cancels the charge instead of whiffing it.
        bool shouldRelease = charged && sameLevel && dx <= chargeHitRange;

        bool shouldAbort =
            dx >= proactiveChargeAbortRange ||
            !sameLevel ||
            overHold;

        if (shouldRelease)
        {
            ReleaseBotCharge();
            actionTimer = proactiveChargeCooldown;
            return true;
        }

        if (shouldAbort)
        {
            StopBotCharge();
            actionTimer = 0.08f;
            return false;
        }

        if (dx > proactiveChargeEarlyReleaseRange)
        {
            float dir = Mathf.Sign(dxSigned);
            ApplyMovement(dir);
        }
        else
        {
            ApplyMovement(0f);
        }

        return true;
    }

    private bool CanStartBotCharge()
    {
        if (character == null || setup == null || botInput == null)
            return false;

        if (botCharging) return false;
        if (botChargeCooldownTimer > 0f) return false;

        return
            !character.IsCasting &&
            !character.IsStunned &&
            !character.IsKnocked &&
            !character.ChargeDisabled &&
            !character.IsCharging &&
            !character.IsCharged;
    }

    private void StartBotCharge()
    {
        if (!CanStartBotCharge())
            return;

        botCharging = true;
        botChargeTimer = 0f;
        shadowSinceOwnCharge = 0f;
        botInput.SetKey(setup.charge, true);
        moveCommitTimer = 0f;
        committedMove = 0f;
    }

    private void ReleaseBotCharge()
    {
        if (!botCharging)
            return;

        botInput.SetKey(setup.charge, false);
        PressOneFrame(setup.charge);
        botCharging = false;
        botChargeTimer = 0f;
        botChargeCooldownTimer = proactiveChargeCooldown;
    }

    // Watchdog: the key-up that ends a charge exists for one frame only. If the character misses it, it stays
    // charging while the bot thinks the charge is over (botCharging == false) -> nobody ever releases it.
    // Adopt the charge as "over the hold limit": HandleBotChargeState then releases it (target in range) or cancels it.
    private void CheckOrphanCharge(float dt)
    {
        if (botCharging || character == null)
        {
            orphanChargeTimer = 0f;
            return;
        }

        // Dead while charging: Die() keeps the charging flag but a dead character reads no input, so the
        // recovery would repeat every 0.3 s until the scene reloads (console spam only; the round is already saved).
        if (character.IsDead())
        {
            orphanChargeTimer = 0f;
            return;
        }

        if (character.IsCharging || character.IsCharged)
        {
            orphanChargeTimer += dt;
            if (orphanChargeTimer >= orphanChargeTimeout)
            {
                botCharging = true;
                botChargeTimer = maxBotChargeHold;
                orphanChargeTimer = 0f;
                Debug.Log($"[Bot] orphan charge recovered on {character.PlayerId}");
            }
        }
        else
        {
            orphanChargeTimer = 0f;
        }
    }

    private void StopBotCharge()
    {
        if (botInput != null && setup != null)
            botInput.SetKey(setup.charge, false);

        botCharging = false;
        botChargeTimer = 0f;
    }

    private bool IsTargetAttackCommitted()
    {
        if (target == null)
            return false;

        return target.LightAttacking || target.HeavyAttacking || target.IsCasting;
    }

    private float GetTargetVelocityX()
    {
        if (target == null)
            return 0f;

        Rigidbody2D rb = target.GetComponent<Rigidbody2D>();
        if (rb != null)
            return rb.velocity.x;

        return 0f;
    }

    private void UpdateLastTargetDistance()
    {
        if (targetTr == null || selfTr == null)
            return;

        lastTargetDistanceX = Mathf.Abs(targetTr.position.x - selfTr.position.x);
    }

    private void ForceRetreatFromTarget(float dxSigned, float duration)
    {
        forcedHorizontalValue = -Mathf.Sign(dxSigned);

        if (Mathf.Abs(forcedHorizontalValue) < 0.01f)
            forcedHorizontalValue = Random.value < 0.5f ? -1f : 1f;

        forcedHorizontalTimer = duration;
        moveCommitTimer = 0f;
        committedMove = 0f;
    }

    private void MaybeRepositionAfterAction(float dxSigned, bool allowToward)
    {
        float chance = Mathf.Min(0.9f, repositionChance * Mathf.Lerp(0.7f, 1.5f, mobilityBias) * MobMul);

        if (Random.value > chance)
            return;

        float dir;

        if (allowToward && Random.value < 0.38f)
            dir = Mathf.Sign(dxSigned);
        else
            dir = -Mathf.Sign(dxSigned);

        if (Mathf.Abs(dir) < 0.01f)
            dir = Random.value < 0.5f ? -1f : 1f;

        forcedHorizontalValue = dir;
        forcedHorizontalTimer = Random.Range(repositionMinDuration, repositionMaxDuration);
        moveCommitTimer = 0f;
    }

    private void UpdateForcedHorizontal(float dt)
    {
        if (forcedHorizontalTimer > 0f)
        {
            forcedHorizontalTimer -= dt;
            if (forcedHorizontalTimer <= 0f)
            {
                forcedHorizontalTimer = 0f;
                forcedHorizontalValue = 0f;
            }
        }
    }

    // Human rhythm: bursts of quick decisions, then irregular (log-normal) pauses. Old: uniform thinkIntervalMin..Max.
    private float NextThinkInterval()
    {
        if (!humanRhythm) return Random.Range(thinkIntervalMin, thinkIntervalMax);
        if (burstLeft > 0)
        {
            burstLeft--;
            return Random.Range(burstIntervalMin, burstIntervalMax);
        }
        if (Random.value < burstStartChance)
        {
            burstLeft = Random.Range(burstExtraMin, burstExtraMax + 1) - 1;
            return Random.Range(burstIntervalMin, burstIntervalMax);
        }
        float u1 = Mathf.Max(1e-6f, Random.value), u2 = Random.value;
        float z = Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);
        return Mathf.Clamp(thinkMedian * Mathf.Exp(thinkSigma * z), thinkMinHuman, thinkMaxHuman);
    }

    private void TriggerDropPlatform()
    {
        isDroppingThroughPlatform = true;

        PressOneFrame(setup.down);

        forcedVerticalValue = -1f;
        forcedVerticalTimer = dropHoldDuration;
    }

    private void UpdateForcedVertical(float dt)
    {
        if (forcedVerticalTimer > 0f)
        {
            forcedVerticalTimer -= dt;

            if (forcedVerticalTimer <= 0f)
            {
                forcedVerticalValue = 0f;
                isDroppingThroughPlatform = false;
            }
        }
    }

    private string GetAxisSuffix()
    {
        return setup.playerNum == 1 ? "_P1" : "_P2";
    }

    // Quick attack with a human-like aim: hold the direction toward the opponent in the same frame (Skipler's quick is a
    // dash in the held direction; no key = right). With probability 1 - quickAimChance the old behaviour (no aim) is kept.
    private void PressQuick(float dxSigned)
    {
        if (Random.value < quickAimChance)
        {
            quickAimDir = dxSigned >= 0f ? 1f : -1f;
            quickAimTimer = quickAimHold;
            ApplyMovement(quickAimDir);
        }
        PressOneFrame(setup.lightAttack);
    }

    // Bot v5 (G): the special (Skipler: dash in the held direction) aimed at the opponent, like PressQuick.
    private void PressSpecial(float dxSigned)
    {
        if (Random.value < specialAimChance)
        {
            quickAimDir = dxSigned >= 0f ? 1f : -1f;
            quickAimTimer = quickAimHold;
            ApplyMovement(quickAimDir);
        }
        PressOneFrame(setup.ability);
    }

    // Bot v5 (H): the opponent is charging / charged, on our level and facing us (its charge hits only in front).
    private bool ChargeThreatFront(float dxSigned, float dx, bool sameLevel)
    {
        if (!chargeAwareness || target == null || !sameLevel) return false;
        bool known = target.IsCharged || (target.IsCharging && trackingEnemyCharge && enemyChargeTimer >= chargeAwareDelay);
        return known && IsTargetChargingTowardsMe(dxSigned);
    }

    // Bot v5 (I): human answer to an opponent's heavy start nearby, after the perception delay.
    private bool TryReactToHeavy()
    {
        if (!heavyReaction || heavyReactTimer > 0f) return false;
        if (character == null || target == null || targetTr == null) return false;
        if (character.IsCasting || character.IsStunned || character.IsKnocked) return false;

        float dxSigned = targetTr.position.x - selfTr.position.x;
        float dx = Mathf.Abs(dxSigned);
        float dy = Mathf.Abs(targetTr.position.y - selfTr.position.y);
        bool threat = target.HeavyAttacking && dy <= sameLevelTolerance && dx <= heavyThreatRange &&
                      IsTargetChargingTowardsMe(dxSigned);   // = the opponent faces us
        if (!PerceptionGate(ref heavyPerceiveTimer, threat)) return false;

        heavyReactTimer = heavyReactCooldown;
        bool close = dx <= heavyCloseRange;
        float blockW = character.BlockDisabled ? 0f : heavyBlockWeight * DefMul * (close ? 1.3f : 0.7f);
        float stepW = heavyStepWeight * MobMul * (close ? 0.5f : 1.4f);
        float parryW = character.CanParry ? heavyParryWeight * DefMul * Mathf.Lerp(0.8f, 1.2f, skill) * parryScale : 0f;
        float noneW = heavyNoneWeight * AggrMul;
        float roll = Random.value * (blockW + stepW + parryW + noneW);

        if (roll < blockW)
        {
            botInput.SetKey(setup.block, true);
            blockHoldTimer = Random.Range(0.40f, 0.65f);   // until the heavy has landed (~0.57 s after its start)
            actionTimer = blockHoldTimer;
            return true;
        }
        roll -= blockW;
        if (roll < stepW)
        {
            ForceRetreatFromTarget(dxSigned, Random.Range(0.25f, 0.40f));
            actionTimer = 0.20f;
            return true;
        }
        roll -= stepW;
        if (roll < parryW)
        {
            PressOneFrame(setup.parry);
            actionTimer = Random.Range(antiRushParryWindowMin, antiRushParryWindowMax);
            return true;
        }
        return false;   // no special answer: keep doing what it was doing (anti-rush may still counter-attack)
    }

    // Perception delay: the first frame a danger is seen starts a sampled delay; when it has passed the bot reacts only if
    // the danger still holds at that moment. timer: -1 idle, > 0 perceiving, 0 perceived. Returns true = react now.
    private bool PerceptionGate(ref float timer, bool danger)
    {
        if (!humanReactionDelay) { timer = -1f; return danger; }
        if (timer < 0f)
        {
            if (danger) timer = SampleReactionDelay();
            return false;
        }
        if (timer > 0f) return false;
        timer = -1f;
        return danger;
    }

    // Log-normal reaction delay; the median shrinks with skill (better players react faster).
    private float SampleReactionDelay()
    {
        float median = Mathf.Lerp(reactionMedianSkill0, reactionMedianSkill1, skill);
        float u1 = Mathf.Max(1e-6f, Random.value), u2 = Random.value;
        float z = Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);
        return Mathf.Clamp(median * Mathf.Exp(reactionSigma * z), reactionMin, reactionMax);
    }

    private void PressOneFrame(KeyCode key)
    {
        botInput.PressKeyOneFrame(key);
        if (shadowMode)
        {
            int i = ShadowKeyIndex(key);   // DAgger option B: remember the shadow's own suggestion
            if (i >= 0) { shadowSinceOwnPress[i] = 0f; shadowOwnPressPending[i] = true; }
        }
    }

    private Character FindOpponent()
    {
        Character[] all = FindObjectsOfType<Character>();

        foreach (Character c in all)
        {
            if (c != null && c != character)
                return c;
        }

        return null;
    }

    public void SetTarget(Character newTarget)
    {
        target = newTarget;
        targetTr = target != null ? target.transform : null;
    }

    public void SetSkill(float newSkill)
    {
        skill = Mathf.Clamp01(newSkill);
    }

    public void Rebind(Character self, Character enemy)
    {
        character = self;
        target = enemy;
        targetTr = target != null ? target.transform : null;
        setup = GetComponent<CharacterSetup>();

        if (botInput == null)
            botInput = new BotInputProvider();

        if (character != null && !shadowMode)   // DAgger shadow: never take control
            character.SetInput(botInput);

        selfTr = transform;
        initialized = (character != null && setup != null && target != null);
    }

    private bool IsTargetChargingTowardsMe(float dxSigned)
    {
        if (target == null) return false;

        float targetFacing = Mathf.Sign(target.transform.localScale.x);
        float dirToMe = -Mathf.Sign(dxSigned);

        if (Mathf.Abs(dirToMe) < 0.01f)
            return true;

        return targetFacing == dirToMe;
    }

    private bool CanUseSpecialNow()
    {
        if (character == null) return false;

        return
            !character.IsCasting &&
            !character.IsStunned &&
            !character.IsKnocked &&
            character.CanCast &&
            !character.OnAbilityCD &&
            !character.SpecialDisabled;
    }

    private void UpdateEnemyChargeTracking(float dt)
    {
        if (target == null)
        {
            trackingEnemyCharge = false;
            enemyChargeTimer = 0f;
            return;
        }

        if (target.IsCharging)
        {
            if (!trackingEnemyCharge)
            {
                trackingEnemyCharge = true;
                enemyChargeTimer = 0f;
            }
            else
            {
                enemyChargeTimer += dt;
            }
        }
        else
        {
            trackingEnemyCharge = false;
            enemyChargeTimer = 0f;
        }
    }

    private bool IsHeadStackingTarget(float dx, float dySigned)
    {
        float absDy = Mathf.Abs(dySigned);

        bool iAmAboveTarget = dySigned < 0f;
        bool closeInX = dx <= headStackXThreshold;
        bool badYBand = absDy >= headStackMinY && absDy <= headStackMaxY;

        return iAmAboveTarget && closeInX && badYBand;
    }
}