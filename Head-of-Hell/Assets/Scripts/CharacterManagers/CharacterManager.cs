using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum CharacterType
{
    Steelager = 0,
    Vander   = 1,
    Rager    = 2,
    Skipler  = 3,
    Fin      = 4,
    LazyBigus= 5,
    Lithra   = 6,
    Chiback  = 7,
    Lupen    = 8,
    Visvia   = 9
}

public class CharacterManager : MonoBehaviour
{
    public string characterName;

    // Player colors
    public Color SteelagerColor;
    public Color FinColor;
    public Color RagerColor;
    public Color SkiplerColor;
    public Color VanderColor;
    public Color LazyBigusColor;
    public Color LithraColor;
    public Color ChibackColor;
    public Color LupenColor;
    public Color LupenSpiritColor;

    // Animator Controllers
    public RuntimeAnimatorController SteelagerAnimatorController;
    public RuntimeAnimatorController FinAnimatorController;
    public RuntimeAnimatorController RagerAnimatorController;
    public RuntimeAnimatorController SkiplerAnimatorController;
    public RuntimeAnimatorController VanderAnimatorController;
    public RuntimeAnimatorController LazyBigusAnimatorController;
    public RuntimeAnimatorController LithraAnimatorController;
    public RuntimeAnimatorController ChibackAnimatorController;
    public RuntimeAnimatorController LupenAnimatorController;
    public RuntimeAnimatorController VisviaAnimatorController;

    Character character;
    Skipler skipler;
    Rager rager;
    Fin fin;
    Steelager steelager;
    LazyBigus bigus;
    Vander vander;
    Lithra lithra;
    Chiback chiback;
    Lupen lupen;
    Animator animator;
    Visvia visvia;

    public CharacterManager enemyHandler;

    public int playerNum;

    public event Action<Character> OnCharacterReady;    // fired once after Awake creates the initial character
    public event Action<Character> OnCharacterChanged;  // fired on ChangeCharacter()

    public bool training = false;

    private CharacterAnimationEvents animEvents;

    public GameManager mngr;

    void Awake()
    {
        // Fetch player character choices
        if (playerNum == 1)
        {
            characterName = PlayerPrefs.GetString("Player1Choice");
        }
        else
        {
            characterName = PlayerPrefs.GetString("Player2Choice");
        }

        if (training)
        {
            characterName = "Random";
        }

        // Handle "Random" selections
        if (characterName == "Random")
        {
            characterName = PickRandomCharacter();
        }

        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();

        animEvents = GetComponent<CharacterAnimationEvents>();
        if (animEvents == null)
            Debug.LogError($"[{name}] Missing CharacterAnimationEvents component!");
            

        // Assign character, color, and animator controller based on selection
        switch (characterName)
        {
            case "Steelager":
                steelager = this.gameObject.AddComponent<Steelager>();
                character = steelager;
                character.characterID = (int)CharacterType.Steelager;
                spriteRenderer.color = SteelagerColor;
                animator = GetComponent<Animator>();
                animator.runtimeAnimatorController = SteelagerAnimatorController;
                break;
            case "Vander":
                vander = this.gameObject.AddComponent<Vander>();
                character = vander;
                character.characterID = (int)CharacterType.Vander;
                spriteRenderer.color = VanderColor;
                animator = GetComponent<Animator>();
                animator.runtimeAnimatorController = VanderAnimatorController;
                break;
            case "Rager":
                rager = this.gameObject.AddComponent<Rager>();
                character = rager;
                character.characterID = (int)CharacterType.Rager;
                spriteRenderer.color = RagerColor;
                animator = GetComponent<Animator>();
                animator.runtimeAnimatorController = RagerAnimatorController;
                break;
            case "Skipler":
                skipler = this.gameObject.AddComponent<Skipler>();
                character = skipler;
                character.characterID = (int)CharacterType.Skipler;
                spriteRenderer.color = SkiplerColor;
                animator = GetComponent<Animator>();
                animator.runtimeAnimatorController = SkiplerAnimatorController;
                break;
            case "Fin":
                fin = this.gameObject.AddComponent<Fin>();
                character = fin;
                character.characterID = (int)CharacterType.Fin;
                spriteRenderer.color = FinColor;
                animator = GetComponent<Animator>();
                animator.runtimeAnimatorController = FinAnimatorController;
                break;
            case "Lazy Bigus":
                bigus = this.gameObject.AddComponent<LazyBigus>();
                character = bigus;
                character.characterID = (int)CharacterType.LazyBigus;
                spriteRenderer.color = LazyBigusColor;
                animator = GetComponent<Animator>();
                animator.runtimeAnimatorController = LazyBigusAnimatorController;
                break;
            case "Lithra":
                lithra = this.gameObject.AddComponent<Lithra>();
                character = lithra;
                character.characterID = (int)CharacterType.Lithra;
                animator = GetComponent<Animator>();
                animator.runtimeAnimatorController = LithraAnimatorController;
                break;
            case "Chiback":
                chiback = this.gameObject.AddComponent<Chiback>();
                character = chiback;
                character.characterID = (int)CharacterType.Chiback;
                spriteRenderer.color = ChibackColor;
                animator = GetComponent<Animator>();
                animator.runtimeAnimatorController = ChibackAnimatorController;
                break;
            case "Lupen":
                lupen = this.gameObject.AddComponent<Lupen>();
                character = lupen;
                character.characterID = (int)CharacterType.Lupen;
                spriteRenderer.color = LupenColor;
                animator = GetComponent<Animator>();
                animator.runtimeAnimatorController = LupenAnimatorController;
                break;
            case "Visvia":
                visvia = this.gameObject.AddComponent<Visvia>();
                character = visvia;
                character.characterID = (int)CharacterType.Visvia;
                spriteRenderer.color = LupenColor;
                animator = GetComponent<Animator>();
                animator.runtimeAnimatorController = VisviaAnimatorController;
                break;
        }

        OnCharacterReady?.Invoke(character);  // <-- tell listeners initial character exists
    }

