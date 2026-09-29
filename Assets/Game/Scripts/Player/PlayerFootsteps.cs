using UnityEngine;

/// <summary>
/// Plays a footstep every stride the player actually covers. Driven by
/// distance travelled rather than a timer, so steps quicken with speed, slow
/// while accelerating, and stop the moment the player does.
///
/// Setup:
///   1. Add to the Player prefab root, next to PlayerController.
///   2. Assign a footstep AudioCueDefinition (several clip variations help).
///   3. Tune Stride Length until the steps match the walk animation.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerFootsteps : MonoBehaviour
{
    [SerializeField] private AudioCueDefinition m_footstepCue;

    [Tooltip("World units covered between two footsteps.")]
    [SerializeField, Min(0.1f)] private float m_strideLength = 1.2f;

    [Tooltip("Moving slower than this (units/second) counts as standing still, so drifting to a " +
             "stop makes no sound.")]
    [SerializeField, Min(0f)] private float m_minSpeed = 0.5f;

    private Rigidbody2D m_rb;
    private float m_distanceSinceStep;

    private void Awake()
    {
        m_rb = GetComponent<Rigidbody2D>();
    }

    private void FixedUpdate()
    {
        float speed = m_rb.linearVelocity.magnitude;

        if (speed < m_minSpeed)
        {
            // Half a stride banked, so the first step after standing still
            // lands quickly rather than a full stride later.
            m_distanceSinceStep = m_strideLength * 0.5f;
            return;
        }

        m_distanceSinceStep += speed * Time.fixedDeltaTime;
        if (m_distanceSinceStep < m_strideLength) return;

        m_distanceSinceStep -= m_strideLength;
        AudioManager.Play(m_footstepCue, transform.position);
    }
}
