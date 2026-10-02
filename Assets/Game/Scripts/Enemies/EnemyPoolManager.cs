using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// Scene-local pool of enemy instances, keyed by prefab, so spawning never
/// Instantiates/Destroys per enemy. Mirrors ProjectilePoolManager's shape.
///
/// Enemies are network objects, and in a session every machine keeps its own
/// pool. The host takes an enemy from its pool and spawns it; Netcode then asks
/// each other machine's pool for an instance (through the handler registered
/// below) instead of creating one. Despawning sends every copy back to its
/// machine's pool the same way. Nothing is created or destroyed on any machine
/// once the pools are warm.
///
/// Setup:
///   1. Drop the EnemyPoolManager prefab into the gameplay scene. It's a plain
///      object, not a network object.
///   2. List every enemy in the GameCatalog, so every machine registers its
///      pool before the host spawns one.
/// </summary>
public class EnemyPoolManager : MonoBehaviour
{
    public static EnemyPoolManager Instance { get; private set; }

    private readonly Dictionary<GameObject, ObjectPool<GameObject>> m_pools = new();
    private readonly Dictionary<GameObject, GameObject> m_instanceToPrefab = new();
    private readonly List<GameObject> m_handledPrefabs = new();

    private void Awake()
    {
        Instance = this;
        RegisterNetworkHandlers();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        NetworkManager network = NetworkManager.Singleton;
        if (network == null) return;

        foreach (GameObject prefab in m_handledPrefabs) network.PrefabHandler.RemoveHandler(prefab);
    }

    /// <summary>
    /// Host (or offline): takes an enemy from the pool and puts it in the world,
    /// for everyone when in a session.
    /// </summary>
    public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        GameObject instance = Take(prefab, position, rotation);

        NetworkManager network = NetworkManager.Singleton;
        if (network != null && network.IsListening && instance.TryGetComponent(out NetworkObject networkObject))
        {
            // Gone with the level, so returning to the lobby leaves no enemies behind.
            networkObject.Spawn(destroyWithScene: true);
        }

        return instance;
    }

    /// <summary>Host (or offline): removes an enemy from the world and returns it to the pool.</summary>
    public void Release(GameObject instance)
    {
        // Despawning sends this and every other machine's copy back through Return.
        if (instance.TryGetComponent(out NetworkObject networkObject) && networkObject.IsSpawned)
        {
            networkObject.Despawn();
            return;
        }

        Return(instance);
    }

    private GameObject Take(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        GameObject instance = GetOrCreatePool(prefab).Get();

        // Placed before it wakes, so nothing reads its old position on enable.
        instance.transform.SetPositionAndRotation(position, rotation);
        instance.SetActive(true);
        return instance;
    }

    private void Return(GameObject instance)
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
            actionOnRelease: instance => instance.SetActive(false),
            actionOnDestroy: instance => Destroy(instance));

        m_pools.Add(prefab, pool);
        return pool;
    }

    private GameObject CreateInstance(GameObject prefab)
    {
        // Left at the scene root rather than under this object: Netcode expects a
        // network object's parent to be another network object, or nothing.
        GameObject instance = Instantiate(prefab);
        instance.SetActive(false);
        m_instanceToPrefab.Add(instance, prefab);
        return instance;
    }

    private void RegisterNetworkHandlers()
    {
        NetworkManager network = NetworkManager.Singleton;
        GameCatalog catalog = GameCatalog.Instance;
        if (network == null || catalog == null) return;

        foreach (EnemyDefinition enemy in catalog.Enemies)
        {
            if (enemy == null || enemy.Prefab == null || m_handledPrefabs.Contains(enemy.Prefab)) continue;

            network.PrefabHandler.AddHandler(enemy.Prefab, new PoolHandler(this, enemy.Prefab));
            m_handledPrefabs.Add(enemy.Prefab);
        }
    }

    /// <summary>Lets Netcode borrow from and return to this pool on machines that aren't the host.</summary>
    private sealed class PoolHandler : INetworkPrefabInstanceHandler
    {
        private readonly EnemyPoolManager m_manager;
        private readonly GameObject m_prefab;

        public PoolHandler(EnemyPoolManager manager, GameObject prefab)
        {
            m_manager = manager;
            m_prefab = prefab;
        }

        public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
        {
            return m_manager.Take(m_prefab, position, rotation).GetComponent<NetworkObject>();
        }

        public void Destroy(NetworkObject networkObject)
        {
            // The level may be unloading, taking the pool with it.
            if (m_manager != null) m_manager.Return(networkObject.gameObject);
            else Object.Destroy(networkObject.gameObject);
        }
    }
}
