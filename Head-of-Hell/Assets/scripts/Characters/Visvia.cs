using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Visvia : Character
{
    private bool grabLandedThisCast = false;
    private readonly HashSet<Character> grabTargetsHitThisCast = new HashSet<Character>();
    bool shotgunReady = true;
    float shotgunForce = 15f;
    float upwardsForce = 6f;
    float shotgunCooldown = 4f;
    float dashDuration = 0.3f;
    float shotgunRange = 1f;
    int shotgunDamage = 6;

    public int blastCounter=0;
    float overheatDuration = 8f;
    float elapsed = 0f;
    float overheatFrequency=0.2f;
    int overheatDamage=1;

    int cooldown = 10;
    int grabDamage = 6;
    bool overHeated = false;

    Transform grabPoint;

    GameObject blast; // for testing
    Transform blastPoint;

    float spellTime = 0.84f;

    public override void Start()
    {
        base.Start();

        grabPoint = resources.grabPoint;
        blast = resources.fart; //for testing
        blastPoint = resources.fartPoint;
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

            audioManager.PlaySFX(audioManager.katanaHit, 1f);
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
            audioManager.PlaySFX(audioManager.katanaSwoosh, 1f);
        }

    }
    #endregion

    #region Spell
    override public void Spell()
    {
        TelemetryManager.Instance?.LogAction(PlayerId, "Special");

        // Telemetry: reset per-cast landing state (for Miss logging + avoid double HitAttempt)
        grabLandedThisCast = false;
        grabTargetsHitThisCast.Clear();
        blastCounter++;
        StartCoroutine(HeatCounter());
        UsingAbility(cooldown);
        animator.SetTrigger("Spell");
        StartCoroutine(GrabEnd());
        StartCoroutine(SpellSafety(spellTime,cooldown));
    }

    public void GrabDmg()
    {
        Vector2 capsuleSize = new Vector2(7f, 0.5f); // Long in X-axis, thin in Y-axis
        Collider2D[] hitEnemies = Physics2D.OverlapCapsuleAll(grabPoint.position, capsuleSize, CapsuleDirection2D.Horizontal, 0f, enemyLayer);
        var targets = ResolveTargetsFromHits(hitEnemies);

        if (targets.Count > 0)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                Character target = targets[i];
                if (grabTargetsHitThisCast.Add(target))
                {
                    TelemetryManager.Instance?.LogHitAttempt(PlayerId, target.PlayerId, MoveType.Special);
                }

                target.SetIncomingDamageContext(PlayerId, MoveType.Special, SourceType.Spell);

                audioManager.PlaySFX(audioManager.stabHit, 2f);
                target.StopPunching();
                target.TakeDamage(grabDamage, true);
                target.BreakCharge();
                target.Knockback(12f, 0.3333f, true);
            }
            grabLandedThisCast = true;
        }
        else
        {
            audioManager.PlaySFX(audioManager.bellPunch, audioManager.swooshVolume);
        }
    }

    public void GrabStartDmg()
    {
        Vector2 capsuleSize = new Vector2(7f, 0.5f); // Long in X-axis, thin in Y-axis
        Collider2D[] hitEnemies = Physics2D.OverlapCapsuleAll(grabPoint.position, capsuleSize, CapsuleDirection2D.Horizontal, 0f, enemyLayer);
        var targets = ResolveTargetsFromHits(hitEnemies);

        if (targets.Count > 0)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                Character target = targets[i];
                if (grabTargetsHitThisCast.Add(target))
                {
                    TelemetryManager.Instance?.LogHitAttempt(PlayerId, target.PlayerId, MoveType.Special);
                }

                target.SetIncomingDamageContext(PlayerId, MoveType.Special, SourceType.Spell);

                target.StopPunching();
                target.TakeDamage(grabDamage, true);
                target.BreakCharge();
                audioManager.PlaySFX(audioManager.katanaSwoosh, 2f);
            }
            grabLandedThisCast = true;
        }
        else
        {
            audioManager.PlaySFX(audioManager.bellPunch, audioManager.swooshVolume);
        }
    }

    IEnumerator GrabEnd()
    {
        yield return new WaitForSeconds(0.59f);

        // Telemetry: if neither damage frame connected, count as a miss
        if (!grabLandedThisCast)
        {
            TelemetryManager.Instance?.LogMiss(PlayerId, MoveType.Special);
        }

        OnCooldown(cooldown);
        grabTargetsHitThisCast.Clear();
        OverheatCheck();
    }
    #endregion

    #region LightAttack
    public override void LightAttack()
    {
        if (shotgunReady)
        {
            Unblock();
            isLightAttacking=true;
            moveSpeed = OGMoveSpeed;
            TelemetryManager.Instance?.LogAction(PlayerId, "Quick");
            blastCounter++;
            shotgunReady = false;
            QuickAttackIndicatorDisable();
            StartCoroutine(ShotgunBlast());
        }
    }

    private IEnumerator ShotgunBlast()
    {
        StartCoroutine(HeatCounter());

        ShowShotgunBlast(0.3f);

        audioManager.PlaySFX(audioManager.shotgunBlast, 2f);

        // Lock orientation
        canRotate = false;

        // Determine direction to dash (opposite of facing)
        float direction = transform.localScale.x > 0 ? -1f : 1f;

        // Apply recoil force (backward)
        rb.velocity = new Vector2(direction * shotgunForce, upwardsForce);

        // Deal damage in front of the player
        Vector2 attackPosition = attackPoint.position;
        Collider2D[] hits = Physics2D.OverlapCircleAll(attackPoint.position, shotgunRange, enemyLayer);
        var targets = ResolveTargetsFromHits(hits);
        
        if (targets.Count > 0)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                Character target = targets[i];
                TelemetryManager.Instance?.LogHitAttempt(PlayerId, target.PlayerId, MoveType.Quick);
                target.SetIncomingDamageContext(PlayerId, MoveType.Quick, SourceType.Melee);
                target.TakeDamage(shotgunDamage, true);
                if (!target.isBlocking)
                {
                    target.Knockback(10f, 0.2f, true);
                }
            }
        }
        else
        {
            TelemetryManager.Instance?.LogMiss(PlayerId, MoveType.Quick);
        }
        //Unlock Rotation
        yield return new WaitForSeconds(dashDuration-0.1f);
        canRotate = true;

        //End recoilDash
        yield return new WaitForSeconds(dashDuration);

        // Unlock orientation and reset velocity
        rb.velocity = Vector2.zero;
        OverheatCheck();

        // Cooldown before you can shotgun again
        yield return new WaitForSeconds(shotgunCooldown);
        shotgunReady = true;
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
    
    public void OverheatCheck(){
        if(blastCounter >= 7){
            chargeDisable = true;
            heavyDisable = true;
            specialDisable = true;
            blockDisable = true;
            quickDisable = true;
            moveSpeed= moveSpeed + 2;
            canAlterSpeed = false;
            overHeated=true;
            StartCoroutine(Overheat());
        }
    }

    IEnumerator Overheat(){

        audioManager.PlaySFX(audioManager.alarm, 2f);

        robberyCountIndicator.text = "X";
        robberyCountIndicator.gameObject.SetActive(true);

        while (elapsed < overheatDuration && !animator.GetBool("isDead"))
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(attackPoint.position, shotgunRange, enemyLayer);
            var targets = ResolveTargetsFromHits(hits);
            for (int i = 0; i < targets.Count; i++)
            {
                Character target = targets[i];
                target.SetIncomingDamageContext(PlayerId, MoveType.PoisonTick, SourceType.Dot);
                target.TakeDamage(overheatDamage, false, false, false);
            }
            ShowShotgunBlast(0.1f);

            yield return new WaitForSeconds(overheatFrequency);
            elapsed += overheatFrequency;
        }

        elapsed = 0;
        moveSpeed = OGMoveSpeed;
        canAlterSpeed = true;
        blastCounter = 0;
        chargeDisable = false;
        heavyDisable = false;
        specialDisable = false;
        blockDisable = false;
        quickDisable = false;
        overHeated=false;
        robberyCountIndicator.gameObject.SetActive(false);
    }

    IEnumerator HeatCounter(){
        robberyCountIndicator.text = blastCounter.ToString();
        robberyCountIndicator.gameObject.SetActive(true);
        yield return new WaitForSeconds(1f);
        if(!overHeated){
            robberyCountIndicator.gameObject.SetActive(false);
        }
        
    }

    void ShowShotgunBlast(float time)   // for testing
    {
        GameObject blastVisual = Instantiate(blast, blastPoint.position, Quaternion.identity);
        //blastVisual.transform.localScale = Vector3.one * shotgunRange * 2f;
        Destroy(blastVisual, time); // Show it briefly
    }


    #endregion

}
