using UnityEngine;

/// <summary>
/// Points the enemy's aim at the target, which flips its sprite and swings its
/// gun through the same CharacterFlip and WeaponAimRotator the player uses.
/// </summary>
[CreateAssetMenu(fileName = "Act_FaceTarget", menuName = "Game/AI/Actions/Face Target")]
public class FaceTargetAction : AIAction
{
    public override void Tick(EnemyBrain brain)
    {
        if (brain.Target != null) brain.Aim.AimAt(brain.Target.position);
    }
}
