using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OffensiveState : State //L3
{
    public OffensiveState(AI ai, StateMachine stateMachine) : base(ai, stateMachine)
    {
    }

    public override void EnterState()
    {
        base.EnterState();
        //ai.SetCurrentStateName("Attack Target State: \n Full target tracking with aggressive movement");
        ai.SetCurrentStateName(2);
        //Debug.Log("Entered Offense");

    }

    public override void ExitState()
    {
        base.ExitState();
        //Debug.Log("Exit Offense");
    }

    public override void Update()
    {
        base.Update();

        // Tracking moved to PhysicsUpdate - see FollowTargetState.

        /*Match can reach "spin lock" behavior where both bots are spinning 
         * rapidly side by side trying to find an opening
         * Im thinking it might be cool to have a x where if we're in this situation for too long
         * It can momentarily turn off its motors, geta perfect track on the enemy
         * And then attack
         */

        /*Also need to implement the pushing -> pin to wall-> ->wedge -> BAM attack, typical for a hammersaw
         * when L4 detected, run an extremely short x where if the player is still in it, disable movement on player?
         * acts as fork wedging the player
         * 
         * 
         */

        /*But before I can do that i need to actually implement a target movement prediction system
         * I saw a vid on it should be possible w just math
         * If theres a way to visualize it thats great too.
         */

        /*Finally movement is kinda weird rn. Impulse and high drag makes for a good convincing movement
         * but it makes pinning not as satisfying and accurate since the drag is so high
         * Might have to change from impulse to Force2D but i rmb theres some problems w that
         */
        if (ai.IsL3() != true && ai.IsL2() == true)
        {
            ai.StateMachine.ChangeState(ai.FollowTargetState);
        }
        else if (ai.IsL3() != true && ai.IsL2() != true)
        {
            ai.StateMachine.ChangeState(ai.SearchTargetState);
        }
        else if (ai.IsIdle() == true)
        {
            ai.StateMachine.ChangeState(ai.IdleState);
        }
    }

    public override void PhysicsUpdate()
    {
        base.PhysicsUpdate();

        //Base behavior of this state: still locked on, at attack speed
        ai.TrackingTarget();
        ai.MoveForward();
    }
}
