/// <summary>
/// L1 behaviour. The bot has a rough idea where the target is, or none at all.
///
/// Two sub-behaviours, chosen each frame:
///   - a cone saw something: drop a marker there and go investigate
///   - nothing at all: return to the centre of the arena, where the vision cones
///     have no dead zones
///
/// These arguably want to be two separate states, but the bool split works.
///
/// The helper methods live on AI rather than here so their tuning values can be
/// [SerializeField] - State classes are plain C# objects and cannot expose
/// anything to the Inspector.
/// </summary>
public class SearchTargetState : State
{
    private bool goCenter;
    private bool investigateMarker;

    public SearchTargetState(AI ai, StateMachine stateMachine) : base(ai, stateMachine)
    {
    }

    public override void EnterState()
    {
        base.EnterState();
        ai.SetCurrentStateName(0);

        // Fresh entry into the state means any previous approach is over.
        goCenter = false;
        investigateMarker = false;
    }

    public override void ExitState()
    {
        base.ExitState();
    }

    public override void Update()
    {
        base.Update();

        if (ai.IsL1() == true)
        {
            goCenter = false;
            ai.PlaceMarker();
            investigateMarker = true;
        }
        else if (ai.GetMarker() == false)   // not already investigating
        {
            // Lost the target entirely: head for the centre for full coverage.
            // Only start the approach on the rising edge, so the turn phase is
            // timed from when the manoeuvre actually begins rather than being
            // restarted every frame.
            if (goCenter == false) ai.BeginCenterApproach();

            goCenter = true;
            investigateMarker = false;
        }

        if (ai.IsL2() == true)
        {
            ai.SetMarker(false);   // no stale markers left behind
            ai.StateMachine.ChangeState(ai.FollowTargetState);
        }
        else if (ai.IsIdle() == true)
        {
            ai.StateMachine.ChangeState(ai.IdleState);
        }
    }

    public override void PhysicsUpdate()
    {
        base.PhysicsUpdate();

        if (investigateMarker == true)
        {
            ai.InvestigateMarker();
        }

        if (goCenter == true)
        {
            ai.MoveToCenter();
        }
    }
}
