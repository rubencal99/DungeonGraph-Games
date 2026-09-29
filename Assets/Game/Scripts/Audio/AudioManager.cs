using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plays every sound in the game through a fixed set of pre-made AudioSources
/// ("voices"), so a burst of gunfire never creates or destroys anything.
///
/// Think of it as a fixed number of speakers. Each request either gets a free
/// speaker, takes one from something less important, or is skipped. Rules for
/// each sound — its cap on copies, its minimum gap, its priority — live on its
/// AudioCueDefinition, so this class never special-cases a sound.
///
/// Callers use the static <see cref="Play"/>, which is safe to call with a null
/// cue, so an unassigned sound slot is silent rather than an error.
///
/// Setup:
///   1. Create an empty GameObject named "AudioManager", add this component,
///      and save it as a prefab so every scene uses the same one.
///   2. Set Voice Count to match Project Settings > Audio > Max Real Voices.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Tooltip("How many sounds can play at once. Keep it equal to Project Settings > Audio > " +
             "Max Real Voices, so this pool and Unity agree on the budget.")]
    [SerializeField, Min(1)] private int m_voiceCount = 32;

    private sealed class Voice
    {
        public AudioSource Source;
        public Transform Transform;
        public AudioCueDefinition Cue;
        public float StartTime;
        public float EndTime;
    }

    // Per-cue play-time memory, kept here rather than on the asset so it
    // starts clean every play session.
    private sealed class CueState
    {
        public float LastPlayTime = float.NegativeInfinity;
        public int LastClipIndex = -1;
    }

    private Voice[] m_voices;
    private readonly Dictionary<AudioCueDefinition, CueState> m_cueStates = new();

    private static bool s_warnedMissing;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"A second {nameof(AudioManager)} was found on '{name}' and removed. Keep one per scene.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        CreateVoices();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Plays a cue at a world position. Position is ignored for cues with
    /// Spatial off. Does nothing for a null cue.
    /// </summary>
    public static void Play(AudioCueDefinition cue, Vector3 position)
    {
        if (cue == null) return;

        if (Instance == null)
        {
            if (!s_warnedMissing)
            {
                Debug.LogWarning($"'{cue.name}' was asked to play, but there is no {nameof(AudioManager)} in the " +
                                 "scene. Drag the AudioManager prefab in. (Shown once.)");
                s_warnedMissing = true;
            }
            return;
        }

        Instance.PlayInternal(cue, position);
    }

    private void PlayInternal(AudioCueDefinition cue, Vector3 position)
    {
        if (!cue.HasClips) return;

        // Unscaled so a paused or slowed game still frees voices on time;
        // audio itself does not follow timeScale.
        float now = Time.unscaledTime;
        CueState state = GetState(cue);

        if (now - state.LastPlayTime < cue.MinInterval) return;

        Voice voice = FindVoice(cue, now);
        if (voice == null) return;

        AudioClip clip = cue.PickClip(ref state.LastClipIndex);
        if (clip == null) return;

        state.LastPlayTime = now;
        StartVoice(voice, cue, clip, position, now);
    }

    /// <summary>
    /// Which voice this request gets, or null to skip it. In order: if the cue
    /// is at its own cap, restart its oldest copy; otherwise any free voice;
    /// otherwise the least important busy voice, if it matters no more than
    /// this one.
    /// </summary>
    private Voice FindVoice(AudioCueDefinition cue, float now)
    {
        Voice free = null;
        Voice oldestSameCue = null;
        Voice weakest = null;
        int sameCueCount = 0;

        for (int i = 0; i < m_voices.Length; i++)
        {
            Voice voice = m_voices[i];

            if (voice.EndTime <= now)
            {
                free ??= voice;
                continue;
            }

            if (voice.Cue == cue)
            {
                sameCueCount++;
                if (oldestSameCue == null || voice.StartTime < oldestSameCue.StartTime) oldestSameCue = voice;
            }

            if (weakest == null || IsWeaker(voice, weakest)) weakest = voice;
        }

        if (cue.MaxInstances > 0 && sameCueCount >= cue.MaxInstances) return oldestSameCue;
        if (free != null) return free;

        return weakest.Cue.Priority <= cue.Priority ? weakest : null;
    }

    /// <summary>Lower priority loses; between equals, the one that has played longest loses.</summary>
    private static bool IsWeaker(Voice a, Voice b)
    {
        if (a.Cue.Priority != b.Cue.Priority) return a.Cue.Priority < b.Cue.Priority;
        return a.StartTime < b.StartTime;
    }

    private static void StartVoice(Voice voice, AudioCueDefinition cue, AudioClip clip, Vector3 position, float now)
    {
        float pitch = cue.RollPitch();
        float delay = cue.RollDelay();
        Vector2 distance = cue.Distance;

        AudioSource source = voice.Source;
        source.Stop();
        source.clip = clip;
        source.volume = cue.RollVolume();
        source.pitch = pitch;
        source.outputAudioMixerGroup = cue.MixerGroup;
        source.spatialBlend = cue.Spatial ? 1f : 0f;
        source.minDistance = distance.x;
        source.maxDistance = Mathf.Max(distance.y, distance.x + 0.01f);
        voice.Transform.position = position;

        if (delay > 0f) source.PlayDelayed(delay);
        else source.Play();

        // Tracked by clock rather than AudioSource.isPlaying, which behaves
        // inconsistently during a delayed start. Pitch speeds the clip up or
        // slows it down, so it scales the length.
        voice.Cue = cue;
        voice.StartTime = now;
        voice.EndTime = now + delay + clip.length / pitch;
    }

    private CueState GetState(AudioCueDefinition cue)
    {
        if (!m_cueStates.TryGetValue(cue, out CueState state))
        {
            state = new CueState();
            m_cueStates.Add(cue, state);
        }

        return state;
    }

    private void CreateVoices()
    {
        m_voices = new Voice[m_voiceCount];

        for (int i = 0; i < m_voiceCount; i++)
        {
            GameObject go = new GameObject($"Voice {i}");
            go.transform.SetParent(transform, false);

            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.rolloffMode = AudioRolloffMode.Linear; // silent exactly at max distance; the default curve never quite reaches zero
            source.dopplerLevel = 0f;                     // no pitch warble from a moving camera

            m_voices[i] = new Voice
            {
                Source = source,
                Transform = go.transform,
                EndTime = float.NegativeInfinity,
            };
        }
    }
}
