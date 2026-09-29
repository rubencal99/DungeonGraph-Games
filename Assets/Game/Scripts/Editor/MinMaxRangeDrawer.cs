using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector drawing for [MinMaxRange]: a number box for the low end, the
/// two-handled slider, and a number box for the high end, on one line.
/// </summary>
[CustomPropertyDrawer(typeof(MinMaxRangeAttribute))]
public class MinMaxRangeDrawer : PropertyDrawer
{
    private const float FieldWidth = 44f;
    private const float Gap = 4f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.Vector2)
        {
            EditorGUI.LabelField(position, label.text, "[MinMaxRange] only works on a Vector2.");
            return;
        }

        MinMaxRangeAttribute range = (MinMaxRangeAttribute)attribute;

        EditorGUI.BeginProperty(position, label, property);
        Rect content = EditorGUI.PrefixLabel(position, label);

        int indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        Rect minRect = new Rect(content.x, content.y, FieldWidth, content.height);
        Rect maxRect = new Rect(content.xMax - FieldWidth, content.y, FieldWidth, content.height);
        Rect sliderRect = new Rect(minRect.xMax + Gap, content.y, content.width - 2f * (FieldWidth + Gap), content.height);

        Vector2 value = property.vector2Value;
        float min = value.x;
        float max = value.y;

        EditorGUI.BeginChangeCheck();
        min = EditorGUI.FloatField(minRect, min);
        EditorGUI.MinMaxSlider(sliderRect, ref min, ref max, range.Min, range.Max);
        max = EditorGUI.FloatField(maxRect, max);

        if (EditorGUI.EndChangeCheck())
        {
            // Rounded to hundredths so dragging does not leave 0.9483721 behind.
            min = Mathf.Round(Mathf.Clamp(min, range.Min, range.Max) * 100f) / 100f;
            max = Mathf.Round(Mathf.Clamp(max, min, range.Max) * 100f) / 100f;
            property.vector2Value = new Vector2(min, max);
        }

        EditorGUI.indentLevel = indent;
        EditorGUI.EndProperty();
    }
}
