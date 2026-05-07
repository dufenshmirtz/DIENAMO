using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Transform = UnityEngine.Transform;

public class LazyBigus : Character
{
    public GameObject bulletPrefab; // The bullet prefab
    public Transform firePoint; // The point from where the bullet will be instantiated
    Transform bulletParent; 
    public float bulletSpeed = 35f; // Speed of the bullet
    bool isShootin = false;
    public float cooldown = 20f;
    public GameObject beam;
    public BeamScript bScript;
    public BulletScript bulletScript;
    int beamDamage = 10;
    int beamPoisonDamage = 10; 
    int passiveDamage = 4;
    float resetBullet=2f;
    private readonly Dictionary<Character, int> poisonStacks = new Dictionary<Character, int>();
    private readonly Dictionary<Character, Coroutine> poisonResetCoroutines = new Dictionary<Character, Coroutine>();
    private readonly HashSet<Character> beamTargetsHitThisCast = new HashSet<Character>();

    float spellTime = 1f;

    public override void Start()
    {
        base.Start();

        firePoint = resources.firePoint;
        beam = resources.beam;
    }

    #region HeavyAttack
    override public void HeavyAttack()
    {
        animator.SetTrigger("HeavyAttack");
        audioManager.PlaySFX(audioManager.volchBite, 1.5f);
        audioManager.PlaySFX(audioManager.volchBiteExtra, 1.5f);
    }

    override public void DealHeavyDamage()
    {
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, enemyLayer);
        Character target = ResolveTargetFromHit(hitEnemies);

