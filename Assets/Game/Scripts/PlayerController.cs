using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Basic top-down 2D WASD player controller driven by an Input Action Reference.
///
/// Setup:
///   1. Add this component to your Player prefab alongside a Rigidbody2D.
///   2. Set the Rigidbody2D Gravity Scale to 0 and Collision Detection to Continuous.
///   3. Create an Input Action Asset with a "Move" action (Value, Vector2).
///   4. Drag that action's reference into the Move Action slot.
///   5. Optional: drag the Character's Animator into the Animator slot to drive
///      its "Speed" float (0 = idle, 1 = full move speed).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    private static readonly int SpeedParam = Animator.StringToHash("Speed");

    // Every player in the level, so enemies can pick the nearest one from a list of
    // at most four instead of searching the scene. Registered for the object's
    // whole life rather than while enabled: on a teammate's copy this component is
    // switched off (it reads this machine's keyboard), but the host's enemies must
    // still be able to chase that teammate.
    private static readonly List<PlayerController> s_all = new();

    public static IReadOnlyList<PlayerController> All => s_all;

    [SerializeField] private InputActionReference m_moveAction;
    [SerializeField] private float m_moveSpeed = 5f;
    [SerializeField] private float m_acceleration = 30f;
    [SerializeField] private float m_deceleration = 30f;
    [SerializeField] private Animator m_animator;

    private Rigidbody2D m_rb;

    private void Awake()
    {
        m_rb = GetComponent<Rigidbody2D>();
        m_rb.gravityScale  = 0f;
        m_rb.freezeRotation = true;
        s_all.Add(this);
    }

    private void OnDestroy()
    {
        s_all.Remove(this);
    }

    private void FixedUpdate()
    {
        if (m_moveAction == null) return;

        Vector2 input = m_moveAction.action.ReadValue<Vector2>();
        Vector2 targetVelocity = input * m_moveSpeed;
        float rate = input.sqrMagnitude > 0.0001f ? m_acceleration : m_deceleration;

        m_rb.linearVelocity = Vector2.MoveTowards(m_rb.linearVelocity, targetVelocity, rate * Time.fixedDeltaTime);

        m_animator?.SetFloat(SpeedParam, m_rb.linearVelocity.magnitude / m_moveSpeed);
    }
}
