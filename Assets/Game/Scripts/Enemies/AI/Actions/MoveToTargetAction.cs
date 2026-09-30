using UnityEngine;

/// <summary>
/// Walks straight at the target at the enemy's Move Speed, and stops on
/// leaving the state. Straight line only — no pathfinding around walls yet.
/// </summary>
[CreateAssetMenu(fileName = "Act_MoveToTarget", menuName = "Game/AI/Actions/Move To Target")]
public class MoveToTargetAction : AIAction
{
    public override void Tick(EnemyBrain brain)
    {
        if (brain.Target == null)
        {
            brain.Motor.Stop();
            return;
        }

        Vector2 toTarget = brain.Target.position - brain.transform.position;
        brain.Motor.Move(toTarget.normalized);
    }

    public override void Exit(EnemyBrain brain)
    {
        brain.Motor.Stop();
    }
}
