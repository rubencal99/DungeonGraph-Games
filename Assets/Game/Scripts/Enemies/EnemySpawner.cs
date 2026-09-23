using UnityEngine;

/// <summary>
/// Fires once, the first time the player enters this room: picks a random
/// count of enemies (weighted by rarity via its EnemySpawnerDefinition) and
/// scatters them within its own trigger collider's bounds.
///
/// Setup:
///   1. Add to a GameObject in the room (hand-built test room, or a child of
///      a DungeonGraph room prefab so it comes along wherever that room
///      appears).
///   2. Add a Collider2D shaped to the room (PolygonCollider2D for a precise
///      fit, or a simple BoxCollider2D) and tick Is Trigger.
///   3. Assign an EnemySpawnerDefinition asset.
///   4. Make sure the Player has its own Collider2D — trigger detection needs
///      one on both sides.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private EnemySpawnerDefinition m_definition;

    private Collider2D m_collider;
    private bool m_hasTriggered;

    private void Awake()
    {
        m_collider = GetComponent<Collider2D>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (m_hasTriggered) return;
        if (other.GetComponentInParent<PlayerController>() == null) return;

        m_hasTriggered = true;
        SpawnEnemies();
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
            EnemyPoolManager.Instance.Get(enemy.Prefab, point, Quaternion.identity);
        }
    }
}
