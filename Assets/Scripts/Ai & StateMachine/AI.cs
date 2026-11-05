using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The core of the enemy AI. Owns the state machine and every behaviour the
/// states share.
///
/// Movement and tracking helpers live here rather than inside the State classes
/// because States are plain C# objects, not MonoBehaviours - only fields on this
/// component can be exposed to the Inspector for tuning.
/// </summary>
public class AI : MonoBehaviour
{
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private int stateName;   // shows the current state's index in the Inspector

    //Starting position
    [SerializeField] private GameObject startPosition;

    //Target
    [SerializeField] private GameObject targetObj;

    //LockOnCircle
    [SerializeField] private GameObject marker;
    [SerializeField] private GameObject notLockOn;

    //CenterTarget
    [SerializeField] private GameObject centerObj;

    [Header("Movement")]
    [SerializeField] private float rotationModifier;      // 10, keep as is
    [SerializeField] private float AImoveSpeed;           // 65, Follow and Offensive states
    [SerializeField] private float AImoveSpeedSlow;       // 55, Search
    [SerializeField] private float AIinvestigateSpeed;    // 55, approaching a marker
    [SerializeField] private float AImoveSpeedCenter;     // 60, returning to centre
    [SerializeField] private float trackingSpeedFast;     // 12, turn speed when locked on
    [SerializeField] private float L1turnSpeed;           // 8, turn speed while orienting

    /*  MANOEUVRE TIMERS  ---------------------------------------------------
     *  MoveToCenter() and InvestigateMarker() are meant to turn towards their
     *  destination for a moment and then just drive, so the bot commits to a
     *  heading instead of endlessly correcting.
     *
     *  Both used to open with "timer = 0f;" and then test "if (timer < 3)".
     *  Resetting on every call meant the condition was always true and the
     *  accumulated time was thrown away every frame - the turn phase never
     *  ended. The timers are fields now, reset at the moment the manoeuvre
     *  actually starts, and the durations are tunable.
     * -------------------------------------------------------------------- */
    [Header("Manoeuvre timing")]
    [Tooltip("Seconds spent turning towards the target when heading for the arena centre.")]
    [SerializeField] private float centerTurnDuration = 3f;
    [Tooltip("Seconds spent turning towards a freshly placed marker.")]
    [SerializeField] private float markerTurnDuration = 1f;
    [Tooltip("Stop driving once this close to the arena centre.")]
    [SerializeField] private float centerArrivalRadius = 2f;
    [Tooltip("A marker counts as reached at this distance.")]
    [SerializeField] private float markerArrivalRadius = 5f;

    private float centerTurnTimer;
    private float markerTurnTimer;

    //for Health and Damage system
    [SerializeField] CombatSystem combatSystem;
    [SerializeField] GameObject explosion;

    // Unique id for each bot: 0 = AI, 1 = Player 1, 2 = Player 2
    [SerializeField] int index = 0;
    private GameObject organizerObj;
    private GameOrganizer organizer;

    Vector2 engineForceVector;
    VisionCone visionCone;

    /*  PERCEPTION  ---------------------------------------------------------
     *  The cone array is sampled ONCE per frame into perceivedCone, and every
     *  IsL1/IsL2/IsL3/IsL4 call reads that cached value. They used to each call
     *  GetConeInt() independently, which re-read the array up to eight times a
     *  frame for no benefit.
     *
     *  reactionDelay feeds a small history buffer so the AI acts on what it saw
     *  a moment ago rather than the current truth. That is what makes an easier
     *  difficulty feel fair - the bot is not clumsy, its sensors are just slower,
     *  which is exactly how a real camera-plus-inference pipeline behaves.
     * -------------------------------------------------------------------- */
    struct ConeSample
    {
        public float time;
        public int cone;
    }

    readonly Queue<ConeSample> coneHistory = new Queue<ConeSample>();
    int perceivedCone = NoDetection;
    float reactionDelay;

