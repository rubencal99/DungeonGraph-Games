using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One box in the inventory bar. Owns two authored Images and does nothing but
/// switch them on and off. The box background is authored art that never
/// changes, so nothing here touches it.
///
/// It builds no hierarchy. The background, icon, and highlight are authored
/// children dragged into the fields below, so the look of a slot changes in the
/// Scene view rather than in code.
///
/// Both switches activate and deactivate the child GameObject rather than
/// toggling Image.enabled. A slot authored with its icon or highlight object
/// turned off would otherwise stay invisible forever, since an inactive
/// GameObject draws nothing however its Image is set up.
///
/// The fields keep their original capitalisation (m_Icon, not m_icon)
/// because the authored SlowViewDefault prefab is already wired to those names.
///
/// Setup:
///   1. Already wired on Assets/Game/Prefabs/UI/SlowViewDefault.prefab.
///   2. Drop three of those under the HUD bar and list them in InventoryBarView.
/// </summary>
public class InventorySlotView : MonoBehaviour
{
    [Tooltip("The weapon's icon. Hidden while the slot is empty.")]
    [SerializeField] private Image m_Icon;

    [Tooltip("Border or glow marking this as the slot currently in hand. Hidden otherwise.")]
    [SerializeField] private Image m_Highlight;

    private void Awake()
    {
        SetItem(null);
        SetHighlighted(false);
    }

    /// <summary>Shows a weapon's icon, or clears the slot.</summary>
    public void SetItem(Sprite icon)
    {
        if (m_Icon == null) return;

        m_Icon.sprite = icon;
        m_Icon.gameObject.SetActive(icon != null);
    }

    /// <summary>Marks this slot as the one currently in hand.</summary>
    public void SetHighlighted(bool highlighted)
    {
        if (m_Highlight != null) m_Highlight.gameObject.SetActive(highlighted);
    }

    /// <summary>Shows or hides the whole box, for inventories with fewer slots than the bar has boxes.</summary>
    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }
}
