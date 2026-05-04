using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-1000)]
public class TutorialManager : MonoBehaviour
{
    private sealed class TutorialStateSnapshot
    {
        public bool hasPlayer1Choice;
        public string player1Choice;
        public bool hasPlayer2Choice;
        public string player2Choice;
        public bool hasSelectedStage;
        public string selectedStage;
        public bool isPvE;
        public PvEBotType selectedBotType;
        public PvEDifficulty selectedDifficulty;
        public PvEBotSide selectedBotSide;
    }

    private enum TutorialStep
    {
        Move = 0,
        Jump = 1,
        Attack = 2,
        Heavy = 3,
        Charge = 4,
        Spell = 5,
        Block = 6,
        Parry = 7,
        Complete = 8
    }

    private const string TutorialCharacter = "Skipler";
    private const string BotCharacter = "Steelager";
    private const string DefaultStage = "Stage 1";
    private const float JumpHeight = 0.5f;
    private const float DefenseSpacing = 1.1f;
    private const float DefenseAttackCooldown = 1.35f;
    private static TutorialStateSnapshot cachedState;
    private static bool skipRestoreOnce;

    private GameManager gameManager;
    private Character playerCharacter;
    private Character opponentCharacter;
    private TextMeshProUGUI promptText;
    private TextMeshProUGUI statusText;
    private TutorialStep currentStep;
    private Vector3 playerStartPosition;
    private BotInputProvider opponentInput;
    private float defenseAttackTimer;
    private bool defenseDemoActive;
    private bool defenseAttackQueued;
    private bool movedLeft;
    private bool movedRight;
    private bool chargeReachedReadyState;
    private int defenseOpponentHealthBeforeAttack;
    private int opponentHealthAtStepStart;

    private void Awake()
    {
        CacheStateIfNeeded();
        currentStep = TutorialStep.Move;
        ClearSavedStep();
        PrepareTutorialState();
    }

    private IEnumerator Start()
    {
        yield return null;

        gameManager = FindObjectOfType<GameManager>();
        if (gameManager == null)
        {
            Debug.LogWarning("TutorialManager: GameManager not found.");
            yield break;
        }

        CreatePromptUI();
        yield return StartCoroutine(BindCharacters());

        if (playerCharacter == null || opponentCharacter == null)
        {
            Debug.LogWarning("TutorialManager: Missing character references.");
            yield break;
        }

        playerStartPosition = playerCharacter.transform.position;
        ResetStepState(currentStep);
        ConfigureForCurrentStep();
        UpdatePrompt();
    }

