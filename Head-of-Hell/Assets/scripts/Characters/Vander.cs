using System.Collections;
using UnityEngine;

public class Vander : Character
{

    int stabDamage = 10, stabHeal = 5;
    public float cooldown = 12f;
    //katana
    int katanaDmg = 3;
    int smallLifesteal = 3;
    bool katanaready = true;
    bool katanaHitThisCast = false;

    int chargeLifesteal=8;

    float katanaCD=2f;

    // --- Flying passive ---
    bool isFlying = false;
    //float flyUpSpeed = 6f;        // how fast he ascends while holding jump
    float spellTime = 0.29f;

    // How quickly we cancel falling (units: velocity per second)
    //[SerializeField] float fallBrakePerSec = 20f;

    // How quickly we ramp upward (units: velocity per second)
    [SerializeField] float ascendAccelPerSec = 18f;

    // Hard cap for upward speed
    [SerializeField] float maxAscendSpeed = 4f;

    // --- Flying passive tuning ---
    [SerializeField] float fallStartEpsilon = 0.02f; // tiny buffer to detect start of fall


    // Keep the gravity to restore on release
    //float baseGravity = 1f;


    #region HeavyAttack
    override public void HeavyAttack()
    {
        animator.SetTrigger("HeavyAttack");
        audioManager.PlaySFX(audioManager.heavyswoosh, audioManager.heavySwooshVolume);
    }

    override public void DealHeavyDamage()
    {
        TelemetryManager.Instance?.LogAction(PlayerId, "Heavy");
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, enemyLayer);
        Character target = ResolveTargetFromHit(hitEnemies);

