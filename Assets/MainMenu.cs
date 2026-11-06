using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Mode select. Hooked up to the menu buttons' OnClick events.
/// </summary>
public class MainMenu : MonoBehaviour
{
    private GameObject organizerObj;
    private GameOrganizer organizer;

    private void Start()
    {
        organizerObj = GameObject.FindWithTag(GameTags.Organizer);
        if (organizerObj != null) organizer = organizerObj.GetComponent<GameOrganizer>();

        if (organizer == null)
        {
            Debug.LogError($"[MainMenu] No GameObject tagged '{GameTags.Organizer}' in this scene.");
        }
    }

    public void Play2PLayer()
    {
        StartMatch(pvp: true, scene: GameScenes.PVP);
    }

    /// <summary>Vs AI at the current difficulty. Defaults to Medium.</summary>
    public void PlayAI()
    {
        StartMatch(pvp: false, scene: GameScenes.PVE);
    }

    /*  DIFFICULTY BUTTONS  -------------------------------------------------
     *  Hook these to three menu buttons' OnClick events instead of PlayAI.
     *  Unity's OnClick inspector cannot pass an enum, which is why there is one
     *  method per level rather than a single PlayAI(AIDifficulty).
     * -------------------------------------------------------------------- */

    public void PlayAIEasy() => StartMatch(false, GameScenes.PVE, AIDifficulty.Easy);
    public void PlayAIMedium() => StartMatch(false, GameScenes.PVE, AIDifficulty.Medium);
    public void PlayAIHard() => StartMatch(false, GameScenes.PVE, AIDifficulty.Hard);

    /// <summary>
    /// Sets up match state, THEN loads.
    ///
    /// These used to navigate with buildIndex + 1 and buildIndex + 2, which made
    /// the order of File > Build Settings part of the game logic - reordering the
    /// list silently sent a button to the wrong scene. Loading by name fixes
    /// that; the scenes still need to be in Build Settings, but the order no
    /// longer matters.
    ///
    /// The state calls also used to come AFTER LoadScene. That happened to work
    /// because LoadScene defers to the end of the frame, but the ordering here
    /// is no longer a coincidence.
    /// </summary>
    void StartMatch(bool pvp, string scene, AIDifficulty? difficulty = null)
    {
        /*  Ask which stick layout the player wants before loading.
         *
         *  The prompt shows itself only when a gamepad is connected - the
         *  choice is between two two-stick layouts, so there is nothing to ask
         *  a keyboard player. With no pad it calls straight back and the match
         *  starts as it always did, so this adds no step for them.             */
        ControlSchemePrompt.Ask(_ => BeginMatch(pvp, scene, difficulty));
    }

    /// <summary>Sets up match state, THEN loads. See StartMatch for why.</summary>
    void BeginMatch(bool pvp, string scene, AIDifficulty? difficulty)
    {
        if (organizer != null)
        {
            organizer.SetLives();
            organizer.SetPVPMode(pvp);

            if (difficulty.HasValue) organizer.SetDifficulty(difficulty.Value);
        }

        SceneManager.LoadScene(scene);
    }
}
