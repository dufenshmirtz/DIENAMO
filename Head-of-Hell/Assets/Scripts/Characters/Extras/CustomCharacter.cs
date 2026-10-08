using System;
using System.Collections;
using UnityEngine;

public class Custom : Character
{
    float cooldown = 16f;
    //int damage = 18;
    float spellTime = 0.94f;

    String lightName;

    String spellName;

    protected float lightCD = 3f;

    //CustomDash
    protected float dashingPower = 40f;
    protected float dashingTime = 0.1f;
    protected int DashDamage = 10;
    bool dashing = false;
    bool dashHit = false;

    //CustomBlink

    bool lightReady = true;
    protected float blinkPower = 10f;
    protected float blinkTime = 0.14f;
    protected int blinkDmg = 5;
    


    #region HeavyAttack
    override public void HeavyAttack()
    {
        animator.SetTrigger("HeavyAttack");
        audioManager.PlaySFX(audioManager.heavyswoosh, audioManager.heavySwooshVolume);
    }

    override public void DealHeavyDamage()
    {
        TelemetryManager.Instance?.LogAction(PlayerId, "Heavy");
        Collider2D hitEnemy = Physics2D.OverlapCircle( attackPoint.position,  attackRange,  enemyLayer);
        Character target = ResolveTargetFromHit(hitEnemy);

        if (target != null)
        {
            audioManager.PlaySFX(audioManager.explosion, audioManager.lessVol);
            TelemetryManager.Instance?.LogHitAttempt(PlayerId, enemy.PlayerId, MoveType.Heavy);
            enemy.SetIncomingDamageContext(PlayerId, MoveType.Heavy, SourceType.Melee);
            enemy.TakeDamage(heavyDamage, true);

            if (!enemy.isBlocking)
            {
                enemy.Knockback(11f, 0.15f, true);
            }

        }
        else
        {
            TelemetryManager.Instance?.LogMiss(PlayerId, MoveType.Heavy);
            audioManager.PlaySFX(audioManager.explosion, audioManager.lessVol);
        }

    }
    #endregion

    #region Spell
    public override void Spell()
    {
        SelectSpell(spellName);
        TelemetryManager.Instance?.LogAction(PlayerId, "Special");
        ignoreDamage = true;
        animator.SetTrigger("Spell");
        audioManager.PlaySFX(audioManager.bigExplosion, audioManager.doubleVol);
        UsingAbility(cooldown);
        stayStatic();
        StartCoroutine(SpellSafety(spellTime,cooldown));
    }

    public void SelectSpell(String name)
    {
        
    }
    #region  CustomDash
    IEnumerator CustomDash()
    {

        ignoreMovement = true;
        ignoreDamage = true;
        dashing = true;

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
        if (!dashHit)
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
        dashHit = false;
        dashing = false;

        // Trigger cooldown
        OnCooldown(cooldown);
    }

    public void DealCustomDashDmg()
    {
        enemy.StopPunching();
        enemy.BreakCharge();

        // Telemetry: HitAttempt + context before damage
        TelemetryManager.Instance?.LogHitAttempt(PlayerId, enemy.PlayerId, MoveType.Special);
        enemy.SetIncomingDamageContext(PlayerId, MoveType.Special, SourceType.Spell);

        enemy.TakeDamage(DashDamage, true);
        audioManager.PlaySFX(audioManager.dashHit, 3f);
    }

    override protected void OnTriggerEnter2D(Collider2D other)
    {
        Character target = ResolveTargetFromHit(other);
        if (dashing && target != null && !dashHit)  //--here
        {
            DealCustomDashDmg();
            dashHit = true;
        }

        base.OnTriggerEnter2D(other);
    }

    #endregion

    #endregion

    #region LightAttack
    public override void LightAttack()
    {
        if (lightReady)
        {
            
            lightReady = false;
            TelemetryManager.Instance?.LogAction(PlayerId, "Quick");
            QuickAttackIndicatorDisable();
            SelectLight(lightName);
            StartCoroutine(Blink());
        }
    }

    public void SelectLight(String name)
    {
        
    }

    #region CustomBlink

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

        StartCoroutine(ResetLight());
    }


    public void DealCustomBlinkDmg()
    {
        Collider2D hitEnemy = Physics2D.OverlapCircle(attackPoint.position, attackRange, enemyLayer);
        Character target = ResolveTargetFromHit(hitEnemy);

        if (target != null)
        {
            TelemetryManager.Instance?.LogHitAttempt(PlayerId, enemy.PlayerId, MoveType.Quick);
            enemy.SetIncomingDamageContext(PlayerId, MoveType.Quick, SourceType.Melee);
            enemy.TakeDamage(blinkDmg, true);
            enemy.Knockback(10f, .15f, true);
            audioManager.PlaySFX(audioManager.dashHit, 0.8f);
        }
        else
        {
            TelemetryManager.Instance?.LogMiss(PlayerId, MoveType.Quick);
        }
    }

    #endregion

    IEnumerator ResetLight()
    {
        yield return new WaitForSeconds(lightCD);
        audioManager.PlaySFX(audioManager.sworDashTada, audioManager.lessVol);
        lightReady = true;
        QuickAttackIndicatorEnable();
    }
    
    #endregion

    #region ChargeAttack
    public override void ChargeAttack()
    {
        base.ChargeAttack();
        animator.SetTrigger("Charge");
    }
    #endregion

}