        if (target != null)
        {

            audioManager.PlaySFX(audioManager.volchBiteSuccess, 1.5f);
            TelemetryManager.Instance?.LogHitAttempt(PlayerId, target.PlayerId, MoveType.Heavy);
            target.SetIncomingDamageContext(PlayerId, MoveType.Heavy, SourceType.Melee);
            target.TakeDamage(heavyDamage, true);
            ToxicTouch(target);

            if (!target.isBlocking)
            {
                target.Knockback(11f, 0.15f, true);
            }

        }
        else
        {
            //audioManager.PlaySFX(audioManager.swoosh, 1f);
        }

    }
    #endregion

    #region Spell
    override public void Spell()
    {
        IgnoreUpdate(true);
        stayStatic();
        UsingAbility(cooldown);
        beam.SetActive(true);
        bScript = beam.GetComponent<BeamScript>();
        bScript.playa = this;
        beamTargetsHitThisCast.Clear();
        animator.SetTrigger("Spell");
        audioManager.PlaySFX(audioManager.beam, audioManager.doubleVol);
        ignoreDamage = true;
        StartCoroutine(SpellSafety(spellTime,cooldown));
    }

    public void BeamHitEnemy(Character target)
    {
        if (target != null && beamTargetsHitThisCast.Add(target))
        {
            SetEnemy(target);
            target.SetIncomingDamageContext(PlayerId, MoveType.Projectile, SourceType.Projectile);
            target.TakeDamage(beamDamage,true);
            target.StopPunching();
            target.BreakCharge();
            target.Knockback(13f, 0.5f, true);
            audioManager.PlaySFX(audioManager.beamHit, 1.8f);
            StartCoroutine(Poison(target, beamPoisonDamage/5,1f,5));
        }
    }

    private IEnumerator Poison(Character target, int damageAmount, float interval, int times)
    {
        if (target == null)
        {
            yield break;
        }

        ResetPoisonStacks(target);
        target.ActivatePoison(true);

        audioManager.PlaySFX(audioManager.poison, 2.5f);
        for (int i = 0; i < times; i++)
        {
            yield return new WaitForSeconds(interval);

            // Deal damage to the enemy
            if (target == null || !target.isActiveAndEnabled)
            {
                yield break;
            }

            target.SetIncomingDamageContext(PlayerId, MoveType.PoisonTick, SourceType.Dot);
            target.TakeDamageNoAnimation(damageAmount,false,false);
        }
        if (target != null && target.isActiveAndEnabled)
        {
            target.ActivatePoison(false);
        }
    }

    public void BeamEnd()
    {
         beamTargetsHitThisCast.Clear();
         OnCooldown(cooldown);
         IgnoreUpdate(false);
         stayDynamic();
        ignoreDamage=false;
    }
    
    #endregion

    #region LightAttack

    public override void LightAttack()
    {
        if (!isShootin)
        {
            Unblock();
            isLightAttacking=true;
            moveSpeed = OGMoveSpeed;
            QuickAttackIndicatorDisable();
            animator.SetTrigger("QuickAttack");
            StartCoroutine(ResetShooting());
        }
    }

    public void Shoot()
    {
        bulletPrefab = resources.bullet;
        firePoint = resources.shootinPoint;
        bulletParent = resources.bulletParent;

        audioManager.PlaySFX(audioManager.volchSpit, audioManager.doubleVol);
        TelemetryManager.Instance?.LogAction(PlayerId,"Projectile");
        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);

        Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
        rb.velocity = new Vector2(transform.localScale.x * bulletSpeed, 0); // Shoots in the direction the character is facing

        bulletScript = bullet.GetComponent<BulletScript>();
        bulletScript.Init(this);

        Destroy(bullet, 2f);
    }

    public void firstShootFrame()
    {
        isShootin = true;
    }

    IEnumerator ResetShooting()
    {

        yield return new WaitForSeconds(resetBullet);
        audioManager.PlaySFX(audioManager.reload, audioManager.normalVol);
        isShootin = false;
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
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, enemyLayer);
        Character target = ResolveTargetFromHit(hitEnemies);

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
            target.Knockback(13f, 0.4f, false);
            ToxicTouch(target);
            audioManager.PlaySFX(audioManager.smash, audioManager.doubleVol);
            if (chargeHitSound != null)
            {
                audioManager.PlaySFX(chargeHitSound, 1.5f);
            }
        }
        else
        {
            if (chargeHitSound != null)
            {
                audioManager.PlaySFX(chargeHitSound, 1.5f);
            }
            else
            {
                TelemetryManager.Instance?.LogMiss(PlayerId, MoveType.Charge);
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

    void ToxicTouch(Character target)
    {
        if (target == null)
        {
            return;
        }

        int currentStacks = GetPoisonStackCount(target);
        if(currentStacks >= 3)
        {
            StartCoroutine(Poison(target, passiveDamage/4,1f,4));
            ResetPoisonStacks(target);
            return;
        }

        AddPoison(target);
    }

    public void AddPoison(Character target)
    {
        if (target == null)
        {
            return;
        }

        int currentStacks = GetPoisonStackCount(target);

        if (!target.IsPoisoned())
        {
            if(currentStacks < 3)
            {
                if(currentStacks==0)
                {
                    target.StackPoison1(true);
                }
                if (currentStacks == 1)
                {
                    target.StackPoison1(false);
                    target.StackPoison2(true);
                }
                if (currentStacks == 2)
                {
                    target.StackPoison2(false);
                    target.StackPoison3(true);
                }
                poisonStacks[target] = currentStacks + 1;
            }
        }
        // Restart the poison reset coroutine
        if (poisonResetCoroutines.TryGetValue(target, out Coroutine poisonResetCoroutine) && poisonResetCoroutine != null)
        {
            StopCoroutine(poisonResetCoroutine);
        }
        poisonResetCoroutines[target] = StartCoroutine(ResetPoisonAfterDelay(target));
    }

    private IEnumerator ResetPoisonAfterDelay(Character target)
    {
        yield return new WaitForSeconds(10f);
        ResetPoisonStacks(target);
        poisonResetCoroutines.Remove(target);
    }

    private void ResetPoisonStacks(Character target)
    {
        if (target == null)
        {
            return;
        }

        target.StackPoison1(false);
        target.StackPoison2(false);
        target.StackPoison3(false);
        poisonStacks.Remove(target);
    }

    private int GetPoisonStackCount(Character target)
    {
        if (target == null)
        {
            return 0;
        }

        return poisonStacks.TryGetValue(target, out int count) ? count : 0;
    }

    public void BeamHit(Character target)
    {
        BeamHitEnemy(target);
    }

    public void StackPoison(Character target)
    {
        AddPoison(target);
    }
    #endregion
}
