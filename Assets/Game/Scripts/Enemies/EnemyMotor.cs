using UnityEngine;

/// <summary>
/// An enemy's legs. AI actions say which way to go (Move) or to halt (Stop);
/// this eases the Rigidbody2D toward that at the definition's speed and
/// acceleration, the same way PlayerController moves the player.
///
/// Setup:
///   1. Add to the enemy prefab root with a Rigidbody2D (Dynamic, Gravity Scale 0,
///      Freeze Rotation Z) and a non-trigger Collider2D.
///   2. Optional: drag the Character's Animator into the Animator slot to drive
///      its "Speed" float (0 = idle, 1 = full move speed).
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Enemy))]
public class EnemyMotor : MonoBehaviour
{
    private static readonly int SpeedParam = Animator.StringToHash("Speed");

    [SerializeField] private Animator m_animator;

    private Rigidbody2D m_rb;
    private Enemy m_enemy;
    private Vector2 m_direction;

    private void Awake()
    {
        m_rb = GetComponent<Rigidbody2D>();
        m_rb.gravityScale = 0f;
        m_rb.freezeRotation = true;
        m_enemy = GetComponent<Enemy>();
    }

    // A pooled enemy must not wake up still sliding from its last life.
    private void OnDisable()
    {
        m_direction = Vector2.zero;
        m_rb.linearVelocity = Vector2.zero;
    }

    /// <summary>Walk this way at full speed. Pass a normalized direction.</summary>
    public void Move(Vector2 direction) => m_direction = direction;

    public void Stop() => m_direction = Vector2.zero;

    private void FixedUpdate()
    {
        EnemyDefinition definition = m_enemy.Definition;
        if (definition == null) return;

        Vector2 targetVelocity = m_direction * definition.MoveSpeed;
        m_rb.linearVelocity = Vector2.MoveTowards(m_rb.linearVelocity, targetVelocity, definition.Acceleration * Time.fixedDeltaTime);

        if (m_animator != null && definition.MoveSpeed > 0f)
        {
            m_animator.SetFloat(SpeedParam, m_rb.linearVelocity.magnitude / definition.MoveSpeed);
        }
    }
}
