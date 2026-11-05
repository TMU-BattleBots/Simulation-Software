using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Match state: lives, the scoreboard, win banners and scene flow.
///
/// A persistent singleton (DontDestroyOnLoad) so scores survive a round
/// restart. Each scene carries its own GameOrganizer prefab instance; the first
/// one to load wins and the rest destroy themselves.
///
/// NOTE: because of that, pressing Play directly in an arena scene means
/// SetPVPMode() is never called and the PVP flag keeps whatever is ticked on the
/// prefab. Always launch from the main menu when testing scoring.
/// </summary>
public class GameOrganizer : MonoBehaviour
{
    GameObject scoreBoard1;
    GameObject scoreBoard2;
    GameObject scoreBoardAI;

    TextMeshProUGUI p1lives;
    TextMeshProUGUI p2lives;
    TextMeshProUGUI aIlives;

    [Header("Lives")]
    [SerializeField] int startingLives = 3;
    [SerializeField] int player1lives = 3;
    [SerializeField] int player2lives = 3;
    [SerializeField] int playerAIlives = 3;

    [Header("Win banners")]
    [SerializeField] GameObject P1WINS;
    [SerializeField] GameObject P2WINS;
    [SerializeField] GameObject AIWINS;

    [Header("Timing")]
    [Tooltip("Pause after a match is won, before returning to the menu.")]
    [SerializeField] float matchEndDelay = 2.9f;
    [Tooltip("Pause after a round is lost, before reloading for the next round.")]
    [SerializeField] float roundRestartDelay = 3f;

    [Header("Mode")]
    [Tooltip("True for PVP, false for PVE. Set by MainMenu - see class summary.")]
    [SerializeField] bool PVP;

    [Tooltip("AI difficulty for PVE. Set by MainMenu, and read by AI.Start(). " +
             "Lives here because this object survives the scene load and the AI " +
             "does not exist yet when the menu button is pressed.")]
    [SerializeField] AIDifficulty difficulty = AIDifficulty.Medium;

    /// <summary>The difficulty the AI should apply when it spawns.</summary>
    public AIDifficulty Difficulty => difficulty;

    int winner;

    /// <summary>
    /// Guards against two bots dying in the same frame, which would otherwise
    /// decrement two life counters and run the win check twice.
    /// </summary>
    bool roundOver;

    /// <summary>
    /// Raised once when a round ends, so surviving bots freeze during the pause
    /// before the reload. Replaces the old StopSignal() polling.
    /// </summary>
    public event Action RoundEnded;

    public static GameOrganizer instance;

    private void Awake()
    {
        if (instance != null)   // a persistent one already came in from the last scene
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        UpdateScores();
    }

    private void Update()
    {
        // Debug hotkeys. GetKeyDown, not GetKey - GetKey fires every frame the
        // key is held, which queued a scene load per frame.
        if (Input.GetKeyDown(KeyCode.L))
        {
            SetLives();
            RestartScene();
        }

        if (Input.GetKeyDown(KeyCode.O))
        {
            SetLives();
            BackToMenuScene();
        }
    }

    private void OnDestroy()
    {
        // Otherwise the static reference becomes a "fake null" Unity object and
        // the next organizer to load destroys itself against a dead instance.
        if (instance == this) instance = null;
    }

    public void SetLives()
    {
        player1lives = startingLives;
        player2lives = startingLives;
        playerAIlives = startingLives;
        roundOver = false;

        // Drop any restart/menu load still pending from the last round, so
        // hitting the L or O hotkey mid-countdown does not queue a second load.
        CancelInvoke();
    }

    public void SetPVPMode(bool mode)
    {
        PVP = mode;
    }

    public void SetDifficulty(AIDifficulty level)
    {
        difficulty = level;
    }

