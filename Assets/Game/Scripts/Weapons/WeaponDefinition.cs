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
    public float Accuracy = 4f; // degrees of random jitter per projectile
    public float RecoilBloomPerShot = 1.5f; // degrees added to jitter per consecutive shot
    public float MaxBloomAngle = 15f;
    public float BloomRecoverySpeed = 20f; // degrees/second, while not firing

    [Header("Multi-Shot")]
    public int ProjectileCount = 1;
    [Range(0f, 180f)] public float SpreadAngle = 0f; // half-angle the ProjectileCount projectiles fan across; 180 = full circle

    [Header("Projectile")]
    public Projectile ProjectilePrefab;
    public float ProjectileSpeed = 25f;
    public float ProjectileSize = 1f;
    public float Damage = 10f;

    [Header("Projectile Behavior")]
    public float Damping = 0f; // speed lost per second; 0 = no slowdown
    public float MinVelocity = 0f; // despawns early once damping drops speed below this
    public int PenetrationCount = 0; // extra PenetrableLayers hits survived before disappearing
    public LayerMask PenetrableLayers;
    public int ReboundCount = 0; // times it can bounce off a solid (non-trigger) collider before disappearing
}
