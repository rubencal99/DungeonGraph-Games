using UnityEngine;

/// <summary>
/// A yes/no question a transition asks: is the player close, can I see them,
/// am I stunned. Checked a few times a second (the brain's think rate), not
/// every frame, so a condition may do a raycast without hurting.
///
/// Conditions are shared assets, so they must not store per-enemy data in fields.
/// </summary>
public abstract class AICondition : ScriptableObject
{
    public abstract bool Evaluate(EnemyBrain brain);
}
