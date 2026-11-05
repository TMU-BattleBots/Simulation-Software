using System;
using UnityEngine;
using static System.Math;

/// <summary>
/// Health and attack-power for a single bot. Deliberately separate from Player
/// and AI so the same component works on both - the collision detection returns
/// this type.
/// </summary>
public class CombatSystem : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] int maxHealth = 100;
    [SerializeField] int currentHealth = 0;

    [Header("UI")]
    [SerializeField] HealthUI healthUI;
    [SerializeField] DamageUI damageUI;
    // The attack-power bar's ceiling. The sustain phase oscillates between about
    // 25.5 and 36.5, so 37 keeps the bar from clipping. Pushed into the Slider
    // on Start so the bar can no longer drift out of sync with the curve.
    [SerializeField] int damageBarMax = 37;

    /*  ATTACK POWER CURVE  ------------------------------------------------
     *  x is a seconds timer modelling spinner spin-up, not a position.
     *
     *  Spin-up phase (x <= xUpperBound):
     *      amplitude   = spinUpBase ^ (spinUpExponent * x)
     *      attackPower = -amplitude * cos(biteFrequency * PI * x) + e^(growthRate * x)
     *
     *  Sustain phase (x > xUpperBound):
     *      attackPower = -sustainAmplitude * cos(biteFrequency * PI * x) + sustainOffset
     *
     *  The cosine gives the damage a rhythm - a "bite" window comes round every
     *  2/biteFrequency seconds - which is what a player times their attacks
     *  against. xUpperBound is where the two curves intersect; above it the
     *  damage stops growing so it cannot run away over a long round.
     *
     *  These were hard-coded constants. They are serialised now so the curve can
     *  be retuned in the Inspector without a recompile.
     * ------------------------------------------------------------------- */
    [Header("Attack power curve")]
    [SerializeField] double xUpperBound = 8.343;       // where the two curves meet
    [SerializeField] double spinUpBase = 2.5;
    [SerializeField] double spinUpExponent = 0.31;
    [SerializeField] double growthRate = 0.4;
    [SerializeField] double biteFrequency = 3.0;       // cos(biteFrequency * PI * x)
    [SerializeField] double sustainAmplitude = 5.5;
    [SerializeField] double sustainOffset = 31.0;

    [Header("Impact response")]
    // How much spin-up an impact costs while still below xUpperBound (floored at 0).
    // Raise it to punish trading hits harder. Was the private field "minusx".
    [SerializeField] float impactPenalty = 4f;
    // Once fully spun up, an impact drops x straight back to this instead.
    [SerializeField] double postBoundResetX = 2.0;

    [Header("Runtime (read only)")]
    [SerializeField] double x;                 // the spin-up timer
    [SerializeField] int damageNumber;

    double attackPower;
    double amplitude;
    bool isDead;

    /// <summary>
    /// Raised once, on the frame this bot's health reaches zero.
    ///
    /// Player and AI subscribe to this instead of polling DeathCheck() every
    /// frame. Polling was what let a dead bot keep dealing damage for the ~3s
    /// before the scene reloaded, because the order in which the pollers noticed
    /// was undefined.
    /// </summary>
    public event Action<CombatSystem> OnDeath;

    public bool IsDead => isDead;
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    public void Start()
    {
        currentHealth = maxHealth;

        // These used to be set by hand in the Inspector and silently drifted out
        // of sync whenever maxHealth changed. Now the code is the source of truth.
        if (healthUI != null) healthUI.SetMaxHealth(maxHealth);
        if (damageUI != null) damageUI.SetMaxDamage(damageBarMax);
    }

    public void Update()
    {
        Attack();
        UpdateUI();
    }

    /// <summary>
    /// Scales this bot's health pool, used by the AI difficulty presets.
    ///
    /// Safe to call before or after Start: it refills to the new maximum either
    /// way, and Start's own "currentHealth = maxHealth" stays correct because
    /// maxHealth has already been scaled by then.
    /// </summary>
    public void ScaleMaxHealth(float multiplier)
    {
        if (multiplier <= 0f) return;

        maxHealth = Mathf.Max(1, Mathf.RoundToInt(maxHealth * multiplier));
        currentHealth = maxHealth;

        if (healthUI != null) healthUI.SetMaxHealth(maxHealth);
    }

    /// <summary>
    /// Subtracts an enemy bot's attack power from our health. Called by the
    /// enemy's hitbox on contact.
    /// </summary>
    public void DealDamage(int hp)
    {
        if (isDead) return;   // ignore hits that land in the same frame as death

        currentHealth -= hp;

        // Was "if (currentHealth < 0)", which let a bot sit on exactly 0 HP with
        // an empty health bar and survive until the next hit.
        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Die();
        }
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;

        if (healthUI != null)
        {
            healthUI.SetHealth(0);
            healthUI.DeathIcon();
        }

        // Stops Attack()/UpdateUI() running, freezing the damage number.
        enabled = false;

        OnDeath?.Invoke(this);
    }

    /// <summary>Attack power increases over time as the spinner spins up.</summary>
    void Attack()
    {
        x += Time.deltaTime;

        if (x <= xUpperBound)
        {
            amplitude = Pow(spinUpBase, spinUpExponent * x);
            attackPower = -amplitude * Cos(biteFrequency * PI * x) + Exp(growthRate * x);
        }
        else
        {
            attackPower = -sustainAmplitude * Cos(biteFrequency * PI * x) + sustainOffset;
        }

        damageNumber = (int)Round(attackPower);
    }

    /// <summary>Used by the hitboxes to read this bot's current attack power.</summary>
    public int GetDamage()
    {
        return damageNumber;
    }

    /// <summary>
    /// Called on every collision to emulate the spinner losing its spin on contact.
    /// </summary>
    public void DecrementDamage()
    {
        if (x < xUpperBound)
        {
            x = Max(0, x - impactPenalty);
        }
        else
        {
            x = postBoundResetX;
        }
    }

    public void UpdateUI()
    {
        if (healthUI != null) healthUI.SetHealth(currentHealth);
        if (damageUI != null) damageUI.SetDamage(damageNumber);
    }

    /// <summary>
    /// Kept for convenience. Prefer subscribing to OnDeath - polling this every
    /// frame is what the event replaced.
    /// </summary>
    public bool DeathCheck()
    {
        return isDead;
    }
}
