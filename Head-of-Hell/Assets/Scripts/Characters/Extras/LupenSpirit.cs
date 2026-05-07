using System.Collections;
using UnityEngine;

public class LupenSpirit : MonoBehaviour
{
    public Lupen lupen;
    public KeyCode ability;
    public string playerString;
    public bool controller;
    bool lupenInFormSpell = false;
    public CharacterManager characterChoiceHandler;
    public Animator animator;
    public CharacterAnimationEvents cEvents;
    public int whipDamage, robberyCounter;
    public Character enemy, stolenCharacter;
    public int currentHealth;
    public helthbarscript healthbar;
    public int maxHealth;
    bool swapped=false;
    bool healthswap = true;
    bool spammingCheck = true;

    // --- Injected input provider (same as the Character uses) ---
    private IInputProvider input;
    public void SetInput(IInputProvider provider) => input = provider;

    public void Start()
    {
        animator = GetComponent<Animator>();
        // Fallback to keyboard if someone forgot to set us:
        if (input == null) input = new KeyboardInputProvider();
    }

    void Update()
    {
        if (swapped && stolenCharacter == null)
        {
            ReturnControlToLupen(currentHealth);
            return;
        }

        if (!healthswap) // set stolen char health once we’ve swapped
        {
            if (stolenCharacter != null)
            {
                stolenCharacter.SetCurrentHealth(currentHealth);
                healthswap = true;
            }
        }

        // If we swapped forms and the form dies, return to Lupen and die.
        if (swapped)
        {
            if (stolenCharacter.GetCurrentHealth() <= 0)
            {
                ReturnControlToLupen(stolenCharacter.GetCurrentHealth());
                lupen.Die();
            }
        }

        // If Lupen is currently in-form spell and the animation finished, return.
        if (lupenInFormSpell && !animator.GetBool("Casting"))
        {
            ReturnControlToLupen(stolenCharacter.GetCurrentHealth());
        }

        // *** INPUT: use provider instead of Input. ***
        // Ability to trigger return while in stolen form:
        bool abilityPressed =
            (input.GetKeyDown(ability) || (controller && input.GetButtonDown("Spell" + playerString))) && lupen.isActiveAndEnabled == false;

        Character currentTarget = stolenCharacter != null ? stolenCharacter.GetEnemy() : enemy;
        bool targetIsCasting = currentTarget != null && currentTarget.AmICasting();

        if (abilityPressed && lupen.isActiveAndEnabled == false && !targetIsCasting && spammingCheck==true)
        {
            spammingCheck = false;
            if (stolenCharacter != null)
            {
                stolenCharacter.chargeDisable = true;
            }
            StartCoroutine(SetLupenInFormSpellAfterDelay(1.7f));
        }
    }

    private void ReturnControlToLupen(int healthAfterForm)
    {
        if (lupen == null)
        {
            return;
        }

        currentHealth = healthAfterForm;
        lupenInFormSpell = false;
        swapped = false;
        spammingCheck = false;

        Character currentTarget = stolenCharacter != null ? stolenCharacter.GetEnemy() : enemy;

        if (stolenCharacter != null)
        {
            stolenCharacter.Casting(false);
            stolenCharacter.IgnoreMovement(false);
            stolenCharacter.IgnoreUpdate(false);
            stolenCharacter.StopCHarge();
            stolenCharacter.Unblock();
            stolenCharacter.stayDynamic();
            stolenCharacter.enabled = false;
            stolenCharacter.StopAllCoroutines();
        }

        lupen.enabled = true;
        lupen.SetInput(input);
        if (currentTarget != null)
        {
            lupen.ChangeEnemy(currentTarget);
        }

        lupen.ReturnToLupen(whipDamage, robberyCounter, currentHealth);
    }

    private IEnumerator SetLupenInFormSpellAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        lupenInFormSpell = true;
    }

    public void Action()
    {
        lupen.enabled = false;
        swapped = true;
        healthswap = false;
        spammingCheck = true;

        // Ensure the *stolen form* receives the SAME input provider,
        // so the Agent/keyboard keeps controlling seamlessly.
        if (stolenCharacter != null)
        {
            stolenCharacter.SetInput(input);
        }
    }
}
