using System;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Shakes the camera it sits on whenever anything in the game asks. Callers use
/// the static <see cref="Play"/> and never need a reference to the camera; every
/// enabled CameraShake subscribes to those requests. That is what lets a gun, a
/// hit, or later an explosion shake the screen without knowing the camera exists
/// — and the camera is spawned at runtime, so nothing could hold a reference to
/// it ahead of time anyway.
///
/// It works by turning two dials on Cinemachine's noise component: Amplitude Gain
/// (how far) and Frequency Gain (how fast). Whatever those dials are set to in the
/// Inspector is the camera's resting state, restored once the last shake ends.
///
/// When several shakes overlap, the strongest one at that moment drives both
/// dials. They are not added together, so a full-auto weapon holds a steady
/// shake instead of building into chaos. Asking for a shake that is already
/// playing restarts it rather than stacking a second copy.
///
/// Callers use <see cref="Play"/>, which is safe with a null shake, so an
/// unassigned slot is simply no shake.
///
/// Setup:
///   1. Add to the CinemachineCamera prefab. This also adds Cinemachine Basic
///      Multi Channel Perlin.
///   2. On that Perlin component, pick a Noise Profile and set its resting
///      Amplitude Gain (0 for a still camera) and Frequency Gain.
/// </summary>
[RequireComponent(typeof(CinemachineBasicMultiChannelPerlin))]
public class CameraShake : MonoBehaviour
{
    private static event Action<CameraShakeDefinition> s_requested;

    private struct ActiveShake
    {
        public CameraShakeDefinition Definition;
        public float Elapsed;
    }

    // Sized for a busy moment so it never grows mid-fight; duplicate requests
    // restart rather than add, so this only holds distinct shakes.
    private readonly List<ActiveShake> m_active = new(8);

    private CinemachineBasicMultiChannelPerlin m_noise;
    private float m_restingAmplitude;
    private float m_restingFrequency;

    /// <summary>Shakes every listening camera. Does nothing for a null shake.</summary>
    public static void Play(CameraShakeDefinition shake)
    {
        if (shake == null) return;
        s_requested?.Invoke(shake);
    }

    private void Awake()
    {
        m_noise = GetComponent<CinemachineBasicMultiChannelPerlin>();
        m_restingAmplitude = m_noise.AmplitudeGain;
        m_restingFrequency = m_noise.FrequencyGain;
    }

    private void OnEnable()
    {
        s_requested += Add;
    }

    private void OnDisable()
    {
        s_requested -= Add;
        m_active.Clear();
        Rest();
    }

    private void Add(CameraShakeDefinition shake)
    {
        for (int i = 0; i < m_active.Count; i++)
        {
            if (m_active[i].Definition != shake) continue;

            m_active[i] = new ActiveShake { Definition = shake };
            return;
        }

        m_active.Add(new ActiveShake { Definition = shake });
    }

    private void Update()
    {
        if (m_active.Count == 0) return;

        float strongest = float.NegativeInfinity;
        float frequency = m_restingFrequency;

        // Backwards, so a finished shake can be swapped out with the last entry
        // (already visited) without skipping anything.
        for (int i = m_active.Count - 1; i >= 0; i--)
        {
            ActiveShake shake = m_active[i];
            shake.Elapsed += Time.deltaTime;

            if (shake.Elapsed >= shake.Definition.Duration)
            {
                int last = m_active.Count - 1;
                m_active[i] = m_active[last];
                m_active.RemoveAt(last);
                continue;
            }

            m_active[i] = shake;

            float t = shake.Elapsed / shake.Definition.Duration;
            float amplitude = shake.Definition.AmplitudeAt(t);

            // Frequency follows whichever shake is strongest, so a gentle buzz
            // never makes a big explosion jitter at the buzz's speed.
            if (amplitude > strongest)
            {
                strongest = amplitude;
                frequency = shake.Definition.FrequencyAt(t);
            }
        }

        if (m_active.Count == 0)
        {
            Rest();
            return;
        }

        m_noise.AmplitudeGain = strongest;
        m_noise.FrequencyGain = frequency;
    }

    private void Rest()
    {
        m_noise.AmplitudeGain = m_restingAmplitude;
        m_noise.FrequencyGain = m_restingFrequency;
    }
}
