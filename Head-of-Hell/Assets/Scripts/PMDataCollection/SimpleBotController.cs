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
    [SerializeField] private float antiChargeBlockWeight = 0.10f;
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
    [SerializeField] private float crossLevelFollowXThreshold = 1.6f;

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

    private bool trackingEnemyCharge;
    private float enemyChargeTimer;

    private BotInputProvider botInput;
    private float thinkTimer;
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

    private bool botCharging;
    private float botChargeTimer;
    private float botChargeCooldownTimer;

    private float lastTargetDistanceX = 999f;

    private void Start()
    {
        selfTr = transform;
        TryInitialize();
        RandomizePersonality();
    }

    private void OnEnable()
    {
        thinkTimer = 0f;
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
        botChargeCooldownTimer -= dt;

        UpdateForcedVertical(dt);
        UpdateForcedHorizontal(dt);
        UpdateEnemyChargeTracking(dt);

        if (blockHoldTimer <= 0f)
            botInput.SetKey(setup.block, false);

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

        if (TryReactToRush())
        {
            HandleMovement(dt);
            UpdateLastTargetDistance();
            return;
        }

        HandleMovement(dt);

        if (thinkTimer <= 0f)
        {
            thinkTimer = Random.Range(thinkIntervalMin, thinkIntervalMax);
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
            character.SetInput(botInput);
        }

        if (target == null)
            target = FindOpponent();

        targetTr = target != null ? target.transform : null;

        if (target != null)
            initialized = true;
    }

    private void RandomizePersonality()
    {
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

        if (moveCommitTimer > 0f)
        {
            moveCommitTimer -= dt;
            ApplyMovement(committedMove);
            return;
        }

        float move = 0f;

        if (targetBelow && dx < 0.6f && !isDroppingThroughPlatform && !headStacking)
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
                + Mathf.Lerp(-0.05f, 0.18f, defenseBias);

            float retreatRange =
                tooCloseRange
                + Mathf.Lerp(-0.04f, 0.08f, defenseBias);

            desiredRange = Mathf.Max(retreatRange + 0.08f, desiredRange);

            if (!sameLevel)
            {
                if (dx > crossLevelFollowXThreshold)
                    move = Mathf.Sign(dxSigned);
                else
                    move = 0f;
            }
            else if (!inEngageRange)
            {
                if (dx > desiredRange)
                    move = Mathf.Sign(dxSigned);
                else if (dx < retreatRange)
                    move = -Mathf.Sign(dxSigned);
            }
            else
            {
                float shimmyChance = idleShimmyChance * Mathf.Lerp(0.7f, 1.5f, mobilityBias);

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
        moveCommitTimer = Random.Range(moveCommitMin, moveCommitMax);

        ApplyMovement(committedMove);
    }

    private void ApplyMovement(float move)
    {
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
        if (actionTimer > 0f || isDroppingThroughPlatform || targetTr == null)
            return;

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

        if (inEngageRange && Random.value < defendChance)
        {
            botInput.SetKey(setup.block, true);
            blockHoldTimer = Random.Range(0.10f, 0.24f);
            actionTimer = Random.Range(0.16f, 0.28f);
            MaybeRepositionAfterAction(dxSigned, false);
            return;
        }

        if (!inPokeRange)
        {
            float jumpInChance = jumpChance * Mathf.Lerp(0.7f, 2.0f, mobilityBias);

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
            float quickW = quickChance * Mathf.Lerp(1.00f, 1.50f, aggression);
            float heavyW = heavyChance * Mathf.Lerp(0.95f, 1.45f, aggression);
            float retreatW = Mathf.Lerp(0.04f, 0.18f, defenseBias);
            float specialW = CanUseSpecialNow() ? specialChance * Mathf.Lerp(0.75f, 1.35f, aggression) : 0f;
            float waitW = Mathf.Lerp(0.01f, 0.08f, patience);

            float total = quickW + heavyW + retreatW + specialW + waitW;
            float roll = Random.value * total;

            if (roll < quickW)
            {
                PressOneFrame(setup.lightAttack);
                actionTimer = Random.Range(0.15f, 0.26f);
                MaybeRepositionAfterAction(dxSigned, true);
                return;
            }
            roll -= quickW;

            if (roll < heavyW)
            {
                PressOneFrame(setup.heavyAttack);
                actionTimer = Random.Range(0.22f, 0.36f);
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
                PressOneFrame(setup.ability);
                actionTimer = Random.Range(0.24f, 0.42f);
                MaybeRepositionAfterAction(dxSigned, true);
                return;
            }

            actionTimer = Random.Range(0.06f, 0.14f);
            return;
        }

        {
            float quickW = quickChance * Mathf.Lerp(0.80f, 1.20f, aggression);
            float heavyW = heavyChance * Mathf.Lerp(0.70f, 1.15f, aggression) * Mathf.Lerp(0.85f, 1.15f, patience);
            float specialW = CanUseSpecialNow() ? specialChance * Mathf.Lerp(0.75f, 1.35f, aggression) : 0f;
            float jumpW = jumpChance * Mathf.Lerp(0.60f, 1.80f, mobilityBias);
            float blockW = blockChance * Mathf.Lerp(0.80f, 1.80f, defenseBias);
            float waitW = Mathf.Lerp(0.05f, 0.22f, patience);

            float total = quickW + heavyW + specialW + jumpW + blockW + waitW;
            float roll = Random.value * total;

            if (roll < quickW)
            {
                PressOneFrame(setup.lightAttack);
                actionTimer = Random.Range(0.20f, 0.36f);
                MaybeRepositionAfterAction(dxSigned, true);
                return;
            }
            roll -= quickW;

            if (roll < heavyW)
            {
                PressOneFrame(setup.heavyAttack);
                actionTimer = Random.Range(0.28f, 0.46f);
                MaybeRepositionAfterAction(dxSigned, true);
                return;
            }
            roll -= heavyW;

            if (roll < specialW)
            {
                PressOneFrame(setup.ability);
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
                blockHoldTimer = Random.Range(0.10f, 0.22f);
                actionTimer = Random.Range(0.15f, 0.26f);
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
            return false;

        float targetVelX = GetTargetVelocityX();
        bool approachingFast =
            Mathf.Abs(targetVelX) >= antiRushApproachVelocity &&
            Mathf.Sign(targetVelX) == Mathf.Sign(dxSigned);

        bool targetAttacking = IsTargetAttackCommitted();
        bool distanceClosingFast = dx < lastTargetDistanceX - 0.03f;

        bool dangerousRush =
            dx <= antiRushDangerRange &&
            (approachingFast || targetAttacking || distanceClosingFast);

        if (!dangerousRush)
            return false;

        antiRushReactionTimer = antiRushReactionCooldown;

        bool critical = dx <= antiRushCriticalRange;

        float blockW = antiRushBlockWeight * Mathf.Lerp(1.0f, 1.25f, defenseBias);
        float parryW = antiRushParryWeight * Mathf.Lerp(0.9f, 1.2f, skill);
        float lightW = antiRushLightWeight * Mathf.Lerp(1.0f, 1.25f, aggression);
        float heavyW = antiRushHeavyWeight * Mathf.Lerp(0.95f, 1.20f, aggression);
        float specialW = CanUseSpecialNow() ? antiRushSpecialWeight : 0f;
        float retreatW = antiRushRetreatWeight;
        float chargeW = CanStartBotCharge() ? antiRushChargeWeight : 0f;

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
            blockHoldTimer = Random.Range(antiRushBlockHoldMin, antiRushBlockHoldMax);
            actionTimer = Random.Range(0.12f, 0.22f);
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
            PressOneFrame(setup.lightAttack);
            actionTimer = Random.Range(0.12f, 0.22f);
            return true;
        }
        roll -= lightW;

        if (roll < heavyW)
        {
            PressOneFrame(setup.heavyAttack);
            actionTimer = Random.Range(0.18f, 0.32f);
            return true;
        }
        roll -= heavyW;

        if (roll < specialW)
        {
            PressOneFrame(setup.ability);
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

        if (!opponentCharging || !closeEnough || !sameLevel)
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
                PressOneFrame(setup.ability);
                antiChargeReactionTimer = antiChargeSpecialCooldown;
                actionTimer = 0.16f;
                return true;
            }

            if (character.IsGrounded && !character.JumpDisabled && !character.IsCasting)
            {
                if (trackingEnemyCharge && enemyChargeTimer >= antiChargeParryDelay &&
                    character.CanParry && Random.value < antiChargeEmergencyParryChance)
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
                PressOneFrame(setup.ability);
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
            antiChargeParryWeight +
            antiChargeJumpWeight +
            antiChargeRetreatWeight +
            antiChargeBlockWeight;

        float roll = Random.value * totalWeight;

        if (roll < antiChargeParryWeight)
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
            roll -= antiChargeParryWeight;
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

        botInput.SetKey(setup.block, true);
        blockHoldTimer = Random.Range(0.18f, 0.32f);
        actionTimer = 0.22f;
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

        float quickW = rushPunishQuickWeight * Mathf.Lerp(0.95f, 1.25f, aggression);
        float heavyW = rushPunishHeavyWeight * Mathf.Lerp(0.90f, 1.20f, aggression);
        float specialW = CanUseSpecialNow() ? rushPunishSpecialWeight * Mathf.Lerp(0.9f, 1.15f, aggression) : 0f;
        float retreatW = rushPunishRetreatWeight * Mathf.Lerp(0.8f, 1.2f, defenseBias);
        float waitW = rushPunishWaitWeight * Mathf.Lerp(0.7f, 1.2f, patience);

        float total = quickW + heavyW + specialW + retreatW + waitW;
        float roll = Random.value * total;

        if (roll < quickW)
        {
            PressOneFrame(setup.lightAttack);
            actionTimer = Random.Range(rushPunishLightCooldownMin, rushPunishLightCooldownMax);
            return true;
        }
        roll -= quickW;

        if (roll < heavyW)
        {
            PressOneFrame(setup.heavyAttack);
            actionTimer = Random.Range(rushPunishHeavyCooldownMin, rushPunishHeavyCooldownMax);
            return true;
        }
        roll -= heavyW;

        if (roll < specialW)
        {
            PressOneFrame(setup.ability);
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

        float chance = proactiveChargeChance * Mathf.Lerp(0.9f, 1.15f, skill);
        chance = Mathf.Clamp01(chance);

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

        bool shouldRelease =
            (botChargeTimer >= proactiveChargeReleaseTime && dx <= proactiveChargeStartRangeMax && sameLevel) ||
            (dx <= proactiveChargeEarlyReleaseRange && sameLevel && opponentCommitted);

        bool shouldAbort =
            dx >= proactiveChargeAbortRange ||
            !sameLevel;

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
        float chance = repositionChance * Mathf.Lerp(0.7f, 1.5f, mobilityBias);

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

    private void PressOneFrame(KeyCode key)
    {
        botInput.PressKeyOneFrame(key);
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

        if (character != null)
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