using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

public enum ReachType
{
    Melee,
    Dash,
    Ranged,
    Global
}

[RequireComponent(typeof(CharacterManager))]
public class FighterAgent : Agent
{
    [Header("Managers")]
    public CharacterManager selfManager;
    public CharacterManager enemyManager;

    [Header("Input")]
    public string playerSuffix = "_P1";
    private AIInputProvider aiInput;

    private Character self;
    private Character opp;

    KeyCode upK, downK, leftK, rightK, lightK, heavyK, blockK, abilityK, chargeK, parryK;

    [Header("Main Rewards")]
    float rewardDamageDealt = +0.01f;
    float rewardDamageTaken = -0.007f;
    float rewardWin = +1.0f;
    float rewardLoss = -1.0f;
    float stepPenalty = -0.0001f;

    [Header("Minimal Spacing Shaping")]
    float spacingBonus = +0.0002f;
    float usefulRangeMinX = 0.4f;
    float usefulRangeMaxX = 0.90f;
    float usefulRangeMaxY = 0.50f;

    [Header("Observation scales")]
    float relXScale = 9f;
    float relYScale = 5f;
    float velScale = 10f;
    int totalCharacterCount = 10;

    [Header("Behavior Hygiene")]
    float mashPenalty = -0.0003f;
    float airJumpPenalty = -0.0008f;
    float edgeCampPenalty = -0.0007f;
    int mashChangeThreshold = 3;
    float edgeZoneX = 8f;
    float edgeGraceTime = 1.75f;
    float edgeSmallMoveThreshold = 0.35f;

    [Header("Move Semantics")]
    private ReachType lightReachType = ReachType.Melee;
    private ReachType specialReachType = ReachType.Melee;

    [Header("Range Logic")]
    float extremeFarThreshold = 8.5f;
    float extremeFarHeavyPenalty = -0.0007f;
    float extremeFarChargePenalty = -0.001f;
    float approachBonus = +0.00065f;
    float approachStartMargin = 0.75f;
    float farMeleeLightPenalty = -0.00035f;
    float farMeleeSpecialPenalty = -0.00035f;

    [Header("Directional Hygiene")]
    float dashNoDirectionPenalty = -0.0005f;
    float wrongFacingSpecialPenalty = -0.0005f;

    int freeConsecutiveCharges = 4;
    float repeatedChargePenaltyBase = -0.0001f;
    float repeatedChargePenaltyStep = -0.0003f;
    float repeatedChargePenaltyCap = -0.0006f;
    float chargeChainDecaySeconds = 0.9f;

    [Header("Charge Release Outcome")]
    float emptyReleasedChargePenalty = -0.00003f;

    [Header("Anti Vertical Cheese")]
    float verticalCheeseGraceTime = 0.25f;
    float verticalCheeseMaxX = 0.5f;
    float verticalCheeseMinY = 0.75f;
    float verticalCheesePenaltyBase = -0.00015f;
    float verticalCheesePenaltyPerSecond = -0.00045f;
    float verticalCheesePenaltyCap = -0.009f;
    float verticalCheeseTimer = 0f;
    float verticalCheeseDecayPerSecond = 1.2f;

    [Header("Block Hold Hygiene")]
    float blockHoldGraceTime = 3f;
    float longBlockHoldPenaltyPerSecond = -0.002f;
    float blockHoldDecayPerSecond = 1.5f;

    [Header("Repeat Move Hygiene")]
    int freeRepeatedSameMoveStarts = 3;
    float repeatedSameMovePenaltyBase = -0.00000f;
    float repeatedSameMovePenaltyStep = -0.00001f;
    float repeatedSameMovePenaltyCap = -0.00009f;

    [Header("Pressure / Aggression Shaping")]
    float forwardPressureBonus = +0.00035f;
    float closePressureBonus = +0.00015f;
    float retreatFromCloseRangePenalty = -0.00001f;
    float closePressureRangeX = 1.4f;
    float closePressureRangeY = 0.7f;

    [Header("Offensive Intent Rewards")]
    float usefulIntentBonus = +0.00035f;
    float attackIntentRangeX = 1.10f;
    float attackIntentRangeY = 0.65f;

    [Header("Anti-Passivity")]
    float passiveNearGraceTime = 1.0f;
    float passiveNearPenaltyPerSecond = -0.0009f;
    float defensivePassivityMultiplier = 1f;
    float passiveNearTimer = 0f;

    [Header("Anti Body-Push Cheese")]
    float bodyPushRangeX = 0.48f;
    float bodyPushRangeY = 0.75f;
    float bodyPushGraceTime = 0.16f;
    float bodyPushPenaltyPerSecond = -0.023f;
    float bodyPushBlockMultiplier = 1.7f;
    float bodyPushRealPressureGrace = 0.22f;
    float bodyPushTimer = 0f;
    float recentRealPressureTimer = 0f;

    int lastHeavyAction = 0;

    bool chargeTrackingActive = false;
    int chargeStartOppHP = 0;
    bool chargeWasFullyCharged = false;

    private FighterAgentRewardDebugger rewardDebugger;

    int lastSelfHP, lastOppHP;
    int lastMoveX = 0;

