using UnityEngine;

/// <summary>
/// Rotates the Weapons container so it points at the player's aim point, and
/// mirrors it (Y flip) when aiming left. Character/weapon art faces "right"
/// (local +X) at identity rotation. That's a different axis than the flip
/// (Y), so the flip alone would double back on the barrel's pointing
/// direction past the vertical — the extra 180 degrees subtracted from the
/// angle below is what cancels that out. (If art is ever redrawn to face
/// "up" (+Y) instead, this compensation is no longer needed — see the git
/// history on this file for that version.)
///
/// Setup:
///   1. Add to the "Weapons" child of the Player prefab.
///   2. Drag the Player root's PlayerAim component into the Player Aim slot.
/// </summary>
public class WeaponAimRotator : MonoBehaviour
{
    [SerializeField] private PlayerAim m_playerAim;

    private void Update()
    {
        if (m_playerAim == null) return;

        bool isFlipped = m_playerAim.IsAimingLeft;
        float angle = m_playerAim.AimAngleDegrees;
        float localZ = isFlipped ? angle - 180f : angle;

        Quaternion aim = Quaternion.Euler(0f, 0f, localZ);
        Quaternion flip = Quaternion.Euler(0f, isFlipped ? 180f : 0f, 0f);
        transform.localRotation = aim * flip;
    }
}
