using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

public class Lithra : Character
{
    public float airSpinSpeed = 5f; // Speed of the air spin
    public float jumpBackForce = 5f; // Force for the jump back
    public float lightAttackDuration = 0.5f; // Duration of the air spin
    public float jumpHeight = 5f; // Initial jump height
    private bool airSpinready = true;
    float cooldown = 10f;
    int bellDamage = 12;
    public Transform bellPoint;
    public Transform bellStunPointTransf;

    int airSpinDamage = 3;
    float airSpinCD = 3f;

    float spellTime = 0.42f;

    public override void Start()
    {
        base.Start();

        bellPoint=resources.bellPoint;
        bellStunPointTransf = resources.bellStunPoint;

        blockSound = audioManager.bellPunch;
    }

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
            audioManager.PlaySFX(audioManager.bellPunch, 1.8f);
            audioManager.PlaySFX(audioManager.lightattack, 0.5f);
            TelemetryManager.Instance?.LogHitAttempt(PlayerId, target.PlayerId, MoveType.Heavy);
            target.SetIncomingDamageContext(PlayerId, MoveType.Heavy, SourceType.Melee);
            target.TakeDamage(heavyDamage, true);
            LuckyBell(target);
            if (!target.isBlocking)
            {
                target.Knockback(11f, 0.15f, true);
            }
        }
        else
        {
            TelemetryManager.Instance?.LogMiss(PlayerId, MoveType.Heavy);
            audioManager.PlaySFX(audioManager.swoosh, 1f);
        }
    }
    #endregion

    #region Spell
    override public void Spell()
    {
        TelemetryManager.Instance?.LogAction(PlayerId, "Special");
        animator.SetTrigger("Spell");
        UsingAbility(cooldown);
        ignoreDamage = true;
        audioManager.PlaySFX(audioManager.swoosh, audioManager.doubleVol);
        StartCoroutine(SpellSafety(spellTime,cooldown));
    }

    public void DealBellDmg()
    {
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(bellPoint.position, attackRange * 2, enemyLayer);
        Collider2D[] bellStunHits = Physics2D.OverlapCircleAll(bellStunPointTransf.position, attackRange / 3, enemyLayer);
        List<Character> targets = ResolveTargetsFromHits(hitEnemies);
        List<Character> stunTargets = ResolveTargetsFromHits(bellStunHits);
        HashSet<Character> stunTargetSet = new HashSet<Character>(stunTargets);

        if (targets.Count > 0)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                Character target = targets[i];
                TelemetryManager.Instance?.LogHitAttempt(PlayerId, target.PlayerId, MoveType.Special);
                target.SetIncomingDamageContext(PlayerId, MoveType.Special, SourceType.Spell);

                target.StopPunching();
                target.BreakCharge();
                target.TakeDamage(bellDamage, true);

                if (stunTargetSet.Contains(target))
                {
                    target.Stun(0.8f);
                }
            }
            audioManager.PlaySFX(audioManager.heavyattack, audioManager.heavyAttackVolume);
            audioManager.PlaySFX(audioManager.bellSpell, 1.5f);
        }
        else
        {
            // Telemetry: special whiff
            TelemetryManager.Instance?.LogMiss(PlayerId, MoveType.Special);

            audioManager.PlaySFX(audioManager.bellPunch, audioManager.swooshVolume);
        }

        attackRange = ogRange;

        OnCooldown(cooldown);
        ignoreDamage = false;
    }
    #endregion

    #region LightAttack
    public override void LightAttack()
    {
        // Check if air spin is ready and the player is moving either left or right (controller or keyboard)
        if (airSpinready && (input.GetKey(left) || input.GetKey(right) || (controller && input.GetAxis("Horizontal" + playerString) != 0)))
        {
            Unblock();
            isLightAttacking=true;
            moveSpeed = OGMoveSpeed;
            TelemetryManager.Instance?.LogAction(PlayerId, "Quick");
            QuickAttackIndicatorDisable();
            StartCoroutine(PerformLightAttack());
        }
    }


    private IEnumerator PerformLightAttack()
    {
        airSpinready = false;
        IgnoreUpdate(true);

        audioManager.PlaySFX(audioManager.bellDash, 1f);

        // Calculate the movement direction (keyboard or controller)
        float moveDirection = input.GetKey(left) ? -1f : (input.GetKey(right) ? 1f : input.GetAxis("Horizontal" + playerString));

        // Only proceed if a direction is given (controller or keyboard)
        if (moveDirection == 0f)
        {
            IgnoreUpdate(false);
            airSpinready = true;
            yield break;
        }

        // Grounded vs. air logic for keyboard/controller
        if (!input.GetKey(up) && isGrounded && (controller && input.GetAxis("Vertical" + playerString) <= 0.5f))
        {
            rb.AddForce(new Vector2(moveDirection * airSpinSpeed, jumpHeight), ForceMode2D.Impulse);
        }
        else
        {
            rb.velocity = new Vector2(rb.velocity.x, 0);
            rb.AddForce(new Vector2(moveDirection * airSpinSpeed, jumpHeight), ForceMode2D.Impulse);
        }

        animator.SetTrigger("QuickAttack");

        // Duration of the attack
        float elapsedTime = 0f;
        bool landed = false;
        while (elapsedTime < lightAttackDuration)
        {
            // Perform the air spin movement
            // transform.Translate(Vector2.right * moveDirection * airSpinSpeed * Time.deltaTime);

            // Check for enemy collision
            Collider2D hitEnemy = Physics2D.OverlapCircle(attackPoint.position, attackRange, enemyLayer);
            Character target = ResolveTargetFromHit(hitEnemy);
            if (target != null)
            {
                TelemetryManager.Instance?.LogHitAttempt(PlayerId, target.PlayerId, MoveType.Quick);
                target.SetIncomingDamageContext(PlayerId, MoveType.Quick, SourceType.Melee);
                landed = true;
                audioManager.PlaySFX(audioManager.bellDashHit, 1f);
                target.TakeDamage(airSpinDamage, true);
                target.Stun(0.5f);

                // Calculate move direction again after hitting (keyboard or controller)
                float moveDirection2 = input.GetKey(left) ? -1f : (input.GetKey(right) ? 1f : input.GetAxis("Horizontal" + playerString));

                // Jump back after hitting the enemy
                rb.velocity = Vector2.zero; // Reset current velocity
                if (moveDirection2 == moveDirection)
                {
                    rb.AddForce(new Vector2(-moveDirection2 * airSpinSpeed / 2, jumpHeight), ForceMode2D.Impulse);
                }
                else
                {
                    rb.AddForce(new Vector2(moveDirection2 * airSpinSpeed / 2, jumpHeight), ForceMode2D.Impulse);
                }

                animator.SetTrigger("Reverse");
                break; // Exit the loop after hitting an enemy
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        if (!landed)
        {
            TelemetryManager.Instance?.LogMiss(PlayerId, MoveType.Quick);
        }

        // Wait until the player is grounded
        while (!isGrounded)
        {
            yield return null;
        }

        IgnoreUpdate(false);
        StartCoroutine(ResetAirSpin());
    }


    IEnumerator ResetAirSpin()
    {
        yield return new WaitForSeconds(airSpinCD);
        audioManager.PlaySFX(audioManager.sworDashTada, audioManager.lessVol);
        airSpinready = true;
        QuickAttackIndicatorEnable();
    }
    #endregion

    #region ChargeAttack
    public override void ChargeAttack()
    {
        base.ChargeAttack();
        animator.SetTrigger("Charge");
    }

    public override void DealChargeDmg()
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
            LuckyBell(target);

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
    #endregion

    #region Passive

    void LuckyBell(Character target)
    {
        if (target != null && StunChance())
        {
            target.Stun(0.8f);
        }
    }
    bool StunChance()
    {
        return Random.value < 0.33f;
    }
    #endregion

    #region Extra
    public void Sip()
    {
        audioManager.PlaySFX(audioManager.sip, 2f);
    }

    #endregion
}
