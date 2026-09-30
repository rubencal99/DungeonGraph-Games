using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runs an enemy's AIState assets. It owns the per-enemy memory those shared
/// assets need (current state, target, attack cooldown) and nothing else — all
/// behavior is in the states, actions, and conditions it runs.
///
/// Two speeds, on purpose:
///   * Thinking (find the nearest player, check transitions) happens every
///     Think Interval, ten times a second by default, with each enemy's start
///     offset at random so a room of them never think on the same frame.
///   * Doing (the current state's actions) happens every frame, so movement
///     and aim stay smooth.
///
/// Setup:
///   1. Add to the enemy prefab root, alongside Enemy, EnemyMotor, and EnemyAim.
///   2. Put exactly one IEnemyAttack (MeleeAttack or RangedAttack) on the
///      prefab or one of its children.
///   3. Set the start state on the EnemyDefinition, not here.
/// </summary>
[RequireComponent(typeof(Enemy), typeof(EnemyMotor), typeof(EnemyAim))]
public class EnemyBrain : MonoBehaviour
{
    [Tooltip("Seconds between decisions. Lower reacts faster; higher is cheaper.")]
    [SerializeField] private float m_thinkInterval = 0.1f;

    public EnemyDefinition Definition => m_enemy.Definition;
    public EnemyMotor Motor { get; private set; }
    public EnemyAim Aim { get; private set; }
    public IEnemyAttack Attack { get; private set; }

    /// <summary>The nearest player, refreshed each think. Null when there is none.</summary>
    public Transform Target { get; private set; }

    /// <summary>Squared distance to Target as of the last think. Squared to skip a square root.</summary>
    public float TargetDistanceSqr { get; private set; }

    /// <summary>When the next attack is allowed. Written by AttackTargetAction.</summary>
    public float NextAttackTime { get; set; }

    public AIState CurrentState => m_state;

    private Enemy m_enemy;
    private AIState m_state;
    private float m_nextThinkTime;

    private void Awake()
    {
        m_enemy = GetComponent<Enemy>();
        Motor = GetComponent<EnemyMotor>();
        Aim = GetComponent<EnemyAim>();
        Attack = GetComponentInChildren<IEnemyAttack>();
    }

    // Runs on every pool reuse, so a recycled enemy forgets its last life.
    private void OnEnable()
    {
        Target = null;
        NextAttackTime = 0f;
        m_nextThinkTime = Time.time + Random.value * m_thinkInterval;
        ChangeState(Definition != null ? Definition.StartState : null);
    }

    // Lets the current state's actions clean up (stop moving, etc.).
    private void OnDisable()
    {
        ChangeState(null);
    }

    private void Update()
    {
        if (Time.time >= m_nextThinkTime)
        {
            m_nextThinkTime = Time.time + m_thinkInterval;
            Think();
        }

        if (m_state != null) m_state.Tick(this);
    }

    private void Think()
    {
        FindTarget();

        if (m_state == null) return;

        AIState next = m_state.Evaluate(this);
        if (next != null && next != m_state) ChangeState(next);
    }

    private void FindTarget()
    {
        IReadOnlyList<PlayerController> players = PlayerController.All;
        Vector2 position = transform.position;

        Target = null;
        TargetDistanceSqr = float.MaxValue;

        for (int i = 0; i < players.Count; i++)
        {
            float distanceSqr = ((Vector2)players[i].transform.position - position).sqrMagnitude;
            if (distanceSqr >= TargetDistanceSqr) continue;

            Target = players[i].transform;
            TargetDistanceSqr = distanceSqr;
        }
    }

    private void ChangeState(AIState next)
    {
        if (m_state != null) m_state.Exit(this);
        m_state = next;
        if (m_state != null) m_state.Enter(this);
    }

#if UNITY_EDITOR
    // Detection (yellow), attack (red), and give-up (grey) ranges, for tuning by eye.
    private void OnDrawGizmosSelected()
    {
        EnemyDefinition definition = GetComponent<Enemy>().Definition;
        if (definition == null) return;

        Vector3 position = transform.position;
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(position, definition.DetectionRange);
        Gizmos.color = new Color(1f, 0.25f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(position, definition.AttackRange);
        Gizmos.color = new Color(0.6f, 0.6f, 0.6f, 0.4f);
        Gizmos.DrawWireSphere(position, definition.LoseTargetRange);
    }
#endif
}
