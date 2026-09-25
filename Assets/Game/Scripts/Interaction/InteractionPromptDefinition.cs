using UnityEngine;

/// <summary>
/// What a prompt says and which button sprite it draws. Kept separate from the
/// interactable so several kinds of object can share one prompt — "Pick Up" is
/// the same word whether it is a pistol, a shotgun, or a relic.
///
/// Setup:
///   1. Assets > Create > Game > Interaction Prompt.
///   2. Set the Label ("Pick Up", "Open") and drag in a key-cap sprite.
/// </summary>
[CreateAssetMenu(fileName = "Prompt_", menuName = "Game/Interaction Prompt")]
public class InteractionPromptDefinition : ScriptableObject
{
    [Tooltip("Verb shown next to the glyph, e.g. 'Pick Up'.")]
    [SerializeField] private string m_label = "Interact";

    [Tooltip("Button sprite drawn beside the label, e.g. an E key cap.")]
    [SerializeField] private Sprite m_glyph;

    public string Label => m_label;
    public Sprite Glyph => m_glyph;
}
