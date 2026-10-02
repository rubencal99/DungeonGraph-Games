using UnityEngine;

/// <summary>
/// Fires the weapon's WeaponDefinition when asked: handles fire-rate timing,
/// ammo, and spread/bloom, then requests a pooled Projectile aimed along its
/// AimSource from the Muzzle transform.
///
/// It has no idea who is pulling the trigger. The player's WeaponHolder calls
/// TryFire while the Attack button is held (via PlayerShooter); an enemy's RangedAttack calls it
/// when its brain decides to shoot. That is what lets one gun prefab work in
/// either hand.
///
/// In a session, the machine that fires also tells every other machine through
/// the owner's ShotNetwork. They replay the same shot with SpawnShot: same
/// muzzle position, angle, and random seed, so the same bullets.
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
    private ShotNetwork m_shotNetwork;
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
        m_shotNetwork = owner != null ? owner.GetComponentInParent<ShotNetwork>() : null;
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

        // The seed is what lets every other machine roll this exact spread, so one
        // small message describes the whole volley.
        uint seed = (uint)Random.Range(1, int.MaxValue);
        Vector2 origin = m_muzzle.position;
        float aimAngle = m_aim.AimAngleDegrees;

        SpawnShot(m_definition, origin, aimAngle, m_currentBloom, seed, m_owner);
        if (m_shotNetwork != null) m_shotNetwork.Send(m_definition, origin, aimAngle, m_currentBloom, seed);

        m_currentBloom = Mathf.Min(m_definition.MaxBloomAngle, m_currentBloom + m_definition.RecoilBloomPerShot);
    }

    /// <summary>
    /// Spawns one trigger pull's projectiles and plays its sound. The same inputs
    /// always give the same projectiles, which is how a shot fired on one machine
    /// is replayed on the others.
    /// </summary>
    public static void SpawnShot(WeaponDefinition definition, Vector2 origin, float aimAngle, float bloom, uint seed, GameObject owner)
    {
        if (definition == null) return;

        ShotRandom random = new ShotRandom(seed);
        int count = Mathf.Max(1, definition.ProjectileCount);
        float fanStep = count > 1 ? definition.SpreadAngle * 2f / (count - 1) : 0f;
        float halfAccuracy = (definition.Accuracy + bloom) * 0.5f;

        for (int i = 0; i < count; i++)
        {
            float fanAngle = count == 1 ? 0f : -definition.SpreadAngle + fanStep * i;
            float angle = aimAngle + fanAngle + random.Range(-halfAccuracy, halfAccuracy);
            Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

            Projectile projectile = ProjectilePoolManager.Instance.Get(definition.ProjectilePrefab, origin, Quaternion.Euler(0f, 0f, angle));
            projectile.Initialize(direction, definition.ProjectileSpeed, definition.ProjectileSize, definition.Damage, definition, owner);
        }

        // Once per shot, not per projectile, so a shotgun is not eight times louder.
        AudioManager.Play(definition.FireCue, origin);
    }

    /// <summary>
    /// A tiny random number generator owned by one shot. Unity's shared Random
    /// can't be used here: other code draws from it in between, so two machines
    /// would get different numbers from the same seed.
    /// </summary>
    private struct ShotRandom
    {
        private uint m_state;

        public ShotRandom(uint seed)
        {
            m_state = seed != 0 ? seed : 1u;
        }

        public float Range(float min, float max)
        {
            // Xorshift: three shifts per number, no allocation.
            m_state ^= m_state << 13;
            m_state ^= m_state >> 17;
            m_state ^= m_state << 5;

            return min + (max - min) * ((m_state & 0xFFFFFF) / 16777216f);
        }
    }
}
