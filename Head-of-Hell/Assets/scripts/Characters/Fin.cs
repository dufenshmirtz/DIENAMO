using System.Collections;
using UnityEngine;

public class Fin : Character
{
    float cooldown = 8f;
    //Roll
    float rollPower = 8f;
    float rollTime = 0.39f;
    bool rollReady = true;
    int passiveDamage = 6;
    float flashStunDuration = 0.7f;
    float resetRoll = 2f;
    // bool safety = true;
    Transform escapePoint;
    GameObject freeBaby;
    float spellTime= 0.2f;

    
    public override void Start()
    {
        base.Start();

        escapePoint = resources.escapeRoute;
        freeBaby = resources.freebaby;
    }

    #region HeavyAttack
    override public void HeavyAttack()
    {
        TelemetryManager.Instance?.LogAction(PlayerId, "Heavy");
        animator.SetTrigger("HeavyAttack");
        audioManager.PlaySFX(audioManager.incense, audioManager.doubleVol);
    }

    override public void DealHeavyDamage()
    {
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, enemyLayer);
        Character target = ResolveTargetFromHit(hitEnemies);

        if (target != null)
        {
            audioManager.PlaySFX(audioManager.heavyattack, 1f);
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
            audioManager.PlaySFX(audioManager.swoosh, 1f);
        }

    }
    #endregion

    #region Spell
    public override void Spell()
    {
        TelemetryManager.Instance?.LogAction(PlayerId, "Special");
        attackRange += 0.5f;
        audioManager.PlaySFX(audioManager.kalhaflash, 2f);
        UsingAbility(cooldown);
        animator.SetTrigger("Spell");
        StartCoroutine(SpellSafety(spellTime,cooldown));
    }

    public void FlashingPriest()
    {
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, enemyLayer);
        Character target = ResolveTargetFromHit(hitEnemies);

        if (target != null)
        {
            // Telemetry: successful special interaction (no damage, but still a "hit")
            TelemetryManager.Instance?.LogHitAttempt(PlayerId, target.PlayerId, MoveType.Special);

            target.StopPunching();
            target.BreakCharge();
            target.SetIncomingDamageContext(PlayerId, MoveType.Special, SourceType.Spell);
            target.TakeDamage(1,true,true,false);
            target.Stun(flashStunDuration);
        }
        else
        {
            // Telemetry: special whiff (no target found)
            TelemetryManager.Instance?.LogMiss(PlayerId, MoveType.Special);

            //audioManager.PlaySFX(audioManager.stab, audioManager.swooshVolume);
        }

        attackRange = ogRange;
        OnCooldown(cooldown);
        ignoreDamage = false;
    }
    #endregion

    #region LightAttack
    override public void LightAttack() 
    {
        if (rollReady)
        {         
            Unblock();
            isLightAttacking=true;
            moveSpeed = OGMoveSpeed;  
            QuickAttackIndicatorDisable();
            rollReady = false;
            StartCoroutine(Roll());
        }
    }

    IEnumerator Roll()
    {
        TelemetryManager.Instance?.LogAction(PlayerId, "Roll");
        IgnoreMovement(true);
        ignoreDamage = true;
        knockable = false;
        isRolling = true;



        // Store the original gravity scale
        float ogGravityScale = rb.gravityScale;

        // Disable gravity while rolling
        rb.gravityScale = 0f;

        // Store the current velocity
        Vector2 currentVelocity = rb.velocity;

        Collider2D[] colliders = GetComponents<Collider2D>();
        foreach (Collider2D collider in colliders)
        {
            collider.enabled = false;
        }
        colliders[5].enabled = true;


        // Determine the roll direction based on the input (keyboard always, controller only if controller == true)
        float moveDirection = input.GetKey(left) ? -1f : (input.GetKey(right) ? 1f : (controller ? input.GetAxis("Horizontal" + playerString) : 0f));

        // If no direction input was given, default to right
        if (moveDirection == 0f)
        {
            moveDirection = 1f; // Default direction in case no input
        }

        audioManager.PlaySFX(audioManager.roll, 1);

        // Calculate the roll velocity
        Vector2 rollVelocity = new Vector2(moveDirection * rollPower, currentVelocity.y);

        // Apply the roll velocity
        rb.velocity = rollVelocity;

        // Trigger the roll animation
        animator.SetTrigger("QuickAttack");

        // Wait for the roll duration
        yield return new WaitForSeconds(rollTime);

        // Reset the velocity after the roll
        rb.velocity = currentVelocity;
        moveSpeed = OGMoveSpeed;

        // Re-enable colliders after rolling
        foreach (Collider2D collider in colliders)
        {
            if (collider != colliders[3])
            {
                collider.enabled = true;
            }
        }
        colliders[5].enabled = false;
        colliders[4].enabled = false;

        // Reset the gravity scale
        rb.gravityScale = ogGravityScale;

        IgnoreMovement(false);
        ignoreDamage = false;
        knockable = true;
        isRolling = false;
        isBlocking = false;

        StartCoroutine(ResetRoll());
    }


    IEnumerator ResetRoll()
    {

        yield return new WaitForSeconds(resetRoll);
        audioManager.PlaySFX(audioManager.rollReady, audioManager.lessVol);
        rollReady = true;
        QuickAttackIndicatorEnable();
    }
    #endregion

    #region ChargeAttack

    #region Passive
    override public void DealCounterDmg()
    {
        base.DealCounterDmg();

        Character target = GetCurrentCombatTarget();
        if (target == null)
        {
            return;
        }

        target.SetIncomingDamageContext(PlayerId, MoveType.Special, SourceType.Melee);
        target.TakeDamageNoAnimation(passiveDamage,false,false);
    }
    #endregion

    #region Animations
    public override void ChargeAttack()
    {
        base.ChargeAttack();
        animator.SetTrigger("Charge");
    }
    #endregion

    public void ThreePointThrow()
    {
        audioManager.PlaySFX(audioManager.swoosh, audioManager.normalVol);
        audioManager.PlaySFX(audioManager.counterScream, audioManager.normalVol);
    }
    public void ThreePointBaptism()
    {
        audioManager.PlaySFX(audioManager.waterSplash, audioManager.normalVol);
    }

    public void ThreePointBall()
    {
        audioManager.PlaySFX(audioManager.fireblast, audioManager.normalVol);
    }

    public void FreeExit()
    {
        StartCoroutine(SpawnBabiesWithDelay());
    }

    IEnumerator SpawnBabiesWithDelay()
    {
        for (int i = 0; i < 3; i++)
        {
            // Instantiate the freeBaby prefab at the escapePoint position
            GameObject baby = Instantiate(freeBaby, escapePoint.position, Quaternion.identity);

            // Determine direction: -1 if facing right, 1 if facing left (inverted for opposite direction)
            int direction = transform.localScale.x > 0 ? -1 : 1;

            // Set direction on the BabyRun script
            BabyRun mover = baby.GetComponent<BabyRun>();
            if (mover != null)
            {
                mover.SetDirection(direction);
            }
            else
            {
                Debug.LogWarning("BabyRun script is missing on the prefab.");
            }

            yield return new WaitForSeconds(1.5f);  // Wait 2 seconds before next spawn
        }
    }

    #endregion

}
