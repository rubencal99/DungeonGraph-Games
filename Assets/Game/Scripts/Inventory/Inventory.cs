using System;
using UnityEngine;

/// <summary>
/// What a character is carrying and which slot is in hand. Every change to
/// either goes through here, and anything that draws the inventory (the HUD bar,
/// the weapon held on screen) reacts to the Changed event rather than polling.
///
/// Pickup rule: fill the first empty slot. If every slot is full, the held
/// weapon is the one traded away — it drops on the floor where you stand, so you
/// always see what you gave up.
///
/// Setup:
///   1. Add to the Player prefab root.
///   2. Assign an InventoryDefinition asset (3 slots).
///   3. Add InventoryInput and WeaponHolder alongside it.
/// </summary>
public class Inventory : MonoBehaviour
{
    public const int NoSlot = -1;

    // This machine's own player's inventory, so the HUD can find it without a
    // scene search. Teammates' copies have inventories too; PlayerNetwork marks
    // which one is ours.
    public static Inventory Local { get; private set; }
    public static event Action<Inventory> LocalChanged;

    [SerializeField] private InventoryDefinition m_definition;
    [SerializeField] private PlayerAim m_playerAim;

    [Tooltip("How far in front of the player a traded-away weapon lands. Far " +
             "enough that the pickup prompt does not immediately re-target it.")]
    [SerializeField] private float m_dropDistance = 1.2f;

    private WeaponDefinition[] m_slots;

    /// <summary>Slot currently in hand, or NoSlot when empty-handed.</summary>
    public int HeldSlot { get; private set; } = NoSlot;

    public int SlotCount => m_slots != null ? m_slots.Length : 0;

    /// <summary>Raised whenever slot contents or the held slot change.</summary>
    public event Action Changed;

    private void Awake()
    {
        m_slots = new WeaponDefinition[m_definition != null ? m_definition.SlotCount : 0];
    }

    /// <summary>Marks which inventory belongs to this machine's player. Null when it despawns.</summary>
    public static void SetLocal(Inventory inventory)
    {
        if (Local == inventory) return;

        Local = inventory;
        LocalChanged?.Invoke(inventory);
    }

    /// <summary>What is in a slot, or null when it is empty or out of range.</summary>
    public WeaponDefinition GetWeapon(int slot)
    {
        if (m_slots == null || slot < 0 || slot >= m_slots.Length) return null;
        return m_slots[slot];
    }

    /// <summary>The weapon in hand, or null when empty-handed.</summary>
    public WeaponDefinition HeldWeapon => GetWeapon(HeldSlot);

    /// <summary>
    /// Takes a weapon in. Returns true when it was accepted, which is the
    /// pickup's cue to remove itself from the world.
    /// </summary>
    public bool TryPickup(WeaponDefinition weapon)
    {
        if (weapon == null || m_slots == null || m_slots.Length == 0) return false;

        int empty = FindFirstEmptySlot();
        if (empty >= 0)
        {
            m_slots[empty] = weapon;

            // Your first weapon goes straight into your hand. Later pickups into
            // a free slot leave your hands alone, so looting mid-fight never
            // disarms you.
            if (HeldSlot == NoSlot) HeldSlot = empty;

            Changed?.Invoke();
            return true;
        }

        int target = HeldSlot >= 0 ? HeldSlot : 0;
        WeaponDefinition displaced = m_slots[target];

        m_slots[target] = weapon;
        HeldSlot = target;

        Changed?.Invoke();
        DropToWorld(displaced);
        return true;
    }

    /// <summary>
    /// Overwrites every slot and the held slot at once. Used on a teammate's copy
    /// to mirror what their own machine says they carry; never drops anything.
    /// </summary>
    public void SetContents(WeaponDefinition[] slots, int heldSlot)
    {
        if (m_slots == null) return;

        for (int i = 0; i < m_slots.Length; i++)
        {
            m_slots[i] = i < slots.Length ? slots[i] : null;
        }

        HeldSlot = heldSlot >= 0 && heldSlot < m_slots.Length ? heldSlot : NoSlot;
        Changed?.Invoke();
    }

    /// <summary>
    /// Moves the held slot to the next filled slot, skipping empties and
    /// wrapping. A no-op when fewer than two slots are filled.
    /// </summary>
    public void CycleSlot(int direction)
    {
        int next = FindNextFilledSlot(HeldSlot, direction);
        if (next == NoSlot || next == HeldSlot) return;

        HeldSlot = next;
        Changed?.Invoke();
    }

    /// <summary>
    /// Next slot holding something, starting one step from <paramref name="from"/>
    /// and wrapping around the end. NoSlot when every slot is empty.
    /// </summary>
    private int FindNextFilledSlot(int from, int direction)
    {
        int count = m_slots.Length;
        if (count == 0) return NoSlot;

        int step = direction >= 0 ? 1 : -1;

        for (int offset = 1; offset <= count; offset++)
        {
            // From empty-handed there is no anchor to step off, so scan the
            // whole rack from whichever end matches the direction.
            int index = from < 0
                ? (step > 0 ? offset - 1 : count - offset)
                : WrapIndex(from + step * offset, count);

            if (m_slots[index] != null) return index;
        }

        return NoSlot;
    }

    private int FindFirstEmptySlot()
    {
        for (int i = 0; i < m_slots.Length; i++)
        {
            if (m_slots[i] == null) return i;
        }

        return -1;
    }

    /// <summary>Puts a traded-away weapon back on the floor in front of the player.</summary>
    private void DropToWorld(WeaponDefinition weapon)
    {
        if (weapon == null) return;

        if (WeaponPickupSpawner.Instance == null)
        {
            Debug.LogError($"'{weapon.name}' was traded out of {name}'s inventory but there is no " +
                           "WeaponPickupSpawner in the scene, so it has been lost. Add one.", this);
            return;
        }

        Vector2 direction = m_playerAim != null ? m_playerAim.AimDirection : Vector2.right;
        WeaponPickupSpawner.Instance.Spawn(weapon, (Vector2)transform.position + direction * m_dropDistance, direction);
    }

    /// <summary>Positive modulo, so cycling back from slot 0 lands on the last slot.</summary>
    private static int WrapIndex(int value, int count) => ((value % count) + count) % count;
}
