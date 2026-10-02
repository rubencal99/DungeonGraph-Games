using UnityEngine;

/// <summary>
/// Shakes the camera every time this object's Health takes a hit. A separate
/// component rather than a field on Health, because Health is shared with every
/// enemy and only counts down and announces; what a hit looks like belongs to
/// whoever listens.
///
/// Setup:
///   1. Add to the Player prefab root, next to Health.
///   2. Assign a CameraShakeDefinition.
/// </summary>
[RequireComponent(typeof(Health))]
public class CameraShakeOnDamage : MonoBehaviour
{
    [SerializeField] private CameraShakeDefinition m_shake;

    private Health m_health;

    private void Awake()
    {
        m_health = GetComponent<Health>();
    }

    private void OnEnable()
    {
        m_health.Damaged += HandleDamaged;
    }

    private void OnDisable()
    {
        m_health.Damaged -= HandleDamaged;
    }

    private void HandleDamaged(DamageInfo damageInfo)
    {
        CameraShake.Play(m_shake);
    }
}
