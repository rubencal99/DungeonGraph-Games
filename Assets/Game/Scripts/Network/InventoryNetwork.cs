using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Mirrors a player's inventory to everyone else, so their copy of you holds
/// the same gun you do. The owner's Inventory is the real one; every change to
/// it is written here, and other machines copy it into theirs. WeaponHolder
/// already redraws on every inventory change, so the held weapon follows.
///
/// Setup:
///   1. Add to the Player prefab root, next to the NetworkObject.
///   2. Drag in the Player's Inventory.
/// </summary>
public class InventoryNetwork : NetworkBehaviour
{
    [SerializeField] private Inventory m_inventory;

    private NetworkList<int> m_slots;

    private readonly NetworkVariable<int> m_heldSlot =
        new(Inventory.NoSlot, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    // Reused for every incoming change so mirroring never allocates.
    private WeaponDefinition[] m_buffer;

    private void Awake()
    {
        // A NetworkList must exist before the object spawns, so it is built here
        // rather than inline.
        m_slots = new NetworkList<int>(null, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            m_inventory.Changed += Push;
            Push();
        }
        else
        {
            m_slots.OnListChanged += OnSlotsChanged;
            m_heldSlot.OnValueChanged += OnHeldSlotChanged;
            Pull();
        }
    }

    public override void OnNetworkDespawn()
    {
        m_inventory.Changed -= Push;
        m_slots.OnListChanged -= OnSlotsChanged;
        m_heldSlot.OnValueChanged -= OnHeldSlotChanged;
    }

    /// <summary>Owner: writes the real inventory out for everyone else.</summary>
    private void Push()
    {
        GameCatalog catalog = GameCatalog.Instance;

        for (int i = 0; i < m_inventory.SlotCount; i++)
        {
            int id = catalog.GetWeaponId(m_inventory.GetWeapon(i));

            if (i >= m_slots.Count) m_slots.Add(id);
            else if (m_slots[i] != id) m_slots[i] = id;
        }

        m_heldSlot.Value = m_inventory.HeldSlot;
    }

    /// <summary>Everyone else: copies what the owner wrote into this machine's copy.</summary>
    private void Pull()
    {
        GameCatalog catalog = GameCatalog.Instance;
        m_buffer ??= new WeaponDefinition[m_inventory.SlotCount];

        for (int i = 0; i < m_buffer.Length; i++)
        {
            m_buffer[i] = i < m_slots.Count ? catalog.GetWeapon(m_slots[i]) : null;
        }

        m_inventory.SetContents(m_buffer, m_heldSlot.Value);
    }

    private void OnSlotsChanged(NetworkListEvent<int> change) => Pull();

    private void OnHeldSlotChanged(int previous, int current) => Pull();
}
