using UnityEngine;

/// <summary>
/// Minimal IDamageable target for verifying the weapon -> projectile -> damage
/// chain without waiting on Milestone 4's real enemies. Logs each hit to the
/// Console. Not a real enemy — delete once actual enemies exist.
///
/// Setup:
///   1. Add to any GameObject with a Collider2D in the Testing scene.
/// </summary>
public class TestDummy : MonoBehaviour, IDamageable
{
    public void TakeDamage(DamageInfo damageInfo)
    {
        string weaponName = damageInfo.Weapon != null ? damageInfo.Weapon.name : "unknown weapon";
        Debug.Log($"{name} took {damageInfo.Amount} damage from {weaponName} (source: {damageInfo.Source?.name}) at {damageInfo.HitPoint}", this);
    }
}
