using System.Collections;
using System.Collections.Generic;
//using UnityEditor.Build;
using UnityEngine;

public class Steelager : Character
{
    public float cooldown = 16f;
    int damage = 16;
    //float speedSaver = 4f;
    float resetBombs = 2f;
    //bombin
    public GameObject bombPrefab; // The bullet prefab
    public Transform bombPoint; // The point from where the bullet will be instantiated
    public Transform bombsParent;
    bool bombCharging = false;
    bombScript bomba;
    int comboDamage = 3;
    public Transform firePoint;
    public Transform explosionPoint;

    float spellTime = 0.94f;

    public override void Start()
    {
        base.Start();

        firePoint=resources.firePoint;
        explosionPoint=resources.explosionPoint;
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
            audioManager.PlaySFX(audioManager.explosion, audioManager.lessVol);
            TelemetryManager.Instance?.LogHitAttempt(PlayerId, target.PlayerId, MoveType.Heavy);
            target.SetIncomingDamageContext(PlayerId, MoveType.Heavy, SourceType.Melee);
            target.TakeDamage(heavyDamage, true);

            if(knocked){
                target.SetIncomingDamageContext(PlayerId, MoveType.PassiveBonus, SourceType.Melee);
                target.TakeDamageNoAnimation(comboDamage,false);
            }

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

    }
    #endregion

    #region Spell
    public override void Spell()
    {
        TelemetryManager.Instance?.LogAction(PlayerId, "Special");
        ignoreDamage = true;
        animator.SetTrigger("Spell");
        audioManager.PlaySFX(audioManager.bigExplosion, audioManager.doubleVol);
        UsingAbility(cooldown);
        stayStatic();
        StartCoroutine(SpellSafety(spellTime,cooldown));
    }

    public void DealExplosionDamage()
    {
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(explosionPoint.position, attackRange * 4, enemyLayer);
        HashSet<Character> hitTargets = new HashSet<Character>();
        bool hitAny = false;

        foreach (Collider2D hit in hitEnemies)
        {
            if (hit == null)
            {
                continue;
            }

            Character target = hit.GetComponent<Character>();
            if (target == null)
            {
                target = hit.GetComponentInParent<Character>();
            }

            if (target == null || target == this || !target.isActiveAndEnabled || !hitTargets.Add(target))
            {
                continue;
            }

            enemy = target;
            target.SetEnemy(this);

            TelemetryManager.Instance?.LogHitAttempt(PlayerId, target.PlayerId, MoveType.Special);
            target.SetIncomingDamageContext(PlayerId, MoveType.Special, SourceType.Spell);

            target.BreakCharge();
            target.TakeDamage(damage, true);
            target.Knockback(10f, 0.8f, false);
            hitAny = true;
        }

        if (!hitAny)
        {
            // Telemetry: special whiff (no target in AoE)
            TelemetryManager.Instance?.LogMiss(PlayerId, MoveType.Special);
        }
    }

    public void ExplosionReset()
    {
        OnCooldown(cooldown);
        stayDynamic();
        ignoreDamage = false;
    }
    #endregion

    #region LightAttack
    public override void LightAttack()
    {
        if (!bombCharging)
        {
            Unblock();
            isLightAttacking=true;
            moveSpeed = OGMoveSpeed;
            TelemetryManager.Instance?.LogAction(PlayerId, "Quick");
            QuickAttackIndicatorDisable();
            ThrowBomb();
        }
        else
        {
            bomba.Explode();
        }
    }
    void ThrowBomb()
    {
        bombPrefab = resources.bomb;
        bombPoint = resources.bombSpawner;
        bombsParent = resources.trash;

        bombCharging = true;
        audioManager.PlaySFX(audioManager.fuse, audioManager.normalVol);

        Transform spawnTransform = bombPoint != null ? bombPoint : firePoint;
        Quaternion spawnRotation = spawnTransform != null ? spawnTransform.rotation : transform.rotation;
        Vector3 spawnPosition = spawnTransform != null ? spawnTransform.position : transform.position;

        GameObject bomb = Instantiate(bombPrefab, spawnPosition, spawnRotation);
        bomba = bomb.GetComponent<bombScript>();
        if (bomba != null)
        {
            bomba.InitializeOwner(this);
        }
        Rigidbody2D rb = bomb.GetComponent<Rigidbody2D>();
        bomb.transform.SetParent(bombsParent);
        StartCoroutine(ResetBomb());
    }

    IEnumerator ResetBomb()
    {
        yield return new WaitForSeconds(resetBombs);
        audioManager.PlaySFX(audioManager.lighter, audioManager.normalVol);
        bombCharging = false;
        QuickAttackIndicatorEnable();
    }

    public void FuseSound()
    {
        audioManager.PlaySFX(audioManager.fuse, 1f); 
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