    void Start()
    {
        if (mngr.trainingMode)
        {
            Resume();
        } 
    }

    void Update()
    {

    }

    public string PickRandomCharacter()
    {
        // Array of possible character type names
        string[] characterTypeNames = {
            "Steelager", "Vander", "Rager",
            "Skipler", "Fin", "Lazy Bigus",
            "Lithra", "Chiback", "Lupen","Visvia"
        };

        // Randomly select an index
        int randomIndex = UnityEngine.Random.Range(0, characterTypeNames.Length);

        if (mngr.trainingMode)
        {
            while (characterTypeNames[randomIndex]=="Lupen" || characterTypeNames[randomIndex] == "Visvia")
            {
                randomIndex = UnityEngine.Random.Range(0, characterTypeNames.Length);
            }
        }

        return characterTypeNames[randomIndex];
    }

    public Character CharacterChoice(int playerNum)
    {
        if (playerNum == 1)
        {
            return character;
        }
        if (playerNum == 2)
        {
            return enemyHandler.CharacterChoice(1);
        }
        print("Randomizer Error");
        return null;
    }

    public string GetCharacterName(int playerNum)
    {
        if (playerNum == 1)
        {
            return characterName;
        }
        if (playerNum == 2)
        {
            return enemyHandler.GetCharacterName(1);
        }
        print("Randomizer Error");
        return null;
    }

    public Animator GetAnimator()
    {
        return animator;
    }

    public void Pause()
    {
        character.stayStatic();
        character.ignoreUpdate = true;
        mngr.roundOn=false;
    }

    public void Resume()
    {
        mngr.roundOn=true;
        //if(!character) return;
        character.stayDynamic();
        character.ignoreUpdate = false;

        if (playerNum == 2)
        {
            mngr.trainingRoundOn = true;
        }    
    }

    public void ChangeCharacter(string givenName)
    {
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();

        characterName = givenName;
        spriteRenderer.color = LupenSpiritColor;

        // Assign character, color, and animator controller based on selection
        switch (givenName)
        {
            case "Steelager":
                steelager = this.gameObject.AddComponent<Steelager>();
                character = steelager;
                animator = GetComponent<Animator>();
                animator.runtimeAnimatorController = SteelagerAnimatorController;
                break;
            case "Vander":
                vander = this.gameObject.AddComponent<Vander>();
                character = vander;
                animator = GetComponent<Animator>();
                animator.runtimeAnimatorController = VanderAnimatorController;
                break;
            case "Rager":
                rager = this.gameObject.AddComponent<Rager>();
                character = rager;
                animator = GetComponent<Animator>();
                animator.runtimeAnimatorController = RagerAnimatorController;
                break;
            case "Skipler":
                skipler = this.gameObject.AddComponent<Skipler>();
                character = skipler;
                animator = GetComponent<Animator>();
                animator.runtimeAnimatorController = SkiplerAnimatorController;
                break;
            case "Fin":
                fin = this.gameObject.AddComponent<Fin>();
                character = fin;
                animator = GetComponent<Animator>();
                animator.runtimeAnimatorController = FinAnimatorController;
                break;
            case "Lazy Bigus":
                bigus = this.gameObject.AddComponent<LazyBigus>();
                character = bigus;
                animator = GetComponent<Animator>();
                animator.runtimeAnimatorController = LazyBigusAnimatorController;
                break;
            case "Lithra":
                lithra = this.gameObject.AddComponent<Lithra>();
                character = lithra;
                animator = GetComponent<Animator>();
                animator.runtimeAnimatorController = LithraAnimatorController;
                break;
            case "Chiback":
                chiback = this.gameObject.AddComponent<Chiback>();
                character = chiback;
                animator = GetComponent<Animator>();
                animator.runtimeAnimatorController = ChibackAnimatorController;
                break;
            case "Lupen":
                character = lupen;
                animator = GetComponent<Animator>();
                spriteRenderer.color = LupenColor;
                animator.runtimeAnimatorController = LupenAnimatorController;
                break;
            case "Visvia":
                character = this.gameObject.AddComponent<Visvia>();
                animator = GetComponent<Animator>();
                animator.runtimeAnimatorController = VisviaAnimatorController;
                break;
        }

        OnCharacterChanged?.Invoke(character); // <-- tell listeners we swapped
    }