    // Toggled by the C key, read by every state. The key is sampled once per
    // frame in Update() - it used to be sampled inside IsIdle(), which meant a
    // query method had a side effect and two callers in one frame would
    // double-toggle it.
    private bool idleBool = false;

    private Transform pingLocation;

    /* Auto-implemented properties for our state machine. */
    public StateMachine StateMachine { get; private set; }
    public SearchTargetState SearchTargetState { get; private set; }
    public FollowTargetState FollowTargetState { get; private set; }
    public OffensiveState OffensiveState { get; private set; }
    public RamAttkState RamAttkState { get; private set; }
    public IdleState IdleState { get; private set; }

    /// <summary>GetConeInt() returns this when no cone is triggered.</summary>
    public const int NoDetection = -1;

    void Awake()
    {
        StateMachine = new StateMachine();

        // States are not MonoBehaviours, so they are constructed by hand.
        IdleState = new IdleState(this, StateMachine);
        SearchTargetState = new SearchTargetState(this, StateMachine);
        FollowTargetState = new FollowTargetState(this, StateMachine);
        OffensiveState = new OffensiveState(this, StateMachine);
        RamAttkState = new RamAttkState(this, StateMachine);
    }

    private void OnEnable()
    {
        if (startPosition != null) this.transform.position = startPosition.transform.position;
        if (marker != null) marker.SetActive(false);
    }

    private void Start()
    {
        visionCone = this.GetComponent<VisionCone>();
        StateMachine.Initialize(SearchTargetState);   // IMPORTANT - sets the opening state

        organizerObj = GameObject.FindWithTag(GameTags.Organizer);
        if (organizerObj != null) organizer = organizerObj.GetComponent<GameOrganizer>();

        if (organizer != null)
        {
            organizer.UpdateScores();
            organizer.RoundEnded += HandleRoundEnded;
        }
        else
        {
            Debug.LogError($"[AI] No GameObject tagged '{GameTags.Organizer}' in this scene.");
        }

        if (combatSystem != null) combatSystem.OnDeath += HandleDeath;

        // The menu stores the chosen level on the persistent GameOrganizer, so
        // it survives the scene load into the arena.
        if (organizer != null) ApplyDifficulty(organizer.Difficulty);
    }

    private void OnDestroy()
    {
        if (combatSystem != null) combatSystem.OnDeath -= HandleDeath;
        if (organizer != null) organizer.RoundEnded -= HandleRoundEnded;
    }

    private void Update()
    {
        // Read the world once, before anything acts on it.
        SampleCone();

        // Sample the idle toggle before the state reads it, so a state sees the
        // change on the same frame the key was pressed.
        if (Input.GetKeyDown(KeyCode.C)) idleBool = !idleBool;

        StateMachine.CurrentEnemyState.Update();   // IMPORTANT
    }

    void FixedUpdate()
    {
        StateMachine.CurrentEnemyState.PhysicsUpdate();   // IMPORTANT
    }

    /// <summary>Raised by our own CombatSystem when health hits zero.</summary>
    void HandleDeath(CombatSystem _)
    {
        if (explosion != null) Instantiate(explosion, this.transform);

        Freeze();

        if (organizer != null) organizer.PlayerLoses(index);
    }

    /// <summary>Raised by GameOrganizer once any bot has died.</summary>
    void HandleRoundEnded()
    {
        Freeze();
    }

    /// <summary>
    /// Stops this bot dead. The hitbox has to be switched off explicitly - it is
    /// a component on a child object, so disabling this script does not stop it.
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

    /*  CONE READING  -------------------------------------------------------
     *  GetCone() returns {firstTrueIndex, lastTrueIndex}. We use the LAST one,
     *  so the innermost triggered cone wins and L3 takes priority over L1 when
     *  the target is inside both.
     *
     *  Array.IndexOf returns -1 when nothing is detected, so this legitimately
     *  returns NoDetection. Any new code that switches on the cone index must
     *  handle it.
     * -------------------------------------------------------------------- */
    /// <summary>
    /// What the AI currently believes it can see. Cached once per frame by
    /// SampleCone(), and delayed by reactionDelay on the easier difficulties.
    /// </summary>
    public int GetConeInt()
    {
        return perceivedCone;
    }

