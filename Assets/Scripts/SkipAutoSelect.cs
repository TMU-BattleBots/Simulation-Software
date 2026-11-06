using UnityEngine;

/// <summary>
/// Marks a button that MenuNavigation should not auto-select.
///
/// MenuNavigation picks the topmost button on screen as the starting selection.
/// The how-to-play "?" button sits in the very top-right corner, so without this
/// it would win that contest and the menu would open with "?" highlighted
/// instead of the first real menu option.
///
/// The button is still reachable by navigating to it - this only excludes it
/// from being chosen automatically.
/// </summary>
public class SkipAutoSelect : MonoBehaviour
{
}
