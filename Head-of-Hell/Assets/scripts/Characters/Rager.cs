using System.Collections;
using UnityEngine;

public class Rager : Character
{
    //Spell
    public float cooldown = 20f;
    int hit1Damage = 2, hit2Damage = 7; //25 actual dmg these variables are ass
    int spellDamage2 = 10;
    int spellDamage1 = 15;
    //Lightattack
    int lightDamage = 4;
    bool spellHit = false;
    bool spellCastActive = false;
    Character comboTarget;
    Character spellLockedTarget;
    bool comboGrabConsumed = false;
    bool comboStartedConsumed = false;
    bool finalHitConsumed = false;
    int remainingComboTicks = 0;
    Coroutine comboDamageCoroutine;

    float spellTime = 2.84f;


    #region HeavyAttack
    override public void HeavyAttack()
    {
        if (usingAbility)
        {
            return;
        }
        animator.SetTrigger("HeavyAttack");
        audioManager.PlaySFX(audioManager.heavyswoosh, audioManager.heavySwooshVolume);
        ResetQuickPunch();
    }

    override public void DealHeavyDamage()
    {
        TelemetryManager.Instance?.LogAction(PlayerId, "Heavy");
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, enemyLayer);
        Character target = ResolveTargetFromHit(hitEnemies);

        if (target != null)
        {

            audioManager.PlaySFX(audioManager.heavyattack, 1f);
            audioManager.PlaySFX(audioManager.explosion, audioManager.lessVol);
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
            audioManager.PlaySFX(audioManager.explosion, audioManager.lessVol);
        }

