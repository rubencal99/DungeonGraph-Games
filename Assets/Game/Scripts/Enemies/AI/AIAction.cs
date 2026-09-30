using UnityEngine;

/// <summary>
/// Something an enemy does while in a state: move, turn, attack. Subclasses
/// override whichever of Enter / Tick / Exit they need.
///
/// Actions are shared assets, so they must not store per-enemy data in fields.
/// Read and write it through the EnemyBrain passed in instead.
/// </summary>
public abstract class AIAction : ScriptableObject
{
    /// <summary>Once, when the state starts.</summary>
    public virtual void Enter(EnemyBrain brain) { }

    /// <summary>Every frame while the state is active.</summary>
    public virtual void Tick(EnemyBrain brain) { }

    /// <summary>Once, when the state ends — including when the enemy despawns.</summary>
    public virtual void Exit(EnemyBrain brain) { }
}