        if (target != null)
        {

            audioManager.PlaySFX(audioManager.katanaHit, 1f);
            TelemetryManager.Instance?.LogHitAttempt(PlayerId, target.PlayerId, MoveType.Heavy);
            target.SetIncomingDamageContext(PlayerId, MoveType.Heavy, SourceType.Melee);
            target.TakeDamage(heavyDamage, true);
            Lifesteal(smallLifesteal);
            if (!target.isBlocking)
            {
                target.Knockback(11f, 0.15f, true);
            }

        }
        else
        {
            TelemetryManager.Instance?.LogMiss(PlayerId, MoveType.Heavy);
            audioManager.PlaySFX(audioManager.katanaSwoosh, 1f);
        }

    }
    #endregion

    #region Spell
    override public void Spell()
    {
        TelemetryManager.Instance?.LogAction(PlayerId, "Special");
        attackRange += 0.5f;
        UsingAbility(cooldown);
        animator.SetTrigger("Spell");
        StartCoroutine(SpellSafety(spellTime,cooldown));
    }

    public void DealStabDmg()
    {
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, enemyLayer);
        var targets = ResolveTargetsFromHits(hitEnemies);

        if (targets.Count > 0)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                Character target = targets[i];
                TelemetryManager.Instance?.LogHitAttempt(PlayerId, target.PlayerId, MoveType.Special);
                target.SetIncomingDamageContext(PlayerId, MoveType.Special, SourceType.Spell);

                target.StopPunching();
                target.BreakCharge();
                target.TakeDamage(stabDamage, true);

                Lifesteal(stabHeal);
            }
            if (healthbar != null)
            {
                healthbar.SetHealth(currHealth);
            }
            audioManager.PlaySFX(audioManager.stabHit, audioManager.doubleVol);
        }
        else
        {
            // Telemetry: special whiff
            TelemetryManager.Instance?.LogMiss(PlayerId, MoveType.Special);

            audioManager.PlaySFX(audioManager.stab, audioManager.swooshVolume);
        }

        attackRange = ogRange;

        OnCooldown(cooldown);
        ignoreDamage = false;
    }

    override public IEnumerator SpellSafety(float time, float cd)
    {
        yield return new WaitForSeconds(time);

        if (!onCooldown)
        {
            OnCooldown(cd);
        }
        attackRange = ogRange;
    }
    #endregion

    #region LightAttack
    public override void LightAttack()
    {
        if (katanaready)
        {
            Unblock();
            isLightAttacking=true;
            moveSpeed = OGMoveSpeed;
            TelemetryManager.Instance?.LogAction(PlayerId, "Quick");
            katanaHitThisCast = false;
            QuickAttackIndicatorDisable();
            animator.SetTrigger("QuickAttack");
            katanaready = false;
            StartCoroutine(ResetKatana());
        }
    }

    public void DealKatanaDmg1()
    {
        Collider2D hitEnemy = Physics2D.OverlapCircle(attackPoint.position, attackRange, enemyLayer);
        Character target = ResolveTargetFromHit(hitEnemy);

        if (target != null)
        {
            TelemetryManager.Instance?.LogHitAttempt(PlayerId, target.PlayerId, MoveType.Quick);
            target.SetIncomingDamageContext(PlayerId, MoveType.Quick, SourceType.Melee);
            target.TakeDamage(katanaDmg, true);
            katanaHitThisCast = true;
            audioManager.PlaySFX(audioManager.katanaHit, audioManager.lightAttackVolume);
        }
        else
        {
            audioManager.PlaySFX(audioManager.katanaSwoosh, audioManager.swooshVolume);
        }

    }

    public void DealKatanaDmg2()
    {
        Collider2D hitEnemy = Physics2D.OverlapCircle(attackPoint.position, attackRange, enemyLayer);
        Character target = ResolveTargetFromHit(hitEnemy);

        if (target != null)
        {
            TelemetryManager.Instance?.LogHitAttempt(PlayerId, target.PlayerId, MoveType.Quick);
            target.SetIncomingDamageContext(PlayerId, MoveType.Quick, SourceType.Melee);
            target.TakeDamage(katanaDmg, true);
            katanaHitThisCast = true;
            Lifesteal(smallLifesteal);
            target.Knockback(10f, .15f, true);
            audioManager.PlaySFX(audioManager.katanaHit2, 1.5f);
        }
        else
        {
            if (!katanaHitThisCast)
            {
                TelemetryManager.Instance?.LogMiss(PlayerId, MoveType.Quick);
            }
            audioManager.PlaySFX(audioManager.katanaSwoosh, audioManager.swooshVolume);
        }

    }

    IEnumerator ResetKatana()
    {

        yield return new WaitForSeconds(katanaCD);
        audioManager.PlaySFX(audioManager.katanaSeath, audioManager.doubleVol);
        katanaready = true;
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

    #region Passive

    override public void DealChargeDmg()
    {
        TelemetryManager.Instance?.LogAction(PlayerId, "ChargeRelease");
        Collider2D hitEnemy = Physics2D.OverlapCircle(attackPoint.position, attackRange, enemyLayer);
        Character target = ResolveTargetFromHit(hitEnemy);

        if (target != null)
        {
            target.StopPunching();
            if (!target.counterIsOn)
            {
                target.BreakCharge();
            }
            TelemetryManager.Instance?.LogHitAttempt(PlayerId, target.PlayerId, MoveType.Charge);
            target.SetIncomingDamageContext(PlayerId, MoveType.Charge, SourceType.Melee);
            target.TakeDamage(chargeDmg, false);
            Lifesteal(chargeLifesteal);
            target.Knockback(13f, 0.4f, false);
            audioManager.PlaySFX(audioManager.smash, audioManager.doubleVol);
            if (chargeHitSound != null)
            {
                audioManager.PlaySFX(chargeHitSound, 1.5f);
            }
        }
        else
        {
            TelemetryManager.Instance?.LogMiss(PlayerId, MoveType.Charge);
            if (chargeHitSound != null)
            {
                audioManager.PlaySFX(chargeHitSound, 1.5f);
            }
            else
            {
                audioManager.PlaySFX(audioManager.swoosh, audioManager.swooshVolume);
            }

        }
        chargeReset = true;
        knockable = true;
        charging = false;
        animator.SetBool("Casting", false);
        animator.SetBool("Charging", false);
    }

    public override void DealCounterDmg()
    {
        base.DealCounterDmg();

        Lifesteal(smallLifesteal);
    }


    void Lifesteal(int amount)
    {
        currHealth += amount;
        if (currHealth > maxHealth)
        {
            currHealth = maxHealth;
        }
        healthbar.SetHealth(currHealth);
    }

    public override void Update()
    {
        // Run the base Character logic first (movement, jump, etc.)
        base.Update();

        // Then apply the flying passive on top
        HandleFlightPassive();
    }

    void HandleFlightPassive()
    {
        // Don’t interfere if gameplay is locked or grounded
        if (ignoreUpdate || casting || stunned || knocked || charging || isStatic || isGrounded)
        {
            StopFlightIfActive();
            return;
        }

        // Use same "jump" input you already use
        bool holdingJump =
            input.GetKey(up) ||
           (controller && input.GetAxis("Vertical" + playerString) > 0.5f);

        if (!holdingJump)
        {
            StopFlightIfActive();
            return;
        }

        float vy = rb.velocity.y;

        // Only allow flight to ENGAGE once we start falling.
        // This prevents the initial ground jump from helping you reach max speed.
        if (!isFlying)
        {
            if (vy > -fallStartEpsilon)
            {
                // Still rising (or almost zero)? Wait until we’re truly falling.
                return;
            }

            // Engage flight at the *moment of falling* and zero vertical speed.
            isFlying = true;
            rb.gravityScale = 0f;
            //animator.SetBool("IsFlying", true); // optional
            vy = 0f; // start from rest for that floaty feel
        }

        // We’re flying: accelerate upward quickly but respect a hard cap.
        vy = Mathf.MoveTowards(vy, maxAscendSpeed, ascendAccelPerSec * Time.deltaTime);

        rb.velocity = new Vector2(rb.velocity.x, vy);
    }

    void StopFlightIfActive()
    {
        if (!isFlying) return;
        isFlying = false;
        rb.gravityScale = originalGravityScale; // from Character.Start()
        //animator.SetBool("IsFlying", false); // optional
    }



    #endregion
}
