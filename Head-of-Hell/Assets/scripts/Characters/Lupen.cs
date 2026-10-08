using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements.Experimental;

public class Lupen : Character
{
    //Spell
    float cooldown = 15f; //Mesos oros twn allwn +- ligo
    CharacterAnimationEvents cEvents;
    LupenSpirit spirit;
    string randomCharacter;
    int wipDamage=1;
    bool wipReady = true;
    Transform wipPoint;
    int robberyCountter=0;
    Character stolenCharacter;

    public float hitBoxWidth = 2f; // Length in the X-axis
    public float hitBoxHeight = 0.5f; // Thinness in the Y-axi
    Vector2 size;

    float resetWip=2f;

    int passiveRange = 4;

    public override void Start()
    {
        base.Start();

        cEvents = this.GetComponent<CharacterAnimationEvents>();
        wipPoint = resources.wipPoint;
        spirit=this.gameObject.AddComponent<LupenSpirit>();
        spirit.lupen = this;
        spirit.ability=ability;
        spirit.playerString = playerString;
        spirit.controller = controller;
        spirit.characterChoiceHandler= characterChoiceHandler;
        spirit.cEvents = cEvents;
        spirit.enemy = enemy;
        

        size = new Vector2(hitBoxWidth, hitBoxHeight);
    }

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
            TelemetryManager.Instance?.LogHitAttempt(PlayerId, target.PlayerId, MoveType.Heavy);
            enemy.SetIncomingDamageContext(PlayerId, MoveType.Heavy, SourceType.Melee);
            enemy.TakeDamage(heavyDamage, true);
            Robbed();