    /// <summary>The raw, undelayed cone reading. Used by the UI readout.</summary>
    public int GetRawConeInt()
    {
        if (visionCone == null) return NoDetection;
        return visionCone.GetCone()[1];
    }

    /// <summary>Reads the cones once per frame, applying any perception lag.</summary>
    void SampleCone()
    {
        int raw = GetRawConeInt();

        if (reactionDelay <= 0f)
        {
            perceivedCone = raw;
            if (coneHistory.Count > 0) coneHistory.Clear();
            return;
        }

        coneHistory.Enqueue(new ConeSample { time = Time.time, cone = raw });

        // Promote every sample that is now older than the delay.
        while (coneHistory.Count > 0 && Time.time - coneHistory.Peek().time >= reactionDelay)
        {
            perceivedCone = coneHistory.Dequeue().cone;
        }
    }

    /// <summary>
    /// Applies a difficulty preset. Called from Start using the level stored on
    /// GameOrganizer, so the menu's choice survives the scene load.
    ///
    /// Everything here is a multiplier on the Inspector values, so retuning the
    /// AI moves all three difficulties with it.
    /// </summary>
    public void ApplyDifficulty(AIDifficulty difficulty)
    {
        AIDifficultyProfile p = AIDifficultyProfile.For(difficulty);

        trackingSpeedFast *= p.trackingSpeedMultiplier;
        L1turnSpeed *= p.trackingSpeedMultiplier;

        AImoveSpeed *= p.moveSpeedMultiplier;
        AImoveSpeedSlow *= p.moveSpeedMultiplier;
        AIinvestigateSpeed *= p.moveSpeedMultiplier;
        AImoveSpeedCenter *= p.moveSpeedMultiplier;

        reactionDelay = p.reactionDelay;

        if (combatSystem != null) combatSystem.ScaleMaxHealth(p.healthMultiplier);

        if (marker != null)
        {
            Marker markerScript = marker.GetComponent<Marker>();
            if (markerScript != null) markerScript.ScaleLifetime(p.markerLifetimeMultiplier);
        }

        Debug.Log($"[AI] Difficulty set to {difficulty} " +
                  $"(tracking x{p.trackingSpeedMultiplier}, speed x{p.moveSpeedMultiplier}, " +
                  $"reaction delay {p.reactionDelay:0.00}s).", this);
    }

    public GameObject GetTargetObj()
    {
        return targetObj;
    }

    public int GetStateName()
    {
        return stateName;
    }

    public void SetCurrentStateName(int name)
    {
        stateName = name;
    }

    public bool IsL1()   // any of the outer sensors
    {
        int i = GetConeInt();
        return i >= 0 && i <= 3;
    }

    public bool IsL2()
    {
        int i = GetConeInt();
        return i >= 4 && i <= 7;
    }

    public bool IsL3()
    {
        int i = GetConeInt();
        return i >= 8 && i <= 9;
    }

    public bool IsL4()   // contact
    {
        return GetConeInt() == 9;
    }

    /// <summary>Whether the bot has been parked in idle by the C key.</summary>
    public bool IsIdle()
    {
        return idleBool;
    }

