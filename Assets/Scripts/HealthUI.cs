using UnityEngine;
using UnityEngine.UI;

/// <summary>Drives a bot's health bar. Values are pushed in by CombatSystem.</summary>
public class HealthUI : MonoBehaviour
{
    [SerializeField] Slider slider;
    [SerializeField] GameObject deathSkull;

    bool skullSpawned;

    /// <summary>
    /// Now actually called - CombatSystem.Start() pushes maxHealth in. Before
    /// this the Slider's Max Value was only set by hand in the Inspector, so
    /// changing maxHealth left the bar reading the wrong scale.
    /// </summary>
    public void SetMaxHealth(int health)
    {
        if (slider == null) return;
        slider.maxValue = health;
        slider.value = health;
    }

    public void SetHealth(int health)
    {
        if (slider == null) return;
        slider.value = health;
    }

    public void DeathIcon()
    {
        // Guarded: CombatSystem can only die once now, but this also protects
        // against a double-spawn if anything else ever calls in.
        if (skullSpawned || deathSkull == null) return;
        skullSpawned = true;

        Instantiate(deathSkull, this.transform);
    }
}