            if (!enemy.isBlocking)
            {
                enemy.Knockback(11f, 0.15f, true);
            }

        }
        else
        {
            TelemetryManager.Instance?.LogMiss(PlayerId, MoveType.Heavy);
            audioManager.PlaySFX(audioManager.swoosh, 1f);
        }

        ResetQuickPunch();

    }
    #endregion

    #region Spell
    override public void Spell()
    {
        TelemetryManager.Instance?.LogAction(PlayerId, "Special");
        stayStatic();
        spirit.healthbar = healthbar;
        spirit.maxHealth = maxHealth;
        audioManager.PlaySFX(audioManager.transformation, 1.8f);
        KnockNearbyEnemies();
        ignoreUpdate = true;
        ignoreDamage = true;
        knockable = false;
        damageShield = false;
        casting=true;
        animator.SetTrigger("Spell");
    }

    void KnockNearbyEnemies()
    {
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(transform.position, passiveRange, enemyLayer);
        var targets = ResolveTargetsFromHits(hitEnemies);

        if (targets.Count > 0)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                Character target = targets[i];
                target.Knockback(9f, 0.5f, false);
            }
        }
    }

    public void Transformation()
    {
        ignoreUpdate = false;
        ignoreDamage = false;
        knockable = true;
        do
        {
            randomCharacter = characterChoiceHandler.PickRandomCharacter();
        } while (randomCharacter == "Lupen" || randomCharacter == "Visvia");

        characterChoiceHandler.ChangeCharacter(randomCharacter);
        cEvents.ChangeCharacterEvents(1);
        stolenCharacter = characterChoiceHandler.CharacterChoice(1);
        stolenCharacter.overrideDeath = true; //in case he dies in form
        stayDynamic();
        Character currentTarget = GetCurrentCombatTarget();
        if (currentTarget != null)
        {
            stolenCharacter.ChangeEnemy(currentTarget);
        }
        //SaveValues and change form
        spirit.SetInput(GetInputProvider());        // NEW: give the same provider
        spirit.stolenCharacter = stolenCharacter;
        spirit.enemy = currentTarget;
        spirit.currentHealth = currHealth;
        spirit.whipDamage = wipDamage;
        spirit.robberyCounter = robberyCountter;
        spirit.Action();
    }

    public void ReturnToLupen(int wdmg, int rc, int currentHealth)
    {
        wipDamage = wdmg;
        robberyCountter = rc;
        currHealth = currentHealth;
        casting = false;
        RestoreOriginalState();
        RemoveStolenForm();
        OnCooldown(cooldown);
    }

    private void RestoreOriginalState()
    {
        ignoreUpdate = false;
        ignoreDamage = false;
        ignoreMovement = false;
        knockable = true;
        damageShield = false;
        usingAbility = false;
        canRotate = true;
        chargeDisable = false;
        canCast = true;
        stunned = false;
        knocked = false;
        isBlocking = false;
        isStatic = false;
        charging = false;
        charged = false;
        chargeAttackActive = false;
        chargeReset = false;
        KBCounter = 0f;
        KBForce = 0f;
        knockfromright = false;
        knockbackXaxis = false;

        StopCHarge();
        Unblock();
        stayDynamic();
        ActivateColliders();
        RestorePlatformSupportIfNeeded();

        if (rb != null)
        {
            rb.gravityScale = originalGravityScale;
            rb.velocity = Vector2.zero;
        }

        if (animator != null)
        {
            animator.Rebind();
            animator.Update(0f);
            animator.SetBool("Casting", false);
            animator.SetBool("Charging", false);
            animator.SetBool("IsRunning", false);
            animator.SetBool("Crouch", false);
            animator.SetBool("cWalk", false);
            animator.SetBool("isUsingAbility", false);
            animator.ResetTrigger("ChargedHit");
            animator.ResetTrigger("tookDmg");
        }
    }

    private void RestorePlatformSupportIfNeeded()
    {
        if (feetTrigger == null)
        {
            return;
        }

        Collider2D[] colliders = GetComponents<Collider2D>();
        if (colliders == null || colliders.Length <= 3)
        {
            return;
        }

        int platformMask = LayerMask.GetMask("PlatformLayer");
        if (platformMask == 0)
        {
            return;
        }

        if (feetTrigger.IsTouchingLayers(platformMask))
        {
            colliders[3].enabled = true;
            isGrounded = true;
        }
    }

    public void RemoveStolenForm()
    {
        if (stolenCharacter != null && stolenCharacter != this)
        {
            stolenCharacter.enabled = false;
            stolenCharacter.StopAllCoroutines();
            Destroy(stolenCharacter);
            stolenCharacter = null;

            characterChoiceHandler.ChangeCharacter("Lupen");
            Lupen activeLupen = characterChoiceHandler.GetCurrentCharacter() as Lupen;
            if (activeLupen == null)
            {
                activeLupen = this;
            }

            activeLupen.enabled = true;
            activeLupen.InitializeCharacter();
            activeLupen.RefreshSetupBindingsForRuntime();
            activeLupen.SetInput(GetInputProvider());
            cEvents.SetCharacter(activeLupen);
            characterChoiceHandler.NotifyCurrentCharacterChanged();

            Character currentTarget = GetCurrentCombatTarget();
            if (currentTarget != null)
            {
                activeLupen.ChangeEnemy(currentTarget);
            }
            P1Name.text = "Lupen";
            return;
        }

        Debug.LogWarning("No stolen form was available to remove from Lupen.");
    }

    #endregion

    #region LightAttack
    public override void LightAttack()
    {
        if (wipReady)
        {
            Unblock();
            isLightAttacking=true;
            moveSpeed = OGMoveSpeed;
            TelemetryManager.Instance?.LogAction(PlayerId, "Quick");
            QuickAttackIndicatorDisable();
            animator.SetTrigger("QuickAttack");
            wipReady = false;
            StartCoroutine(ResetWip());
        }
    }

    public void DealWipDamage()
    {
        audioManager.PlaySFX(audioManager.whip , audioManager.normalVol);
        Collider2D hitEnemy = Physics2D.OverlapCapsule(wipPoint.position,size, CapsuleDirection2D.Horizontal,0f,enemyLayer);
        Character target = ResolveTargetFromHit(hitEnemy);

        if (target != null)
        {
            TelemetryManager.Instance?.LogHitAttempt(PlayerId, enemy.PlayerId, MoveType.Quick);
            enemy.SetIncomingDamageContext(PlayerId, MoveType.Quick, SourceType.Melee);
            enemy.TakeDamage(wipDamage, true);
            Robbed();
            enemy.Slow(1.5f,2f);
            StartCoroutine(SpeedUpCoroutine(1.5f,2f));
            audioManager.PlaySFX(audioManager.coinSound, audioManager.normalVol);
            StartCoroutine(TriggerRobberyIndicator());
        }
        else
        {
            TelemetryManager.Instance?.LogMiss(PlayerId, MoveType.Quick);
        }
        

    }

    public IEnumerator SpeedUpCoroutine(float time, float amount)
    {

        moveSpeed = moveSpeed + amount;

        yield return new WaitForSeconds(time);

        moveSpeed = OGMoveSpeed;
    }

    IEnumerator ResetWip()
    {
        yield return new WaitForSeconds(resetWip);
        audioManager.PlaySFX(audioManager.katanaSeath, audioManager.doubleVol);
        wipReady = true;
        QuickAttackIndicatorEnable();
    }

    IEnumerator TriggerRobberyIndicator()
    {
        robberyCountIndicator.text = (wipDamage).ToString();
        robberyCountIndicator.gameObject.SetActive(true);
        yield return new WaitForSeconds(1f);
        robberyCountIndicator.gameObject.SetActive(false);
    }

    #endregion

    #region ChargeAttack
    public override void ChargeAttack()
    {
        base.ChargeAttack();
        animator.SetTrigger("Charge");
    }

    public override bool ChargeCheck(KeyCode charge)
    {
        if (charging)
        {
            chargeAttackActive = true;
            stayStatic();
            ignoreMovement = true;
            if (charged)
            {
                if (input.GetKeyUp(charge) || (controller && input.GetButtonUp("ChargeAttack" + playerString)))
                {
                    int randomIndex = Random.Range(0, 6); // if you have 6 hurt animations
                    animator.SetFloat("Index", randomIndex);
                    animator.SetTrigger("ChargedHit");
                    charged = false;
                    animator.SetBool("Casting", true);
                    animator.ResetTrigger("tookDmg");
                    StartCoroutine(ChargeSafety(0.83f));
                }
                return true;
            }
            else
            {
                if (input.GetKeyUp(charge) || (controller && input.GetButtonUp("ChargeAttack" + playerString)))
                {
                    stayDynamic();
                    ignoreMovement = false;
                    animator.SetBool("Charging", false);
                    charging = false;
                    knockable = true;
                    animator.ResetTrigger("tookDmg");
                    chargeAttackActive = false;
                }
                return true;
            }
        }
        return false;
    }
    #endregion

    #region Passive
    public void Robbed()
    {
        robberyCountter++;
        if (robberyCountter == 1)
        {
            int randomDamage = Random.Range(0, passiveRange); // 0, 1, 2, or 3
            wipDamage += randomDamage;
            robberyCountter = 0;
        }
    }

    #endregion

    public override void TakeDamage(int dmg, bool blockable, bool parryable=true, bool canCrit = true)
    {
        base.TakeDamage(dmg, blockable);       

        animator.SetTrigger(characterChoiceHandler.PickRandomCharacter());
    }
}
