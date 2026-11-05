using UnityEngine;

/// <summary>
/// Damage application and knockback for a bot's spinner hitbox.
///
/// PlayerHitBox and AIHitBox were near-identical copies that had drifted apart:
/// different force constants, and - almost certainly by accident - different
/// force modes. AIHitBox used ForceMode2D.Force where PlayerHitBox used
/// ForceMode2D.Impulse, which made the AI's knockback roughly fifty times
/// weaker than the player's. All the behaviour now lives here, so the two can
/// no longer drift.
///
/// PlayerHitBox and AIHitBox still exist as empty subclasses. That is on
/// purpose: the prefabs reference them by script GUID, so keeping the class
/// names preserves every Inspector reference. They can be collapsed into one
/// script later, but that needs a manual rewire in the Editor.
///
/// Every tuning value is serialised, so knockback can be rebalanced per-prefab
/// in the Inspector without a recompile.
/// </summary>
public class BotHitBox : MonoBehaviour
{
    [Header("Wiring (set in Inspector)")]
    [SerializeField] protected CombatSystem enemyBot;      // whose health we subtract from
    [SerializeField] protected CircleCollider2D enemyCol;  // used for the knockback direction
    [SerializeField] protected Rigidbody2D enemyrb;        // them
    [SerializeField] protected Rigidbody2D playerrb;       // us

    /*  KNOCKBACK  ----------------------------------------------------------
     *  Both scripts used to compute a force in the tens of thousands and then
     *  multiply it by Time.deltaTime at the moment of application:
     *
     *      enemyrb.AddForce(direction * force * Time.deltaTime, Impulse);
     *
     *  Inside a physics callback Time.deltaTime returns Time.fixedDeltaTime, so
     *  that was not the frame-rate bug it looks like - it was a constant 0.02
     *  scale factor. The real problem is subtler: it silently tied hit strength
     *  to Edit > Project Settings > Time > Fixed Timestep. Halve the timestep to
     *  get smoother physics and every hit in the game quietly halves too.
     *
     *  The deltaTime factor is gone and the defaults below are the old values
     *  pre-multiplied by 0.02, so the player's knockback is unchanged.
     * -------------------------------------------------------------------- */
    [Header("Knockback tuning")]
    [Tooltip("Force added per point of attack power. Old value: 625 (before the x0.02 fold-in).")]
    [SerializeField] float forcePerDamage = 12.5f;
    [Tooltip("Flat force applied on any hit. Old value: 25000.")]
    [SerializeField] float forceBase = 500f;
    [Tooltip("Lower clamp on the force applied to the enemy. Old value: 25000.")]
    [SerializeField] float forceMin = 500f;
    [Tooltip("Upper clamp on the force applied to the enemy. Old value: 50000.")]
    [SerializeField] float forceMax = 1000f;
    [Tooltip("Flat addition to the recoil we take ourselves. Old value: 10000.")]
    [SerializeField] float selfKnockbackBase = 200f;

    /*  HIT COOLDOWN  -------------------------------------------------------
     *  Damage only lands on OnTriggerEnter2D - the FIRST frame of an overlap.
     *  While knockback was weak the bots ground against each other and stayed
     *  overlapped, so very few damage events fired. Now that they are shoved
     *  cleanly apart they separate and re-collide far more often, and each
     *  re-entry is another full hit. Same damage curve, many more hits landing.
     *
     *  This puts a floor on the time between two hits from the same spinner.
     *  Set it to 0 to restore the old unlimited behaviour.
     * -------------------------------------------------------------------- */
    [Header("Hit rate")]
    [Tooltip("Minimum seconds between two damage events from this hitbox. 0 = no limit.")]
    [SerializeField] float hitCooldown = 0.2f;

    [Header("Runtime (read only)")]
    [SerializeField] protected float force;
    [SerializeField] protected float knockbackForce;

    protected CombatSystem ownerBot;
    Collider2D myCollider;
    int damage;
    float lastHitTime = -999f;

    protected virtual void Start()
    {
        ownerBot = GetComponentInParent<CombatSystem>();
        myCollider = GetComponent<Collider2D>();

        // A dead bot's spinner used to keep dealing damage for the ~3 seconds
        // between death and the scene reloading, because setting enabled=false
        // on Player/AI does not touch a component on a child object.
        if (ownerBot != null) ownerBot.OnDeath += HandleOwnerDeath;
    }

    protected virtual void OnDestroy()
    {
        if (ownerBot != null) ownerBot.OnDeath -= HandleOwnerDeath;
    }

    void HandleOwnerDeath(CombatSystem _)
    {
        SetHitboxActive(false);
    }

    /// <summary>Switches this hitbox off - used on death and when a round ends.</summary>
    public void SetHitboxActive(bool active)
    {
        if (myCollider == null) myCollider = GetComponent<Collider2D>();
        if (myCollider != null) myCollider.enabled = active;

        enabled = active;
    }

    void FixedUpdate()
    {
        if (ownerBot == null) return;

        // Read our own attack power, then scale the knockback to match it.
        damage = ownerBot.GetDamage();
        SetKnockBacks(damage);
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.gameObject.CompareTag(GameTags.Bot)) return;
        if (ownerBot == null || ownerBot.IsDead) return;
        if (enemyBot == null || enemyBot.IsDead) return;

        // Rate-limit damage without rate-limiting the shove - bouncing off each
        // other should still feel physical even when the hit does not count.
        if (hitCooldown > 0f && Time.time - lastHitTime < hitCooldown)
        {
            ApplyKnockback();
            return;
        }

        lastHitTime = Time.time;

        ApplyKnockback();

        enemyBot.DealDamage(damage);
        ownerBot.DecrementDamage();   // our spinner loses its spin on contact
    }

    /// <summary>
    /// Shoves both bots apart. Was called Explosion(), which was confusing - it
    /// has nothing to do with the Explosion prefab spawned on death.
    ///
    /// Note that "direction" is deliberately NOT normalised: its magnitude is
    /// the distance between the two colliders, so glancing contact at range
    /// pushes harder than a dead-centre hit. That is load-bearing for the feel,
    /// so it has been left alone.
    /// </summary>
    void ApplyKnockback()
    {
        if (enemyCol == null || enemyrb == null || playerrb == null) return;

        Vector2 direction = enemyCol.transform.position - transform.position;

        enemyrb.AddForce(direction * force, ForceMode2D.Impulse);
        playerrb.AddForce(-direction * knockbackForce, ForceMode2D.Impulse);
    }

    /// <summary>Scales the knockback force to the current attack power.</summary>
    public void SetKnockBacks(float attackPower)
    {
        force = attackPower * forcePerDamage + forceBase;
        force = Mathf.Clamp(force, forceMin, forceMax);

        knockbackForce = force / 2f + selfKnockbackBase;
    }
}
