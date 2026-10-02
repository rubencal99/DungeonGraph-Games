using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Copies where a character is aiming from the machine that decides it to every
/// other machine. The owner (a player's own machine, or the host for an enemy)
/// writes its aim point; everyone else feeds it into their copy of the same
/// AimSource. CharacterFlip and WeaponAimRotator only read AimSource, so the
/// sprite flip and gun angle follow with no changes of their own.
///
/// Setup:
///   1. Add to the root of the Player prefab and of every enemy prefab, next
///      to the NetworkObject.
///   2. Drag in the root's AimSource (PlayerAim or EnemyAim).
/// </summary>
public class AimNetwork : NetworkBehaviour
{
    [SerializeField] private AimSource m_aim;

    [Tooltip("Aim moves smaller than this, in world units, aren't sent. Saves bandwidth on a still mouse.")]
    [SerializeField, Min(0f)] private float m_sendThreshold = 0.05f;

    private readonly NetworkVariable<Vector2> m_aimPoint =
        new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private void Update()
    {
        if (!IsSpawned || m_aim == null) return;

        if (IsOwner)
        {
            if ((m_aim.AimPoint - m_aimPoint.Value).sqrMagnitude > m_sendThreshold * m_sendThreshold)
            {
                m_aimPoint.Value = m_aim.AimPoint;
            }
        }
        else
        {
            m_aim.ApplyRemoteAim(m_aimPoint.Value);
        }
    }
}
