using UnityEngine;
using UnityEngine.UI;

/// <summary>Drives the ATK POWER bar. Values are pushed in by CombatSystem.</summary>
public class DamageUI : MonoBehaviour
{
    [SerializeField] Slider slider;

    /// <summary>
    /// Set by CombatSystem on Start so the bar's ceiling always matches the
    /// damage curve. Previously the Slider's Max Value was only editable by hand
    /// in the Inspector, so retuning the curve left the bar mis-scaled.
    /// </summary>
    public void SetMaxDamage(int damage)
    {
        if (slider == null) return;
        slider.maxValue = damage;
    }

    public void SetDamage(int damage)
    {
        if (slider == null) return;
        slider.value = damage;
    }
}
