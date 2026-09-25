using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Fires the weapon's WeaponDefinition on the Attack action: handles fire-rate
/// timing, ammo, and spread/bloom, then requests a pooled Projectile aimed at
/// the player's current aim angle from the Muzzle transform.
///
/// Setup:
///   1. Add to the weapon prefab (e.g. TestPistol), alongside its sprite.
///   2. Add an empty child named "Muzzle" at the barrel tip and assign it.
///   3. Assign a WeaponDefinition asset.
///   4. Drag the "Attack" action (Player map) into the Fire Action slot.
///      Player Aim is wired at runtime by WeaponHolder, so leave it empty on
///      the prefab.
///   5. Reference this prefab from its WeaponDefinition's Weapon Prefab slot.
/// </summary>
public class Weapon : MonoBehaviour
{
    [SerializeField] private WeaponDefinition m_definition;
    [SerializeField] private Transform m_muzzle;
    [SerializeField] private PlayerAim m_playerAim;
    [SerializeField] private InputActionReference m_fireAction;

    /// <summary>Which weapon this is, so WeaponHolder can tell one instance from another.</summary>
    public WeaponDefinition Definition => m_definition;

    private float m_nextFireTime;
    private float m_currentBloom;
    private int m_currentAmmo;

    private void Awake()
    {
        m_currentAmmo = m_definition != null ? m_definition.AmmoCapacity : 0;
    }

    /// <summary>
    /// Points this instance at the player aiming it. Called by WeaponHolder the
    /// moment the weapon is spawned, because a prefab cannot hold a reference to
    /// a scene object.
    /// </summary>
    public void Initialize(PlayerAim playerAim)
    {
        m_playerAim = playerAim;
    }

    private void OnEnable()
    {
        m_fireAction?.action.Enable();
    }

    // Deliberately no OnDisable that disables the action. Every weapon instance
    // points at the same shared Attack action, so a stowed weapon switching it
    // off would disarm the one just drawn.

    private void Update()
    {
        if (m_definition == null) return;

        m_currentBloom = Mathf.Max(0f, m_currentBloom - m_definition.BloomRecoverySpeed * Time.deltaTime);

        if (m_fireAction == null || m_muzzle == null || m_playerAim == null) return;
        if (!m_fireAction.action.IsPressed()) return;
        if (Time.time < m_nextFireTime) return;
        if (m_currentAmmo <= 0) return;

        Fire();
    }

    private void Fire()
    {
        m_nextFireTime = Time.time + 1f / m_definition.FireRate;
        m_currentAmmo--;

        int count = Mathf.Max(1, m_definition.ProjectileCount);
        float fanStep = count > 1 ? m_definition.SpreadAngle * 2f / (count - 1) : 0f;

        for (int i = 0; i < count; i++)
        {
            float fanAngle = count == 1 ? 0f : -m_definition.SpreadAngle + fanStep * i;
            SpawnProjectile(fanAngle);
        }

        m_currentBloom = Mathf.Min(m_definition.MaxBloomAngle, m_currentBloom + m_definition.RecoilBloomPerShot);
    }

    private void SpawnProjectile(float fanAngle)
    {
        float halfAccuracy = (m_definition.Accuracy + m_currentBloom) * 0.5f;
        float jitter = Random.Range(-halfAccuracy, halfAccuracy);
        float angle = m_playerAim.AimAngleDegrees + fanAngle + jitter;
        Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

        Projectile projectile = ProjectilePoolManager.Instance.Get(m_definition.ProjectilePrefab, m_muzzle.position, Quaternion.Euler(0f, 0f, angle));
        projectile.Initialize(direction, m_definition.ProjectileSpeed, m_definition.ProjectileSize, m_definition.Damage, m_definition, transform.root.gameObject);
    }
}
