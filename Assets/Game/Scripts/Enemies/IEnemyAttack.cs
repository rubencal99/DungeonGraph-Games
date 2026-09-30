using UnityEngine;

/// <summary>
/// How an enemy hurts the player. The AI only decides when (AttackTargetAction);
/// the component implementing this decides how. Swap MeleeAttack for
/// RangedAttack and the same brain becomes a different enemy.
/// </summary>
public interface IEnemyAttack
{
    /// <summary>Starts an attack at the target. Returns false if it could not attack right now.</summary>
    bool TryAttack(Transform target);
}
