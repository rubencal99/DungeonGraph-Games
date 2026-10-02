using System;
using System.Collections.Generic;
using DungeonGraph;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Gives every player the same dungeon without sending a single tile. The host
/// picks a random seed, every machine builds the dungeon from that seed
/// locally, and the same seed gives the same layout, the way two people
/// shuffling with the same trick get the same deck.
///
/// The DungeonGraph plugin wasn't written with multiplayer in mind, so each
/// client reports a fingerprint of the rooms it built and the host checks it
/// against its own. A mismatch is logged loudly rather than silently letting
/// players walk through different walls.
///
/// Because every machine built the same rooms in the same order, things inside
/// room prefabs can be named by position in a list. "Chest #3" means the same
/// chest everywhere, which is how chests open for everyone without being
/// network objects themselves.
///
/// Setup:
///   1. On the gameplay scene's DungeonGenerator object: add a NetworkObject
///      and this component.
///   2. Untick the DungeonGenerator's Generate On Start; this triggers it.
///   3. Drag in the DungeonGenerator and the same Dungeon Graph asset it uses.
///      The graph must use the Grid generation style, which is the seedable one.
/// </summary>
public class DungeonNetwork : NetworkBehaviour
{
    public static DungeonNetwork Instance { get; private set; }

    /// <summary>Host only. Raised once every connected machine has built the dungeon.</summary>
    public event Action<DungeonGenerationResult> AllReady;

    [SerializeField] private DungeonGenerator m_generator;

    [Tooltip("The same graph asset the generator builds. Its seed is overridden while a build runs, then put back.")]
    [SerializeField] private DungeonGraphAsset m_graph;

    private readonly NetworkVariable<int> m_seed = new();
    private readonly Dictionary<ulong, int> m_clientFingerprints = new();

    private Chest[] m_chests = Array.Empty<Chest>();
    private int m_fingerprint;
    private bool m_built;
    private bool m_allReady;

    private int m_savedGraphSeed;
    private UnityEngine.Random.State m_savedRandomState;

    public override void OnNetworkSpawn()
    {
        Instance = this;

        if (IsServer)
        {
            m_seed.Value = UnityEngine.Random.Range(1, int.MaxValue);
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
        }

        Build(m_seed.Value);
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this) Instance = null;

        StopListeningToGenerator();
        if (IsServer) NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    /// <summary>Asks the host to open a chest for everyone. Called by the chest itself on E.</summary>
    public void RequestOpenChest(Chest chest)
    {
        int index = Array.IndexOf(m_chests, chest);
        if (index >= 0) OpenChestRpc(index);
    }

    private void Build(int seed)
    {
        // The plugin reads its seed from the graph asset and some of its choices
        // from Unity's shared random generator, so both are pinned to the seed.
        m_savedGraphSeed = m_graph.Settings.floodFillSeed;
        m_savedRandomState = UnityEngine.Random.state;

        m_graph.Settings.floodFillSeed = seed;
        UnityEngine.Random.InitState(seed);

        m_generator.OnGenerationComplete += OnGenerated;
        m_generator.OnGenerationFailed += OnGenerationFailed;
        m_generator.Generate();
    }

    private void OnGenerated(DungeonGenerationResult result)
    {
        RestoreSeeds();

        m_chests = result.dungeonParent.GetComponentsInChildren<Chest>(true);
        m_fingerprint = Fingerprint(result.dungeonParent.transform);
        m_built = true;

        if (IsServer) TryFinish();
        else ReportBuiltRpc(m_fingerprint);
    }

    private void OnGenerationFailed()
    {
        RestoreSeeds();
        Debug.LogError("[DungeonNetwork] The dungeon failed to generate on this machine, so this player can't " +
                       "enter the level. See the DungeonGenerator errors above.", this);
    }

    private void RestoreSeeds()
    {
        StopListeningToGenerator();

        // Put the graph asset back, so a runtime seed never ends up saved into it.
        m_graph.Settings.floodFillSeed = m_savedGraphSeed;
        UnityEngine.Random.state = m_savedRandomState;
    }

    private void StopListeningToGenerator()
    {
        m_generator.OnGenerationComplete -= OnGenerated;
        m_generator.OnGenerationFailed -= OnGenerationFailed;
    }

    [Rpc(SendTo.Server)]
    private void ReportBuiltRpc(int fingerprint, RpcParams rpcParams = default)
    {
        m_clientFingerprints[rpcParams.Receive.SenderClientId] = fingerprint;
        TryFinish();
    }

    private void OnClientDisconnected(ulong clientId) => TryFinish();

    /// <summary>Host: once its own dungeon and every client's are built, checks them and starts the level.</summary>
    private void TryFinish()
    {
        if (m_allReady || !m_built) return;

        foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
        {
            if (clientId == NetworkManager.LocalClientId) continue;
            if (!m_clientFingerprints.ContainsKey(clientId)) return;
        }

        foreach (KeyValuePair<ulong, int> report in m_clientFingerprints)
        {
            if (report.Value == m_fingerprint) continue;

            Debug.LogError($"[DungeonNetwork] Client {report.Key} built a different dungeon from the host. " +
                           "They'll see different walls and rooms. Check the graph uses the Grid style and " +
                           "that nothing else draws random numbers during generation.", this);
        }

        m_allReady = true;
        AllReady?.Invoke(m_generator.LastResult);
    }

    [Rpc(SendTo.Server)]
    private void OpenChestRpc(int index)
    {
        Chest chest = ChestAt(index);
        if (chest == null || chest.IsOpen) return;

        // Opened here first, so a second request arriving this same frame finds it already open.
        chest.Open();
        chest.DropLoot();
        ChestOpenedRpc(index);
    }

    [Rpc(SendTo.NotServer)]
    private void ChestOpenedRpc(int index)
    {
        Chest chest = ChestAt(index);
        if (chest != null) chest.Open();
    }

    private Chest ChestAt(int index) => index >= 0 && index < m_chests.Length ? m_chests[index] : null;

    /// <summary>A number summarizing which rooms were built and where. Equal fingerprints mean equal layouts.</summary>
    private static int Fingerprint(Transform dungeonRoot)
    {
        unchecked
        {
            int hash = 17;

            foreach (Transform room in dungeonRoot)
            {
                // A hand-rolled string hash, so it's guaranteed identical on every machine.
                foreach (char c in room.name) hash = hash * 31 + c;

                Vector3Int cell = Vector3Int.RoundToInt(room.position);
                hash = hash * 31 + cell.x;
                hash = hash * 31 + cell.y;
            }

            return hash;
        }
    }
}