        ResetQuickPunch();

    }
    #endregion

    #region Spell
    override public void Spell()
    {
        if (spellCastActive)
        {
            return;
        }

        spellCastActive = true;
        TelemetryManager.Instance?.LogAction(PlayerId, "Special");
        comboGrabConsumed = false;
        comboStartedConsumed = false;
        finalHitConsumed = false;
        remainingComboTicks = spellDamage1;
        UsingAbility(cooldown);
        stayStatic();
        ignoreUpdate = true;
        canRotate = false;
        if (rb != null)
        {
            rb.velocity = new Vector2(0f, rb.velocity.y);
        }
        animator.SetTrigger("Spell");
        StartCoroutine(SpellSafety(spellTime,cooldown));
    }

    public void DealComboDmg()
    {
        if (!spellCastActive)
        {
            return;
        }

        if (comboGrabConsumed)
        {
            return;
        }

        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, enemyLayer);
        Character target = ResolveTargetFromHit(hitEnemies);

        if (target != null)
        {
            comboGrabConsumed = true;

            // Telemetry: combo special successfully connected (log once here)
            TelemetryManager.Instance?.LogHitAttempt(PlayerId, target.PlayerId, MoveType.Special);

            target.StopPunching();
            target.BreakCharge();
            SetCooldownSpriteSafe(activeSprite);

            // dmg and sound (0 damage "confirm" hit)
            target.SetIncomingDamageContext(PlayerId, MoveType.Special, SourceType.Spell);
            target.TakeDamage(0, true, true, false);
            audioManager.PlaySFX(audioManager.lightattack, audioManager.lightAttackVolume);
            comboTarget = target;
            comboTarget.BeginDeferredCritFeedback();

            if (spellLockedTarget != null && spellLockedTarget != comboTarget)
            {
                spellLockedTarget.AbilityEnabled();
            }

            spellLockedTarget = comboTarget;

            // playerState
            stayStatic();
            ignoreUpdate = true;
            canRotate = false;

            // enemy state
            comboTarget.stayStatic();
            comboTarget.blockBreaker();
            comboTarget.AbilityDisabled();
            comboTarget.Grabbed();
            animator.SetBool("ComboReady", true);
            spellHit = true;
        }
        else
        {
            // Telemetry: special whiff (didn't grab/connect)
            TelemetryManager.Instance?.LogMiss(PlayerId, MoveType.Special);

            audioManager.PlaySFX(audioManager.swoosh, audioManager.swooshVolume);
            animator.SetBool("isUsingAbility", false);
            ResetComboState(false);
        }
    }

    public void Startcombo()
    {
        if (!spellCastActive)
        {
            return;
        }

        if (!spellHit)
        {
            return;
        }

        if (comboStartedConsumed)
        {
            //Debug.Log($"[RagerSpell] Cast {currentSpellCastId} ignored duplicate combo start for {PlayerId}.");
            return;
        }

        comboStartedConsumed = true;
        animator.SetTrigger("Combo");

        if (comboDamageCoroutine != null)
        {
            StopCoroutine(comboDamageCoroutine);
        }

        comboDamageCoroutine = StartCoroutine(DealComboDamageOverTime(2f, spellDamage1));
    }

    private IEnumerator DealComboDamageOverTime(float totalDuration, int totalHits)
    {
        audioManager.PlaySFX(audioManager.chainsaw, 1f);

        float delayBetweenHits = totalDuration / totalHits; // Calculate delay between hits

        for (int i = 0; i < totalHits; i++)
        {
            if (comboTarget != null && comboTarget.isActiveAndEnabled && remainingComboTicks > 0)
            {
                // Telemetry: context before each tick (no extra HitAttempt spam)
                comboTarget.SetIncomingDamageContext(PlayerId, MoveType.Special, SourceType.Spell);
                comboTarget.TakeDamage(1, false, false, false);
                remainingComboTicks--;
            }
            yield return new WaitForSeconds(delayBetweenHits); // Wait before the next hit
        }

        comboDamageCoroutine = null;
    }

    public void FirstHit() // legacy animation event: keep as no-op to avoid extra spell damage
    {
    }

    public void SecondHit() // legacy animation event: keep as no-op to avoid extra spell damage
    {
    }

    public void ThirdHit()
    {
        if (!spellCastActive)
        {
            return;
        }

        if (finalHitConsumed)
        {
            return;
        }

        Character target = comboTarget != null ? comboTarget : GetCurrentCombatTarget();
        if (target == null)
        {
            ResetComboState(false);
            return;
        }

        finalHitConsumed = true;
        target.SetIncomingDamageContext(PlayerId, MoveType.Special, SourceType.Spell);
        target.TakeDamage(spellDamage2,true);
        target.EndDeferredCritFeedback(true);
        audioManager.PlaySFX(audioManager.klong, audioManager.doubleVol);

        // player state reset
        stayDynamic();
        ignoreUpdate = false;
        canRotate = true;

        // enemy state
        target.stayDynamic();
        ReleaseSpellLock();
        target.moveSpeed = OGMoveSpeed;
        target.Knockback(8f, .25f, false);

        ResetComboState(false);
    }
    #endregion

    #region LightAttack
    public override void LightAttack()
    {
        TelemetryManager.Instance?.LogAction(PlayerId, "Quick");
        Unblock();
        isLightAttacking=true;
        animator.SetTrigger("QuickAttack");
    }
    public void QuickPunchDamage()
    {
        Collider2D hitEnemy = Physics2D.OverlapCircle( attackPoint.position,  attackRange,  enemyLayer);
        Character target = ResolveTargetFromHit(hitEnemy);

        if (target != null)
        {
            TelemetryManager.Instance?.LogHitAttempt(PlayerId, target.PlayerId, MoveType.Quick);
            target.SetIncomingDamageContext(PlayerId, MoveType.Quick, SourceType.Melee);
            target.TakeDamageNoAnimation(lightDamage, true, false);
            audioManager.PlaySFX(audioManager.lightattack, audioManager.lightAttackVolume);
        }
        else
        {
            TelemetryManager.Instance?.LogMiss(PlayerId, MoveType.Quick);
            audioManager.PlaySFX(audioManager.swoosh, audioManager.swooshVolume);
        }

        animator.SetBool("QuickPunch", false);
    }

    public void QuickPunchStart()
    {
        animator.SetBool("QuickPunch", true);

    }
    #endregion

    private void ResetComboState(bool restoreTargetState)
    {
        Character target = comboTarget != null ? comboTarget : GetCurrentCombatTarget();

        if (target != null && target.isActiveAndEnabled)
        {
            target.EndDeferredCritFeedback(false);
        }

        ReleaseSpellLock();

        if (restoreTargetState && target != null && target.isActiveAndEnabled)
        {
            target.stayDynamic();
            target.moveSpeed = OGMoveSpeed;
        }

        spellHit = false;
        spellCastActive = false;
        comboTarget = null;
        comboGrabConsumed = false;
        comboStartedConsumed = false;
        finalHitConsumed = false;
        remainingComboTicks = 0;
        if (comboDamageCoroutine != null)
        {
            StopCoroutine(comboDamageCoroutine);
            comboDamageCoroutine = null;
        }
        stayDynamic();
        ignoreUpdate = false;
        canRotate = true;
        ResetQuickPunch();
        animator.SetBool("ComboReady", false);
        OnCooldown(cooldown);
    }

    private void ReleaseSpellLock()
    {
        if (spellLockedTarget == null)
        {
            return;
        }

        spellLockedTarget.AbilityEnabled();
        spellLockedTarget = null;
    }

    #region ChargeAttack
    public override void ChargeAttack()
    {
        base.ChargeAttack();
        animator.SetTrigger("Charge");
    }
    #endregion
}
