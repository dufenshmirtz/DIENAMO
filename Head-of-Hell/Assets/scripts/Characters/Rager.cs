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
    Character comboTarget;

    float spellTime = 2.84f;


    #region HeavyAttack
    override public void HeavyAttack()
    {
        if (usingAbility)
        {
            return;
        }
        TelemetryManager.Instance?.LogAction(PlayerId, "Heavy");
        animator.SetTrigger("HeavyAttack");
        audioManager.PlaySFX(audioManager.heavyswoosh, audioManager.heavySwooshVolume);
        ResetQuickPunch();
    }

    override public void DealHeavyDamage()
    {
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
            audioManager.PlaySFX(audioManager.explosion, audioManager.lessVol);
        }

        ResetQuickPunch();

    }
    #endregion

    #region Spell
    override public void Spell()
    {
        TelemetryManager.Instance?.LogAction(PlayerId, "Special");
        UsingAbility(cooldown);
        animator.SetTrigger("Spell");
        StartCoroutine(SpellSafety(spellTime,cooldown));
    }

    public void DealComboDmg()
    {
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, enemyLayer);
        Character target = ResolveTargetFromHit(hitEnemies);

        if (target != null)
        {
            // Telemetry: combo special successfully connected (log once here)
            TelemetryManager.Instance?.LogHitAttempt(PlayerId, target.PlayerId, MoveType.Special);

            target.StopPunching();
            target.BreakCharge();
            SetCooldownSpriteSafe(activeSprite);

            // dmg and sound (0 damage "confirm" hit)
            target.SetIncomingDamageContext(PlayerId, MoveType.Special, SourceType.Spell);
            target.TakeDamage(0, true);
            audioManager.PlaySFX(audioManager.lightattack, audioManager.lightAttackVolume);
            comboTarget = target;
            comboTarget.BeginDeferredCritFeedback();

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
        if (spellHit)
        {
            animator.SetTrigger("Combo");

            StartCoroutine(DealComboDamageOverTime(2f, spellDamage1));
        }
    }

    private IEnumerator DealComboDamageOverTime(float totalDuration, int totalHits)
    {
        audioManager.PlaySFX(audioManager.chainsaw, 1f);

        float delayBetweenHits = totalDuration / totalHits; // Calculate delay between hits

        for (int i = 0; i < totalHits; i++)
        {
            if (comboTarget != null && comboTarget.isActiveAndEnabled)
            {
                // Telemetry: context before each tick (no extra HitAttempt spam)
                comboTarget.SetIncomingDamageContext(PlayerId, MoveType.Special, SourceType.Spell);
                comboTarget.TakeDamage(1, false, false, false);
            }
            yield return new WaitForSeconds(delayBetweenHits); // Wait before the next hit
        }
    }

    public void FirstHit() // old and useless remove
    {
        Character target = comboTarget != null ? comboTarget : GetCurrentCombatTarget();
        if (target == null)
        {
            return;
        }

        target.SetIncomingDamageContext(PlayerId, MoveType.Special, SourceType.Spell);
        target.TakeDamage(hit1Damage, true); //--here
        audioManager.PlaySFX(audioManager.lightattack, audioManager.lightAttackVolume);
    }

    public void SecondHit() // old and useless remove
    {
        Character target = comboTarget != null ? comboTarget : GetCurrentCombatTarget();
        if (target == null)
        {
            return;
        }

        target.SetIncomingDamageContext(PlayerId, MoveType.Special, SourceType.Spell);
        target.TakeDamage(hit2Damage, true); //--here
        audioManager.PlaySFX(audioManager.heavyattack, audioManager.lightAttackVolume);
    }

    public void ThirdHit()
    {
        Character target = comboTarget != null ? comboTarget : GetCurrentCombatTarget();
        if (target == null)
        {
            ResetComboState(false);
            return;
        }

        target.SetIncomingDamageContext(PlayerId, MoveType.Special, SourceType.Spell);
        target.TakeDamage(spellDamage2,true); //--here
        target.EndDeferredCritFeedback(true);
        audioManager.PlaySFX(audioManager.klong, audioManager.doubleVol);

        // player state reset
        stayDynamic();
        ignoreUpdate = false;
        canRotate = true;

        // enemy state
        target.stayDynamic();
        target.AbilityEnabled();
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

        if (restoreTargetState && target != null && target.isActiveAndEnabled)
        {
            target.stayDynamic();
            target.AbilityEnabled();
            target.moveSpeed = OGMoveSpeed;
        }

        spellHit = false;
        comboTarget = null;
        stayDynamic();
        ignoreUpdate = false;
        canRotate = true;
        ResetQuickPunch();
        animator.SetBool("ComboReady", false);
        OnCooldown(cooldown);
    }

    #region ChargeAttack
    public override void ChargeAttack()
    {
        base.ChargeAttack();
        animator.SetTrigger("Charge");
    }
    #endregion
}
