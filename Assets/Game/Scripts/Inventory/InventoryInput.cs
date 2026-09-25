using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Turns the scroll wheel into inventory slot changes. Reads the device and
/// nothing else — which slot you land on is the Inventory's business.
///
/// Event-driven rather than polled, so one notch of the wheel is one step.
///
/// Setup:
///   1. Add to the Player prefab root, next to Inventory.
///   2. Add a "CycleSlot" action to the Player map (Action Type: Value,
///      Control Type: Axis) bound to <Mouse>/scroll/y, and drag it in here.
/// </summary>
public class InventoryInput : MonoBehaviour
{
    [SerializeField] private Inventory m_inventory;
    [SerializeField] private InputActionReference m_cycleSlotAction;

    private void OnEnable()
    {
        if (m_cycleSlotAction == null) return;
        m_cycleSlotAction.action.Enable();
        m_cycleSlotAction.action.performed += OnCyclePerformed;
    }

    private void OnDisable()
    {
        if (m_cycleSlotAction == null) return;
        m_cycleSlotAction.action.performed -= OnCyclePerformed;
        m_cycleSlotAction.action.Disable();
    }

    private void OnCyclePerformed(InputAction.CallbackContext context)
    {
        if (m_inventory == null) return;

        // Scroll delta is reported in notches of 120 on Windows, 1 elsewhere.
        // Only the sign matters here, so neither needs handling.
        float scroll = context.ReadValue<float>();
        if (Mathf.Approximately(scroll, 0f)) return;

        m_inventory.CycleSlot(scroll > 0f ? 1 : -1);
    }
}
