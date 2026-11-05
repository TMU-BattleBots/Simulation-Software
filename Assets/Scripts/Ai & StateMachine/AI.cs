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

    /*  TARGET PREDICTION  --------------------------------------------------
     *  The AI used to aim at targetObj.transform.position - where the target is
     *  RIGHT NOW - which means it permanently trails a moving opponent and can
     *  only land a hit by accident. Leading the shot is what turns the attack
     *  states into something that actually threatens you.
     *
     *  Two things happen here, and the order matters:
     *
     *  1. WHERE THE TARGET WAS. Target position and velocity go through the
     *     same reactionDelay buffer as the vision cones, so perception is
     *     coherent: an easy AI that cannot see you promptly should not be able
     *     to aim at your exact live position either. Previously the cones were
     *     delayed but tracking was not, which was an inconsistency waiting to
     *     be noticed.
     *
     *  2. WHERE IT IS GOING. From that (possibly stale) sample, lead by the
     *     time it would take to close the gap:
     *
     *         leadTime = distance / closingSpeed      (capped at maxLeadTime)
     *         aimPoint = position + velocity * leadTime * predictionStrength
     *
     *     This is a single-step intercept rather than a full iterative solve.
     *     It is accurate enough at arena distances, cannot oscillate, and costs
     *     nothing. The cap matters: without it, a nearly stationary AI computes
     *     an enormous lead and aims off into the far wall.
     * -------------------------------------------------------------------- */
    [Header("Target prediction")]
    [Tooltip("How far to lead the target. 0 aims where it is now, 1 is a full " +
             "predicted intercept. Difficulty scales this - see AIDifficulty.")]
    [SerializeField] float predictionStrength = 1f;

    [Tooltip("Never lead further ahead than this many seconds, however far away " +
             "the target is.")]
    [SerializeField] float maxLeadTime = 0.6f;

    [Tooltip("Assumed closing speed when the AI is slow or stationary, so it does " +
             "not compute an absurd lead time. Effectively its cruise speed.")]
    [SerializeField] float minClosingSpeed = 6f;

    [Tooltip("Seconds of smoothing on the target's measured velocity. Bodies driven " +
             "by an impulse every physics tick report spiky velocities, and a " +
             "knockback hit would otherwise fling the aim point across the arena. " +
             "0 = use the raw reading.")]
    [SerializeField] float velocitySmoothing = 0.12f;

    [Tooltip("Solve the true intercept instead of a simple distance-over-speed lead. " +
             "Turn off to A/B the two against each other.")]
    [SerializeField] bool useInterceptSolve = true;

    [Tooltip("Add the time needed to swing onto the new heading to the intercept " +
             "time. Without this the AI under-leads, because the intercept maths " +
             "assumes it starts closing immediately when it actually has to turn first.")]
    [SerializeField] bool includeTurnTimeInLead = true;

    [Tooltip("Heading error small enough to count as aimed. Used to estimate how " +
             "long a turn takes.")]
    [SerializeField] float aimToleranceDegrees = 5f;

    [Tooltip("Draw the predicted aim point in the Scene view while playing.")]
    [SerializeField] bool drawPredictionGizmo = true;

    struct TargetSample
    {
        public float time;
        public Vector2 position;
        public Vector2 velocity;
    }

    readonly Queue<TargetSample> targetHistory = new Queue<TargetSample>();
    TargetSample perceivedTarget;
    Rigidbody2D targetRb;
    Vector2 lastTargetPosition;
    Vector2 smoothedTargetVelocity;

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
        StateMachine.Initialize(SearchTargetState);   // IMPORTANT

        // Seed the prediction state. Without this the delay buffer starts empty
        // and the AI would briefly aim at the world origin.
        if (targetObj != null)
        {
            targetRb = targetObj.GetComponent<Rigidbody2D>();
            lastTargetPosition = targetObj.transform.position;

            perceivedTarget = new TargetSample
            {
                time = Time.time,
                position = lastTargetPosition,
                velocity = Vector2.zero,
            };

            if (targetRb == null)
            {
                // Not fatal - we fall back to differencing its position.
                Debug.Log("[AI] Target has no Rigidbody2D; estimating its velocity " +
                          "from frame-to-frame movement instead.", this);
            }
        } - sets the opening state

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
        // Sample the target before the state acts, so tracking this tick uses
        // this tick's reading.
        SampleTarget();

        StateMachine.CurrentEnemyState.PhysicsUpdate();   // IMPORTANT
    }

    /// <summary>
    /// Records where the target is and how fast it is moving, then promotes the
    /// sample the AI is allowed to know about given its reaction delay.
    /// </summary>
    void SampleTarget()
    {
        if (targetObj == null) return;

        Vector2 position = targetObj.transform.position;

        Vector2 rawVelocity;
        if (targetRb != null)
        {
            rawVelocity = targetRb.velocity;
        }
        else if (Time.fixedDeltaTime > 0f)
        {
            rawVelocity = (position - lastTargetPosition) / Time.fixedDeltaTime;
        }
        else
        {
            rawVelocity = Vector2.zero;
        }

        lastTargetPosition = position;

        // Exponential smoothing, framed in seconds so it behaves the same at any
        // timestep. Without this a single knockback impulse spikes the measured
        // velocity for one tick and throws the aim point wildly off.
        float alpha = velocitySmoothing <= 0f
            ? 1f
            : 1f - Mathf.Exp(-Time.fixedDeltaTime / velocitySmoothing);

        smoothedTargetVelocity = Vector2.Lerp(smoothedTargetVelocity, rawVelocity, alpha);

        var sample = new TargetSample
        {
            time = Time.time,
            position = position,
            velocity = smoothedTargetVelocity,
        };

        if (reactionDelay <= 0f)
        {
            perceivedTarget = sample;
            if (targetHistory.Count > 0) targetHistory.Clear();
            return;
        }

        targetHistory.Enqueue(sample);

        while (targetHistory.Count > 0 && Time.time - targetHistory.Peek().time >= reactionDelay)
        {
            perceivedTarget = targetHistory.Dequeue();
        }
    }

    /// <summary>
    /// Where the AI should actually aim: the target's last perceived position,
    /// led by its velocity. Public so UI or debug tooling can draw it.
    /// </summary>
    public Vector2 GetAimPoint()
    {
        if (targetObj == null) return rb != null ? rb.position : (Vector2)transform.position;

        Vector2 position = perceivedTarget.position;

        if (predictionStrength <= 0f) return position;

        Vector2 self = rb != null ? rb.position : (Vector2)transform.position;
        Vector2 toTarget = position - self;
        Vector2 targetVelocity = perceivedTarget.velocity;

        // Our own speed is the closing rate, floored so a stationary bot does
        // not produce a huge lead time.
        float closingSpeed = Mathf.Max(rb != null ? rb.velocity.magnitude : 0f, minClosingSpeed);

        float leadTime = useInterceptSolve
            ? SolveInterceptTime(toTarget, targetVelocity, closingSpeed)
            : toTarget.magnitude / closingSpeed;

        // The intercept maths assumes we start closing the instant we decide to.
        // We do not - the bot has to rotate onto the new heading first, and only
        // really drives along transform.up. Ignoring that systematically
        // under-leads, worst of all for a slow-turning bot that needs the help
        // most.
        if (includeTurnTimeInLead) leadTime += EstimateTurnTime(toTarget);

        leadTime = Mathf.Clamp(leadTime, 0f, maxLeadTime);

        return position + targetVelocity * (leadTime * predictionStrength);
    }

    /// <summary>
    /// Roughly how long it takes to swing onto the heading that points at the
    /// target.
    ///
    /// TrackTarget closes the heading gap exponentially - LerpAngle covers a
    /// fixed FRACTION of the remaining error each tick, at rate
    /// trackingSpeedFast - so getting from an error of `e` down to a tolerance
    /// `tol` takes ln(e / tol) / rate seconds. Being logarithmic, a full
    /// about-face costs only a little more than a modest correction, which
    /// matches how the bot actually behaves.
    ///
    /// trackingSpeedFast is already scaled by difficulty, so a slow-turning easy
    /// bot naturally estimates a longer turn and leads further to compensate.
    /// </summary>
    float EstimateTurnTime(Vector2 toTarget)
    {
        if (rb == null || trackingSpeedFast <= 0f) return 0f;
        if (toTarget.sqrMagnitude < 0.0001f) return 0f;

        float desired = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg - 90f;
        float error = Mathf.Abs(Mathf.DeltaAngle(rb.rotation, desired));

        float tolerance = Mathf.Max(aimToleranceDegrees, 0.1f);
        if (error <= tolerance) return 0f;

        return Mathf.Log(error / tolerance) / trackingSpeedFast;
    }

    /// <summary>
    /// Time until we and the target can occupy the same point, assuming both
    /// hold their current velocity.
    ///
    /// The naive lead was distance / ownSpeed, which measures the gap to where
    /// the target is NOW rather than to where it is going - so it under-leads a
    /// target running away and over-leads one closing in. The exact condition is
    ///
    ///     | d + v*t | = s*t
    ///
    /// with d the offset to the target, v its velocity and s our speed. Squaring
    /// both sides gives a quadratic in t:
    ///
    ///     (v.v - s^2) t^2  +  2(d.v) t  +  d.d  =  0
    ///
    /// Because d.d is always positive, whenever we are faster than the target
    /// (v.v &lt; s^2, so the leading coefficient is negative) there is exactly one
    /// positive root - no ambiguity and nothing to oscillate between.
    ///
    /// Returns the simple lead as a fallback when no intercept exists, which
    /// happens when the target is faster than us and running: we then just chase
    /// in roughly the right direction.
    /// </summary>
    static float SolveInterceptTime(Vector2 d, Vector2 v, float s)
    {
        float fallback = d.magnitude / Mathf.Max(s, 0.0001f);

        float a = Vector2.Dot(v, v) - s * s;
        float b = 2f * Vector2.Dot(d, v);
        float c = Vector2.Dot(d, d);

        // Target speed almost exactly matches ours: the quadratic degenerates
        // into a linear equation, and solving it as a quadratic would divide by
        // something near zero.
        if (Mathf.Abs(a) < 0.0001f)
        {
            if (Mathf.Abs(b) < 0.0001f) return fallback;

            float linear = -c / b;

            // A non-positive root here means the target is running at exactly our
            // speed and we can never close - keep chasing rather than aiming at
            // where it stands.
            return linear > 0f ? linear : fallback;
        }

        float discriminant = b * b - 4f * a * c;
        if (discriminant < 0f) return fallback;   // no intercept exists

        float root = Mathf.Sqrt(discriminant);
        float t1 = (-b + root) / (2f * a);
        float t2 = (-b - root) / (2f * a);

        // Earliest interception that is actually in the future.
        float best = Mathf.Infinity;
        if (t1 > 0f) best = Mathf.Min(best, t1);
        if (t2 > 0f) best = Mathf.Min(best, t2);

        return float.IsInfinity(best) ? fallback : best;
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
        predictionStrength *= p.predictionMultiplier;

        if (combatSystem != null) combatSystem.ScaleMaxHealth(p.healthMultiplier);

        if (marker != null)
        {
            Marker markerScript = marker.GetComponent<Marker>();
            if (markerScript != null) markerScript.ScaleLifetime(p.markerLifetimeMultiplier);
        }

        Debug.Log($"[AI] Difficulty set to {difficulty} " +
                  $"(tracking x{p.trackingSpeedMultiplier}, speed x{p.moveSpeedMultiplier}, " +
                  $"reaction delay {p.reactionDelay:0.00}s, prediction {predictionStrength:0.00}).", this);
    }

    /// <summary>
    /// Draws the prediction in the Scene view: yellow is where the AI is aiming,
    /// grey is the offset from where it believes the target actually is. A long
    /// grey line means a big lead; no grey line means prediction is off.
    ///
    /// The original design note asked for a way to visualise this - gizmos are
    /// the cheapest version and cost nothing at runtime.
    /// </summary>
    void OnDrawGizmos()
    {
        if (!drawPredictionGizmo || !Application.isPlaying) return;
        if (targetObj == null) return;

        Vector3 aim = GetAimPoint();
        Vector3 believed = perceivedTarget.position;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(aim, 0.5f);
        Gizmos.DrawLine(transform.position, aim);

        // The gap between where it thinks you are and where it is shooting.
        Gizmos.color = Color.grey;
        Gizmos.DrawLine(believed, aim);
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

        // Aim where the target is GOING, not where it is. GetAimPoint folds in
        // both the reaction delay and the predictive lead.
        Vector2 aim = GetAimPoint();
        Vector2 vectorToTarget = aim - rb.position;
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
