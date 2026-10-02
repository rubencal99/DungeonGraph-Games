using UnityEngine;

/// <summary>
/// Anything that points somewhere: the player's mouse (PlayerAim) or an
/// enemy's eyes (EnemyAim). Weapon, WeaponAimRotator, and CharacterFlip only
/// read this, so the same gun, rotator, and flip work in either hand.
///
/// A base class rather than an interface because Unity can serialize a
/// reference to it in the Inspector, and because the angle/side math below is
/// shared by every subclass.
/// </summary>
public abstract class AimSource : MonoBehaviour
{
    public Vector2 AimPoint { get; private set; }
    public float AimAngleDegrees { get; private set; }
    public Vector2 AimDirection { get; private set; } = Vector2.right;
    public bool IsAimingLeft { get; private set; }

    /// <summary>
    /// Points where another machine says this character is aiming. AimNetwork
    /// calls this every frame on copies of players and enemies this machine
    /// doesn't control.
    /// </summary>
    public void ApplyRemoteAim(Vector2 point) => SetAimPoint(point);

    /// <summary>Points at a world position and derives the angle, direction, and side from it.</summary>
    protected void SetAimPoint(Vector2 point)
    {
        AimPoint = point;

        Vector2 toAim = point - (Vector2)transform.position;
        AimAngleDegrees = Mathf.Atan2(toAim.y, toAim.x) * Mathf.Rad2Deg;
        AimDirection = toAim.sqrMagnitude > 0.0001f ? toAim.normalized : AimDirection;
        IsAimingLeft = point.x < transform.position.x;
    }
}
