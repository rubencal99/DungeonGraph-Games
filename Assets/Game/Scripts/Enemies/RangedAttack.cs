using UnityEngine;

/// <summary>
/// Shoots with a real Weapon — the same prefab, definition, projectiles, and
/// sounds the player uses. Aiming is handled by FaceTargetAction through
/// EnemyAim, so this only pulls the trigger.
///
/// Setup:
///   1. Add to the enemy prefab root.
///   2. Give the enemy a "Weapons" child with WeaponAimRotator (Aim = EnemyAim),
///      and drop a weapon prefab under it. On that instance, tick Infinite Ammo.
///   3. Assign that weapon instance to the Weapon slot.
/// </summary>
[RequireComponent(typeof(EnemyAim))]
public class RangedAttack : MonoBehaviour, IEnemyAttack
{
    [SerializeField] private Weapon m_weapon;

    private void Awake()
    {
        if (m_weapon != null) m_weapon.Initialize(GetComponent<EnemyAim>(), gameObject);
    }

    public bool TryAttack(Transform target) => m_weapon != null && m_weapon.TryFire();
}
