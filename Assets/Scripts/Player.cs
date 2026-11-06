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

    /*  TUNNELLING GUARD  ---------------------------------------------------
     *  A knockback impulse can be several times larger than anything normal
     *  driving produces - force 1000 on a mass-15 body is about 133 units/s,
     *  against a cruising speed nearer 35. At 133 u/s a bot covers 2.7 units in
     *  a single 0.02s physics step, which is wider than the arena walls, so it
     *  can pass clean through between two steps.
     *
     *  Two defences, because either alone is fragile:
     *    1. Continuous collision detection, which sweeps the path between steps
     *       instead of just testing the end points. Set in code so it holds even
     *       if a prefab is saved with Discrete.
     *    2. A hard speed ceiling, so no impulse can produce a step large enough
     *       to outrun the sweep in the first place.
     * -------------------------------------------------------------------- */
    [Header("Safety")]
    [Tooltip("Hard ceiling on this bot's speed, in units per second. Stops a big " +
             "knockback from launching it through a wall. 0 disables the clamp.")]
    [SerializeField] float maxSpeed = 60f;

    void Start()
    {
        if (rb != null)
        {
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

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

    // Quit used to be polled here with the P key. It moved to GameOrganizer:
    // this script exists once per bot, so in PVP two Players were both watching
    // for the quit key. Nothing per-frame is needed on Player any more.

    void FixedUpdate()
    {
        AccelSystem();
        SteerSystem();
        ClampSpeed();
    }

    /// <summary>Keeps the bot below the speed at which it could skip a wall.</summary>
    void ClampSpeed()
    {
        if (rb == null || maxSpeed <= 0f) return;

        if (rb.velocity.sqrMagnitude > maxSpeed * maxSpeed)
        {
            rb.velocity = rb.velocity.normalized * maxSpeed;
        }
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

}
