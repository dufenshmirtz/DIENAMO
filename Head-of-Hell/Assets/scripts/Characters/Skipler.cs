using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Skipler : Character
{
    public float cooldown = 8f;
    //Spell
    protected float dashingPower = 40f;
    protected float dashingTime = 0.1f;
    protected int DashDamage = 10;
    bool dashing = false;
    private readonly HashSet<Character> dashTargetsHit = new HashSet<Character>();
    //LightAttack
    bool lightReady = true;
    protected float blinkPower = 10f;
    protected float blinkTime = 0.14f;
    protected int blinkDmg = 5;
    protected float blinkCD = 3f;
    bool isIdlePlaying = false;

    public Transform skiplerPoint;
    public GameObject skiplerDouble;


    #region configure
    public override void Start()
    {
        base.Start();

        characterJump = audioManager.jumpGlitch;
        chargeHitSound= audioManager.chargeGlitch;
        //winQuip = audioManager.skipWin;
    }

    override public void Update()
    {
        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);

        // Check if the current state is not idle
        if (!currentState.IsName("Idle"))
        {
            if (isIdlePlaying)
            {
                StopIdleGlitch();
            }
        }

        base.Update();
    }
    #endregion
    
    #region HeavyAttack
    override public void HeavyAttack()
    {
        TelemetryManager.Instance?.LogAction(PlayerId, "Heavy");
        animator.SetTrigger("HeavyAttack");
        audioManager.PlaySFX(audioManager.heavyGlitch, 0.8f);
    }

    override public void DealHeavyDamage()
    {
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, enemyLayer);
        Character target = ResolveTargetFromHit(hitEnemies);

        if (target != null)
        {

            audioManager.PlaySFX(audioManager.heavyGlitchHit, 1.3f);
            TelemetryManager.Instance?.LogHitAttempt(PlayerId, target.PlayerId, MoveType.Heavy);
            target.SetIncomingDamageContext(PlayerId, MoveType.Heavy, SourceType.Melee);
            target.TakeDamage(heavyDamage, true);

            if (!target.isBlocking)
            {
                target.Knockback(11f, 0.15f, true);
            }

        }
        else
        {
            TelemetryManager.Instance?.LogMiss(PlayerId, MoveType.Heavy);
            audioManager.PlaySFX(audioManager.nothitGlitch, 1.8f);
        }

    }
    #endregion

    #region Spell
    override public void Spell()
    {
        TelemetryManager.Instance?.LogAction(PlayerId, "Special");
        UsingAbility(cooldown);
        StartCoroutine(Dash());
        StartCoroutine(SpellSafety(dashingTime,cooldown));
    }

    IEnumerator Dash()
    {
        skiplerDouble = resources.skiplerDouble;
        skiplerPoint = resources.skiplerPoint;

        // Instantiate the skipler double
        GameObject skipDouble = Instantiate(skiplerDouble, skiplerPoint.position, skiplerPoint.rotation);
        Vector3 newScale = skipDouble.transform.localScale;  // Get the current scale
        newScale.x = this.transform.localScale.x;            // Set the x scale to match the player's x scale
        skipDouble.transform.localScale = newScale;

        ignoreMovement = true;
        ignoreDamage = true;
        dashing = true;
        dashTargetsHit.Clear();

        // Disable gravity while dashing
        rb.gravityScale = 0f;

        // Store the current velocity
        Vector2 currentVelocity = rb.velocity;

        // Determine the dash direction based on input (keyboard always, controller only if controller == true)
        float moveDirection = input.GetKey(left) ? -1f : (input.GetKey(right) ? 1f : (controller ? input.GetAxis("Horizontal" + playerString) : 0f));

        // If no direction input was given, default to right
        if (moveDirection == 0f)
        {
            moveDirection = 1f; // Default direction in case no input
        }

        // Disable colliders temporarily during the dash
        Collider2D[] colliders = GetColliders();
        foreach (Collider2D collider in colliders)
        {
            collider.enabled = false;
        }
        colliders[3].enabled = true;  // Keep specific colliders enabled
        colliders[4].enabled = true;
        colliders[5].enabled = true;

        // Play dash sound effects
        audioManager.PlaySFX(audioManager.dash, 1);
        audioManager.PlaySFX(audioManager.dashGlitch, 1f);

        // Calculate the dash velocity
        Vector2 dashVelocity = new Vector2(moveDirection * dashingPower, currentVelocity.y);

        // Apply the dash velocity
        rb.velocity = dashVelocity;

        // Trigger the dash animation
        animator.SetTrigger("Spell");

        // Wait for the dash duration
        yield return new WaitForSeconds(dashingTime);

        // Telemetry: dash ended without landing a hit
        if (dashTargetsHit.Count == 0)
        {
            TelemetryManager.Instance?.LogMiss(PlayerId, MoveType.Special);
        }

        // Reset the velocity after the dash
        rb.velocity = currentVelocity;

        // Reset the gravity scale
        rb.gravityScale = originalGravityScale;


        // Re-enable damage and colliders
        foreach (Collider2D collider in colliders)
        {
            if (collider != colliders[3])
            {
                collider.enabled = true;
            }
        }
        colliders[3].enabled = false;
        colliders[4].enabled = false;
        colliders[5].enabled = false;

        // Reset dash state
        dashing = false;

        // Trigger cooldown
        OnCooldown(cooldown);
    }

    public void DealDashDmg(Character target)
    {
        if (target == null)
        {
            return;
        }

        target.StopPunching();
        target.BreakCharge();

        // Telemetry: HitAttempt + context before damage
        TelemetryManager.Instance?.LogHitAttempt(PlayerId, target.PlayerId, MoveType.Special);
        target.SetIncomingDamageContext(PlayerId, MoveType.Special, SourceType.Spell);

        target.TakeDamage(DashDamage, true);
        audioManager.PlaySFX(audioManager.dashHit, 3f);
    }

    override protected void OnTriggerEnter2D(Collider2D other)
    {
        Character collidedCharacter = GetCharacterFromCollider(other);

        if (dashing
            && gameManager != null
            && gameManager.IsTwoVersusTwoMatch()
            && collidedCharacter != null
            && gameManager.ArePlayersTeammates(playerNum, collidedCharacter.GetPlayerNum()))
        {
            return;
        }

        Character target = ResolveTargetFromHit(other);
        if (dashing && target != null && dashTargetsHit.Add(target))  //--here
        {
            DealDashDmg(target);
        }

        base.OnTriggerEnter2D(other);
    }
    #endregion

    #region LightAttack
    override public void LightAttack()
    {
        if (lightReady)
        {
            Unblock();
            isLightAttacking=true;
            moveSpeed = OGMoveSpeed;
            lightReady = false;
            TelemetryManager.Instance?.LogAction(PlayerId, "Quick");
            QuickAttackIndicatorDisable();
            StartCoroutine(Blink());
        }
    }

    IEnumerator Blink()
    {
        IgnoreMovement(true);

        // Disable gravity while dashing
        rb.gravityScale = 0f;

        // Store the current velocity
        Vector2 currentVelocity = rb.velocity;

        // Determine the dash direction based on input (keyboard always, controller only if controller == true)
        float moveDirection = input.GetKey(left) ? -1f : (input.GetKey(right) ? 1f : (controller ? input.GetAxis("Horizontal" + playerString) : 0f));

        // If no direction input was given, default to right
        if (moveDirection == 0f)
        {
            moveDirection = 1f; // Default direction in case no input
        }

        audioManager.PlaySFX(audioManager.quickGlitch, 0.7f);

        // Calculate the dash velocity
        Vector2 blinkVelocity = new Vector2(moveDirection * blinkPower, currentVelocity.y);

        // Apply the dash velocity
        rb.velocity = blinkVelocity;

        // Trigger the dash animation
        animator.SetTrigger("QuickAttack");

        // Wait for the dash duration
        yield return new WaitForSeconds(blinkTime);

        // Reset the velocity after the dash
        rb.velocity = currentVelocity;

        moveSpeed = OGMoveSpeed;

        // Reset the gravity scale
        rb.gravityScale = originalGravityScale;


        IgnoreMovement(false);

        StartCoroutine(ResetBlink());
    }


    IEnumerator ResetBlink()
    {
        yield return new WaitForSeconds(blinkCD);
        audioManager.PlaySFX(audioManager.sworDashTada, audioManager.lessVol);
        lightReady = true;
        QuickAttackIndicatorEnable();
    }

    public void DealBlinkDmg()
    {
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, enemyLayer);
        Character target = ResolveTargetFromHit(hitEnemies);

        if (target != null)
        {
            TelemetryManager.Instance?.LogHitAttempt(PlayerId, target.PlayerId, MoveType.Quick);
            target.SetIncomingDamageContext(PlayerId, MoveType.Quick, SourceType.Melee);
            target.TakeDamage(blinkDmg, true);
            target.Knockback(10f, .15f, true);
            audioManager.PlaySFX(audioManager.dashHit, 0.8f);
            ReduceCD();
        }
        else
        {
            TelemetryManager.Instance?.LogMiss(PlayerId, MoveType.Quick);
        }
    }
    #endregion

    #region ChargeAttack
    public override void ChargeAttack()
    {
        base.ChargeAttack();
        animator.SetTrigger("Charge");
    }
    #endregion

    #region Passive
    void ReduceCD()
    {
        cdTimer -= 2f;
    }

    public override void TutorialRefreshQuickAttack()
    {
        if (dashing || ignoreMovement || isLightAttacking)
        {
            return;
        }

        lightReady = true;
        QuickAttackIndicatorEnable();
    }
    #endregion

    #region Idle
    public void IdleGlitch()
    {
        audioManager.PlayAndStoreSFX(audioManager.idleGlitch, audioManager.normalVol);
        isIdlePlaying = true;
    }

    public void StopIdleGlitch()
    {
        audioManager.StopAndStoreSFX(audioManager.idleGlitch);  
        isIdlePlaying = false;
    }
    #endregion
}
