using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The master list of every weapon, character, and enemy that can cross the
/// network. Two machines can't hand each other an asset, only a number, so
/// "the pistol" travels as its position in this list and each machine looks
/// it up in its own copy. Same build, same list, same answer.
///
/// Something missing from here still works on one machine but can't be sent,
/// so add every new weapon, character, and enemy as you make it.
///
/// Loaded from Resources so every scene and every network component can reach
/// it without an Inspector slot of its own.
///
/// Setup:
///   1. Create the folder Assets/Game/Resources.
///   2. In it, Assets > Create > Game > Game Catalog, named exactly "GameCatalog".
///   3. Fill in the three lists. Order is free, but every machine must run the
///      same build, so never reorder between builds you test together.
/// </summary>
[CreateAssetMenu(fileName = "GameCatalog", menuName = "Game/Game Catalog")]
public class GameCatalog : ScriptableObject
{
    public const int NoId = -1;

    private const string ResourcePath = "GameCatalog";

    private static GameCatalog s_instance;

    [SerializeField] private List<WeaponDefinition> m_weapons = new();
    [SerializeField] private List<CharacterDefinition> m_characters = new();

    [Tooltip("Every enemy a session can spawn. Their prefabs are pooled on every machine.")]
    [SerializeField] private List<EnemyDefinition> m_enemies = new();

    public static GameCatalog Instance
    {
        get
        {
            if (s_instance != null) return s_instance;

            s_instance = Resources.Load<GameCatalog>(ResourcePath);
            if (s_instance == null)
            {
                Debug.LogError($"No {nameof(GameCatalog)} found at Resources/{ResourcePath}. Create one with " +
                               "Assets > Create > Game > Game Catalog inside Assets/Game/Resources.");
            }

            return s_instance;
        }
    }

    public IReadOnlyList<CharacterDefinition> Characters => m_characters;
    public IReadOnlyList<EnemyDefinition> Enemies => m_enemies;

    /// <summary>The number that stands for this weapon on the network, or NoId for null.</summary>
    public int GetWeaponId(WeaponDefinition weapon)
    {
        if (weapon == null) return NoId;

        int id = m_weapons.IndexOf(weapon);
        if (id < 0)
        {
            Debug.LogError($"'{weapon.name}' is not in the {nameof(GameCatalog)}, so it can't be sent to " +
                           "other players. Add it to the Weapons list.", this);
        }

        return id;
    }

    public WeaponDefinition GetWeapon(int id) => id >= 0 && id < m_weapons.Count ? m_weapons[id] : null;

    public CharacterDefinition GetCharacter(int id) => id >= 0 && id < m_characters.Count ? m_characters[id] : null;
}
