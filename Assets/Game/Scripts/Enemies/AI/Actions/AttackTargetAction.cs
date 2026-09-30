using UnityEngine;

/// <summary>
/// Attacks the target whenever the enemy's Attack Cooldown allows. How it
/// attacks is not decided here: it hands off to whatever IEnemyAttack the
/// prefab carries (MeleeAttack, RangedAttack). That is why a melee and a
/// ranged enemy can share this asset and the whole state tree around it.
/// </summary>
[CreateAssetMenu(fileName = "Act_AttackTarget", menuName = "Game/AI/Actions/Attack Target")]
public class AttackTargetAction : AIAction
{
    public override void Tick(EnemyBrain brain)
    {
        if (brain.Target == null || brain.Attack == null) return;
        if (Time.time < brain.NextAttackTime) return;

        if (brain.Attack.TryAttack(brain.Target))
        {
            brain.NextAttackTime = Time.time + brain.Definition.AttackCooldown;
        }
    }
}
