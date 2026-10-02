using System;
using UnityEngine;

/// <summary>
/// Hit points for anything that can be hurt: the player, an enemy, a crate.
/// It only counts down and announces. What dying means belongs to whoever
/// listens — an Enemy drops loot and returns to its pool; the player has no
/// death handling yet.
///
/// In a session, a sibling HealthNetwork decides which machine each hit counts
/// on, so Damaged and Died only fire on the machine that owns this health: your
/// own machine for your player, the host for enemies.
///
/// Setup:
///   1. Add to the root of anything with a Collider2D that should take damage
///      (projectiles and melee hits find it with GetComponentInParent).
///   2. Set Max Health. Enemies overwrite it from their EnemyDefinition.
/// </summary>
public class Health : MonoBehaviour, IDamageable
{
    [SerializeField] private float m_maxHealth = 100f;

    public float Max => m_maxHealth;
    public float Current { get; private set; }
    public bool IsDead => Current <= 0f;

    public event Action<DamageInfo> Damaged;
    public event Action<DamageInfo> Died;

    private HealthNetwork m_network;

    private void Awake()
    {
        Current = m_maxHealth;
        TryGetComponent(out m_network);
    }

    /// <summary>Sets a new maximum and refills to it. Pooled enemies call this every time they respawn.</summary>
    public void ResetHealth(float max)
    {
        m_maxHealth = max;
        Current = max;
    }

    public void TakeDamage(DamageInfo damageInfo)
    {
        if (IsDead) return;
        if (m_network != null && !m_network.ShouldApplyLocally(damageInfo)) return;

        ApplyDamage(damageInfo);
    }

    /// <summary>
    /// Applies a hit with no network check. HealthNetwork calls this when a hit
    /// decided on another machine arrives; everything else goes through TakeDamage.
    /// </summary>
    public void ApplyDamage(DamageInfo damageInfo)
    {
        // Already dead: a shotgun's other pellets landing in the same frame must not die it twice.
        if (IsDead) return;

        Current = Mathf.Max(0f, Current - damageInfo.Amount);
        Damaged?.Invoke(damageInfo);

        if (IsDead) Died?.Invoke(damageInfo);
    }
}
