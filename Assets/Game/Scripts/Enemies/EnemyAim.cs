using UnityEngine;

/// <summary>
/// The enemy's version of PlayerAim: instead of following a mouse, it looks
/// wherever the AI tells it (FaceTargetAction). Because it is an AimSource,
/// CharacterFlip, WeaponAimRotator, and Weapon work on an enemy unchanged.
///
/// Setup:
///   1. Add to the enemy prefab root.
///   2. Drag it into the Aim slot of the Character's CharacterFlip and, for a
///      ranged enemy, the Weapons child's WeaponAimRotator.
/// </summary>
public class EnemyAim : AimSource
{
    // Face right on every spawn until the AI has something to look at.
    private void OnEnable()
    {
        SetAimPoint((Vector2)transform.position + Vector2.right);
    }

    public void AimAt(Vector2 point) => SetAimPoint(point);
}
