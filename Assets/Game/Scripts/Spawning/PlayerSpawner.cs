using DungeonGraph;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Spawns the player and its supporting rig — AimPlane, Cinemachine camera,
/// its aim-pulled tracking target, and the projectile pool — at the Start
/// room's center once the DungeonGraph generator has finished building the
/// dungeon, and wires the references between them that cross prefab
/// boundaries. Those can't be wired in the Inspector ahead of time: the
/// Player doesn't exist until generation completes, and the room's location
/// isn't known until then either. See the plugin's own
/// Documentation/4_Runtime_API.md for the events/result API this consumes.
///
/// Setup:
///   1. Add to the same GameObject as a DungeonGraph DungeonGenerator
///      component (the one already configured with a Dungeon Graph asset and
///      Floor Label in the Testing/gameplay scene).
///   2. Drag the Player, AimPlane, Cinemachine Camera, AimCameraTarget, and
///      ProjectilePoolManager prefabs into their slots. The scene no longer
///      needs hand-placed instances of any of these — remove any leftover
///      ones so you don't end up with duplicates alongside the spawned rig.
/// </summary>
[RequireComponent(typeof(DungeonGenerator))]
public class PlayerSpawner : MonoBehaviour
{
    [SerializeField] private GameObject m_player;
    [SerializeField] private GameObject m_aimPlane;
    [SerializeField] private CinemachineCamera m_cinemachineCamera;
    [SerializeField] private AimCameraTarget m_aimCameraTarget;
    [SerializeField] private ProjectilePoolManager m_projectilePoolManager;

    private DungeonGenerator m_generator;

    private void Awake()
    {
        m_generator = GetComponent<DungeonGenerator>();
    }

    private void OnEnable()
    {
        m_generator.OnGenerationComplete += HandleGenerationComplete;
        m_generator.OnGenerationFailed += HandleGenerationFailed;
    }

    private void OnDisable()
    {
        m_generator.OnGenerationComplete -= HandleGenerationComplete;
        m_generator.OnGenerationFailed -= HandleGenerationFailed;
    }

    private void HandleGenerationComplete(DungeonGenerationResult result)
    {
        var startRooms = result.GetStartRoom();
        if (startRooms.Count == 0)
        {
            Debug.LogWarning("[PlayerSpawner] Dungeon generated with no Start room; player not spawned.", this);
            return;
        }

        SpawnRig(startRooms[0].roomCenter);
    }

    private void SpawnRig(Vector3 spawnPoint)
    {
        if (m_projectilePoolManager != null) Instantiate(m_projectilePoolManager);

        // AimPlane only needs to sit at the right depth (Z = 0, matching the
        // gameplay plane) — its X/Y follows the spawn point so its fixed size
        // still covers the area around the player regardless of where the
        // Start room landed in this run's layout.
        //if (m_aimPlane != null) Instantiate(m_aimPlane, new Vector3(spawnPoint.x, spawnPoint.y, 0f), new Quaternion(90f, 0f, 0f, 0f));

        if (m_player == null) return;
        GameObject playerInstance = Instantiate(m_player, spawnPoint, Quaternion.identity);
        PlayerAim playerAim = playerInstance.GetComponent<PlayerAim>();

        AimCameraTarget cameraTarget = m_aimCameraTarget != null
            ? Instantiate(m_aimCameraTarget, spawnPoint, Quaternion.identity)
            : null;
        cameraTarget?.SetPlayerAim(playerAim);

        if (m_cinemachineCamera != null)
        {
            CinemachineCamera cameraInstance = Instantiate(m_cinemachineCamera);
            if (cameraTarget != null) cameraInstance.Follow = cameraTarget.transform;
        }
    }

    private void HandleGenerationFailed()
    {
        Debug.LogError("[PlayerSpawner] Dungeon generation failed; player not spawned.", this);
    }
}
