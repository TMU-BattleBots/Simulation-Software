using System;
using UnityEngine;

// NOTE: this file used to have "using UnityEditor;". That compiles in the Editor
// but breaks a standalone player build ("The type or namespace name 'UnityEditor'
// could not be found"), because the UnityEditor assembly is not shipped. Runtime
// scripts must never reference it - if you need editor-only code, put it in a
// folder called Editor, or wrap it in #if UNITY_EDITOR.

/// <summary>
/// Collects the detection state of all the child vision cones for one bot.
///
/// HOW TO ADD A CONE
///   1. Duplicate an existing cone child and edit its PolygonCollider2D
///   2. Attach ChildCone and give it a unique, contiguous coneID
///   3. Add a bool to coneState below AND grow the serialised array on the prefab
///   4. Update the IsL1/IsL2/IsL3/IsL4 index ranges in AI.cs
///   5. Grow the Cone Name ID array on the UI component
///
/// HOW IT WORKS
///   Each child cone polls its own collider against Target and reports up here.
///   This script just holds the resulting array.
///
/// KNOWN LIMITATION
///   Detection is collider overlap, not line of sight - ChildCone calls
///   IsTouching, which knows nothing about walls, so the bot can see the target
///   through arena geometry. Fixing that means a Physics2D.Raycast occlusion
///   check before accepting a hit.
/// </summary>
public class VisionCone : MonoBehaviour
{
    [SerializeField] Rigidbody2D rb;

    /// <summary>The single collider every cone tests against.</summary>
    [SerializeField] BoxCollider2D Target;

    /// <summary>
    /// One entry per cone; the index matches the coneID on each ChildCone.
    ///   0-3  L1 front/left/back/right
    ///   4-7  L2 front/left/back/right
    ///   8    L3
    ///   9    L4 (contact)
    /// </summary>
    [SerializeField] public bool[] coneState = { false, false, false, false, false, false, false, false, false, false };

    /// <summary>Value reported when no cone is triggered.</summary>
    public const int NoDetection = -1;

    int firstTrueIndex;
    int lastTrueIndex;
    [SerializeField] int[] coneStateTrue = { 0, 0 };

    /// <summary>
    /// Which cones are currently detecting, as {first, last}.
    ///
    /// Two cones can overlap the target at once, so both ends are reported:
    /// the FIRST index is the better signal for the target entering the cone
    /// array, the LAST for prioritising the innermost cone. AI.GetConeInt()
    /// uses the last.
    ///
    /// Returns {NoDetection, NoDetection} when nothing is detected, because
    /// Array.IndexOf yields -1. Callers must handle that.
    /// </summary>
    public int[] GetCone()
    {
        firstTrueIndex = Array.IndexOf(coneState, true);
        lastTrueIndex = Array.LastIndexOf(coneState, true);

        coneStateTrue[0] = firstTrueIndex;
        coneStateTrue[1] = lastTrueIndex;

        return coneStateTrue;
    }

    public void SetConeStateT(int id)
    {
        if (id < 0 || id >= coneState.Length) return;
        coneState[id] = true;
    }

    public void SetConeStateF(int id)
    {
        if (id < 0 || id >= coneState.Length) return;
        coneState[id] = false;
    }

    public BoxCollider2D GetTarget()
    {
        return Target;
    }
}

//old cone size
//L1 pos 22.9, xy 11
//L2 pos 12.6, xy 6
