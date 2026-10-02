using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// The level's win condition: every enemy spawner has been triggered and no
/// enemy is left alive. The host checks a few times a second (both are just a
/// count, so it costs nothing) and flips one shared flag when it's true, which
/// shows the Level Complete screen for everyone.
///
/// Setup:
///   1. Add to the same object as DungeonNetwork (it shares that NetworkObject).
///   2. Drag in the DungeonNetwork.
/// </summary>
public class LevelProgress : NetworkBehaviour
{
    public static LevelProgress Instance { get; private set; }

    /// <summary>Raised on every machine when the level is won.</summary>
    public static event Action Completed;

    [SerializeField] private DungeonNetwork m_dungeon;
    [SerializeField, Min(0.05f)] private float m_checkInterval = 0.25f;

    private readonly NetworkVariable<bool> m_complete = new();

    private bool m_tracking;
    private float m_nextCheckTime;

    public bool IsComplete => m_complete.Value;

    public override void OnNetworkSpawn()
    {
        Instance = this;
        m_complete.OnValueChanged += OnCompleteChanged;

        if (IsServer) m_dungeon.AllReady += OnDungeonReady;
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this) Instance = null;
        m_complete.OnValueChanged -= OnCompleteChanged;

        if (IsServer) m_dungeon.AllReady -= OnDungeonReady;
    }

    private void OnDungeonReady(DungeonGraph.DungeonGenerationResult result)
    {
        // Spawners arrive inside room prefabs, so they all exist once the dungeon has been built.
        m_tracking = true;

        if (EnemySpawner.All.Count == 0)
        {
            Debug.LogWarning("[LevelProgress] This dungeon has no EnemySpawners, so the level counts as " +
                             "complete straight away. Add spawners to your room prefabs.", this);
        }
    }

    private void Update()
    {
        if (!IsServer || !m_tracking || m_complete.Value) return;
        if (Time.time < m_nextCheckTime) return;

        m_nextCheckTime = Time.time + m_checkInterval;

        if (EnemySpawner.AllTriggered && Enemy.ActiveCount == 0) m_complete.Value = true;
    }

    private void OnCompleteChanged(bool previous, bool current)
    {
        if (current) Completed?.Invoke();
    }
}
