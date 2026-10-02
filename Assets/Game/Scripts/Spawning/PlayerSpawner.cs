using DungeonGraph;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Puts every player into the dungeon and gives this machine its own camera.
///
/// Two jobs, on two kinds of machine:
///   * The host waits until every machine has built the dungeon (DungeonNetwork),
///     then spawns one avatar per player around the Start room, each owned by
///     its player.
///   * Every machine, when its own avatar arrives, builds a camera rig that
///     follows it. Cameras are never shared; you only ever see through yours.
///
/// It also creates this machine's projectile pool, since every machine spawns
/// its own copies of every bullet.
///
/// Setup:
///   1. Add to the gameplay scene's DungeonGenerator object, next to DungeonNetwork.
///   2. Drag in DungeonNetwork and the Player, Cinemachine Camera,
///      AimCameraTarget, and ProjectilePoolManager prefabs. The scene needs no
///      hand-placed copies of any of these.
/// </summary>
public class PlayerSpawner : MonoBehaviour
{
    [SerializeField] private DungeonNetwork m_dungeon;
    [SerializeField] private GameObject m_player;
    [SerializeField] private GameObject m_aimPlane;
    [SerializeField] private CinemachineCamera m_cinemachineCamera;
    [SerializeField] private AimCameraTarget m_aimCameraTarget;
    [SerializeField] private ProjectilePoolManager m_projectilePoolManager;

    [Tooltip("How far apart players appear around the Start room's center, so they don't spawn inside each other.")]
    [SerializeField, Min(0f)] private float m_spawnSpread = 1f;

    private void Awake()
    {
        if (m_projectilePoolManager != null) Instantiate(m_projectilePoolManager);
    }

    private void OnEnable()
    {
        m_dungeon.AllReady += SpawnPlayers;
        PlayerNetwork.LocalSpawned += BuildCameraRig;
    }

    private void OnDisable()
    {
        m_dungeon.AllReady -= SpawnPlayers;
        PlayerNetwork.LocalSpawned -= BuildCameraRig;
    }

    /// <summary>Host only: one avatar per connected player, owned by that player.</summary>
    private void SpawnPlayers(DungeonGenerationResult result)
    {
        var startRooms = result.GetStartRoom();
        if (startRooms.Count == 0)
        {
            Debug.LogWarning("[PlayerSpawner] Dungeon generated with no Start room; players not spawned.", this);
            return;
        }

        Vector2 center = startRooms[0].roomCenter;
        var clientIds = NetworkManager.Singleton.ConnectedClientsIds;

        for (int i = 0; i < clientIds.Count; i++)
        {
            // Evenly around a small circle; a lone player stands at the center.
            float angle = i * Mathf.PI * 2f / clientIds.Count;
            Vector2 offset = clientIds.Count > 1 ? new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * m_spawnSpread : Vector2.zero;

            GameObject player = Instantiate(m_player, center + offset, Quaternion.identity);
            player.GetComponent<NetworkObject>().SpawnWithOwnership(clientIds[i], destroyWithScene: true);
        }
    }

    /// <summary>Every machine: a camera that follows this machine's own player.</summary>
    private void BuildCameraRig(PlayerNetwork player)
    {
        // AimPlane only needs to sit at the right depth (Z = 0, matching the
        // gameplay plane) — its X/Y follows the spawn point so its fixed size
        // still covers the area around the player regardless of where the
        // Start room landed in this run's layout.
        //if (m_aimPlane != null) Instantiate(m_aimPlane, new Vector3(spawnPoint.x, spawnPoint.y, 0f), new Quaternion(90f, 0f, 0f, 0f));

        Vector3 position = player.transform.position;

        AimCameraTarget cameraTarget = m_aimCameraTarget != null
            ? Instantiate(m_aimCameraTarget, position, Quaternion.identity)
            : null;
        if (cameraTarget != null) cameraTarget.SetPlayerAim(player.Aim);

        if (m_cinemachineCamera != null)
        {
            CinemachineCamera cameraInstance = Instantiate(m_cinemachineCamera, position, Quaternion.identity);
            if (cameraTarget != null) cameraInstance.Follow = cameraTarget.transform;
        }
    }
}
