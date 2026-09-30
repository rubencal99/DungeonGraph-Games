using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One state of an enemy's brain (Idle, Chase, Attack, ...): what it does
/// while in this state, and when to leave.
///
/// A state is an asset, not code. Its behavior is a list of AIAction assets
/// run top to bottom every frame; its exits are a list of transitions, each a
/// set of AICondition assets that must all be true. Giving an enemy a new
/// habit is dragging assets into these lists, and two enemy types share a
/// state simply by pointing at the same asset.
///
/// States hold no per-enemy data — fifty enemies share one Chase asset. Anything
/// that varies per enemy lives on its EnemyBrain or EnemyDefinition.
///
/// Setup:
///   Assets > Create > Game > AI > State, then fill Actions and Transitions.
/// </summary>
[CreateAssetMenu(fileName = "AIState_", menuName = "Game/AI/State")]
public class AIState : ScriptableObject
{
    [Tooltip("Run top to bottom every frame while in this state. Order matters: put Face Target " +
             "before Attack so the shot goes where the enemy is looking.")]
    [SerializeField] private List<AIAction> m_actions = new();

    [Tooltip("Checked top to bottom; the first one whose conditions all pass wins. For an OR, add " +
             "two transitions to the same state.")]
    [SerializeField] private List<AITransition> m_transitions = new();

    public void Enter(EnemyBrain brain)
    {
        for (int i = 0; i < m_actions.Count; i++)
        {
            if (m_actions[i] != null) m_actions[i].Enter(brain);
        }
    }

    public void Tick(EnemyBrain brain)
    {
        for (int i = 0; i < m_actions.Count; i++)
        {
            if (m_actions[i] != null) m_actions[i].Tick(brain);
        }
    }

    public void Exit(EnemyBrain brain)
    {
        for (int i = 0; i < m_actions.Count; i++)
        {
            if (m_actions[i] != null) m_actions[i].Exit(brain);
        }
    }

    /// <summary>The state to switch to, or null to stay put.</summary>
    public AIState Evaluate(EnemyBrain brain)
    {
        for (int i = 0; i < m_transitions.Count; i++)
        {
            if (m_transitions[i].IsMet(brain)) return m_transitions[i].TargetState;
        }

        return null;
    }
}

/// <summary>A way out of a state: go to Target State when every condition passes.</summary>
[Serializable]
public class AITransition
{
    [Tooltip("All must pass. An empty list never passes, so a half-authored row can't fire by accident.")]
    public List<AIConditionEntry> Conditions = new();

    public AIState TargetState;

    public bool IsMet(EnemyBrain brain)
    {
        if (TargetState == null || Conditions.Count == 0) return false;

        for (int i = 0; i < Conditions.Count; i++)
        {
            if (!Conditions[i].Evaluate(brain)) return false;
        }

        return true;
    }
}

/// <summary>
/// A condition plus a Negate tick box, so "out of range" or "can't see the
/// player" reuse the same asset as their opposite instead of needing a twin.
/// </summary>
[Serializable]
public struct AIConditionEntry
{
    public AICondition Condition;

    [Tooltip("Pass when the condition is false instead.")]
    public bool Negate;

    public bool Evaluate(EnemyBrain brain)
    {
        if (Condition == null) return false;

        return Condition.Evaluate(brain) != Negate;
    }
}
