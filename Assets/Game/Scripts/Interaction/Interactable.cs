using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base for anything the player can walk up to and press E on: a weapon lying
/// on the floor, a chest, later a lever or a teleporter.
///
/// It answers three questions — where is the prompt, how close must you be, and
/// can this particular character use me — and exposes one method that does the
/// thing. Adding a new kind of interactable is a new subclass plus an authored
/// prefab; the scanner, the prompt, and the input path are untouched.
///
/// Setup:
///   1. Put a subclass (WeaponPickup, Chest, ...) on the interactable prefab.
///   2. Drag a PromptAnchor prefab in as a child and assign its
///      InteractionPromptView to the Prompt View slot.
///   3. Assign an InteractionPromptDefinition asset to the Prompt slot.
/// </summary>
public abstract class Interactable : MonoBehaviour
{
    // Every enabled interactable, so PlayerInteractor can sweep a short list
    // instead of searching the scene or running a physics query each scan.
    private static readonly List<Interactable> s_all = new();

    public static IReadOnlyList<Interactable> All => s_all;

    [SerializeField] private InteractionPromptDefinition m_prompt;
    [SerializeField] private InteractionPromptView m_promptView;
    [SerializeField] private float m_range = 2.5f;

    [Tooltip("Played when this is successfully used — a chest creak, a lever clunk. Optional.")]
    [SerializeField] private AudioCueDefinition m_activateCue;

    public float RangeSquared => m_range * m_range;
    public float Range => m_range;
    public InteractionPromptDefinition Prompt => m_prompt;

    protected virtual void OnEnable()
    {
        s_all.Add(this);
        SetPromptVisible(false);
    }

    protected virtual void OnDisable()
    {
        s_all.Remove(this);
    }

    /// <summary>
    /// Whether this character could use this right now, ignoring distance. An
    /// opened chest or a full-and-identical pickup returns false, which is all
    /// it takes for the prompt to stop appearing.
    /// </summary>
    public abstract bool CanInteract(GameObject interactor);

    /// <summary>Does the thing. Called once per E press, after range and CanInteract pass.</summary>
    public abstract void Activate(GameObject interactor);

    /// <summary>
    /// Subclasses call this once Activate has actually done something, so a
    /// refused interaction stays silent.
    /// </summary>
    protected void PlayActivateCue()
    {
        AudioManager.Play(m_activateCue, transform.position);
    }

    /// <summary>Shows or hides this object's floating prompt.</summary>
    public virtual void SetPromptVisible(bool visible)
    {
        if (m_promptView != null) m_promptView.SetVisible(visible, m_prompt);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.42f, 0.18f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, m_range);
    }
#endif
}
