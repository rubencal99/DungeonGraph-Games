using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The floating "E — Pick Up" popup on an interactable. Turns itself on when
/// the player's scanner selects this object and off when it moves on.
///
/// Entirely local presentation: it populates the authored Image and text, it
/// never builds them, and it decides nothing.
///
/// The three serialized fields keep their original capitalisation (m_Root, not
/// m_root) because the authored PromptAnchor prefab is already wired to those
/// names. Renaming them would silently blank the prefab's references.
///
/// There is deliberately no Awake here. Hiding the root in Awake looks harmless
/// but is not: Interactable.OnEnable hides this canvas, and Unity does not run
/// Awake on an inactive object. The first SetActive(true) would therefore run
/// Awake for the first time and hide the prompt in the same frame it was asked
/// to appear -- and only when the interactable happened to be initialised first,
/// which made it look intermittent.
///
/// Setup:
///   1. Already wired on Assets/Game/Prefabs/UI/PromptAnchor.prefab.
///   2. Drag that prefab in as a child of any interactable and assign this
///      component to the Interactable's Prompt View slot.
/// </summary>
public class InteractionPromptView : MonoBehaviour
{
    [Tooltip("The object toggled on and off. Usually the world-space Canvas itself.")]
    [SerializeField] private GameObject m_Root;

    [Tooltip("Image showing the button glyph.")]
    [SerializeField] private Image m_GlyphImage;

    [Tooltip("Optional text beside the glyph. Leave empty for a glyph-only prompt.")]
    [SerializeField] private TMP_Text m_LabelText;

    /// <summary>
    /// The object switched on and off. Falls back to this GameObject so a prompt
    /// authored without an explicit root still works.
    /// </summary>
    private GameObject Root => m_Root != null ? m_Root : gameObject;

    /// <summary>Shows or hides this prompt. Called by Interactable.SetPromptVisible.</summary>
    public void SetVisible(bool visible, InteractionPromptDefinition prompt)
    {
        Root.SetActive(visible);
        if (!visible || prompt == null) return;

        if (m_LabelText != null) m_LabelText.text = prompt.Label;

        if (m_GlyphImage != null)
        {
            m_GlyphImage.sprite = prompt.Glyph;
            m_GlyphImage.enabled = prompt.Glyph != null;
        }
    }
}
