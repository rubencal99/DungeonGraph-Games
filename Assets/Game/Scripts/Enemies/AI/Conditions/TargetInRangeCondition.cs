using UnityEngine;

/// <summary>
/// Is the target within one of the enemy's ranges? The distance itself comes
/// from each enemy's EnemyDefinition, so one "In Attack Range" asset serves a
/// melee enemy that swings at 1 unit and a ranged one that shoots from 7.
/// </summary>
[CreateAssetMenu(fileName = "Cond_TargetInRange", menuName = "Game/AI/Conditions/Target In Range")]
public class TargetInRangeCondition : AICondition
{
    public enum RangeType
    {
        Detection,
        Attack,
        LoseTarget
    }

    [Tooltip("Which of the EnemyDefinition's ranges to compare against.")]
    [SerializeField] private RangeType m_range = RangeType.Detection;

    public override bool Evaluate(EnemyBrain brain)
    {
        if (brain.Target == null) return false;

        float range = m_range switch
        {
            RangeType.Detection => brain.Definition.DetectionRange,
            RangeType.Attack => brain.Definition.AttackRange,
            RangeType.LoseTarget => brain.Definition.LoseTargetRange,
            _ => 0f
        };

        return brain.TargetDistanceSqr <= range * range;
    }
}