    public void ChangeCharacterTraining(String name)
    {
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();

        characterName = name;
        // Assign character, color, and animator controller based on selection
        switch (name)
        {
            case "Steelager":
                steelager = this.gameObject.AddComponent<Steelager>();
                character = steelager;
                character.characterID = (int)CharacterType.Steelager;
                spriteRenderer.color = SteelagerColor;
                animator = GetComponent<Animator>();
                animator.runtimeAnimatorController = SteelagerAnimatorController;
                break;
            case "Vander":
                vander = this.gameObject.AddComponent<Vander>();
                character = vander;
                character.characterID = (int)CharacterType.Vander;
                spriteRenderer.color = VanderColor;
                animator = GetComponent<Animator>();
                animator.runtimeAnimatorController = VanderAnimatorController;
                break;
            case "Rager":
                rager = this.gameObject.AddComponent<Rager>();
                character = rager;
                character.characterID = (int)CharacterType.Rager;
                spriteRenderer.color = RagerColor;
                animator = GetComponent<Animator>();
                animator.runtimeAnimatorController = RagerAnimatorController;
                break;
            case "Skipler":
                skipler = this.gameObject.AddComponent<Skipler>();
                character = skipler;
                character.characterID = (int)CharacterType.Skipler;
                spriteRenderer.color = SkiplerColor;
                animator = GetComponent<Animator>();
                animator.runtimeAnimatorController = SkiplerAnimatorController;
                break;
            case "Fin":
                fin = this.gameObject.AddComponent<Fin>();
                character = fin;
                character.characterID = (int)CharacterType.Fin;
                spriteRenderer.color = FinColor;
                animator = GetComponent<Animator>();
                animator.runtimeAnimatorController = FinAnimatorController;
                break;
            case "Lazy Bigus":
                bigus = this.gameObject.AddComponent<LazyBigus>();
                character = bigus;
                character.characterID = (int)CharacterType.LazyBigus;
                spriteRenderer.color = LazyBigusColor;
                animator = GetComponent<Animator>();
                animator.runtimeAnimatorController = LazyBigusAnimatorController;
                break;
            case "Lithra":
                lithra = this.gameObject.AddComponent<Lithra>();
                character = lithra;
                character.characterID = (int)CharacterType.Lithra;
                spriteRenderer.color = LithraColor;
                animator = GetComponent<Animator>();
                animator.runtimeAnimatorController = LithraAnimatorController;
                break;
            case "Chiback":
                chiback = this.gameObject.AddComponent<Chiback>();
                character = chiback;
                character.characterID = (int)CharacterType.Chiback;
                spriteRenderer.color = ChibackColor;
                animator = GetComponent<Animator>();
                animator.runtimeAnimatorController = ChibackAnimatorController;
                break;
            case "Lupen":
                lupen = this.gameObject.AddComponent<Lupen>();
                character = lupen;
                character.characterID = (int)CharacterType.Lupen;
                spriteRenderer.color = LupenColor;
                animator = GetComponent<Animator>();
                animator.runtimeAnimatorController = LupenAnimatorController;
                break;
            case "Visvia":
                visvia = this.gameObject.AddComponent<Visvia>();
                character = visvia;
                character.characterID = (int)CharacterType.Visvia;
                spriteRenderer.color = LupenColor;
                animator = GetComponent<Animator>();
                animator.runtimeAnimatorController = VisviaAnimatorController;
                break;
        }

        OnCharacterReady?.Invoke(character);  // <-- tell listeners initial character exists

        animEvents.SetCharacter(character);

    }

    // Small helper so others don’t need to know about CharacterChoice(1)
    public Character GetCurrentCharacter() => character;

    public IEnumerator RerollRandomCharacter_TrainingOnly_Co()
    {
        if (!training) yield break;

        if (animator == null) animator = GetComponent<Animator>();
        animator.Rebind();
        animator.Update(0f);

        // 1) διάλεξε νέο
        string name = PickRandomCharacter();
        // 2) “κλείδωσε” τον παλιό για να μην τρέχει άλλο
        // if (character != null)
        // {
        //     character.ignoreUpdate = true;
        //     character.DeactivateColliders();
        // }

        // 3) καταστροφή παλιού component
        if (character != null)
        {
            Destroy(character);
            character = null;
        }

        // 4) περίμενε να ολοκληρωθεί το Destroy end-of-frame
        yield return new WaitForEndOfFrame();

        // 5) φτιάξε νέο character component
        ChangeCharacterTraining(name); // αυτό κάνει AddComponent + set runtime controller + OnCharacterChanged

        var me = GetCurrentCharacter();
        var enemy = enemyHandler.GetCurrentCharacter();

        me.SetEnemy(enemy);
        if (enemy != null)
        {
           enemy.SetEnemy(me); 
        }
        
    }
}
