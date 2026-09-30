using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A swing driven by animation. TryAttack only plays the attack clip; the hit
/// itself lands when the clip reaches an Animation Event calling DealHit, so
/// the damage lines up with the frame where the weapon visibly connects. The
/// hit is one overlap circle at Hit Point — no colliders to enable/disable.
///
/// Setup:
///   1. Add to the Character child of the enemy — the object with the Animator.
///      Animation Events can only call methods on the Animator's own object.
///   2. Give the Animator a trigger parameter named "Attack" that plays the
///      attack clip.
///   3. In the Animation window, add an Animation Event on the impact frame of
///      that clip and pick the DealHit function.
///   4. Add an empty child "HitPoint" in front of the enemy, assign it, and set
///      Hit Radius (the red gizmo) to the swing's reach.
///   5. Set Target Layers to the Player layer.
/// </summary>
[RequireComponent(typeof(Animator))]
public class MeleeAttack : MonoBehaviour, IEnemyAttack
{
    private static readonly int AttackParam = Animator.StringToHash("Attack");

    [SerializeField] private Transform m_hitPoint;
    [SerializeField] private float m_hitRadius = 0.6f;
    [SerializeField] private LayerMask m_targetLayers;

    private Animator m_animator;
    private Enemy m_enemy;
    private ContactFilter2D m_filter;

    // Reused every hit so swinging never allocates.
    private readonly List<Collider2D> m_overlaps = new(8);
    private readonly List<IDamageable> m_damaged = new(4);

    private void Awake()
    {
        m_animator = GetComponent<Animator>();
        m_enemy = GetComponentInParent<Enemy>();

        m_filter = new ContactFilter2D { useTriggers = true };
        m_filter.SetLayerMask(m_targetLayers);
    }

    public bool TryAttack(Transform target)
    {
        m_animator.SetTrigger(AttackParam);
        return true;
    }

    /// <summary>Called by an Animation Event on the attack clip's impact frame.</summary>
    public void DealHit()
    {
        Vector2 center = m_hitPoint != null ? m_hitPoint.position : transform.position;
        Physics2D.OverlapCircle(center, m_hitRadius, m_filter, m_overlaps);

        // One target can have several colliders; each should only be hit once per swing.
        m_damaged.Clear();

        for (int i = 0; i < m_overlaps.Count; i++)
        {
            IDamageable damageable = m_overlaps[i].GetComponentInParent<IDamageable>();
            if (damageable == null || m_damaged.Contains(damageable)) continue;

            m_damaged.Add(damageable);
            damageable.TakeDamage(new DamageInfo(m_enemy.Definition.AttackDamage, m_enemy.gameObject, null, center));
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.25f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(m_hitPoint != null ? m_hitPoint.position : transform.position, m_hitRadius);
    }
#endif
}
