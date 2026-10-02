using UnityEngine;

/// <summary>
/// One kind of camera shake — a pistol kick, a hit taken, an explosion — and
/// how it behaves over its life. Callers only ever say "play this shake";
/// how long, how hard, and how fast are tuned here in the Inspector.
///
/// Both curves run on a 0–1 timeline (0 = the moment it starts, 1 = Duration
/// later), so changing Duration stretches the shape rather than cutting it off.
///
/// Setup:
///   1. Assets > Create > Game > Camera Shake.
///   2. Set Duration, then shape the Amplitude and Frequency curves.
/// </summary>
[CreateAssetMenu(fileName = "Shake_", menuName = "Game/Camera Shake")]
public class CameraShakeDefinition : ScriptableObject
{
    [Tooltip("How long the shake lasts, in seconds.")]
    [SerializeField, Min(0.01f)] private float m_duration = 0.15f;

    [Tooltip("How hard the camera shakes over the shake's life. Height is the noise Amplitude Gain: " +
             "0 = still, 1 = the camera's noise profile at full strength.")]
    [SerializeField] private AnimationCurve m_amplitude = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Tooltip("How fast the camera jitters over the shake's life. Height is the noise Frequency Gain: " +
             "1 = the noise profile's own speed, 2 = twice as fast.")]
    [SerializeField] private AnimationCurve m_frequency = AnimationCurve.Constant(0f, 1f, 1f);

    public float Duration => m_duration;

    /// <summary>Amplitude Gain at <paramref name="t"/>, from 0 (start) to 1 (end).</summary>
    public float AmplitudeAt(float t) => m_amplitude.Evaluate(t);

    /// <summary>Frequency Gain at <paramref name="t"/>, from 0 (start) to 1 (end).</summary>
    public float FrequencyAt(float t) => m_frequency.Evaluate(t);
}
