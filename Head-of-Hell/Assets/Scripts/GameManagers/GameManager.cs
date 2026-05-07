using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;
    private bool roundTelemetryClosed = false;
    static int player1Wins = 0;
    static int player2Wins = 0;
    static int player3Wins = 0;
    static int player4Wins = 0;
    public GameObject[] stages;
    string stageName;
    public TextMeshProUGUI winner;
    public TextMeshProUGUI finalWinner;
    public TextMeshProUGUI p1ProfileNameText;
    public TextMeshProUGUI p2ProfileNameText;
    public TextMeshProUGUI p3ProfileNameText;
    public TextMeshProUGUI p4ProfileNameText;
    string p1, p2;
    static int roundNumber = 1;
    static int roundCounter = 1;
    public CharacterManager p1Manager, p2Manager, p3Manager, p4Manager;
    public GameObject playAgainButton;
    public GameObject mainMenuButton;
    public GameObject saveReplayButton;
    public GameObject victoryScreenNavigation;
    public AudioManager audioManager;
    public GameObject p1R1, p1R2, p1R3;
    public GameObject p2R1, p2R2, p2R3;
    public GameObject p3R1, p3R2, p3R3;
    public GameObject p4R1, p4R2, p4R3;
    //static int p1Rounds = 0, p2Rounds = 0;
    bool tie = false;
    static string c1Name, c2Name;
    static bool p1Random = false;
    static bool p2Random = false;
    bool gameEnd = false;
    static int portalNumber;
    public GameObject[] portalPairs;
    bool chanChan;
    public int maxHealth = -1;

    //training
    public bool trainingMode = false;           // tick this for training scene
    public FighterAgent agentP1, agentP2;       // drag the two FighterAgent components
    public Transform p1Spawn, p2Spawn;          // empty transforms as spawn points

    public float tScale = 1f;

    public bool roundOn = false;
    public bool trainingRoundOn = false;
    public TrainingOpponentDirector opponentDirector;


    // Start is called before the first frame update
    void Start()
    {
        Debug.Log("[GameManager] CurrentMode = " + GameModeSelectionState.CurrentMode);
        Debug.Log("[GameManager] PlayerCount = " + GameModeSelectionState.PlayerCount);
        Debug.Log("[GameManager] RequiresThirdPlayerSelection = " + GameModeSelectionState.RequiresThirdPlayerSelection);
        roundTelemetryClosed = false;
        bool threePlayerMode = IsThreePlayerMode();
        bool fourPlayerMode = IsFourPlayerMode();
        bool thirdPlayerEnabled = GameModeSelectionState.RequiresThirdPlayerSelection;

        if (fourPlayerMode)
        {
            EnsureFourPlayerRuntimeSetup();
            ConfigureTwoVersusTwoTeams();
        }

        if (p3Manager != null)
        {
            p3Manager.gameObject.SetActive(thirdPlayerEnabled);

            if (thirdPlayerEnabled && !roundOn)
            {
                p3Manager.Pause();
            }
        }

        if (p4Manager != null)
        {
            p4Manager.gameObject.SetActive(fourPlayerMode);

            if (fourPlayerMode && !roundOn)
            {
                p4Manager.Pause();
            }
        }

        SetP3HudVisible(thirdPlayerEnabled);
        SetP4HudVisible(fourPlayerMode);

        stageName = PlayerPrefs.GetString("SelectedStage", "Stage 1");
        if (stageName == "Stage 1")
        {
            stages[0].SetActive(true);
        }
        else if (stageName == "Stage 2")
        {
            stages[1].SetActive(true);
        }
        else if (stageName == "Stage 3")
        {
            stages[2].SetActive(true);
        }



        TelemetryManager.Instance?.StartSession();

        TelemetryManager.Instance?.SetMatchMeta(new TelemetryMatchMeta
        {
            map = stageName,
            mode = trainingMode ? "training" : "1v1",
            roundNumber_ = roundCounter,
            trainingMode = trainingMode
        });

        TelemetryManager.Instance?.SetPlayers(
            "P1", p1Manager ? p1Manager.GetCharacterName(1) : "",
            "P2", p2Manager ? p2Manager.GetCharacterName(1) : ""
        );

        // Profile telemetry
        var p1Profile = ProfileManager.I?.GetTelemetryIdentity(1) ?? ("NONE", "None");
        var p2Profile = ProfileManager.I?.GetTelemetryIdentity(2) ?? ("NONE", "None");
        var p3Profile = ProfileManager.I?.GetTelemetryIdentity(3) ?? ("NONE", "None");
        var p4Profile = ProfileManager.I?.GetTelemetryIdentity(4) ?? ("NONE", "None");
        if (p1ProfileNameText != null)
            p1ProfileNameText.text = p1Profile.name;

        if (p2ProfileNameText != null)
            p2ProfileNameText.text = p2Profile.name;

        if (p3ProfileNameText != null)
            p3ProfileNameText.text = thirdPlayerEnabled ? p3Profile.name : "";

        if (p4ProfileNameText != null)
            p4ProfileNameText.text = fourPlayerMode ? p4Profile.name : "";
        TelemetryManager.Instance?.SetMatchMeta(new TelemetryMatchMeta
        {
            p1ProfileId = p1Profile.id,
            p1ProfileName = p1Profile.name,
            p2ProfileId = p2Profile.id,
            p2ProfileName = p2Profile.name,
            p3ProfileId = thirdPlayerEnabled ? p3Profile.id : "",
            p3ProfileName = thirdPlayerEnabled ? p3Profile.name : ""
        });


        int selectedSlot = RulesetSelectionState.SelectedSlot;
        Debug.Log("Gameplay SelectedSlot = " + selectedSlot);

        if (selectedSlot > 0)
        {
            CustomRuleset loadedRuleset = RulesetManager.Instance.LoadCustomRuleset(selectedSlot);

            if (loadedRuleset != null)
            {
                Debug.Log("Loaded custom ruleset: " + loadedRuleset.slotName);

                roundNumber = loadedRuleset.rounds;
                portalNumber = loadedRuleset.portals;
                chanChan = loadedRuleset.chanChan;
                maxHealth = loadedRuleset.health;

                ApplyRulesetToCurrentCharacters(loadedRuleset);
            }
            else
            {
                Debug.LogWarning("No custom ruleset found for selected slot: " + selectedSlot);
            }
        }
        else
        {
            Debug.Log("Default ruleset selected.");
        }

        if (chanChan)
        {
            //maxHealth = Random.Range(100, 201);
            portalNumber = Random.Range(0, 5);
        }

        if (trainingMode)
        {
            roundOn = true;
            portalNumber = 0;
            Time.timeScale = tScale;
        }

        switch (portalNumber)
        {
            case 0:
                break;
            case 1:
                portalPairs[0].SetActive(true);
                break;
            case 2:
                portalPairs[0].SetActive(true);
                portalPairs[1].SetActive(true);
                break;
            case 3:
                portalPairs[2].SetActive(true);
                break;
            case 4:
                portalPairs[2].SetActive(true);
                portalPairs[3].SetActive(true);
                break;
        }

        ActivateIndicators();
    }

    private void SetP3HudVisible(bool visible)
    {
        string[] p3HudObjectNames =
        {
            "p3healthbar",
            "p3CooldownBar",
            "p3DamageCounter",
            "QuickAttackIndicatorP3",
            "P3ProfileName",
            "P3Name"
        };

        foreach (string objectName in p3HudObjectNames)
        {
            GameObject hudObject = FindHudObjectByTrimmedName(objectName);
            if (hudObject != null)
            {
                hudObject.SetActive(visible);
            }
        }
    }

    private void SetP4HudVisible(bool visible)
    {
        string[] p4HudObjectNames =
        {
            "p4healthbar",
            "p4CooldownBar",
            "p4DamageCounter",
            "QuickAttackIndicatorP4",
            "P4ProfileName",
            "P4Name"
        };

        foreach (string objectName in p4HudObjectNames)
        {
            GameObject hudObject = FindHudObjectByTrimmedName(objectName);
            if (hudObject != null)
            {
                hudObject.SetActive(visible);
            }
        }
    }

    private GameObject FindHudObjectByTrimmedName(string objectName)
    {
        GameObject exactMatch = GameObject.Find(objectName);
        if (exactMatch != null)
        {
            return exactMatch;
        }

        Transform[] allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform candidate = allTransforms[i];
            if (candidate == null)
            {
                continue;
            }

            if (candidate.hideFlags != HideFlags.None)
            {
                continue;
            }

            if (!candidate.gameObject.scene.IsValid())
            {
                continue;
            }

            if (candidate.name.Trim() == objectName)
            {
                return candidate.gameObject;
            }
        }

        return null;
    }

    private void EnsureFourPlayerRuntimeSetup()
    {
        if (!IsFourPlayerMode() || p3Manager == null || p4Manager != null)
            return;

        CharacterSetup p3Setup = p3Manager.GetComponent<CharacterSetup>();
        if (p3Setup == null)
            return;

        string originalP3Choice = PlayerPrefs.GetString("Player3Choice");
        string p4Choice = PlayerPrefs.GetString("Player4Choice", originalP3Choice);
        PlayerPrefs.SetString("Player3Choice", p4Choice);

        GameObject clone = Instantiate(p3Manager.gameObject, p3Manager.transform.parent);
        clone.name = p3Manager.gameObject.name + "_P4";

        PlayerPrefs.SetString("Player3Choice", originalP3Choice);

        p4Manager = clone.GetComponent<CharacterManager>();
        CharacterSetup p4Setup = clone.GetComponent<CharacterSetup>();
        if (p4Manager == null || p4Setup == null)
            return;

        GameObject p4Healthbar = CloneHudObject("p3healthbar", "p4healthbar", new Vector2(0f, -48f));
        GameObject p4CooldownBar = CloneHudObject("p3CooldownBar", "p4CooldownBar", new Vector2(0f, -48f));
        GameObject p4DamageCounter = CloneHudObject("p3DamageCounter", "p4DamageCounter", new Vector2(0f, -48f));
        GameObject p4NameObject = CloneHudObject("P3Name", "P4Name", new Vector2(0f, -48f));
        GameObject p4ProfileNameObject = CloneHudObject("P3ProfileName", "P4ProfileName", new Vector2(0f, -48f));
        GameObject p4QuickIndicator = CloneHudObject("QuickAttackIndicatorP3", "QuickAttackIndicatorP4", new Vector2(0f, -48f));

        p4ProfileNameText = p4ProfileNameObject != null ? p4ProfileNameObject.GetComponent<TextMeshProUGUI>() : null;
        p4Setup.healthbar = p4Healthbar != null ? p4Healthbar.GetComponent<helthbarscript>() : null;
        p4Setup.cooldownSlider = p4CooldownBar != null ? p4CooldownBar.GetComponent<Slider>() : null;
        p4Setup.damageCounter = p4DamageCounter != null ? p4DamageCounter.GetComponent<TextMeshProUGUI>() : null;
        p4Setup.P1Name = p4NameObject != null ? p4NameObject.GetComponent<TextMeshProUGUI>() : null;
        p4Setup.quickAttackIndicator = p4QuickIndicator;
        p4Setup.playerNum = 4;
        p4Setup.gameManager = this;

        Vector3 p2Position = p2Manager != null ? p2Manager.transform.position : p3Manager.transform.position;
        clone.transform.position = p2Position + new Vector3(0f, 2.25f, 0f);

        p4Manager.ConfigureRuntimeClone(4, p1Manager, this);
    }

    private void ConfigureTwoVersusTwoTeams()
    {
        int teamALayer = LayerMask.NameToLayer("Player1layer");
        int teamBLayer = LayerMask.NameToLayer("Player2Layer");

        ApplyTeamRuntimeBindings(p1Manager, teamALayer, "Player2Layer", p2Manager, 1, false);
        ApplyTeamRuntimeBindings(p3Manager, teamALayer, "Player2Layer", p2Manager, 3, false);
        ApplyTeamRuntimeBindings(p2Manager, teamBLayer, "Player1layer", p1Manager, 2, true);
        ApplyTeamRuntimeBindings(p4Manager, teamBLayer, "Player1layer", p1Manager, 4, true);
    }

    private void ApplyTeamRuntimeBindings(CharacterManager manager, int layer, string enemyLayerName, CharacterManager defaultEnemy, int playerNum, bool faceLeft)
    {
        if (manager == null)
            return;

        SetLayerRecursively(manager.gameObject, layer);
        NormalizePlayerTransform(manager.transform, faceLeft);

        CharacterSetup setup = manager.GetComponent<CharacterSetup>();
        if (setup != null)
        {
            setup.enemyLayer = LayerMask.GetMask(enemyLayerName);
            setup.playerNum = playerNum;
            setup.gameManager = this;
        }

        manager.ConfigureRuntimeClone(playerNum, defaultEnemy, this);
    }

    private void NormalizePlayerTransform(Transform playerTransform, bool faceLeft)
    {
        if (playerTransform == null)
            return;

        playerTransform.localRotation = Quaternion.identity;

        Vector3 scale = playerTransform.localScale;
        float magnitude = Mathf.Abs(scale.x);
        if (magnitude <= 0f)
            magnitude = 1f;

        scale.x = faceLeft ? -magnitude : magnitude;
        playerTransform.localScale = scale;
    }

    private void SetLayerRecursively(GameObject root, int layer)
    {
        if (root == null || layer < 0)
            return;

        root.layer = layer;
        foreach (Transform child in root.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    private GameObject CloneHudObject(string sourceName, string cloneName, Vector2 anchoredOffset)
    {
        GameObject source = GameObject.Find(sourceName);
        if (source == null)
            return null;

        GameObject clone = Instantiate(source, source.transform.parent);
        clone.name = cloneName;

        RectTransform sourceRect = source.GetComponent<RectTransform>();
        RectTransform cloneRect = clone.GetComponent<RectTransform>();
        if (sourceRect != null && cloneRect != null)
        {
            cloneRect.anchoredPosition = sourceRect.anchoredPosition + anchoredOffset;
        }

        return clone;
    }

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            //DontDestroyOnLoad(this.gameObject);
            //I keep that useless awake in case I did need it for some reason and I should know that this is the reason behind a bug
        }
        else
        {
            Destroy(gameObject);
        }

        QualitySettings.vSyncCount = 0;

        if (trainingMode)
            Application.targetFrameRate = -1;   // unlimited
        else
            Application.targetFrameRate = 60;
    }
    private bool IsThreePlayerMode()
    {
        return GameModeSelectionState.CurrentMode == SelectedGameMode.PvP_1v1v1;
    }

    private bool IsFourPlayerMode()
    {
        return GameModeSelectionState.CurrentMode == SelectedGameMode.PvP_2v2;
    }

    public bool IsThreePlayerMatch()
    {
        return IsThreePlayerMode();
    }

    public bool IsTwoVersusTwoMatch()
    {
        return IsFourPlayerMode();
    }

    private Character GetCharacterForPlayer(int playerNum)
    {
        if (playerNum == 1)
            return p1Manager != null ? p1Manager.GetCurrentCharacter() : null;

        if (playerNum == 2)
            return p2Manager != null ? p2Manager.GetCurrentCharacter() : null;

        if (playerNum == 3)
            return p3Manager != null ? p3Manager.GetCurrentCharacter() : null;

        if (playerNum == 4)
            return p4Manager != null ? p4Manager.GetCurrentCharacter() : null;

        return null;
    }

    private bool IsCharacterAlive(Character character)
    {
        return character != null && character.isActiveAndEnabled && !character.IsDead();
    }

    private string GetWinnerId(int playerNum)
    {
        if (playerNum == 1) return "P1";
        if (playerNum == 2) return "P2";
        if (playerNum == 3) return "P3";
        if (playerNum == 4) return "P4";
        return "";
    }

    private string GetWinnerCharacterName(int playerNum)
    {
        if (playerNum == 1)
            return p1Manager != null ? p1Manager.GetCharacterName(1) : "";

        if (playerNum == 2)
            return p2Manager != null ? p2Manager.GetCharacterName(1) : "";

        if (playerNum == 3)
            return p3Manager != null ? p3Manager.GetCharacterName(1) : "";

        if (playerNum == 4)
            return p4Manager != null ? p4Manager.GetCharacterName(1) : "";

        return "";
    }

    private string GetWinnerDisplayName(int playerNum)
    {
        return GetWinnerCharacterName(playerNum);
    }

    public bool ArePlayersTeammates(int playerA, int playerB)
    {
        if (playerA == playerB)
            return true;

        if (!IsFourPlayerMode())
            return false;

        return (playerA == 1 && playerB == 3)
            || (playerA == 3 && playerB == 1)
            || (playerA == 2 && playerB == 4)
            || (playerA == 4 && playerB == 2);
    }

    public bool ArePlayersOpponents(int playerA, int playerB)
    {
        if (playerA == playerB)
            return false;

        return !ArePlayersTeammates(playerA, playerB);
    }

    public bool AreCharactersOpponents(Character a, Character b)
    {
        if (a == null || b == null)
            return false;

        return ArePlayersOpponents(a.GetPlayerNum(), b.GetPlayerNum());
    }

    private int GetTeamIdForPlayer(int playerNum)
    {
        if (!IsFourPlayerMode())
            return 0;

        if (playerNum == 1 || playerNum == 3)
            return 1;

        if (playerNum == 2 || playerNum == 4)
            return 2;

        return 0;
    }

    private string GetTeamDisplayName(int teamId)
    {
        if (teamId == 1)
            return "Team P1/P3";

        if (teamId == 2)
            return "Team P2/P4";

        return "Team";
    }

    public bool HandleThreePlayerDeath(Character deadCharacter)
    {
        if (!IsThreePlayerMode() || deadCharacter == null)
        {
            return false;
        }

        int aliveCount = 0;
        int survivingPlayerNum = 0;

        for (int playerNum = 1; playerNum <= 3; playerNum++)
        {
            Character character = GetCharacterForPlayer(playerNum);
            if (!IsCharacterAlive(character))
            {
                continue;
            }

            aliveCount++;
            survivingPlayerNum = playerNum;
        }

        if (aliveCount != 1)
        {
            return false;
        }

        string winnerName = GetWinnerDisplayName(survivingPlayerNum);
        RoundEndThreePlayer(survivingPlayerNum, winnerName);
        return true;
    }

    public bool HandleTeamDeath(Character deadCharacter)
    {
        if (!IsFourPlayerMode() || deadCharacter == null)
        {
            return false;
        }

        int deadTeamId = GetTeamIdForPlayer(deadCharacter.GetPlayerNum());
        if (deadTeamId == 0)
        {
            return false;
        }

        for (int playerNum = 1; playerNum <= 4; playerNum++)
        {
            if (GetTeamIdForPlayer(playerNum) != deadTeamId)
            {
                continue;
            }

            Character character = GetCharacterForPlayer(playerNum);
            if (IsCharacterAlive(character))
            {
                return false;
            }
        }

        int winningTeamId = deadTeamId == 1 ? 2 : 1;
        RoundEndTeam(winningTeamId, GetTeamDisplayName(winningTeamId));
        return true;
    }

    private void RoundEndThreePlayer(int winnerPlayerNum, string winnerName)
    {
        if (trainingMode)
        {
            SoftResetRound(winnerPlayerNum);
            return;
        }

        winner.gameObject.SetActive(true);
        DisableGamePlay();
        winner.text = winnerName + " prevails!";

        if (!roundTelemetryClosed)
        {
            TelemetryManager.Instance?.SetMatchMeta(new TelemetryMatchMeta
            {
                map = stageName,
                mode = GetTelemetryMode(),
                roundNumber_ = roundCounter,
                trainingMode = trainingMode,

                p1Id = "P1",
                p1Character = GetWinnerCharacterName(1),
                p2Id = "P2",
                p2Character = GetWinnerCharacterName(2),
                p3Id = "P3",
                p3Character = GetWinnerCharacterName(3),

                winnerId = GetWinnerId(winnerPlayerNum),
                winnerCharacter = GetWinnerCharacterName(winnerPlayerNum)
            });

            TelemetryManager.Instance?.EndSession($"RoundEnded_LastAlive_winner={winnerName}");
            roundTelemetryClosed = true;
        }

        StartCoroutine(WaitAndCheckThreePlayer(winnerName));
        roundOn = false;
    }

    private void RoundEndTeam(int winnerTeamId, string winnerName)
    {
        if (trainingMode)
        {
            SoftResetRound(winnerTeamId);
            return;
        }

        winner.gameObject.SetActive(true);
        DisableGamePlay();
        winner.text = winnerName + " prevails!";
        StartCoroutine(WaitAndCheckTeam(winnerName));
        roundOn = false;
    }

    private IEnumerator WaitAndCheckThreePlayer(string winnerName)
    {
        yield return new WaitForSeconds(3f);

        finalWinner.text = "Victory belongs to " + winnerName + "!\n Chan Chan smiles...";
        winner.gameObject.SetActive(false);
        finalWinner.gameObject.SetActive(true);
        roundCounter = 1;
        player1Wins = 0;
        player2Wins = 0;
        player3Wins = 0;

        audioManager.PlaySFX(audioManager.dramaticDrums, audioManager.doubleVol);

        gameEnd = true;
        if (victoryScreenNavigation != null)
            victoryScreenNavigation.SetActive(true);

        playAgainButton.SetActive(true);
        mainMenuButton.SetActive(true);
        saveReplayButton.SetActive(true);

        CheckForRandomCharacters();
    }

    private IEnumerator WaitAndCheckTeam(string winnerName)
    {
        yield return new WaitForSeconds(3f);

        finalWinner.text = "Victory belongs to " + winnerName + "!\n Chan Chan smiles...";
        winner.gameObject.SetActive(false);
        finalWinner.gameObject.SetActive(true);
        roundCounter = 1;
        player1Wins = 0;
        player2Wins = 0;
        player3Wins = 0;
        player4Wins = 0;

        audioManager.PlaySFX(audioManager.dramaticDrums, audioManager.doubleVol);

        gameEnd = true;
        if (victoryScreenNavigation != null)
            victoryScreenNavigation.SetActive(true);

        playAgainButton.SetActive(true);
        mainMenuButton.SetActive(true);
        saveReplayButton.SetActive(true);
    }

    private string GetTelemetryMode()
    {
        if (trainingMode)
            return "training";

        if (IsFourPlayerMode())
            return "2v2";

        return IsThreePlayerMode() ? "1v1v1" : "1v1";
    }
    private void ApplyRulesetToCurrentCharacters(CustomRuleset ruleset)
    {
        if (p1Manager != null)
        {
            Character p1 = p1Manager.GetCurrentCharacter();
            if (p1 != null)
                p1.ApplyCustomRuleset(ruleset);
        }

        if (p2Manager != null)
        {
            Character p2 = p2Manager.GetCurrentCharacter();
            if (p2 != null)
                p2.ApplyCustomRuleset(ruleset);
        }

        if (p3Manager != null)
        {
            Character p3 = p3Manager.GetCurrentCharacter();
            if (p3 != null)
                p3.ApplyCustomRuleset(ruleset);
        }

        if (p4Manager != null)
        {
            Character p4 = p4Manager.GetCurrentCharacter();
            if (p4 != null)
                p4.ApplyCustomRuleset(ruleset);
        }
    }


    public void RoundEnd(int playerNum, string winnerName)
    {

        if (trainingMode)//training
        {
            SoftResetRound(playerNum);
            return;
        }

        winner.gameObject.SetActive(true);
        DisableGamePlay();
        winner.text = winnerName + " prevails!";

        ShortWins(playerNum, winnerName);

        if (!roundTelemetryClosed)
        {
            string p1Char = p1Manager ? p1Manager.GetCharacterName(1) : "";
            string p2Char = p2Manager ? p2Manager.GetCharacterName(1) : "";
            string winnerId = (playerNum == 1) ? "P1" : "P2";
            string winnerCharacter = (playerNum == 1) ? p1Char : p2Char;

            // ✅ Update meta with winner/outcome right before writing JSON
            TelemetryManager.Instance?.SetMatchMeta(new TelemetryMatchMeta
            {
                map = stageName,
                mode = trainingMode ? "training" : "1v1",
                roundNumber_ = roundCounter,
                trainingMode = trainingMode,

                p1Id = "P1",
                p1Character = p1Char,
                p2Id = "P2",
                p2Character = p2Char,

                winnerId = winnerId,
                winnerCharacter = winnerCharacter
            });

            TelemetryManager.Instance?.EndSession($"RoundEnded_KO_winner={winnerName}");
            roundTelemetryClosed = true;
        }

        StartCoroutine(WaitAndCheck(playerNum, winnerName));
        roundOn = false;
    }

    public void RoundEndTie(int playerNum)
    {
        if (trainingMode) //training
        {
            // undo the “short win” penalty/bonus you do for ties and just reset
            SoftResetRound(0);
            return;
        }

        winner.gameObject.SetActive(true);
        DisableGamePlay();
        winner.text = "Tie?\nDEATH PREVAILS...";
        tie = true;

        if (!roundTelemetryClosed)
        {
            // ✅ Update meta for tie right before writing JSON
            TelemetryManager.Instance?.SetMatchMeta(new TelemetryMatchMeta
            {
                map = stageName,
                mode = trainingMode ? "training" : "1v1",
                roundNumber_ = roundCounter,
                trainingMode = trainingMode,

                p1Id = "P1",
                p1Character = p1Manager ? p1Manager.GetCharacterName(1) : "",
                p2Id = "P2",
                p2Character = p2Manager ? p2Manager.GetCharacterName(1) : "",

                winnerId = "",
                winnerCharacter = ""
            });

            TelemetryManager.Instance?.EndSession("RoundEnded_Tie");
            roundTelemetryClosed = true;
        }

        if (playerNum == 1)
        {
            player1Wins--;
        }
        else
        {
            player2Wins--;
        }

        ActivateIndicators();
        CheckForRandomCharacters();
        StartCoroutine(WaitAndrestart());
        roundOn = false;
    }

    public void RoundEndFlawless(int playerNum, string winnerName)
    {

        if (trainingMode)
        {
            SoftResetRound(playerNum);
            return;
        }

        winner.gameObject.SetActive(true);
        DisableGamePlay();
        winner.text = "FLAWLESS\n" + winnerName + " prevails!";

        ShortWins(playerNum, winnerName);

        if (!roundTelemetryClosed)
        {
            string p1Char = p1Manager ? p1Manager.GetCharacterName(1) : "";
            string p2Char = p2Manager ? p2Manager.GetCharacterName(1) : "";
            string winnerId = (playerNum == 1) ? "P1" : "P2";
            string winnerCharacter = (playerNum == 1) ? p1Char : p2Char;

            // ✅ Update meta with winner/outcome right before writing JSON
            TelemetryManager.Instance?.SetMatchMeta(new TelemetryMatchMeta
            {
                map = stageName,
                mode = trainingMode ? "training" : "1v1",
                roundNumber_ = roundCounter,
                trainingMode = trainingMode,

                p1Id = "P1",
                p1Character = p1Char,
                p2Id = "P2",
                p2Character = p2Char,

                winnerId = winnerId,
                winnerCharacter = winnerCharacter
            });

            TelemetryManager.Instance?.EndSession($"RoundEnded_Flawless_winner={winnerName}");
            roundTelemetryClosed = true;
        }

        StartCoroutine(WaitAndCheck(playerNum, winnerName));
        roundOn = false;
    }

    public void ShortWins(int playerNum, string winnerName)
    {
        if (playerNum == 1)
        {
            player1Wins++;
        }
        else if (playerNum == 2)
        {
            player2Wins++;
        }
        else if (playerNum == 3)
        {
            player3Wins++;
        }
        else if (playerNum == 4)
        {
            player4Wins++;
        }
        ActivateIndicators();
    }

    public void CheckForEnd(int playerNum, string winnerName)
    {
        if (tie && roundNumber != 1)
        {
            tie = false;
            return;
        }

        if (player1Wins > roundNumber / 2 || player2Wins > roundNumber / 2 || roundNumber==1)
        {
            if (trainingMode)//training
            {
                // For training: don’t show end-of-match UI, just soft reset
                SoftResetRound(playerNum);
                return;
            }

            finalWinner.text = "Victory belongs to " + winnerName + "!\n Chan Chan smiles...";
            winner.gameObject.SetActive(false);
            finalWinner.gameObject.SetActive(true);
            roundCounter = 1;
            player1Wins = 0;
            player2Wins = 0;
            player3Wins = 0;
            player4Wins = 0;

            audioManager.PlaySFX(audioManager.dramaticDrums, audioManager.doubleVol);

            gameEnd = true;
            if (victoryScreenNavigation != null)
                victoryScreenNavigation.SetActive(true);

            playAgainButton.SetActive(true);
            mainMenuButton.SetActive(true);
            saveReplayButton.SetActive(true);

            CheckForRandomCharacters();
        }
        else
        {
            if (trainingMode) //training
            {
                SoftResetRound(playerNum);
                return;
            }

            roundCounter++;
            CheckForRandomCharacters();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

    }



    private IEnumerator WaitAndCheck(int playerNum, string winnerName)
    {
        // Wait for 3 seconds
        yield return new WaitForSeconds(3f);


        // Call the ShortWins method after the delay
        CheckForEnd(playerNum, winnerName);
    }

    private IEnumerator WaitAndrestart()
    {
        // Wait for 3 seconds
        yield return new WaitForSeconds(3f);

        tie = false;

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

    }

    public int GetRoundCounter()
    {
        return roundCounter;
    }

    public void EnableGamePlay()
    {
        if (p1Manager != null) p1Manager.Resume();
        if (p2Manager != null) p2Manager.Resume();

        if (p3Manager != null && p3Manager.gameObject.activeInHierarchy)
            p3Manager.Resume();
        if (IsFourPlayerMode() && p4Manager != null && p4Manager.gameObject.activeInHierarchy)
            p4Manager.Resume();
    }

    public void DisableGamePlay()
    {
        if (p1Manager != null) p1Manager.Pause();
        if (p2Manager != null) p2Manager.Pause();

        if (p3Manager != null && p3Manager.gameObject.activeInHierarchy)
            p3Manager.Pause();
        if (IsFourPlayerMode() && p4Manager != null && p4Manager.gameObject.activeInHierarchy)
            p4Manager.Pause();
    }

    void ActivateIndicators()
    {
        SetRoundIndicators(p1R1, p1R2, p1R3, player1Wins);
        SetRoundIndicators(p2R1, p2R2, p2R3, player2Wins);
        SetRoundIndicators(p3R1, p3R2, p3R3, player3Wins);
        SetRoundIndicators(p4R1, p4R2, p4R3, player4Wins);
    }

    private void SetRoundIndicators(GameObject round1, GameObject round2, GameObject round3, int wins)
    {
        if (round1 != null) round1.SetActive(wins >= 1);
        if (round2 != null) round2.SetActive(wins >= 2);
        if (round3 != null) round3.SetActive(wins >= 3);
    }

    public void CheckForRandomCharacters()
    {
        if (PlayerPrefs.GetString("Player1Choice") == "Random" && roundCounter > 1)
        {
            c1Name = p1Manager.GetCharacterName(1);
            PlayerPrefs.SetString("Player1Choice", c1Name);
            p1Random = true;
        }

        if (PlayerPrefs.GetString("Player2Choice") == "Random" && roundCounter > 1)
        {
            c2Name = p2Manager.GetCharacterName(1);
            PlayerPrefs.SetString("Player2Choice", c2Name);
            p2Random = true;
        }

        if (p1Random && gameEnd)
        {
            PlayerPrefs.SetString("Player1Choice", "Random");
        }

        if (p2Random && gameEnd)
        {
            PlayerPrefs.SetString("Player2Choice", "Random");
        }
    }


    // Update is called once per frame
    void Update()
    {
        // Restart anytime during normal gameplay (not training)
        if (!trainingMode && Input.GetKeyDown(KeyCode.Return))
        {
            QuickRestart();
        }
    }

    private void QuickRestart()
    {
        // Reset basic state
        tie = false;
        gameEnd = false;
        roundCounter = 1;
        player1Wins = 0;
        player2Wins = 0;
        player3Wins = 0;
        player4Wins = 0;

        if (trainingMode)
        {
            SoftResetRound();
            return;
        }
        // IMPORTANT: reset telemetry properly (optional but cleaner)
        TelemetryManager.Instance?.EndSession("ManualRestart");

        // Reload scene
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
    public void ResetStatics()
    {
        roundCounter = 1;
        player1Wins = 0;
        player2Wins = 0;
        player3Wins = 0;
        player4Wins = 0;
    }

    // Training
    public void SoftResetRound(int winnerPlayerNum = 0)
    {
        if (trainingRoundOn)
        {
            trainingRoundOn = false;
            if (trainingMode && opponentDirector != null)
            {
                opponentDirector.RecordEpisodeResult(winnerPlayerNum);
            }

            StartCoroutine(SoftResetRound_Co());
        }
        
    }

    private IEnumerator SoftResetRound_Co()
    {
        DisableGamePlay();
        // Hide UI
        if (victoryScreenNavigation != null)
            victoryScreenNavigation.SetActive(false);
        winner.gameObject.SetActive(false);
        finalWinner.gameObject.SetActive(false);
        playAgainButton.SetActive(false);
        mainMenuButton.SetActive(false);
        saveReplayButton.SetActive(false);

        tie = false;
        gameEnd = false;

        if (trainingMode)
        {
            // 0) ΤΕΛΕΙΩΣΕ ΤΑ EPISODES ΠΡΩΤΑ
            if (agentP1 != null && agentP1.enabled)
            {
                agentP1.ApplyTerminalReward();
                agentP1.DebugEndEpisode("SOFT_RESET");
                agentP1.EndEpisode();
            }

            if (agentP2 != null && agentP2.enabled)
            {
                agentP2.ApplyTerminalReward();
                agentP2.DebugEndEpisode("SOFT_RESET");
                agentP2.EndEpisode();
            }
                
            // 1) περίμενε 1 frame να "καθαρίσει" animator/coroutines/destroy
            yield return null;

            // 2) Επίλεξε opponent mode για το επόμενο episode
            if (opponentDirector != null)
                opponentDirector.PrepareNextEpisode();

            yield return null;

            // 3) Reroll (και περίμενε να τελειώσει)
            if (p1Manager) yield return StartCoroutine(p1Manager.RerollRandomCharacter_TrainingOnly_Co());
            if (p2Manager) yield return StartCoroutine(p2Manager.RerollRandomCharacter_TrainingOnly_Co());

            yield return null;

            // 4) rebind enemies
            var p1 = p1Manager ? p1Manager.GetCurrentCharacter() : null;
            var p2 = p2Manager ? p2Manager.GetCurrentCharacter() : null;
            if (p1 && p2) p1.ChangeEnemy(p2);
            if (p2 && p1) p2.ChangeEnemy(p1);

            if (opponentDirector != null)
                opponentDirector.RebindAfterCharacterSwap();

            yield return null;
        }

        // 2) Πάρε τους current χαρακτήρες (ΤΩΡΑ είναι οι σωστοί)
        var c1 = p1Manager ? p1Manager.GetCurrentCharacter() : null;
        var c2 = p2Manager ? p2Manager.GetCurrentCharacter() : null;

        //Debug.Log("(*) SoftReset");

        // 3) Reset χαρακτήρων
        if (c1) c1.ResetForEpisode2();
        if (c2) c2.ResetForEpisode2();


        // 4) Re-enable gameplay
        EnableGamePlay();

        // 5) ΤΕΛΟΣ, τώρα κλείσε το episode (ώστε OnEpisodeBegin να δει καθαρό state)
    }
}
