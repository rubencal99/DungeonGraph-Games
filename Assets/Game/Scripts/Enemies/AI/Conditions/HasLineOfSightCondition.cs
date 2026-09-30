using UnityEngine;

/// <summary>
/// Can the enemy see its target, or is a wall in the way? One line cast per
/// check, only against the obstacle layers, so it costs next to nothing at the
/// brain's think rate.
/// </summary>
[CreateAssetMenu(fileName = "Cond_HasLineOfSight", menuName = "Game/AI/Conditions/Has Line Of Sight")]
public class HasLineOfSightCondition : AICondition
{
    [Tooltip("Layers that block sight — the dungeon's wall tilemap. Leave the Player and Enemy " +
             "layers out, or the target blocks its own view.")]
    [SerializeField] private LayerMask m_obstacles;

    public override bool Evaluate(EnemyBrain brain)
    {
        if (brain.Target == null) return false;

        return Physics2D.Linecast(brain.transform.position, brain.Target.position, m_obstacles).collider == null;
    }
}