    /// <summary>Rotate towards the target. Call from PhysicsUpdate.</summary>
    public void TrackTarget(float trackingSpeed)
    {
        if (targetObj == null || rb == null) return;

        Vector3 vectorToTarget = targetObj.transform.position - transform.position;
        float targetAngle = Mathf.Atan2(vectorToTarget.y, vectorToTarget.x) * Mathf.Rad2Deg - 90f;

        // Was: transform.rotation = Quaternion.Slerp(...) called from Update().
        // Writing transform.rotation directly fights the Rigidbody2D, and running
        // it on the render frame made the turn rate frame-rate dependent, which
        // is where the rotation jitter came from. LerpAngle + MoveRotation is the
        // same easing, applied through the physics system like Player does.
        float nextAngle = Mathf.LerpAngle(rb.rotation, targetAngle, Time.fixedDeltaTime * trackingSpeed);
        rb.MoveRotation(nextAngle);
    }

    /// <summary>Track at the fast lock-on speed. Used by the states.</summary>
    public void TrackingTarget()
    {
        TrackTarget(trackingSpeedFast);
    }

    public void MoveForward()
    {
        if (rb == null) return;

        engineForceVector = transform.up * AImoveSpeed;
        rb.AddForce(engineForceVector, ForceMode2D.Impulse);
    }

    public void MoveSlowly()
    {
        if (rb == null) return;

        engineForceVector = transform.up * AImoveSpeedSlow;
        rb.AddForce(engineForceVector, ForceMode2D.Impulse);
    }

    /// <summary>
    /// Called by SearchTargetState when it starts heading for the centre, so the
    /// turn phase begins from zero.
    /// </summary>
    public void BeginCenterApproach()
    {
        centerTurnTimer = 0f;
    }

    /// <summary>
    /// At the centre the bot has the best view with no dead zones, and it is
    /// easier to sit and rotate than to chase around the edges.
    /// </summary>
    public void MoveToCenter()
    {
        if (centerObj == null || rb == null) return;

        if (centerTurnTimer < centerTurnDuration)
        {
            centerTurnTimer += Time.fixedDeltaTime;
            TrackTarget(L1turnSpeed);
        }

        float distance = Vector2.Distance(centerObj.transform.position, this.transform.position);
        if (distance >= centerArrivalRadius)
        {
            rb.AddForce(transform.up * AImoveSpeedCenter, ForceMode2D.Impulse);
        }
    }

    /// <summary>
    /// Drop a marker in the quadrant that saw the target, so the bot has
    /// somewhere to investigate. Only one marker exists at a time - a new one can
    /// only be placed once the last has been reached or expired.
    /// </summary>
    public void PlaceMarker()
    {
        if (marker == null || marker.activeSelf) return;

        int i = GetConeInt();
        if (i < 0 || i > 3) return;   // only the four L1 quadrants place markers

        // Children 0..3 are the front/left/back/right ping positions.
        pingLocation = this.gameObject.transform.GetChild(i);
        marker.transform.position = pingLocation.transform.position;
        marker.SetActive(true);

        // Fresh marker, so start the turn phase again.
        markerTurnTimer = 0f;
    }

    public void SetMarker(bool b)
    {
        if (marker != null) marker.SetActive(b);
    }

    public bool GetMarker()
    {
        return marker != null && marker.activeSelf;
    }

    /// <summary>Turn towards the marker briefly, then drive at it until reached.</summary>
    public void InvestigateMarker()
    {
        if (marker == null || rb == null) return;

        if (markerTurnTimer < markerTurnDuration)
        {
            markerTurnTimer += Time.fixedDeltaTime;

            Vector3 vectorToMarker = marker.transform.position - transform.position;
            float targetAngle = Mathf.Atan2(vectorToMarker.y, vectorToMarker.x) * Mathf.Rad2Deg - 90f;
            float nextAngle = Mathf.LerpAngle(rb.rotation, targetAngle, Time.fixedDeltaTime * L1turnSpeed);
            rb.MoveRotation(nextAngle);
        }

        float distance = Vector2.Distance(marker.transform.position, this.transform.position);
        if (distance >= markerArrivalRadius)
        {
            rb.AddForce(transform.up * AIinvestigateSpeed, ForceMode2D.Impulse);
        }
        else
        {
            marker.SetActive(false);   // reached it
        }
    }
}