    int lastActionIntent = 0;
    int consecutiveActionChanges = 0;

    float edgeStayTimer = 0f;
    float edgeAnchorX = 0f;
    bool edgeAnchorInitialized = false;
    float lastAbsDx = 0f;
    bool profileLoaded = false;

    int lastLightAction = 0;
    int lastSpecialAction = 0;
    int lastJumpAction = 0;

    int lastChargeModeForSpam = 0;
    int lastChargeModeForOutcome = 0;
    int consecutiveChargeStarts = 0;

    float timeSinceLastChargeStart = 999f;

    float blockHoldTimer = 0f;

    int lastStartedIntent = 0;
    int consecutiveSameMoveStarts = 0;

    FighterAgent oppAgent;

    void Start()
    {
        TryBindNow();
    }

    void Update()
    {
        if (self == null || opp == null)
        {
            TryBindNow();
        }

        if (self != null && !profileLoaded)
        {
            RefreshCharacterProfile();
        }
    }

    void TryBindNow()
    {
        if (self == null && selfManager != null)
        {
            var c = selfManager.CharacterChoice(1);
            if (c != null)
            {
                BindSelf(c);
            }
        }

        if (opp == null && enemyManager != null)
        {
            var e = enemyManager.CharacterChoice(1);
            if (e != null)
            {
                BindEnemy(e);
            }
        }
    }

    public override void Initialize()
    {
        if (!selfManager)
        {
            selfManager = GetComponent<CharacterManager>();
        }

        aiInput = new AIInputProvider(playerSuffix);

        selfManager.OnCharacterReady += BindSelf;
        selfManager.OnCharacterChanged += BindSelf;

        if (enemyManager != null)
        {
            enemyManager.OnCharacterReady += BindEnemy;
            enemyManager.OnCharacterChanged += BindEnemy;
        }

        rewardDebugger = GetComponent<FighterAgentRewardDebugger>();
    }

    private void BindSelf(Character c)
    {
        self = c;
        if (self == null)
        {
            return;
        }

        var setup = self.GetComponent<CharacterSetup>();

        upK = setup.up;
        downK = setup.down;
        leftK = setup.left;
        rightK = setup.right;
        lightK = setup.lightAttack;
        heavyK = setup.heavyAttack;
        blockK = setup.block;
        abilityK = setup.ability;
        chargeK = setup.charge;
        parryK = setup.parry;

        if (aiInput == null)
        {
            aiInput = new AIInputProvider(playerSuffix);
        }

        aiInput.SetKeys(leftK, rightK, upK, downK, lightK, heavyK, blockK, abilityK, chargeK, parryK);

        self.SetInput(aiInput);

        lastSelfHP = self.GetCurrentHealth();

        RefreshCharacterProfile();
    }

