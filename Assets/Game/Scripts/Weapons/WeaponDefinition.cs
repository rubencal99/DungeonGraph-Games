using UnityEngine;

/// <summary>
/// Tunable stats for a weapon. Create one asset per weapon type via
/// Assets > Create > Game > Weapon Definition, then reference it from a
/// weapon prefab's Weapon component.
/// </summary>
[CreateAssetMenu(fileName = "New Weapon", menuName = "Game/Weapon Definition")]
public class WeaponDefinition : ScriptableObject
{
    [Header("Firing")]
    public float FireRate = 6f; // shots per second
    public int AmmoCapacity = 12;

    [Header("Accuracy")]
    public float SpreadAngle = 4f; // degrees of random spread per shot
    public float RecoilBloomPerShot = 1.5f; // degrees added to spread per consecutive shot
    public float MaxBloomAngle = 15f;
    public float BloomRecoverySpeed = 20f; // degrees/second, while not firing

    [Header("Projectile")]
    public Projectile ProjectilePrefab;
    public float ProjectileSpeed = 25f;
    public float ProjectileSize = 1f;
    public float Damage = 10f;
}
