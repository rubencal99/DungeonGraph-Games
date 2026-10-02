using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Fires once, the first time a player enters this room: picks a random count
/// of enemies (weighted by rarity via its EnemySpawnerDefinition) and scatters
/// them within its own collider's bounds.
///
/// Spawners live inside room prefabs, so every machine has a copy, but only
/// the host's copy ever acts; the rest stay idle. Rather than physics
/// triggers (which don't fire for a teammate's copy, since it's moved by the
/// network rather than by physics), it checks whether any player stands inside
/// its collider five times a second. That's a handful of point tests per
/// spawner, cheaper than a trigger, and it works the same for every player.
///
/// Setup:
///   1. Add to a GameObject inside a room prefab, so it comes along wherever
///      that room appears.
///   2. Add a Collider2D shaped to the room (PolygonCollider2D for a precise
///      fit, or a simple BoxCollider2D) and tick Is Trigger so it never blocks
///      anything.
///   3. Assign an EnemySpawnerDefinition asset.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class EnemySpawner : MonoBehaviour
{
    private static readonly List<EnemySpawner> s_all = new();

    /// <summary>Every spawner in the current dungeon.</summary>
    public static IReadOnlyList<EnemySpawner> All => s_all;

    /// <summary>True once every spawner in the dungeon has fired. Part of the level's win condition.</summary>
    public static bool AllTriggered
    {
        get
        {
            for (int i = 0; i < s_all.Count; i++)
            {
                if (!s_all[i].m_hasTriggered) return false;
            }

            return true;
        }
    }

    [SerializeField] private EnemySpawnerDefinition m_definition;

    [Tooltip("Seconds between checks for a player in the room.")]
    [SerializeField, Min(0.05f)] private float m_checkInterval = 0.2f;

    private Collider2D m_collider;
    private bool m_hasTriggered;
    private float m_nextCheckTime;

    public bool HasTriggered => m_hasTriggered;

    private void Awake()
    {
        m_collider = GetComponent<Collider2D>();
    }

    private void OnEnable()
    {
        s_all.Add(this);
    }

    private void OnDisable()
    {
        s_all.Remove(this);
    }

    private void Update()
    {
        if (m_hasTriggered || Time.time < m_nextCheckTime) return;
        m_nextCheckTime = Time.time + m_checkInterval;

        if (!IsHostOrOffline()) return;

        IReadOnlyList<PlayerController> players = PlayerController.All;
        for (int i = 0; i < players.Count; i++)
        {
            if (!m_collider.OverlapPoint(players[i].transform.position)) continue;

            m_hasTriggered = true;
            SpawnEnemies();
            return;
        }
    }

    private void SpawnEnemies()
    {
        if (m_definition == null) return;

        int count = Random.Range(m_definition.MinCount, m_definition.MaxCount + 1);
        Bounds bounds = m_collider.bounds;

        for (int i = 0; i < count; i++)
        {
            EnemyDefinition enemy = m_definition.GetRandomEnemy();
            if (enemy == null || enemy.Prefab == null) continue;

            Vector2 point = new Vector2(Random.Range(bounds.min.x, bounds.max.x), Random.Range(bounds.min.y, bounds.max.y));
            EnemyPoolManager.Instance.Spawn(enemy.Prefab, point, Quaternion.identity);
        }
    }

    private static bool IsHostOrOffline()
    {
        NetworkManager network = NetworkManager.Singleton;
        return network == null || !network.IsListening || network.IsServer;
    }
}
