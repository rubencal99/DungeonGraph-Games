using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A weighted pool of what can drop. Entries are either weapons or other loot
/// tables, so one table can describe "a common weapon" and another can say
/// "most of the time, roll on the rare pool".
///
/// Weights are relative, not percentages. Nothing has to add up to 100. An
/// entry's chance is its weight over the total of every usable entry, so giving
/// one weapon a 10 and another a 1 makes the first ten times as likely — and
/// adding a third row later never means editing the two around it.
///
/// Tables nest instead of there being a separate "pool" type. A tiered chest
/// wants a weighted list of weighted lists, and that is just a loot table whose
/// entries are loot tables. One recursive type gives any depth, and gives it to
/// the enemy drops of milestone 4 for free.
///
/// Rolling reads; it never consumes. A table that emptied itself would be
/// editing an authored asset, and the change would survive leaving play mode.
///
/// The two lists keep their original field names (m_Items, m_Tables, with key
/// and value inside) so the existing Loot_*.asset files still load.
///
/// Setup:
///   1. Assets > Create > Game > Loot Table.
///   2. Add rows and give each a weight above zero.
/// </summary>
[CreateAssetMenu(fileName = "Loot_", menuName = "Game/Loot Table")]
public class LootTableDefinition : ScriptableObject
{
    /// <summary>
    /// How deep nested tables may go before a roll gives up. OnValidate catches
    /// a cycle at edit time; this is the runtime backstop for one that got past.
    /// </summary>
    public const int MaxNestingDepth = 8;

    [Serializable]
    public struct WeaponEntry
    {
        [Tooltip("Weapon this row can drop.")] public WeaponDefinition key;
        [Tooltip("Relative weight. A 10 next to a 1 is ten times as likely.")] public float value;
    }

    [Serializable]
    public struct TableEntry
    {
        [Tooltip("Another loot table to defer to.")] public LootTableDefinition key;
        [Tooltip("Relative weight, weighed against the weapon rows above.")] public float value;
    }

    [Tooltip("Weapons this table can produce, and how likely each is relative to every other row.")]
    [SerializeField] private List<WeaponEntry> m_Items = new();

    [Tooltip("Other loot tables this one can defer to. This is what makes a tiered chest: point a " +
             "chest at a table whose rows are your common, rare, and legendary pools.")]
    [SerializeField] private List<TableEntry> m_Tables = new();

    /// <summary>Sum of every usable row's weight. Zero means this table can never give anything.</summary>
    public float TotalWeight
    {
        get
        {
            float total = 0f;

            for (int i = 0; i < m_Items.Count; i++)
            {
                if (IsUsableWeapon(m_Items[i])) total += m_Items[i].value;
            }

            for (int i = 0; i < m_Tables.Count; i++)
            {
                if (IsUsableTable(m_Tables[i])) total += m_Tables[i].value;
            }

            return total;
        }
    }

    /// <summary>Picks one weapon, descending into a nested table if the roll lands on one.</summary>
    /// <returns>The weapon rolled, or null when this table has nothing usable to give.</returns>
    public WeaponDefinition Roll() => Roll(0);

    private WeaponDefinition Roll(int depth)
    {
        if (depth >= MaxNestingDepth)
        {
            Debug.LogError($"{name}: nested loot tables are more than {MaxNestingDepth} deep, which " +
                           "almost certainly means they form a loop. This roll produced nothing.", this);
            return null;
        }

        float total = TotalWeight;
        if (total <= 0f) return null;

        float point = UnityEngine.Random.value * total;

        // The last usable row of each kind, in walk order. Floating point means
        // the running subtraction can finish a hair above zero on the final row —
        // and Random.value can return exactly 1 — so without a fallback a full
        // table would silently drop nothing once in a great while.
        WeaponDefinition lastWeapon = null;
        LootTableDefinition lastTable = null;

        for (int i = 0; i < m_Items.Count; i++)
        {
            if (!IsUsableWeapon(m_Items[i])) continue;

            lastWeapon = m_Items[i].key;
            point -= m_Items[i].value;
            if (point < 0f) return m_Items[i].key;
        }

        for (int i = 0; i < m_Tables.Count; i++)
        {
            if (!IsUsableTable(m_Tables[i])) continue;

            lastTable = m_Tables[i].key;
            point -= m_Tables[i].value;
            if (point < 0f) return m_Tables[i].key.Roll(depth + 1);
        }

        return lastTable != null ? lastTable.Roll(depth + 1) : lastWeapon;
    }

    /// <summary>
    /// Whether a weapon row can be rolled. The null check is Unity's overload,
    /// so a row left pointing at a deleted asset reads as empty rather than
    /// throwing.
    /// </summary>
    private static bool IsUsableWeapon(WeaponEntry entry) => entry.key != null && entry.value > 0f;

    /// <summary>Whether a nested table row can be rolled. A table never defers to itself.</summary>
    private bool IsUsableTable(TableEntry entry) => entry.key != null && entry.value > 0f && entry.key != this;

    /// <summary>
    /// Catches the ways an authored table silently produces nothing, plus the
    /// loop that would otherwise only show up as an error mid-run.
    /// </summary>
    private void OnValidate()
    {
        if (TotalWeight <= 0f)
        {
            Debug.LogWarning($"{name}: every row is empty or weighted zero, so this table can never " +
                             "drop anything.", this);
        }

        for (int i = 0; i < m_Tables.Count; i++)
        {
            LootTableDefinition nested = m_Tables[i].key;
            if (nested == null) continue;

            if (nested == this)
            {
                Debug.LogError($"{name}: this table lists itself. That row is ignored at runtime; " +
                               "delete it.", this);
            }
            else if (FormsCycle(nested, this, 0))
            {
                Debug.LogError($"{name}: nesting '{nested.name}' creates a loop back to here. Rolling " +
                               "it would recurse to the depth limit and then give nothing.", this);
            }
        }
    }

    /// <summary>Whether following <paramref name="from"/> can reach <paramref name="target"/> again.</summary>
    private static bool FormsCycle(LootTableDefinition from, LootTableDefinition target, int depth)
    {
        if (from == null || depth >= MaxNestingDepth) return false;
        if (from == target) return true;

        for (int i = 0; i < from.m_Tables.Count; i++)
        {
            if (FormsCycle(from.m_Tables[i].key, target, depth + 1)) return true;
        }

        return false;
    }
}
