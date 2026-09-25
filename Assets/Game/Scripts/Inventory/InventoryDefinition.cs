using UnityEngine;

/// <summary>
/// The shape of an inventory: how many slots it has. An asset rather than a
/// constant, so a future pack-mule character is a second asset instead of a
/// second code path.
///
/// Setup:
///   1. Assets > Create > Game > Inventory Definition.
///   2. Leave Slot Count at 3 and assign it to the Player's Inventory component.
/// </summary>
[CreateAssetMenu(fileName = "Inventory_", menuName = "Game/Inventory Definition")]
public class InventoryDefinition : ScriptableObject
{
    [Tooltip("Number of weapon slots. Must match the number of boxes on the HUD bar.")]
    [SerializeField, Range(1, 8)] private int m_slotCount = 3;

    public int SlotCount => m_slotCount;
}
