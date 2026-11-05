using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FollowTargetState : State //L2
{
    public FollowTargetState(AI ai, StateMachine stateMachine) : base(ai, stateMachine)
    {
    }

    public override void EnterState()
    {
        base.EnterState();
        //ai.SetCurrentStateName("Follow Target State: \n Full target tracking");
        ai.SetCurrentStateName(1);
        //Debug.Log("Entered FollowTargetState");
    }

    public override void ExitState()
    {
        base.ExitState();
        //Debug.Log("Exit FollowTargetState");
    }

    public override void Update()
    {
        base.Update();

        // Tracking moved to PhysicsUpdate - rotation now goes through the
        // Rigidbody2D, so it belongs on the physics tick. Update() is for
        // reading state and deciding transitions.

        if (ai.IsL3() == true)
        {
            ai.StateMachine.ChangeState(ai.OffensiveState);
        }
        if (ai.IsL2() != true && ai.IsL3() != true)
        {
            ai.StateMachine.ChangeState(ai.SearchTargetState);
        }
        else if (ai.IsIdle() == true)
        {
            ai.StateMachine.ChangeState(ai.IdleState);
        }

        //Switch back if lose target
        //CHECK THIS LAST
    }

    public override void PhysicsUpdate()
    {
        base.PhysicsUpdate();

        //Base behavior of this state: lock on to target, at reduced speed
        ai.TrackingTarget();
        ai.MoveSlowly();
    }
}
