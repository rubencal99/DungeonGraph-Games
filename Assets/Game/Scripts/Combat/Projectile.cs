using UnityEngine;

/// <summary>
/// Straight-line pooled projectile with optional drag, penetration, and
/// rebound — all tunable per weapon via WeaponDefinition. Spawned and reused
/// by ProjectilePoolManager. Faces local +X at rest, same as the
/// Character/Weapon art — but since a fresh projectile is a plain Z rotation
/// with no flip involved, its facing convention wouldn't matter even if it
/// differed.
///
/// Penetration and rebound are independent: hitting a collider on
/// WeaponDefinition's Penetrable Layers always passes through (up to
/// Penetration Count times) regardless of rebound state, and hitting any
/// other solid (non-trigger) collider always bounces instead (up to Rebound
/// Count times) regardless of penetration state. Neither counter affects the
/// other.
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
    private int m_penetrationRemaining;
    private int m_reboundRemaining;
    private Vector2 m_previousPosition;

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
        m_penetrationRemaining = weapon != null ? weapon.PenetrationCount : 0;
        m_reboundRemaining = weapon != null ? weapon.ReboundCount : 0;

        transform.localScale = Vector3.one * size;
        m_rb.linearVelocity = direction * speed;
        m_previousPosition = m_rb.position;
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
            return;
        }

        ApplyDamping();
        m_previousPosition = m_rb.position;
    }

    private void ApplyDamping()
    {
        if (m_weapon == null || m_weapon.Damping <= 0f) return;

        float speed = m_rb.linearVelocity.magnitude;
        float dampenedSpeed = Mathf.Max(0f, speed - m_weapon.Damping * Time.deltaTime);

        if (dampenedSpeed <= m_weapon.MinVelocity)
        {
            ProjectilePoolManager.Instance.Release(this);
            return;
        }

        m_rb.linearVelocity = m_rb.linearVelocity.normalized * dampenedSpeed;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.attachedRigidbody != null && other.attachedRigidbody.gameObject == m_source) return;

        IDamageable damageable = other.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(new DamageInfo(m_damage, m_source, m_weapon, transform.position));
        }

        if (IsPenetrable(other))
        {
            m_penetrationRemaining--;
            if (m_penetrationRemaining >= 0) return;
        }
        else if (!other.isTrigger && m_reboundRemaining > 0)
        {
            Rebound(other);
            return;
        }

        ProjectilePoolManager.Instance.Release(this);
    }

    private bool IsPenetrable(Collider2D other)
    {
        return m_weapon != null && (m_weapon.PenetrableLayers.value & (1 << other.gameObject.layer)) != 0;
    }

    private void Rebound(Collider2D other)
    {
        Vector2 incomingDirection = m_rb.linearVelocity.normalized;
        Vector2 normal = GetSurfaceNormal(other, incomingDirection);

        Vector2 reflected = Vector2.Reflect(m_rb.linearVelocity, normal);
        m_rb.linearVelocity = reflected;
        m_rb.rotation = Mathf.Atan2(reflected.y, reflected.x) * Mathf.Rad2Deg;
        m_reboundRemaining--;
    }

    // ClosestPoint returns the query point itself once it's inside a solid
    // collider, which a fast-moving bullet usually already is by the time its
    // trigger fires — that degenerate (zero-length) normal was collapsing
    // every bounce into a straight reversal. Raycasting back along the
    // incoming path to where it actually crossed the surface gives the real
    // normal instead; ClosestPoint is kept only as a fallback for the rare
    // case the raycast finds nothing.
    private Vector2 GetSurfaceNormal(Collider2D other, Vector2 incomingDirection)
    {
        float travelDistance = Vector2.Distance(m_previousPosition, m_rb.position) + 0.5f;
        int layerMask = 1 << other.gameObject.layer;
        RaycastHit2D hit = Physics2D.Raycast(m_previousPosition, incomingDirection, travelDistance, layerMask);
        if (hit.collider != null) return hit.normal;

        Vector2 closest = other.ClosestPoint(m_rb.position);
        Vector2 fallbackNormal = m_rb.position - closest;
        return fallbackNormal.sqrMagnitude > 0.0001f ? fallbackNormal.normalized : -incomingDirection;
    }
}
