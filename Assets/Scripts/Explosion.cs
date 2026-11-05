using UnityEngine;

/// <summary>Death explosion. Cleans itself up after its animation has played.</summary>
public class Explosion : MonoBehaviour
{
    [Tooltip("Seconds before the explosion object destroys itself.")]
    [SerializeField] float lifetime = 1f;

    void Start()
    {
        // Was a manual timer in Update that also logged on every destroy.
        Destroy(this.gameObject, lifetime);
    }
}
