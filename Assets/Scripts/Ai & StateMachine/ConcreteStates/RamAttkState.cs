/// <summary>
/// Ram attack: drive straight at the target while tracking it.
///
/// NOT CURRENTLY REACHABLE. AI.Awake() constructs it, but no state transitions
/// into it, so it never runs. It is kept because it is the starting point for
/// the "wedge and pin" behaviour on the roadmap, not because it is live code.
///
/// Two things were wrong with it and are now fixed, so that when someone does
/// wire it up it behaves:
///   - it drove from Update() instead of PhysicsUpdate(), so its acceleration
///     was frame-rate dependent (this is the "bug" the old comment referred to)
///   - it had no transitions out, so entering it would have trapped the AI
///
/// To bring it into play, add a transition from OffensiveState, e.g.
///     if (ai.IsL4()) ai.StateMachine.ChangeState(ai.RamAttkState);
/// </summary>
public class RamAttkState : State
{
    public RamAttkState(AI ai, StateMachine stateMachine) : base(ai, stateMachine)
    {
    }

    public override void EnterState()
    {
        base.EnterState();
        ai.SetCurrentStateName(2);   // shares the Offensive label until it gets its own
    }

    public override void ExitState()
    {
        base.ExitState();
    }

    public override void Update()
    {
        base.Update();

        // Transitions out. Without these, entering this state was a one-way trip.
        if (ai.IsIdle() == true)
        {
            ai.StateMachine.ChangeState(ai.IdleState);
        }
        else if (ai.IsL3() == false && ai.IsL2() == true)
        {
            ai.StateMachine.ChangeState(ai.FollowTargetState);
        }
        else if (ai.IsL3() == false && ai.IsL2() == false)
        {
            ai.StateMachine.ChangeState(ai.SearchTargetState);
        }
    }

    public override void PhysicsUpdate()
    {
        base.PhysicsUpdate();

        ai.TrackingTarget();
        ai.MoveForward();
    }
}
