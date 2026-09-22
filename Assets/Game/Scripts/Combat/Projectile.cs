using UnityEngine;

/// <summary>
/// Straight-line pooled projectile. Spawned and reused by ProjectilePoolManager;
/// carries a reference to the WeaponDefinition that fired it so IDamageable
/// implementations can react to weapon-specific data. Faces local +X at rest,
/// same as the Character/Weapon art — but since a fresh projectile is a plain
/// Z rotation with no flip involved, its facing convention wouldn't matter
/// even if it differed.
///
/// Setup:
///   1. Add to a projectile prefab alongside a sprite, a Rigidbody2D, and a
///      trigger Collider2D.
///   2. Set the Rigidbody2D's Gravity Scale to 0 and Collision Detection to
///      Continuous, and tick "Is Trigger" on the Collider2D.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Projectile : MonoBehaviour
{
    [SerializeField] private float m_maxLifetime = 3f;

    private Rigidbody2D m_rb;
    private WeaponDefinition m_weapon;
    private GameObject m_source;
    private float m_damage;
    private float m_despawnTime;

    private void Awake()
    {
        m_rb = GetComponent<Rigidbody2D>();
        m_rb.gravityScale = 0f;
        m_rb.freezeRotation = true;
    }

    public void Initialize(Vector2 direction, float speed, float size, float damage, WeaponDefinition weapon, GameObject source)
    {
        m_weapon = weapon;
        m_source = source;
        m_damage = damage;
        m_despawnTime = Time.time + m_maxLifetime;

        transform.localScale = Vector3.one * size;
        m_rb.linearVelocity = direction * speed;
    }

    public void ResetState()
    {
        m_rb.linearVelocity = Vector2.zero;
    }

    private void Update()
    {
        if (Time.time >= m_despawnTime)
        {
            ProjectilePoolManager.Instance.Release(this);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.attachedRigidbody != null && other.attachedRigidbody.gameObject == m_source) return;

        IDamageable damageable = other.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(new DamageInfo(m_damage, m_source, m_weapon, transform.position));
        }

        ProjectilePoolManager.Instance.Release(this);
    }
}
