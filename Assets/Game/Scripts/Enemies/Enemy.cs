using UnityEngine;

/// <summary>
/// The enemy's identity and life cycle: which EnemyDefinition it is, full
/// health on every spawn, and on death a loot roll and a trip back to the pool.
/// Decisions live in EnemyBrain; this only handles being born and dying.
///
/// Setup:
///   1. Add to the enemy prefab root, alongside Health, EnemyBrain,
///      EnemyMotor, and EnemyAim.
///   2. Assign this enemy's EnemyDefinition, and point that definition's
///      Prefab back at this prefab.
/// </summary>
[RequireComponent(typeof(Health))]
public class Enemy : MonoBehaviour
{
    [SerializeField] private EnemyDefinition m_definition;

    public EnemyDefinition Definition => m_definition;
    public Health Health { get; private set; }

    private void Awake()
    {
        Health = GetComponent<Health>();
        Health.Died += HandleDied;
    }

    // Runs on every pool reuse, so a recycled enemy never comes back half dead.
    private void OnEnable()
    {
        if (m_definition != null) Health.ResetHealth(m_definition.MaxHealth);
    }

    private void HandleDied(DamageInfo killingBlow)
    {
        DropLoot();

        if (EnemyPoolManager.Instance != null) EnemyPoolManager.Instance.Release(gameObject);
        else Destroy(gameObject);
    }

    /// <summary>Same path as a chest: roll the table, hand the result to WeaponPickupSpawner.</summary>
    private void DropLoot()
    {
        if (m_definition == null || m_definition.DeathLoot == null) return;
        if (Random.value > m_definition.DropChance) return;
        if (WeaponPickupSpawner.Instance == null) return;

        WeaponDefinition weapon = m_definition.DeathLoot.Roll();
        if (weapon == null) return;

        WeaponPickupSpawner.Instance.Spawn(weapon, transform.position, Random.insideUnitCircle.normalized);
    }
}
