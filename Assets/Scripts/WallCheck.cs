using UnityEngine;

/// <summary>
/// Placeholder for wall-escape behaviour.
///
/// The bot used to get stuck on walls - driving forward but not rotating enough,
/// because of friction against the wall. The shipped fix was to make the walls
/// very slippery (Assets/Prefabs/Slippery.physicsMaterial2D), which works well,
/// so the logic that was going to live here was never written.
///
/// KEPT ON PURPOSE, EVEN THOUGH IT DOES NOTHING.
/// This component sits on a child object of AI.prefab that also carries a
/// collider. Deleting the script would leave a Missing Script on the prefab, and
/// deleting the whole object would remove that collider and change the AI's
/// physics. The empty Start() and Update() have been removed, though - an empty
/// Update() still costs a per-frame call into managed code, whereas a
/// MonoBehaviour with no Update at all costs nothing.
///
/// The behaviour this was meant to hold is still wanted: on wall contact,
/// reverse and turn. That is best written as a proper AI state (see the
/// how-to-add-a-state recipe in the documentation) rather than as a component
/// here.
/// </summary>
public class WallCheck : MonoBehaviour
{
}
