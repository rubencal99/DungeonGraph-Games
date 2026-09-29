using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// One sound the game can make — a pistol shot, a footstep, a chest creak —
/// and every rule for how it plays. Callers only ever say "play this cue here";
/// which clip, how loud, how high, and whether it is even allowed to play are
/// all decided by this asset, so tuning is an Inspector job.
///
/// Randomization is rolled fresh on every play. Ten pistol shots in a row get
/// ten slightly different pitches and volumes, which is what stops a repeated
/// sound from sounding like a machine.
///
/// Setup:
///   1. Assets > Create > Game > Audio Cue.
///   2. Drag in one or more clips (variations of the same sound).
///   3. Set the mixer group and tune the ranges.
/// </summary>
[CreateAssetMenu(fileName = "Cue_", menuName = "Game/Audio Cue")]
public class AudioCueDefinition : ScriptableObject
{
    [Header("Clips")]
    [Tooltip("Variations of this sound. One is picked at random each play.")]
    [SerializeField] private AudioClip[] m_clips;

    [Tooltip("Never pick the same clip twice in a row. Needs at least two clips.")]
    [SerializeField] private bool m_avoidRepeat = true;

    [Header("Randomization (rolled fresh every play)")]
    [Tooltip("Loudness is picked between these two, 0 = silent, 1 = the clip's full level.")]
    [MinMaxRange(0f, 1f)]
    [SerializeField] private Vector2 m_volume = new Vector2(0.9f, 1f);

    [Tooltip("Pitch is picked between these two. 1 = unchanged, 0.5 = an octave down, 2 = an octave up. " +
             "A narrow band like 0.95–1.05 is enough to stop repeats sounding identical.")]
    [MinMaxRange(0.1f, 3f)]
    [SerializeField] private Vector2 m_pitch = new Vector2(0.95f, 1.05f);

    [Tooltip("Random wait before the sound starts, in seconds. A few milliseconds keeps identical " +
             "sounds from landing on the exact same instant, which can make them hollow and phasey.")]
    [MinMaxRange(0f, 0.25f)]
    [SerializeField] private Vector2 m_delay = Vector2.zero;

    [Header("Output")]
    [Tooltip("Which mixer channel this plays through, e.g. SFX/Weapons. Controls its volume slider.")]
    [SerializeField] private AudioMixerGroup m_mixerGroup;

    [Tooltip("On: heard from where it happens — panned left/right and quieter further away. " +
             "Off: flat and centred, for UI and other sounds that have no place in the world.")]
    [SerializeField] private bool m_spatial = true;

    [Tooltip("Spatial only. Full volume inside the low value, silent beyond the high value, in world units.")]
    [MinMaxRange(0f, 60f)]
    [SerializeField] private Vector2 m_distance = new Vector2(5f, 25f);

    [Header("Budget")]
    [Tooltip("When every voice is busy, this sound may take over a voice playing something with a " +
             "lower priority. Player and UI sounds high, other players' gunfire lower.")]
    [Range(0, 100)]
    [SerializeField] private int m_priority = 50;

    [Tooltip("Most copies of this cue that may play at once. When full, the oldest copy is cut and " +
             "restarted instead. 0 = no cap.")]
    [SerializeField, Min(0)] private int m_maxInstances = 4;

    [Tooltip("Requests arriving sooner than this after the last play are dropped, in seconds. Stops a " +
             "burst of identical requests on one frame stacking into one loud spike.")]
    [SerializeField, Min(0f)] private float m_minInterval = 0.03f;

    public bool HasClips => m_clips != null && m_clips.Length > 0;
    public AudioMixerGroup MixerGroup => m_mixerGroup;
    public bool Spatial => m_spatial;
    public Vector2 Distance => m_distance;
    public int Priority => m_priority;
    public int MaxInstances => m_maxInstances;
    public float MinInterval => m_minInterval;

    public float RollVolume() => Random.Range(m_volume.x, m_volume.y);
    public float RollPitch() => Random.Range(m_pitch.x, m_pitch.y);
    public float RollDelay() => Random.Range(m_delay.x, m_delay.y);

    /// <summary>
    /// A random clip, skipping the one played last time when Avoid Repeat is on.
    /// The caller owns <paramref name="lastIndex"/> so no play-time state is
    /// stored on this asset.
    /// </summary>
    public AudioClip PickClip(ref int lastIndex)
    {
        if (!HasClips) return null;

        int count = m_clips.Length;
        int index;

        if (count == 1)
        {
            index = 0;
        }
        else if (m_avoidRepeat && lastIndex >= 0 && lastIndex < count)
        {
            // Roll among the other count-1 clips, then step over the last one.
            index = Random.Range(0, count - 1);
            if (index >= lastIndex) index++;
        }
        else
        {
            index = Random.Range(0, count);
        }

        lastIndex = index;
        return m_clips[index];
    }
}
