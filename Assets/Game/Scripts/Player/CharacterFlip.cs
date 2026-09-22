using UnityEngine;

/// <summary>
/// Mirrors this object left/right by rotating 180 degrees around Y instead of
/// negating scale, so it composes cleanly with the weapon's independent aim
/// rotation (see WeaponAimRotator). Unlike the Weapons object, this has no Z
/// rotation to compensate for, so it works regardless of which way the sprite
/// is drawn facing at rest.
///
/// Setup:
///   1. Add to the Character child of the Player prefab.
///   2. Drag the Player root's PlayerAim component into the Player Aim slot.
/// </summary>
public class CharacterFlip : MonoBehaviour
{
    [SerializeField] private PlayerAim m_playerAim;

    private void Update()
    {
        if (m_playerAim == null) return;
        transform.localEulerAngles = new Vector3(0f, m_playerAim.IsAimingLeft ? 180f : 0f, 0f);
    }
}
