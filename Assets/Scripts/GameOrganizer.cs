using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
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

        // Esc quits. This used to be P, and it lived on Player.cs - which meant
        // every bot in the scene polled for it independently, so in PVP two
        // Players both raced to quit. It belongs here, on the one persistent
        // object. P still works as the old alias.
        // While the how-to-play overlay is up, Esc dismisses it. Without this
        // check a single press would close the panel AND quit in the same frame.
        // The control-scheme prompt is modal for the same reason: Esc there
        // should back out of the prompt, not quit the game out from under it.
        if (!HowToPlayPanel.IsOpen && !ControlSchemePrompt.IsOpen &&
            (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P)))
        {
            QuitGame();
        }

        ReadGamepadShortcuts();
    }

    /// <summary>
    /// Gamepad equivalents of the restart and menu keys.
    ///
    /// Without these a pad player has no way out of a match - every exit was a
    /// keyboard key, which is awkward on a couch and impossible on a build where
    /// only a controller is to hand. Start leaves to the menu, Select restarts.
    ///
    /// There is deliberately no gamepad "quit": a pad has no natural key for it,
    /// and in a browser build quitting is not possible at all.
    /// </summary>
    void ReadGamepadShortcuts()
    {
        Gamepad pad = Gamepad.current;
        if (pad == null) return;

        // A modal owns these buttons while it is up - east should dismiss the
        // panel, not leave the match out from under it.
        if (HowToPlayPanel.IsOpen || ControlSchemePrompt.IsOpen) return;

        /*  Two ways back, because Start alone was not discoverable.
         *  East (B on Xbox, Circle on PlayStation, A on a Switch Pro pad) is
         *  the near-universal "back out of this" button, so it is worth having
         *  even though Start already did the job. Start stays as it was.      */
        bool back = pad.startButton.wasPressedThisFrame
                    || pad.buttonEast.wasPressedThisFrame;

        if (back)
        {
            // Pressing back at the menu would reload the menu - a visible
            // flicker that does nothing. There is nowhere further back to go.
            if (SceneManager.GetActiveScene().name == GameScenes.MainMenu) return;

            SetLives();
            BackToMenuScene();
        }
        else if (pad.selectButton.wasPressedThisFrame)
        {
            SetLives();
            RestartScene();
        }
    }

    /// <summary>
    /// Leaves the game.
    ///
    /// Application.Quit() does nothing in the Editor and nothing in a WebGL
    /// build, so the Editor branch stops play mode instead - otherwise Esc looks
    /// broken while you are testing. The UNITY_EDITOR guard is what keeps the
    /// UnityEditor reference out of player builds.
    /// </summary>
    public void QuitGame()
    {
#if UNITY_EDITOR
        // Application.Quit() does nothing in the Editor, so stop play mode -
        // otherwise Esc looks broken in the place you test it most.
        // This branch is checked FIRST on purpose: with the build target set to
        // WebGL, UNITY_WEBGL is also defined while in the Editor.
        Debug.Log("[GameOrganizer] Quit pressed - stopping play mode.");
        UnityEditor.EditorApplication.isPlaying = false;

#elif UNITY_WEBGL
        // A web page cannot close its own tab, so Application.Quit() is silently
        // a no-op in a browser build - on itch.io the key would appear dead.
        // Returning to the menu is the useful thing Esc can actually do there.
        if (SceneManager.GetActiveScene().name == GameScenes.MainMenu)
        {
            Debug.Log("[GameOrganizer] Web build: already at the menu.");
            return;
        }

        Debug.Log("[GameOrganizer] Web build: returning to the menu instead of quitting.");
        SetLives();
        BackToMenuScene();

#else
        Debug.Log("[GameOrganizer] Quitting game...");
        Application.Quit();
#endif
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
