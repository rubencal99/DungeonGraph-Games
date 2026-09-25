using UnityEngine;

/// <summary>
/// The bottom-right inventory bar. Binds to the local player's inventory when it
/// spawns and mirrors its state into the authored slot boxes.
///
/// Strictly a reader. It never changes a slot — scrolling is InventoryInput's
/// job — and only reacts once the inventory has actually moved. That is why the
/// highlight can never lie about what you are holding.
///
/// It binds through Inventory.Local rather than a scene search, so it works
/// whether the HUD or the player is created first.
///
/// Setup:
///   1. Create a Canvas (Screen Space - Overlay) with an empty child anchored
///      bottom-right, and add this component to that child.
///   2. Drop three SlowViewDefault prefabs under it and list them, left to
///      right, in the Slot Views array.
/// </summary>
public class InventoryBarView : MonoBehaviour
{
    [Tooltip("The three authored slot boxes, in slot order, left to right.")]
    [SerializeField] private InventorySlotView[] m_slotViews;

    private Inventory m_bound;

    public bool IsBound => m_bound != null;

    private void OnEnable()
    {
        Inventory.LocalChanged += Bind;

        // The player may already exist before this HUD was enabled, in which
        // case the event has been and gone.
        Bind(Inventory.Local);
    }

    private void OnDisable()
    {
        Inventory.LocalChanged -= Bind;
        Unbind();
    }

    private void Bind(Inventory inventory)
    {
        if (m_bound == inventory) return;

        Unbind();
        m_bound = inventory;

        if (m_bound == null)
        {
            Clear();
            return;
        }

        m_bound.Changed += Refresh;

        if (m_slotViews == null || m_slotViews.Length < m_bound.SlotCount)
        {
            int authored = m_slotViews != null ? m_slotViews.Length : 0;
            Debug.LogError($"{nameof(InventoryBarView)} on '{name}' has {authored} slot boxes but " +
                           $"'{m_bound.name}' has {m_bound.SlotCount} slots, so the last " +
                           $"{m_bound.SlotCount - authored} can never be drawn. Add the missing " +
                           "SlowViewDefault prefabs to the Slot Views array.", this);
        }

        Refresh();
    }

    private void Unbind()
    {
        if (m_bound == null) return;

        m_bound.Changed -= Refresh;
        m_bound = null;
        Clear();
    }

    /// <summary>Pushes the bound inventory's state into every slot box.</summary>
    private void Refresh()
    {
        if (m_bound == null)
        {
            Clear();
            return;
        }

        for (int i = 0; i < m_slotViews.Length; i++)
        {
            InventorySlotView view = m_slotViews[i];
            if (view == null) continue;

            // A bar authored with more boxes than this character has slots hides
            // the extras rather than drawing slots that can never be filled.
            bool slotExists = i < m_bound.SlotCount;
            view.SetVisible(slotExists);
            if (!slotExists) continue;

            WeaponDefinition weapon = m_bound.GetWeapon(i);
            view.SetItem(weapon != null ? weapon.Icon : null);
            view.SetHighlighted(i == m_bound.HeldSlot);
        }
    }

    /// <summary>Blanks every box, for when there is no player to mirror.</summary>
    private void Clear()
    {
        if (m_slotViews == null) return;

        foreach (InventorySlotView view in m_slotViews)
        {
            if (view == null) continue;

            view.SetVisible(true);
            view.SetItem(null);
            view.SetHighlighted(false);
        }
    }
}