    private void Update()
    {
        if (playerCharacter == null || opponentCharacter == null)
            return;

        MaintainTutorialState();

        if (defenseDemoActive)
            UpdateDefenseDemo();

        switch (currentStep)
        {
            case TutorialStep.Move:
                bool moveStateChanged = false;

                if (Input.GetKey(playerCharacter.left) && !movedLeft)
                {
                    movedLeft = true;
                    moveStateChanged = true;
                }

                if (Input.GetKey(playerCharacter.right) && !movedRight)
                {
                    movedRight = true;
                    moveStateChanged = true;
                }

                if (moveStateChanged)
                    UpdatePrompt();

                if (movedLeft && movedRight)
                {
                    AdvanceStep(TutorialStep.Jump);
                }
                break;

            case TutorialStep.Jump:
                if (Input.GetKeyDown(playerCharacter.up) ||
                    (!playerCharacter.isGrounded && playerCharacter.transform.position.y > playerStartPosition.y + JumpHeight))
                {
                    AdvanceStep(TutorialStep.Attack);
                }
                break;

            case TutorialStep.Attack:
                if (DidPlayerHitOpponent())
                    AdvanceStep(TutorialStep.Heavy);
                break;

            case TutorialStep.Heavy:
                if (DidPlayerHitOpponent())
                    AdvanceStep(TutorialStep.Charge);
                break;

            case TutorialStep.Charge:
                if (playerCharacter.IsCharged)
                {
                    if (!chargeReachedReadyState)
                    {
                        chargeReachedReadyState = true;
                        UpdatePrompt();
                    }
                }
                else if (chargeReachedReadyState && !playerCharacter.IsCharging && DidPlayerHitOpponent())
                {
                    AdvanceStep(TutorialStep.Spell);
                }
                break;

            case TutorialStep.Spell:
                if (Input.GetKeyDown(playerCharacter.ability) || playerCharacter.IsCasting)
                    AdvanceStep(TutorialStep.Block);
                break;

            case TutorialStep.Block:
                if (playerCharacter.isBlocking && defenseAttackQueued && opponentCharacter.HeavyAttacking)
                    AdvanceStep(TutorialStep.Parry);
                break;

            case TutorialStep.Parry:
                if (defenseAttackQueued &&
                    opponentCharacter.GetCurrentHealth() < defenseOpponentHealthBeforeAttack)
                    AdvanceStep(TutorialStep.Complete);
                break;

            case TutorialStep.Complete:
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    ClearSavedStep();
                    SceneManager.LoadScene(0);
                }
                break;
        }
    }

    private void PrepareTutorialState()
    {
        PvESelectionState.IsPvE = false;

        PlayerPrefs.SetString("Player1Choice", TutorialCharacter);

        if (string.IsNullOrWhiteSpace(PlayerPrefs.GetString("Player2Choice", string.Empty)))
            PlayerPrefs.SetString("Player2Choice", BotCharacter);

        if (string.IsNullOrWhiteSpace(PlayerPrefs.GetString("SelectedStage", string.Empty)))
            PlayerPrefs.SetString("SelectedStage", DefaultStage);

        PlayerPrefs.Save();
    }

    private IEnumerator BindCharacters()
    {
        for (int i = 0; i < 60; i++)
        {
            playerCharacter = gameManager.p1Manager != null ? gameManager.p1Manager.GetCurrentCharacter() : null;
            opponentCharacter = gameManager.p2Manager != null ? gameManager.p2Manager.GetCurrentCharacter() : null;

            if (playerCharacter != null && opponentCharacter != null)
                break;

            yield return null;
        }
    }

    private void ConfigureForCurrentStep()
    {
        if (gameManager.p1Manager != null &&
            gameManager.p1Manager.characterName != TutorialCharacter)
        {
            ReloadIntoTutorialCharacter();
            return;
        }

        if (IsDefenseStep(currentStep))
            BeginDefenseDemo();
        else
            FreezeOpponent();
    }

    private void MaintainTutorialState()
    {
        if (gameManager != null)
            gameManager.roundOn = true;

        if (playerCharacter != null)
        {
            playerCharacter.ignoreDamage = true;
            playerCharacter.overrideDeath = true;

            if (playerCharacter.GetCurrentHealth() < playerCharacter.maxHealth)
                playerCharacter.SetCurrentHealth(playerCharacter.maxHealth);
        }

        if (opponentCharacter != null)
            opponentCharacter.overrideDeath = true;
    }

    private void FreezeOpponent()
    {
        defenseDemoActive = false;
        defenseAttackQueued = false;

        if (opponentInput != null)
            opponentInput.ClearFrameState();

        if (opponentCharacter == null)
            return;

        opponentCharacter.ignoreUpdate = true;

        Rigidbody2D body = opponentCharacter.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.velocity = Vector2.zero;
            body.constraints = RigidbodyConstraints2D.FreezePositionX |
                               RigidbodyConstraints2D.FreezePositionY |
                               RigidbodyConstraints2D.FreezeRotation;
        }
    }

    private void BeginDefenseDemo()
    {
        if (playerCharacter == null || opponentCharacter == null)
            return;

        defenseDemoActive = true;
        defenseAttackQueued = false;
        defenseAttackTimer = 0.75f;
        defenseOpponentHealthBeforeAttack = opponentCharacter.GetCurrentHealth();

        if (opponentInput == null)
            opponentInput = new BotInputProvider();

        opponentCharacter.SetInput(opponentInput);
        opponentCharacter.ignoreUpdate = false;

        Rigidbody2D body = opponentCharacter.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.velocity = Vector2.zero;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        PositionDefenseActors();
    }

    private void UpdateDefenseDemo()
    {
        if (opponentInput == null)
            return;

        opponentInput.ClearFrameState();
        PositionDefenseActors();

        if (opponentCharacter.IsCasting || opponentCharacter.HeavyAttacking)
            return;

        defenseAttackTimer -= Time.deltaTime;
        if (defenseAttackTimer > 0f)
            return;

        defenseAttackQueued = true;
        defenseAttackTimer = DefenseAttackCooldown;
        defenseOpponentHealthBeforeAttack = opponentCharacter.GetCurrentHealth();
        opponentInput.PressKeyOneFrame(opponentCharacter.heavyAttack);
    }

    private void PositionDefenseActors()
    {
        if (playerCharacter == null || opponentCharacter == null)
            return;

        Vector3 playerPos = playerCharacter.transform.position;
        Vector3 enemyPos = opponentCharacter.transform.position;

        enemyPos.x = playerPos.x + DefenseSpacing;
        enemyPos.y = playerPos.y;
        enemyPos.z = opponentCharacter.transform.position.z;

        opponentCharacter.transform.position = enemyPos;
        opponentCharacter.transform.localScale = new Vector3(-1f, 1f, 1f);
        playerCharacter.transform.localScale = new Vector3(1f, 1f, 1f);
    }

    private void AdvanceStep(TutorialStep nextStep)
    {
        currentStep = nextStep;
        ResetStepState(currentStep);

        if (gameManager.p1Manager != null &&
            gameManager.p1Manager.characterName != TutorialCharacter)
        {
            ReloadIntoTutorialCharacter();
            return;
        }

        if (currentStep == TutorialStep.Complete)
            ClearSavedStep();

        ConfigureForCurrentStep();
        UpdatePrompt();
    }

    private void ReloadIntoTutorialCharacter()
    {
        skipRestoreOnce = true;
        PlayerPrefs.SetString("Player1Choice", TutorialCharacter);
        PlayerPrefs.Save();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void OnDestroy()
    {
        if (skipRestoreOnce)
        {
            skipRestoreOnce = false;
            return;
        }

        RestoreCachedState();
    }

    private void CreatePromptUI()
    {
        Canvas canvas = new GameObject("TutorialCanvas").AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvas.gameObject.AddComponent<GraphicRaycaster>();

        GameObject textObject = new GameObject("TutorialPrompt");
        textObject.transform.SetParent(canvas.transform, false);

        RectTransform rectTransform = textObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 1f);
        rectTransform.anchorMax = new Vector2(0.5f, 1f);
        rectTransform.pivot = new Vector2(0.5f, 1f);
        rectTransform.anchoredPosition = new Vector2(0f, -60f);
        rectTransform.sizeDelta = new Vector2(1000f, 220f);

        promptText = textObject.AddComponent<TextMeshProUGUI>();
        promptText.alignment = TextAlignmentOptions.Center;
        promptText.fontSize = 40f;
        promptText.color = Color.white;
        promptText.outlineWidth = 0.2f;
        promptText.enableWordWrapping = true;

        GameObject statusObject = new GameObject("TutorialStatus");
        statusObject.transform.SetParent(canvas.transform, false);

        RectTransform statusRect = statusObject.AddComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0.5f, 1f);
        statusRect.anchorMax = new Vector2(0.5f, 1f);
        statusRect.pivot = new Vector2(0.5f, 1f);
        statusRect.anchoredPosition = new Vector2(0f, -190f);
        statusRect.sizeDelta = new Vector2(900f, 110f);

        statusText = statusObject.AddComponent<TextMeshProUGUI>();
        statusText.alignment = TextAlignmentOptions.Center;
        statusText.fontSize = 34f;
        statusText.color = new Color(1f, 0.84f, 0.25f, 1f);
        statusText.outlineWidth = 0.2f;
        statusText.enableWordWrapping = true;
    }

    private void ResetStepState(TutorialStep step)
    {
        defenseAttackQueued = false;

        if (step == TutorialStep.Move)
        {
            movedLeft = false;
            movedRight = false;
            playerStartPosition = playerCharacter != null
                ? playerCharacter.transform.position
                : playerStartPosition;
        }

        if (step == TutorialStep.Charge)
            chargeReachedReadyState = false;

        if (RequiresSuccessfulHit(step) && opponentCharacter != null)
            opponentHealthAtStepStart = opponentCharacter.GetCurrentHealth();
    }

    private void UpdatePrompt()
    {
        if (promptText == null || playerCharacter == null)
            return;

        switch (currentStep)
        {
            case TutorialStep.Move:
                promptText.text = "Move: press [" + playerCharacter.left + "] and [" + playerCharacter.right +
                                  "]  Left: " + (movedLeft ? "done" : "pending") +
                                  "  Right: " + (movedRight ? "done" : "pending");
                SetStatusText(string.Empty);
                break;

            case TutorialStep.Jump:
                promptText.text = "Jump: press [" + playerCharacter.up + "]";
                SetStatusText(string.Empty);
                break;

            case TutorialStep.Attack:
                promptText.text = "Quick attack: press [" + playerCharacter.lightAttack + "] and hit the enemy.";
                //SetStatusText("This step only completes when the enemy takes damage.");
                break;

            case TutorialStep.Heavy:
                promptText.text = "Heavy attack: press [" + playerCharacter.heavyAttack + "] and hit the enemy.";
                //SetStatusText("This step only completes when the enemy takes damage.");
                break;

            case TutorialStep.Charge:
                promptText.text = "Charge: hold [" + playerCharacter.charge + "] until the indicator says RELEASE, then hit the enemy.";
               //SetStatusText(chargeReachedReadyState ? "RELEASE NOW AND LAND THE HIT" : "HOLD...");
                break;

            case TutorialStep.Spell:
                promptText.text = "Spell: use a direction with the spell input. Try [" + playerCharacter.left + "] + [" + playerCharacter.ability + "] or [" + playerCharacter.right + "] + [" + playerCharacter.ability + "].";
                //SetStatusText("Do not just press the spell button by itself. Add a left or right direction.");
                break;

            case TutorialStep.Block:
                promptText.text = "Block: hold [" + playerCharacter.block + "] when the bot attacks.";
                SetStatusText(string.Empty);
                break;

            case TutorialStep.Parry:
                promptText.text = "Parry: press [" + playerCharacter.parry + "] just before the bot hits you.";
                SetStatusText(string.Empty);
                break;

            case TutorialStep.Complete:
                promptText.text = "Tutorial complete. Press Esc to return to the main menu.";
                SetStatusText(string.Empty);
                break;
        }
    }

    private void SetStatusText(string text)
    {
        if (statusText == null)
            return;

        statusText.text = text;
        statusText.gameObject.SetActive(!string.IsNullOrEmpty(text));
    }

    private static bool IsDefenseStep(TutorialStep step)
    {
        return step == TutorialStep.Block || step == TutorialStep.Parry;
    }

    private static bool RequiresSuccessfulHit(TutorialStep step)
    {
        return step == TutorialStep.Attack ||
               step == TutorialStep.Heavy ||
               step == TutorialStep.Charge;
    }

    private bool DidPlayerHitOpponent()
    {
        if (opponentCharacter == null)
            return false;

        return opponentCharacter.GetCurrentHealth() < opponentHealthAtStepStart;
    }

    private static void ClearSavedStep()
    {
        PlayerPrefs.DeleteKey("TutorialScene.CurrentStep");
        PlayerPrefs.Save();
    }

    private static void CacheStateIfNeeded()
    {
        if (cachedState != null)
            return;

        cachedState = new TutorialStateSnapshot
        {
            hasPlayer1Choice = PlayerPrefs.HasKey("Player1Choice"),
            player1Choice = PlayerPrefs.GetString("Player1Choice", string.Empty),
            hasPlayer2Choice = PlayerPrefs.HasKey("Player2Choice"),
            player2Choice = PlayerPrefs.GetString("Player2Choice", string.Empty),
            hasSelectedStage = PlayerPrefs.HasKey("SelectedStage"),
            selectedStage = PlayerPrefs.GetString("SelectedStage", string.Empty),
            isPvE = PvESelectionState.IsPvE,
            selectedBotType = PvESelectionState.SelectedBotType,
            selectedDifficulty = PvESelectionState.SelectedDifficulty,
            selectedBotSide = PvESelectionState.SelectedBotSide
        };
    }

    private static void RestoreCachedState()
    {
        if (cachedState == null)
            return;

        if (cachedState.hasPlayer1Choice)
            PlayerPrefs.SetString("Player1Choice", cachedState.player1Choice);
        else
            PlayerPrefs.DeleteKey("Player1Choice");

        if (cachedState.hasPlayer2Choice)
            PlayerPrefs.SetString("Player2Choice", cachedState.player2Choice);
        else
            PlayerPrefs.DeleteKey("Player2Choice");

        if (cachedState.hasSelectedStage)
            PlayerPrefs.SetString("SelectedStage", cachedState.selectedStage);
        else
            PlayerPrefs.DeleteKey("SelectedStage");

        PvESelectionState.IsPvE = cachedState.isPvE;
        PvESelectionState.SelectedBotType = cachedState.selectedBotType;
        PvESelectionState.SelectedDifficulty = cachedState.selectedDifficulty;
        PvESelectionState.SelectedBotSide = cachedState.selectedBotSide;

        PlayerPrefs.Save();
        cachedState = null;
    }
}
