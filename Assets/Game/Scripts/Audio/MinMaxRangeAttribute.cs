using UnityEngine;

/// <summary>
/// Draws a Vector2 field as a two-handled slider, with X as the low end and Y
/// as the high end. Used wherever a value is rolled at random between two
/// bounds, so the range reads as a range in the Inspector rather than as two
/// unrelated numbers.
///
/// Usage: [MinMaxRange(0f, 1f)] [SerializeField] private Vector2 m_volume;
/// </summary>
public class MinMaxRangeAttribute : PropertyAttribute
{
    public readonly float Min;
    public readonly float Max;

    public MinMaxRangeAttribute(float min, float max)
    {
        Min = min;
        Max = max;
    }
}