    private void BindEnemy(Character c)
    {
        opp = c;

        if (opp != null)
        {
            lastOppHP = opp.GetCurrentHealth();
        }

        if (self != null && opp != null)
        {
            lastAbsDx = Mathf.Abs(opp.transform.position.x - self.transform.position.x);
        }

        if (enemyManager != null)
        {
            oppAgent = enemyManager.GetComponent<FighterAgent>();
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (!self || !opp)
        {
            for (int i = 0; i < 67; i++)
            {
                sensor.AddObservation(0f);
            }
            return;
        }

        var rb = self.GetComponent<Rigidbody2D>();
        var orb = opp.GetComponent<Rigidbody2D>();

        Vector2 vel = rb ? rb.velocity : Vector2.zero;
        Vector2 ovel = orb ? orb.velocity : Vector2.zero;
        Vector2 rel = (Vector2)(opp.transform.position - self.transform.position);

        float nx = Mathf.Clamp(rel.x / relXScale, -1f, 1f);
        float ny = Mathf.Clamp(rel.y / relYScale, -1f, 1f);
        float absDxNorm = Mathf.Clamp(Mathf.Abs(rel.x) / relXScale, 0f, 1f);
        float absDyNorm = Mathf.Clamp(Mathf.Abs(rel.y) / relYScale, 0f, 1f);

        float facingSign = Mathf.Sign(self.transform.localScale.x);
        float oppDirSign = Mathf.Sign(rel.x);
        float facingCorrectly = (facingSign == oppDirSign) ? 1f : 0f;

        sensor.AddObservation(nx);
        sensor.AddObservation(ny);

        sensor.AddObservation(absDxNorm);
        sensor.AddObservation(absDyNorm);

        sensor.AddObservation(Mathf.Clamp(vel.x / velScale, -1f, 1f));
        sensor.AddObservation(Mathf.Clamp(vel.y / velScale, -1f, 1f));
        sensor.AddObservation(Mathf.Clamp(ovel.x / velScale, -1f, 1f));
        sensor.AddObservation(Mathf.Clamp(ovel.y / velScale, -1f, 1f));

        sensor.AddObservation(self.GetCurrentHealth() / 100f);
        sensor.AddObservation(opp.GetCurrentHealth() / 100f);

        sensor.AddOneHotObservation(self.characterID, totalCharacterCount);
        sensor.AddOneHotObservation(opp.characterID, totalCharacterCount);

        sensor.AddObservation(self.IsGrounded);
        sensor.AddObservation(self.IsBlocking);
        sensor.AddObservation(self.IsCasting);
        sensor.AddObservation(self.IsStunned);
        sensor.AddObservation(self.IsKnocked);
        sensor.AddObservation(self.IsCharging);
        sensor.AddObservation(self.IsCharged);
        sensor.AddObservation(self.OnAbilityCD);
        sensor.AddObservation(self.AbilityCooldown01);
        sensor.AddObservation(self.CanCast);
        sensor.AddObservation(self.CanParry);
        sensor.AddObservation(self.LightAttacking);
        sensor.AddObservation(self.HeavyAttacking);
        sensor.AddObservation(self.Parrying);

        sensor.AddObservation(self.QuickDisabled);
        sensor.AddObservation(self.HeavyDisabled);
        sensor.AddObservation(self.BlockDisabled);
        sensor.AddObservation(self.SpecialDisabled);
        sensor.AddObservation(self.ChargeDisabled);
        sensor.AddObservation(self.JumpDisabled);

        sensor.AddObservation(opp.IsGrounded);
        sensor.AddObservation(opp.IsBlocking);
        sensor.AddObservation(opp.IsCasting);
        sensor.AddObservation(opp.IsStunned);
        sensor.AddObservation(opp.IsKnocked);
        sensor.AddObservation(opp.IsCharging);
        sensor.AddObservation(opp.IsCharged);
        sensor.AddObservation(opp.OnAbilityCD);
        sensor.AddObservation(opp.AbilityCooldown01);
        sensor.AddObservation(opp.CanCast);
        sensor.AddObservation(opp.CanParry);
        sensor.AddObservation(opp.LightAttacking);
        sensor.AddObservation(opp.HeavyAttacking);
        sensor.AddObservation(opp.Parrying);

        sensor.AddObservation(oppDirSign);
        sensor.AddObservation(facingSign);
        sensor.AddObservation(facingCorrectly);
    }

    void ShapingRewards()
    {
        if (self == null || opp == null)
        {
            return;
        }

        if (GameManager.instance == null || !GameManager.instance.trainingRoundOn)
        {
            return;
        }

        float absDx = Mathf.Abs(opp.transform.position.x - self.transform.position.x);
        float absDy = Mathf.Abs(opp.transform.position.y - self.transform.position.y);

        bool inUsefulRange =
            absDx >= usefulRangeMinX &&
            absDx <= usefulRangeMaxX &&
            absDy <= usefulRangeMaxY;

        if (inUsefulRange)
        {
            AddReward(spacingBonus);
            rewardDebugger?.LogSpacing(spacingBonus);
        }

        bool badVerticalCheese =
            absDx <= verticalCheeseMaxX &&
            absDy >= verticalCheeseMinY;

        if (badVerticalCheese)
        {
            float dt = GetSafeDeltaTime();
            verticalCheeseTimer += dt;

            if (verticalCheeseTimer > verticalCheeseGraceTime)
            {
                float extraTime = verticalCheeseTimer - verticalCheeseGraceTime;

                float penalty =
                    verticalCheesePenaltyBase +
                    extraTime * verticalCheesePenaltyPerSecond;

                penalty = Mathf.Max(penalty, verticalCheesePenaltyCap);

                AddReward(penalty);
                rewardDebugger?.LogStackPenalty(penalty);
            }
        }
        else
        {
            float dt = GetSafeDeltaTime();

            verticalCheeseTimer -= verticalCheeseDecayPerSecond * dt;
            if (verticalCheeseTimer < 0f)
            {
                verticalCheeseTimer = 0f;
            }
        }
    }

    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        if (self == null)
        {
            return;
        }

        bool locked = self.IsStunned || self.IsCasting;

        if (self.IsCharging)
        {
            actionMask.SetActionEnabled(1, 1, false);
            actionMask.SetActionEnabled(2, 1, false);
            actionMask.SetActionEnabled(3, 1, false);
            actionMask.SetActionEnabled(4, 1, false);
            actionMask.SetActionEnabled(5, 1, false);
            actionMask.SetActionEnabled(6, 1, false);
            actionMask.SetActionEnabled(8, 1, false);

            actionMask.SetActionEnabled(0, 0, false);
            actionMask.SetActionEnabled(0, 2, false);

            if (self.IsCharged)
            {
                actionMask.SetActionEnabled(7, 0, false);
                actionMask.SetActionEnabled(7, 1, false);
            }
            else
            {
                actionMask.SetActionEnabled(7, 0, false);
            }

            return;
        }

        bool canJumpNow = self.IsGrounded;
        if (!canJumpNow || locked || self.JumpDisabled)
        {
            actionMask.SetActionEnabled(1, 1, false);
        }

        if (!self.IsGrounded || locked)
        {
            actionMask.SetActionEnabled(2, 1, false);
        }

        if (locked || self.QuickDisabled)
        {
            actionMask.SetActionEnabled(3, 1, false);
        }

        if (locked || self.HeavyDisabled)
        {
            actionMask.SetActionEnabled(4, 1, false);
        }

        if (locked || self.BlockDisabled)
        {
            actionMask.SetActionEnabled(5, 1, false);
        }

        if (locked || self.OnAbilityCD || !self.CanCast || self.SpecialDisabled)
        {
            actionMask.SetActionEnabled(6, 1, false);
        }

        bool canChargeNow = !locked && !self.ChargeDisabled;
        if (!canChargeNow)
        {
            actionMask.SetActionEnabled(7, 1, false);
            actionMask.SetActionEnabled(7, 2, false);
        }

        if (locked || !self.CanParry)
        {
            actionMask.SetActionEnabled(8, 1, false);
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (!self)
        {
            return;
        }

        if (GameManager.instance == null || !GameManager.instance.trainingRoundOn)
        {
            return;
        }

        int moveBranch = actions.DiscreteActions[0];
        int jump = actions.DiscreteActions[1];
        int drop = actions.DiscreteActions[2];
        int light = actions.DiscreteActions[3];
        int heavy = actions.DiscreteActions[4];
        int blockHold = actions.DiscreteActions[5];
        int special = actions.DiscreteActions[6];
        int chargeMode = actions.DiscreteActions[7];
        int parry = actions.DiscreteActions[8];

        int moveX = moveBranch == 0 ? -1 : (moveBranch == 1 ? 0 : 1);
        lastMoveX = moveX;

        var cmd = new AIInputProvider.Command
        {
            moveX = moveX,
            jump = (jump == 1),
            drop = (drop == 1),
            light = (light == 1),
            heavy = (heavy == 1),
            blockHold = (blockHold == 1),
            special = (special == 1),
            chargeHold = (chargeMode == 1),
            chargeRelease = (chargeMode == 2),
            parry = (parry == 1)
        };

        aiInput.Apply(cmd);

        AddReward(stepPenalty);
        rewardDebugger?.LogStepPenalty(stepPenalty);

        int selfHP = self.GetCurrentHealth();
        int oppHP = (opp != null) ? opp.GetCurrentHealth() : lastOppHP;

        int dealt = Mathf.Max(0, lastOppHP - oppHP);
        int taken = Mathf.Max(0, lastSelfHP - selfHP);

        float dealtReward = dealt * rewardDamageDealt;
        float takenReward = taken * rewardDamageTaken;

        AddReward(dealtReward);
        AddReward(takenReward);

        rewardDebugger?.LogDamageDealt(dealtReward);
        rewardDebugger?.LogDamageTaken(takenReward);

        lastSelfHP = selfHP;
        lastOppHP = oppHP;

        ShapingRewards();
        BehaviorHygieneRewards(jump, drop, light, heavy, blockHold, special, chargeMode, parry);
        TacticalRangeRewards(light, heavy, special, chargeMode);
        OffensiveIntentRewards(jump, drop, light, heavy, blockHold, special, chargeMode, parry);
        DirectionalHygieneRewards(moveX, light, special);
        PressureRewards(moveX, blockHold);
        PassivityPenalty(light, heavy, special, chargeMode, parry, blockHold);
        BodyPushCheesePenalty(moveX, light, heavy, special, chargeMode, parry, blockHold);
        ChargeSpamPenalty(chargeMode);
        ChargeReleaseOutcomePenalty(chargeMode);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var d = actionsOut.DiscreteActions;

        d[0] = 1;
        if (Input.GetKey(leftK))
        {
            d[0] = 0;
        }
        if (Input.GetKey(rightK))
        {
            d[0] = 2;
        }

        d[1] = Input.GetKey(upK) ? 1 : 0;
        d[2] = Input.GetKey(downK) ? 1 : 0;

        d[3] = Input.GetKey(lightK) ? 1 : 0;
        d[4] = Input.GetKey(heavyK) ? 1 : 0;
        d[5] = Input.GetKey(blockK) ? 1 : 0;
        d[6] = Input.GetKey(abilityK) ? 1 : 0;

        if (Input.GetKeyUp(chargeK))
        {
            d[7] = 2;
        }
        else if (Input.GetKey(chargeK))
        {
            d[7] = 1;
        }
        else
        {
            d[7] = 0;
        }

        d[8] = Input.GetKey(parryK) ? 1 : 0;
    }

    public override void OnEpisodeBegin()
    {
        TryBindNow();

        if (self == null || opp == null)
        {
            return;
        }

        rewardDebugger?.BeginEpisode(
            playerSuffix,
            CurrentCharacterIdOrNull(),
            lightReachType,
            specialReachType,
            profileLoaded
        );

        lastSelfHP = self.GetCurrentHealth();
        lastOppHP = opp.GetCurrentHealth();
        lastMoveX = 0;

        if (self != null && opp != null)
        {
            lastAbsDx = Mathf.Abs(opp.transform.position.x - self.transform.position.x);
        }
        else
        {
            lastAbsDx = 0f;
        }

        lastActionIntent = 0;
        consecutiveActionChanges = 0;

        edgeStayTimer = 0f;
        edgeAnchorX = 0f;
        edgeAnchorInitialized = false;

        if (aiInput != null)
        {
            aiInput.Apply(new AIInputProvider.Command
            {
                moveX = 0,
                jump = false,
                drop = false,
                light = false,
                heavy = false,
                blockHold = false,
                special = false,
                chargeHold = false,
                chargeRelease = false,
                parry = false
            });
        }

        lastLightAction = 0;
        lastSpecialAction = 0;
        lastJumpAction = 0;

        lastChargeModeForSpam = 0;
        lastChargeModeForOutcome = 0;
        consecutiveChargeStarts = 0;
        timeSinceLastChargeStart = 999f;

        chargeTrackingActive = false;
        chargeStartOppHP = 0;
        chargeWasFullyCharged = false;

        verticalCheeseTimer = 0f;

        blockHoldTimer = 0f;
        lastStartedIntent = 0;
        consecutiveSameMoveStarts = 0;

        passiveNearTimer = 0f;
        lastHeavyAction = 0;

        bodyPushTimer = 0f;
        recentRealPressureTimer = 0f;
    }

    private void OnDestroy()
    {
        if (selfManager != null)
        {
            selfManager.OnCharacterReady -= BindSelf;
            selfManager.OnCharacterChanged -= BindSelf;
        }

        if (enemyManager != null)
        {
            enemyManager.OnCharacterReady -= BindEnemy;
            enemyManager.OnCharacterChanged -= BindEnemy;
        }
    }

    void BehaviorHygieneRewards(int jump, int drop, int light, int heavy, int blockHold, int special, int chargeMode, int parry)
    {
        if (self == null || opp == null)
        {
            return;
        }

        if (GameManager.instance == null || !GameManager.instance.trainingRoundOn)
        {
            return;
        }

        int currentIntent = GetActionIntent(jump, drop, light, heavy, blockHold, special, chargeMode, parry);
        int previousIntent = lastActionIntent;

        if (currentIntent != 0 && previousIntent != 0 && currentIntent != previousIntent)
        {
            consecutiveActionChanges++;
        }
        else if (currentIntent == 0 || currentIntent == previousIntent)
        {
            consecutiveActionChanges = 0;
        }

        if (consecutiveActionChanges >= mashChangeThreshold)
        {
            AddReward(mashPenalty);
            rewardDebugger?.LogMashPenalty(mashPenalty);
        }

        bool jumpPressedNow = (jump == 1 && lastJumpAction == 0);
        if (jumpPressedNow && !self.IsGrounded)
        {
            AddReward(airJumpPenalty);
            rewardDebugger?.LogAirJumpPenalty(airJumpPenalty);
        }

        float dt = GetSafeDeltaTime();

        if (blockHold == 1)
        {
            blockHoldTimer += dt;

            if (blockHoldTimer > blockHoldGraceTime)
            {
                float blockPenalty = longBlockHoldPenaltyPerSecond * dt;
                AddReward(blockPenalty);
                rewardDebugger?.LogBlockHoldPenalty(blockPenalty);
            }
        }
        else
        {
            blockHoldTimer -= blockHoldDecayPerSecond * dt;
            if (blockHoldTimer < 0f)
            {
                blockHoldTimer = 0f;
            }
        }

        bool startedNow = (currentIntent != 0 && previousIntent == 0);

        if (startedNow)
        {
            if (currentIntent == lastStartedIntent)
            {
                consecutiveSameMoveStarts++;
            }
            else
            {
                lastStartedIntent = currentIntent;
                consecutiveSameMoveStarts = 1;
            }

            if (consecutiveSameMoveStarts > freeRepeatedSameMoveStarts)
            {
                int extraRepeats = consecutiveSameMoveStarts - freeRepeatedSameMoveStarts - 1;

                float repeatPenalty =
                    repeatedSameMovePenaltyBase +
                    extraRepeats * repeatedSameMovePenaltyStep;

                repeatPenalty = Mathf.Max(repeatPenalty, repeatedSameMovePenaltyCap);

                // AddReward(repeatPenalty);
                // rewardDebugger?.LogRepeatSameMovePenalty(repeatPenalty);
            }
        }

        float x = self.transform.position.x;
        bool nearEdge = Mathf.Abs(x) >= edgeZoneX;

        if (nearEdge)
        {
            if (!edgeAnchorInitialized)
            {
                edgeAnchorInitialized = true;
                edgeAnchorX = x;
                edgeStayTimer = 0f;
            }

            float movedFromAnchor = Mathf.Abs(x - edgeAnchorX);

            if (movedFromAnchor <= edgeSmallMoveThreshold)
            {
                edgeStayTimer += dt;

                if (edgeStayTimer > edgeGraceTime)
                {
                    AddReward(edgeCampPenalty);
                    rewardDebugger?.LogEdgeCampPenalty(edgeCampPenalty);
                }
            }
            else
            {
                edgeAnchorX = x;
                edgeStayTimer = Mathf.Max(0f, edgeStayTimer - dt * 1.5f);
            }
        }
        else
        {
            edgeAnchorInitialized = false;
            edgeStayTimer = 0f;
        }

        lastActionIntent = currentIntent;
        lastJumpAction = jump;
    }

    int GetActionIntent(int jump, int drop, int light, int heavy, int blockHold, int special, int chargeMode, int parry)
    {
        if (parry == 1)
        {
            return 7;
        }

        if (chargeMode != 0)
        {
            return 6;
        }

        if (special == 1)
        {
            return 5;
        }

        if (blockHold == 1)
        {
            return 4;
        }

        if (heavy == 1)
        {
            return 3;
        }

        if (light == 1)
        {
            return 2;
        }

        if (jump == 1 || drop == 1)
        {
            return 1;
        }

        return 0;
    }

    bool IsStrictMelee(ReachType reachType)
    {
        return reachType == ReachType.Melee;
    }

    void TacticalRangeRewards(int light, int heavy, int special, int chargeMode)
    {
        if (self == null || opp == null)
        {
            return;
        }

        if (GameManager.instance == null || !GameManager.instance.trainingRoundOn)
        {
            return;
        }

        float absDx = Mathf.Abs(opp.transform.position.x - self.transform.position.x);

        float approachStartDistance = usefulRangeMaxX + approachStartMargin;
        bool farFromOpponent = absDx > approachStartDistance;

        if (farFromOpponent && absDx < lastAbsDx)
        {
            AddReward(approachBonus);
            rewardDebugger?.LogApproachReward(approachBonus);
        }

        bool absurdlyFar = absDx >= extremeFarThreshold;

        if (absurdlyFar)
        {
            if (heavy == 1)
            {
                AddReward(extremeFarHeavyPenalty);
                rewardDebugger?.LogExtremeFarHeavyPenalty(extremeFarHeavyPenalty);
            }

            if (chargeMode == 1)
            {
                AddReward(extremeFarChargePenalty);
                rewardDebugger?.LogExtremeFarChargePenalty(extremeFarChargePenalty);
            }

            if (light == 1 && IsStrictMelee(lightReachType))
            {
                AddReward(farMeleeLightPenalty);
                rewardDebugger?.LogFarMeleeLightPenalty(farMeleeLightPenalty);
            }

            if (special == 1 && IsStrictMelee(specialReachType))
            {
                AddReward(farMeleeSpecialPenalty);
                rewardDebugger?.LogFarMeleeSpecialPenalty(farMeleeSpecialPenalty);
            }
        }

        lastAbsDx = absDx;
    }

    void RefreshCharacterProfile()
    {
        lightReachType = ReachType.Melee;
        specialReachType = ReachType.Melee;
        profileLoaded = false;

        if (self == null)
        {
            return;
        }

        if (self.characterID < 0)
        {
            return;
        }

        if (CharacterMLProfileDatabase.Instance == null)
        {
            return;
        }

        var profile = CharacterMLProfileDatabase.Instance.GetProfileByID(self.characterID);
        if (profile == null)
        {
            return;
        }

        lightReachType = profile.lightReachType;
        specialReachType = profile.specialReachType;
        profileLoaded = true;
    }

    bool IsDashType(ReachType reachType)
    {
        return reachType == ReachType.Dash;
    }

    bool IsFacingOpponent()
    {
        if (self == null || opp == null)
        {
            return true;
        }

        float relX = opp.transform.position.x - self.transform.position.x;
        float oppDirSign = Mathf.Sign(relX);
        float facingSign = Mathf.Sign(self.transform.localScale.x);

        return facingSign == oppDirSign;
    }

    void DirectionalHygieneRewards(int moveX, int light, int special)
    {
        if (self == null || opp == null)
        {
            return;
        }

        if (GameManager.instance == null || !GameManager.instance.trainingRoundOn)
        {
            return;
        }

        bool lightPressedNow = (light == 1 && lastLightAction == 0);
        bool specialPressedNow = (special == 1 && lastSpecialAction == 0);

        if (moveX == 0)
        {
            if (lightPressedNow && IsDashType(lightReachType))
            {
                AddReward(dashNoDirectionPenalty);
                rewardDebugger?.LogDashNoDirectionPenalty(dashNoDirectionPenalty);
            }

            if (specialPressedNow && IsDashType(specialReachType))
            {
                AddReward(dashNoDirectionPenalty);
                rewardDebugger?.LogDashNoDirectionPenalty(dashNoDirectionPenalty);
            }
        }

        bool facingOpponent = IsFacingOpponent();

        if (specialPressedNow && !facingOpponent && specialReachType != ReachType.Global)
        {
            AddReward(wrongFacingSpecialPenalty);
            rewardDebugger?.LogWrongFacingSpecialPenalty(wrongFacingSpecialPenalty);
        }

        lastLightAction = light;
        lastSpecialAction = special;
    }

    public void ClearInput()
    {
        if (aiInput != null)
        {
            aiInput.Apply(new AIInputProvider.Command
            {
                moveX = 0,
                jump = false,
                drop = false,
                light = false,
                heavy = false,
                blockHold = false,
                special = false,
                chargeHold = false,
                chargeRelease = false,
                parry = false
            });
        }
    }

    public void ForceRebind()
    {
        self = null;
        opp = null;
        profileLoaded = false;
        TryBindNow();
    }

    int? CurrentCharacterIdOrNull()
    {
        return self != null ? self.characterID : (int?)null;
    }

    public void DebugEndEpisode(string reason)
    {
        rewardDebugger?.EndEpisode(
            playerSuffix,
            reason,
            CurrentCharacterIdOrNull(),
            lightReachType,
            specialReachType,
            profileLoaded
        );
    }

    public void ApplyTerminalReward()
    {
        if (self == null) return;

        int selfHP = self.GetCurrentHealth();
        int oppHP = (opp != null) ? opp.GetCurrentHealth() : lastOppHP;

        lastSelfHP = selfHP;
        lastOppHP = oppHP;

        if (selfHP <= 0 && oppHP <= 0)
        {
            return;
        }

        if (oppHP <= 0)
        {
            AddReward(rewardWin);
            rewardDebugger?.LogWinReward(rewardWin);
            return;
        }

        if (selfHP <= 0)
        {
            AddReward(rewardLoss);
            rewardDebugger?.LogLossReward(rewardLoss);
            return;
        }
    }

    void ChargeSpamPenalty(int chargeMode)
    {
        if (self == null || opp == null)
        {
            return;
        }

        if (GameManager.instance == null || !GameManager.instance.trainingRoundOn)
        {
            return;
        }

        float dt = GetSafeDeltaTime();

        timeSinceLastChargeStart += dt;

        if (timeSinceLastChargeStart > chargeChainDecaySeconds)
        {
            consecutiveChargeStarts = 0;
        }

        bool chargeStartedNow = (chargeMode == 1 && lastChargeModeForSpam != 1);

        if (chargeStartedNow)
        {
            if (timeSinceLastChargeStart > chargeChainDecaySeconds)
            {
                consecutiveChargeStarts = 0;
            }

            consecutiveChargeStarts++;
            timeSinceLastChargeStart = 0f;

            if (consecutiveChargeStarts > freeConsecutiveCharges)
            {
                int extraCharges = consecutiveChargeStarts - freeConsecutiveCharges - 1;

                float penalty = repeatedChargePenaltyBase + extraCharges * repeatedChargePenaltyStep;
                penalty = Mathf.Max(penalty, repeatedChargePenaltyCap);

                AddReward(penalty);
                rewardDebugger?.LogChargeSpamPenalty(penalty);
            }
        }

        lastChargeModeForSpam = chargeMode;
    }

    void ChargeReleaseOutcomePenalty(int chargeMode)
    {
        if (self == null || opp == null)
        {
            return;
        }

        if (GameManager.instance == null || !GameManager.instance.trainingRoundOn)
        {
            return;
        }

        bool chargeStartedNow = (chargeMode == 1 && lastChargeModeForOutcome != 1);
        bool chargeReleasedNow = (chargeMode == 2);

        if (chargeStartedNow)
        {
            chargeTrackingActive = true;
            chargeStartOppHP = opp.GetCurrentHealth();
            chargeWasFullyCharged = false;
        }

        if (chargeTrackingActive && self.IsCharged)
        {
            chargeWasFullyCharged = true;
        }

        if (chargeTrackingActive && chargeReleasedNow)
        {
            int oppHPNow = opp.GetCurrentHealth();
            bool dealtDamage = oppHPNow < chargeStartOppHP;

            if (chargeWasFullyCharged && !dealtDamage)
            {
                // AddReward(emptyReleasedChargePenalty);
                // rewardDebugger?.LogEmptyChargeReleasePenalty(emptyReleasedChargePenalty);
            }

            chargeTrackingActive = false;
            chargeWasFullyCharged = false;
        }

        if (chargeTrackingActive && !self.IsCharging && chargeMode == 0 && lastChargeModeForOutcome == 1)
        {
            chargeTrackingActive = false;
            chargeWasFullyCharged = false;
        }

        lastChargeModeForOutcome = chargeMode;
    }

    void PressureRewards(int moveX, int blockHold)
    {
        if (self == null || opp == null)
        {
            return;
        }

        if (GameManager.instance == null || !GameManager.instance.trainingRoundOn)
        {
            return;
        }

        float dx = opp.transform.position.x - self.transform.position.x;
        float absDx = Mathf.Abs(dx);
        float absDy = Mathf.Abs(opp.transform.position.y - self.transform.position.y);

        int towardOpponent = dx > 0f ? 1 : -1;
        bool movingToward = moveX != 0 && moveX == towardOpponent;
        bool movingAway = moveX != 0 && moveX == -towardOpponent;

        bool closeEnoughToPressure =
            absDx <= closePressureRangeX &&
            absDy <= closePressureRangeY;

        bool outsideCloseRange =
            absDx > usefulRangeMaxX + 0.2f;

        if (outsideCloseRange && movingToward)
        {
            AddReward(forwardPressureBonus);
            rewardDebugger?.LogApproachReward(forwardPressureBonus);
        }

        bool goodPressureDistance =
            absDx >= usefulRangeMinX &&
            absDx <= closePressureRangeX &&
            absDy <= closePressureRangeY;

        bool tooCloseBodyPushZone =
            absDx <= bodyPushRangeX &&
            absDy <= bodyPushRangeY;

        bool doingRealPressure =
            self.LightAttacking ||
            self.HeavyAttacking ||
            self.IsCasting ||
            self.IsCharging ||
            self.IsCharged ||
            self.Parrying;

        if (goodPressureDistance && movingToward && blockHold == 0 && (!tooCloseBodyPushZone || doingRealPressure))
        {
            AddReward(closePressureBonus);
        }

        if (closeEnoughToPressure && movingAway)
        {
            AddReward(retreatFromCloseRangePenalty);
        }
    }

    void OffensiveIntentRewards(int jump, int drop, int light, int heavy, int blockHold, int special, int chargeMode, int parry)
    {
        if (self == null || opp == null)
        {
            return;
        }

        if (GameManager.instance == null || !GameManager.instance.trainingRoundOn)
        {
            return;
        }

        float absDx = Mathf.Abs(opp.transform.position.x - self.transform.position.x);
        float absDy = Mathf.Abs(opp.transform.position.y - self.transform.position.y);

        bool facingOpponent = IsFacingOpponent();

        bool inCloseRange =
            absDx <= attackIntentRangeX &&
            absDy <= attackIntentRangeY &&
            facingOpponent;

        bool nonMovementAction =
            jump == 1 ||
            drop == 1 ||
            light == 1 ||
            heavy == 1 ||
            blockHold == 1 ||
            special == 1 ||
            chargeMode != 0 ||
            parry == 1;

        if (inCloseRange && nonMovementAction)
        {
            AddReward(usefulIntentBonus);
        }
    }

    void PassivityPenalty(int light, int heavy, int special, int chargeMode, int parry, int blockHold)
    {
        if (self == null || opp == null)
        {
            return;
        }

        if (GameManager.instance == null || !GameManager.instance.trainingRoundOn)
        {
            return;
        }

        float dt = GetSafeDeltaTime();

        float absDx = Mathf.Abs(opp.transform.position.x - self.transform.position.x);
        float absDy = Mathf.Abs(opp.transform.position.y - self.transform.position.y);

        bool closeNeutral =
            absDx <= closePressureRangeX &&
            absDy <= closePressureRangeY;

        bool offensiveAction =
            (light == 1) ||
            (heavy == 1) ||
            (special == 1) ||
            (chargeMode != 0);

        bool defensiveAction =
            (blockHold == 1) ||
            (parry == 1);

        if (closeNeutral && !offensiveAction)
        {
            passiveNearTimer += dt;

            if (passiveNearTimer > passiveNearGraceTime)
            {
                float penalty = passiveNearPenaltyPerSecond * dt;

                if (defensiveAction)
                {
                    penalty *= defensivePassivityMultiplier;
                }

                AddReward(penalty);
            }
        }
        else
        {
            passiveNearTimer -= 2f * dt;
            if (passiveNearTimer < 0f)
            {
                passiveNearTimer = 0f;
            }
        }
    }

    void BodyPushCheesePenalty(int moveX, int light, int heavy, int special, int chargeMode, int parry, int blockHold)
    {
        if (self == null || opp == null)
        {
            return;
        }

        if (GameManager.instance == null || !GameManager.instance.trainingRoundOn)
        {
            return;
        }

        float dt = GetSafeDeltaTime();

        float dx = opp.transform.position.x - self.transform.position.x;
        float absDx = Mathf.Abs(dx);
        float absDy = Mathf.Abs(opp.transform.position.y - self.transform.position.y);

        int towardOpponent = dx > 0f ? 1 : -1;

        bool movingToward =
            moveX != 0 &&
            moveX == towardOpponent;

        bool veryClose =
            absDx <= bodyPushRangeX &&
            absDy <= bodyPushRangeY;

        bool actionStartedThisStep =
            light == 1 ||
            heavy == 1 ||
            special == 1 ||
            chargeMode != 0 ||
            parry == 1;

        bool realActivePressure =
            self.LightAttacking ||
            self.HeavyAttacking ||
            self.IsCasting ||
            self.IsCharging ||
            self.IsCharged ||
            self.Parrying;

        if (actionStartedThisStep || realActivePressure)
        {
            recentRealPressureTimer = bodyPushRealPressureGrace;
            bodyPushTimer = 0f;
        }
        else
        {
            recentRealPressureTimer -= dt;
            if (recentRealPressureTimer < 0f)
            {
                recentRealPressureTimer = 0f;
            }
        }

        bool protectedByRealPressure =
            recentRealPressureTimer > 0f;

        bool blockingPush =
            blockHold == 1 &&
            movingToward &&
            veryClose;

        bool bodyPushCheese =
            veryClose &&
            movingToward &&
            !protectedByRealPressure;

        if (bodyPushCheese)
        {
            bodyPushTimer += dt;

            if (bodyPushTimer > bodyPushGraceTime)
            {
                float penalty = bodyPushPenaltyPerSecond * dt;

                if (blockingPush)
                {
                    penalty *= bodyPushBlockMultiplier;
                }
                rewardDebugger?.LogChargeSpamPenalty(penalty);
                AddReward(penalty);
            }
        }
        else
        {
            bodyPushTimer -= 3f * dt;
            if (bodyPushTimer < 0f)
            {
                bodyPushTimer = 0f;
            }
        }
    }

    float GetSafeDeltaTime()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f)
        {
            dt = 0.016f;
        }

        return dt;
    }
}