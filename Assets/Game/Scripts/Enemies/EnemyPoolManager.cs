using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// Scene-local pool of plain GameObject instances, keyed by prefab, so
/// EnemySpawner never Instantiates/Destroys per spawn. Mirrors
/// ProjectilePoolManager's shape; kept separate for now rather than merged
/// into one generic pooler, since enemies don't need Projectile's
/// Initialize/ResetState hooks — nothing yet says the two should share code.
///
/// Setup:
///   1. Create an empty GameObject named "EnemyPoolManager" in the scene. No
///      further configuration needed.
/// </summary>
public class EnemyPoolManager : MonoBehaviour
{
    public static EnemyPoolManager Instance { get; private set; }

    private readonly Dictionary<GameObject, ObjectPool<GameObject>> m_pools = new();
    private readonly Dictionary<GameObject, GameObject> m_instanceToPrefab = new();

    private void Awake()
    {
        Instance = this;
    }

    public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        ObjectPool<GameObject> pool = GetOrCreatePool(prefab);
        GameObject instance = pool.Get();
        instance.transform.SetPositionAndRotation(position, rotation);
        return instance;
    }

    public void Release(GameObject instance)
    {
        if (!m_instanceToPrefab.TryGetValue(instance, out GameObject prefab))
        {
            Destroy(instance);
            return;
        }

        m_pools[prefab].Release(instance);
    }

    private ObjectPool<GameObject> GetOrCreatePool(GameObject prefab)
    {
        if (m_pools.TryGetValue(prefab, out ObjectPool<GameObject> pool)) return pool;

        pool = new ObjectPool<GameObject>(
            createFunc: () => CreateInstance(prefab),
            actionOnGet: instance => instance.SetActive(true),
            actionOnRelease: instance => instance.SetActive(false),
            actionOnDestroy: instance => Destroy(instance));

        m_pools.Add(prefab, pool);
        return pool;
    }

    private GameObject CreateInstance(GameObject prefab)
    {
        GameObject instance = Instantiate(prefab, transform);
        m_instanceToPrefab.Add(instance, prefab);
        return instance;
    }
}