    /// <summary>
    /// Called by a bot when it dies, to decrement its life count and drive the
    /// match forward.
    /// </summary>
    public void PlayerLoses(int index)
    {
        // Both bots can die on the same frame. Only the first counts.
        if (roundOver) return;
        roundOver = true;

        switch (index)
        {
            case 0: //ai
                playerAIlives -= 1;
                if (aIlives != null) aIlives.text = playerAIlives.ToString();
                break;

            case 1: //player 1
                player1lives -= 1;
                if (p1lives != null) p1lives.text = player1lives.ToString();
                break;

            case 2: //player 2
                player2lives -= 1;
                if (p2lives != null) p2lives.text = player2lives.ToString();
                break;

            default:
                Debug.LogWarning($"[GameOrganizer] PlayerLoses called with unknown index {index}.");
                break;
        }

        // Freeze everyone still standing so they cannot keep scoring during the pause.
        RoundEnded?.Invoke();

        CheckGame();
    }

    /// <summary>
    /// Decides whether the match is over, or just the round.
    ///
    /// This used to schedule BackToMenuScene at 2.9s on a win and then fall
    /// through and ALSO schedule RestartScene at 3.0s. It only worked because
    /// the first Invoke fired 0.1s earlier and the scene load cancelled the
    /// second - the two were racing.
    /// </summary>
    private void CheckGame()
    {
        bool matchOver = playerAIlives <= 0 || player1lives <= 0 || player2lives <= 0;

        if (matchOver)
        {
            winner = PVP ? PVPWinner() : PVEWinner();

            DisplayWinningBanner(winner);
            Invoke(nameof(BackToMenuScene), matchEndDelay);
        }
        else
        {
            Invoke(nameof(RestartScene), roundRestartDelay);
        }
    }

    /// <summary>
    /// Re-finds the scoreboard text objects by tag.
    ///
    /// Inspector references do not survive a scene load on a persistent object,
    /// so this has to be done in code. Bots call it from their own Start()
    /// because OnEnable does not fire again for an object that was never
    /// disabled.
    /// </summary>
    public void UpdateScores()
    {
        scoreBoardAI = GameObject.FindWithTag(GameTags.ScoringAI);
        scoreBoard1 = GameObject.FindWithTag(GameTags.Scoring1);
        scoreBoard2 = GameObject.FindWithTag(GameTags.Scoring2);

        // Expected to come up empty on the main menu, which has no scoreboards.
        if (scoreBoardAI == null || scoreBoard1 == null || scoreBoard2 == null) return;

        aIlives = scoreBoardAI.GetComponent<TextMeshProUGUI>();
        p1lives = scoreBoard1.GetComponent<TextMeshProUGUI>();
        p2lives = scoreBoard2.GetComponent<TextMeshProUGUI>();

        if (aIlives != null) aIlives.text = playerAIlives.ToString();
        if (p1lives != null) p1lives.text = player1lives.ToString();
        if (p2lives != null) p2lives.text = player2lives.ToString();
    }

    private void RestartScene()
    {
        roundOver = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void BackToMenuScene()
    {
        roundOver = false;
        SceneManager.LoadScene(GameScenes.MainMenu);
    }

    /// <summary>Index of the winning bot in a PVP match.</summary>
    public int PVPWinner()
    {
        return player1lives > player2lives ? 1 : 2;
    }

    /// <summary>Index of the winning bot in a PVE match.</summary>
    public int PVEWinner()
    {
        return player1lives > playerAIlives ? 1 : 0;
    }

    private void DisplayWinningBanner(int index)
    {
        if (Camera.main == null) return;

        // Middle of the screen, in world space.
        Vector3 middlePosition = Camera.main.ScreenToWorldPoint(
            new Vector3(Screen.width / 2f, Screen.height / 2f, Camera.main.nearClipPlane));

        GameObject banner = index switch
        {
            0 => AIWINS,
            1 => P1WINS,
            2 => P2WINS,
            _ => null,
        };

        if (banner == null)
        {
            Debug.LogWarning($"[GameOrganizer] No win banner assigned for index {index}.");
            return;
        }

        Instantiate(banner, middlePosition, Quaternion.identity);
    }
}
