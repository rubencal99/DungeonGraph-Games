using UnityEngine;

/// <summary>
/// Fires the weapon's WeaponDefinition when asked: handles fire-rate timing,
/// ammo, and spread/bloom, then requests a pooled Projectile aimed along its
/// AimSource from the Muzzle transform.
///
/// It has no idea who is pulling the trigger. The player's WeaponHolder calls
/// TryFire while the Attack button is held; an enemy's RangedAttack calls it
/// when its brain decides to shoot. That is what lets one gun prefab work in
/// either hand.
///
/// Setup:
///   1. Add to the weapon prefab (e.g. TestPistol), alongside its sprite.
///   2. Add an empty child named "Muzzle" at the barrel tip and assign it.
///   3. Assign a WeaponDefinition asset.
///   4. Reference this prefab from its WeaponDefinition's Weapon Prefab slot.
///      The aim and owner are wired at runtime (WeaponHolder for the player,
///      RangedAttack for enemies).
/// </summary>
public class Weapon : MonoBehaviour
{
    [SerializeField] private WeaponDefinition m_definition;
    [SerializeField] private Transform m_muzzle;

    [Tooltip("Never runs dry. Tick this on enemy-held weapons, which have no way to reload.")]
    [SerializeField] private bool m_infiniteAmmo;

    /// <summary>Which weapon this is, so WeaponHolder can tell one instance from another.</summary>
    public WeaponDefinition Definition => m_definition;

    private AimSource m_aim;
    private GameObject m_owner;
    private float m_nextFireTime;
    private float m_currentBloom;
    private int m_currentAmmo;

    private void Awake()
    {
        m_currentAmmo = m_definition != null ? m_definition.AmmoCapacity : 0;
    }

    /// <summary>
    /// Points this instance at whoever is aiming it, and records who owns its
    /// shots so they never hit their own shooter. Called at spawn, because a
    /// prefab cannot hold a reference to a scene object.
    /// </summary>
    public void Initialize(AimSource aim, GameObject owner)
    {
        m_aim = aim;
        m_owner = owner;
    }

    private void Update()
    {
        if (m_definition == null) return;

        m_currentBloom = Mathf.Max(0f, m_currentBloom - m_definition.BloomRecoverySpeed * Time.deltaTime);
    }

    /// <summary>
    /// Fires if the weapon is ready: aimed, off cooldown, and loaded. Safe to
    /// call every frame; returns whether a shot actually went out.
    /// </summary>
    public bool TryFire()
    {
        if (m_definition == null || m_muzzle == null || m_aim == null) return false;
        if (Time.time < m_nextFireTime) return false;
        if (!m_infiniteAmmo && m_currentAmmo <= 0) return false;

        Fire();
        return true;
    }

    private void Fire()
    {
        m_nextFireTime = Time.time + 1f / m_definition.FireRate;
        if (!m_infiniteAmmo) m_currentAmmo--;

        int count = Mathf.Max(1, m_definition.ProjectileCount);
        float fanStep = count > 1 ? m_definition.SpreadAngle * 2f / (count - 1) : 0f;

        for (int i = 0; i < count; i++)
        {
            float fanAngle = count == 1 ? 0f : -m_definition.SpreadAngle + fanStep * i;
            SpawnProjectile(fanAngle);
        }

        m_currentBloom = Mathf.Min(m_definition.MaxBloomAngle, m_currentBloom + m_definition.RecoilBloomPerShot);

        // Once per shot, not per projectile, so a shotgun is not eight times louder.
        AudioManager.Play(m_definition.FireCue, m_muzzle.position);
    }

    private void SpawnProjectile(float fanAngle)
    {
        float halfAccuracy = (m_definition.Accuracy + m_currentBloom) * 0.5f;
        float jitter = Random.Range(-halfAccuracy, halfAccuracy);
        float angle = m_aim.AimAngleDegrees + fanAngle + jitter;
        Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

        Projectile projectile = ProjectilePoolManager.Instance.Get(m_definition.ProjectilePrefab, m_muzzle.position, Quaternion.Euler(0f, 0f, angle));
        projectile.Initialize(direction, m_definition.ProjectileSpeed, m_definition.ProjectileSize, m_definition.Damage, m_definition, m_owner);
    }
}
