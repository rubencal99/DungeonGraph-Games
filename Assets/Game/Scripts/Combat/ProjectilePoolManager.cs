using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// Scene-local pool of Projectile instances, keyed by prefab, so weapons never
/// Instantiate/Destroy per shot.
///
/// Setup:
///   1. Create an empty GameObject named "ProjectilePoolManager" in the Testing
///      scene. No further configuration needed.
/// </summary>
public class ProjectilePoolManager : MonoBehaviour
{
    public static ProjectilePoolManager Instance { get; private set; }

    private readonly Dictionary<Projectile, ObjectPool<Projectile>> m_pools = new();
    private readonly Dictionary<Projectile, Projectile> m_instanceToPrefab = new();

    private void Awake()
    {
        Instance = this;
    }

    public Projectile Get(Projectile prefab, Vector3 position, Quaternion rotation)
    {
        ObjectPool<Projectile> pool = GetOrCreatePool(prefab);
        Projectile instance = pool.Get();
        instance.transform.SetPositionAndRotation(position, rotation);
        return instance;
    }

    public void Release(Projectile instance)
    {
        if (!m_instanceToPrefab.TryGetValue(instance, out Projectile prefab))
        {
            Destroy(instance.gameObject);
            return;
        }

        m_pools[prefab].Release(instance);
    }

    private ObjectPool<Projectile> GetOrCreatePool(Projectile prefab)
    {
        if (m_pools.TryGetValue(prefab, out ObjectPool<Projectile> pool)) return pool;

        pool = new ObjectPool<Projectile>(
            createFunc: () => CreateInstance(prefab),
            actionOnGet: instance => instance.gameObject.SetActive(true),
            actionOnRelease: instance =>
            {
                instance.ResetState();
                instance.gameObject.SetActive(false);
            },
            actionOnDestroy: instance => Destroy(instance.gameObject));

        m_pools.Add(prefab, pool);
        return pool;
    }

    private Projectile CreateInstance(Projectile prefab)
    {
        Projectile instance = Instantiate(prefab, transform);
        m_instanceToPrefab.Add(instance, prefab);
        return instance;
    }
}
