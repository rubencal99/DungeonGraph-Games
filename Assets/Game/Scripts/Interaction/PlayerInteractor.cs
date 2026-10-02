using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Works out which interactable the player is standing next to, lights that
/// object's prompt, and runs it on an E press.
///
/// The proximity check is a plain squared-distance compare over Interactable.All,
/// not a physics overlap. With a handful of interactables in a room that is
/// cheaper, allocates nothing, and needs no trigger collider on every prefab.
/// It runs ten times a second rather than every frame — well under the time it
/// takes to walk into range.
///
/// Setup:
///   1. Add to the Player prefab root, alongside PlayerController.
///   2. Drag the "Interact" action (Player map) into the Interact Action slot.
/// </summary>
public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private InputActionReference m_interactAction;
    [SerializeField] private float m_scanInterval = 0.1f;

    private Interactable m_current;
    private float m_nextScanTime;

    /// <summary>The interactable an E press would trigger right now, or null.</summary>
    public Interactable CurrentTarget => m_current;

    private void OnEnable()
    {
        if (m_interactAction == null) return;
        m_interactAction.action.performed += OnInteractPerformed;
    }

    private void OnDisable()
    {
        if (m_interactAction != null) m_interactAction.action.performed -= OnInteractPerformed;

        // Leave no prompt burning on whatever we were standing next to.
        SetTarget(null);
    }

    private void Update()
    {
        if (Time.time < m_nextScanTime) return;

        m_nextScanTime = Time.time + m_scanInterval;
        SetTarget(FindNearestInRange());
    }

    /// <summary>
    /// Closest interactable that is inside its own range and available to us.
    /// Range lives on the interactable, so a chest can be reachable from further
    /// away than a dropped pistol with no special casing here.
    /// </summary>
    private Interactable FindNearestInRange()
    {
        Interactable best = null;
        float bestDistance = float.MaxValue;
        Vector2 position = transform.position;

        for (int i = 0; i < Interactable.All.Count; i++)
        {
            Interactable candidate = Interactable.All[i];
            if (candidate == null) continue;

            float distanceSquared = ((Vector2)candidate.transform.position - position).sqrMagnitude;
            if (distanceSquared > candidate.RangeSquared || distanceSquared >= bestDistance) continue;
            if (!candidate.CanInteract(gameObject)) continue;

            best = candidate;
            bestDistance = distanceSquared;
        }

        return best;
    }

    /// <summary>
    /// Moves the lit prompt from the old target to a new one. Exactly one prompt
    /// is ever on, so walking through a pile of loot never lights the whole pile.
    /// </summary>
    private void SetTarget(Interactable target)
    {
        if (m_current == target) return;

        if (m_current != null) m_current.SetPromptVisible(false);
        m_current = target;
        if (m_current != null) m_current.SetPromptVisible(true);
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (m_current == null || !m_current.isActiveAndEnabled) return;
        if (!m_current.CanInteract(gameObject)) return;

        m_current.Activate(gameObject);
    }
}
