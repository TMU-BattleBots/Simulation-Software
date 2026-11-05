using System.Collections;
using UnityEngine;

/// <summary>
/// The AI's investigation ping.
///
/// A marker only lives for a finite time. Once it expires the AI can place a
/// new one or fall back to moving to the centre of the arena - without the
/// expiry the bot would chase a stale ping indefinitely.
///
/// Raising expTime makes the AI easier to escape, because it commits longer to
/// a position the target has already left.
/// </summary>
public class Marker : MonoBehaviour
{
    [Tooltip("Seconds before the marker deactivates itself.")]
    [SerializeField] float expTime = 3f;

    /// <summary>
    /// Scales how long a marker survives. Used by the AI difficulty presets - a
    /// longer-lived marker means the AI commits harder to stale information,
    /// which makes it easier to bait.
    /// </summary>
    public void ScaleLifetime(float multiplier)
    {
        if (multiplier <= 0f) return;
        expTime *= multiplier;
    }

    void OnEnable()
    {
        StartCoroutine(Countdown(expTime));
    }

    void OnDisable()
    {
        // The object is pooled - it gets reactivated rather than destroyed - so
        // an old countdown must not survive into the next activation.
        StopAllCoroutines();
    }

    IEnumerator Countdown(float seconds)
    {
        yield return new WaitForSeconds(seconds);

        this.gameObject.SetActive(false);
    }
}
