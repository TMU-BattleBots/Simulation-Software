using UnityEngine;

/// <summary>
/// A human-controlled bot. Turns input vectors into physics, and reacts to its
/// own death and to the end of a round.
/// </summary>
public class Player : MonoBehaviour
{
    //Physics Stats
    [SerializeField] Rigidbody2D rb;
    [SerializeField] float movementSpeed;
    [SerializeField] float boostSpeed;
    [SerializeField] float currentSpeed;
    [SerializeField] float turnSpeed;

    //Controls
    [SerializeField] Vector2 inputVectors;

    //Enemy Bot
    [SerializeField] GameObject enemy;

    //vars
    float accelInputs = 0;
    float steerInputs = 0;
    float rotationAngle = 0;

    // Unique id for each bot. See GameOrganizer.PlayerLoses.
    //   0 = AI, 1 = Player 1, 2 = Player 2
    // (An old comment here claimed 0 = p1, 1 = p2, 2 = AI. That was wrong and
    //  contradicted AI.cs, GameOrganizer and the values set in the scenes.)
    [SerializeField] int index = 0;
    private GameObject organizerObj;
    private GameOrganizer organizer;

    //for Health and Damage system
    [SerializeField] CombatSystem combatSystem;
    [SerializeField] GameObject explosion;

    void Start()
    {
        currentSpeed = movementSpeed;

        organizerObj = GameObject.FindWithTag(GameTags.Organizer);
        if (organizerObj != null) organizer = organizerObj.GetComponent<GameOrganizer>();

        if (organizer != null)
        {
            // GameOrganizer cannot reliably re-bind its own UI after a scene
            // reload, so the bots prompt it once they exist.
            organizer.UpdateScores();
            organizer.RoundEnded += HandleRoundEnded;
        }
        else
        {
            Debug.LogError($"[Player {index}] No GameObject tagged '{GameTags.Organizer}' in this scene.");
        }

        if (combatSystem != null) combatSystem.OnDeath += HandleDeath;
    }

    void OnDestroy()
    {
        // Always unsubscribe. GameOrganizer outlives the scene, so a Player that
        // stays subscribed would be invoked after it has been destroyed.
        if (combatSystem != null) combatSystem.OnDeath -= HandleDeath;
        if (organizer != null) organizer.RoundEnded -= HandleRoundEnded;
    }

    void Update()
    {
        QuitGame();
    }

    void FixedUpdate()
    {
        AccelSystem();
        SteerSystem();
    }

    /// <summary>
    /// Raised by our own CombatSystem the frame our health hits zero. This used
    /// to be a DeathCheck() poll in Update().
    /// </summary>
    void HandleDeath(CombatSystem _)
    {
        if (explosion != null) Instantiate(explosion, this.transform);

        Freeze();

        if (organizer != null) organizer.PlayerLoses(index);
    }

    /// <summary>
    /// Raised by GameOrganizer once any bot has died, so the survivors stop
    /// moving and stop scoring during the pause before the scene reloads. This
    /// replaces the old StopSignal() poll.
    /// </summary>
    void HandleRoundEnded()
    {
        Freeze();
    }

    /// <summary>
    /// Stops this bot dead: no movement, no damage. Disabling the hitbox matters
    /// - it lives on a child object, so switching this script off does not stop
    /// it on its own.
    /// </summary>
    void Freeze()
    {
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        foreach (BotHitBox hitBox in GetComponentsInChildren<BotHitBox>())
        {
            hitBox.SetHitboxActive(false);
        }

        enabled = false;
    }

    public int GetPlayerIndex()
    {
        return index;
    }

    /// <summary>Called by PlayerInputHandler when the Move action fires.</summary>
    public void SetInputs(Vector2 inputVector)
    {
        inputVectors = inputVector;
        steerInputs = inputVectors.x;
        accelInputs = inputVectors.y;
    }

    void AccelSystem()
    {
        // transform.up is forward. accel/steer inputs are 0/1, gating the force.
        Vector2 engineForceVector = transform.up * accelInputs * currentSpeed;
        rb.AddForce(engineForceVector, ForceMode2D.Impulse);
    }

    void SteerSystem()
    {
        rotationAngle -= steerInputs * turnSpeed;
        rb.MoveRotation(rotationAngle);
    }

    /// <summary>Called by PlayerInputHandler when the Boost action fires.</summary>
    public void Boost2(float boost)
    {
        currentSpeed = (boost == 1) ? boostSpeed : movementSpeed;
    }

    /// <summary>Demo convenience key.</summary>
    void QuitGame()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            Debug.Log("Quitting game... ");
            Application.Quit();
        }
    }
}
