using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Mirrors this object left/right by rotating 180 degrees around Y instead of
/// negating scale, so it composes cleanly with the weapon's independent aim
/// rotation (see WeaponAimRotator). Unlike the Weapons object, this has no Z
/// rotation to compensate for, so it works regardless of which way the sprite
/// is drawn facing at rest.
///
/// Setup:
///   1. Add to the Character child of the Player prefab.
///   2. Drag the Player root's PlayerAim component into the Aim slot
///      (an enemy's EnemyAim works the same way).
/// </summary>
public class CharacterFlip : MonoBehaviour
{
    [FormerlySerializedAs("m_playerAim")]
    [SerializeField] private AimSource m_aim;

    private void Update()
    {
        if (m_aim == null) return;
        transform.localEulerAngles = new Vector3(0f, m_aim.IsAimingLeft ? 180f : 0f, 0f);
    }
}
