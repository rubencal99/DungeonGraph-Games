using UnityEngine;

/// <summary>
/// Draws whatever the inventory says is in hand. Keeps one instantiated weapon
/// per filled slot under the Weapons container and shows only the held one, so
/// scrolling through three guns is three SetActive calls rather than three
/// Instantiate/Destroy pairs — and each weapon keeps its own ammo count while
/// stowed.
///
/// Stowed weapons are hidden rather than holstered: a top-down character sprite
/// with three guns strapped to it reads as noise.
///
/// Setup:
///   1. Add to the Player prefab root.
///   2. Drag the Player's Inventory, PlayerAim, and the "Weapons" child
///      transform into the matching slots.
///   3. Give every WeaponDefinition asset a Weapon Prefab.
/// </summary>
public class WeaponHolder : MonoBehaviour
{
    [SerializeField] private Inventory m_inventory;
    [SerializeField] private PlayerAim m_playerAim;

    [Tooltip("The 'Weapons' child of the Player, which WeaponAimRotator spins. " +
             "Weapon instances are parented here so they inherit the aim rotation.")]
    [SerializeField] private Transform m_weaponParent;

    // One instance per slot, parallel to the inventory's slots.
    private Weapon[] m_instances;
    private Weapon m_held;

    private void OnEnable()
    {
        if (m_inventory == null) return;
        m_inventory.Changed += OnInventoryChanged;

        // Silent: this is catching up with the inventory, not the player doing anything.
        Refresh();
    }

    private void OnDisable()
    {
        if (m_inventory != null) m_inventory.Changed -= OnInventoryChanged;
    }

    /// <summary>
    /// Every pickup, trade, and scroll arrives here, which makes this the one
    /// place the equip sound can play without ever playing twice for one action.
    /// </summary>
    private void OnInventoryChanged()
    {
        WeaponDefinition arrived = Refresh();
        if (arrived != null) AudioManager.Play(arrived.EquipCue, transform.position);
    }

    /// <summary>
    /// Brings the instantiated weapons back in line with the inventory: build
    /// what is newly held, tear down what was traded away, show only the slot in
    /// hand. Returns the weapon that just came into hand or into the pack, or
    /// null when nothing new arrived.
    /// </summary>
    private WeaponDefinition Refresh()
    {
        if (m_weaponParent == null) return null;

        WeaponDefinition arrived = null;

        if (m_instances == null || m_instances.Length != m_inventory.SlotCount)
        {
            m_instances = new Weapon[m_inventory.SlotCount];
        }

        for (int i = 0; i < m_instances.Length; i++)
        {
            WeaponDefinition definition = m_inventory.GetWeapon(i);

            // A slot whose weapon changed (traded away, or swapped for another)
            // throws away the old instance; there is nothing to reuse between
            // two different guns.
            if (m_instances[i] != null && m_instances[i].Definition != definition)
            {
                Destroy(m_instances[i].gameObject);
                m_instances[i] = null;
            }

            if (definition != null && m_instances[i] == null)
            {
                m_instances[i] = CreateInstance(definition);
                arrived = definition;
            }

            if (m_instances[i] != null)
            {
                m_instances[i].gameObject.SetActive(i == m_inventory.HeldSlot);
            }
        }

        // What is now in hand wins over something slipped into the pack.
        int held = m_inventory.HeldSlot;
        Weapon previousHeld = m_held;
        m_held = held >= 0 && held < m_instances.Length ? m_instances[held] : null;
        if (m_held != null && m_held != previousHeld) arrived = m_held.Definition;

        return arrived;
    }

    private Weapon CreateInstance(WeaponDefinition definition)
    {
        if (definition.WeaponPrefab == null)
        {
            Debug.LogError($"'{definition.name}' has no Weapon Prefab assigned, so it cannot be " +
                           "drawn or fired. Assign one on the definition asset.", this);
            return null;
        }

        Weapon instance = Instantiate(definition.WeaponPrefab, m_weaponParent);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;

        // The prefab cannot hold a reference to a scene object, so aim is wired
        // here at spawn.
        instance.Initialize(m_playerAim);
        return instance;
    }
}
