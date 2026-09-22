using UnityEngine;

namespace DungeonGraph
{
    /// <summary>
    /// Worked example: generates a dungeon at runtime and drops a player prefab into the
    /// Start room. Copy this into your own project and adapt it — it is a sample, not a
    /// required part of the pipeline.
    /// </summary>
    [AddComponentMenu("Dungeon Graph/Samples/Runtime Dungeon Generator")]
    [RequireComponent(typeof(DungeonGenerator))]
    public class RuntimeDungeonGenerator : MonoBehaviour
    {
        [Tooltip("The generator to drive. Defaults to the one on this GameObject.")]
        [SerializeField]
        private DungeonGenerator dungeonGenerator;

        [Tooltip("Optional. Instantiated at the center of the Start room once generation finishes.")]
        [SerializeField]
        private GameObject player;

        private async void Start()
        {
            if (dungeonGenerator == null)
                dungeonGenerator = GetComponent<DungeonGenerator>();

            if (dungeonGenerator == null)
            {
                Debug.LogError("[RuntimeDungeonGenerator] No DungeonGenerator assigned.", this);
                return;
            }

            DungeonGenerationResult result = await dungeonGenerator.GenerateDungeon();

            if (result == null)
            {
                Debug.LogError("[RuntimeDungeonGenerator] Generation failed. See earlier errors.", this);
                return;
            }

            SpawnPlayerInStartRoom(result);
        }

        private void SpawnPlayerInStartRoom(DungeonGenerationResult result)
        {
            if (player == null)
                return; // No player assigned — generating the dungeon alone is a valid setup.

            var startRooms = result.GetRoom("Start");
            if (startRooms.Count == 0)
            {
                Debug.LogWarning("[RuntimeDungeonGenerator] Graph produced no Start room; player not spawned.", this);
                return;
            }

            var startRoom = startRooms[0];
            if (startRoom.gameObject != null)
                Instantiate(player, startRoom.roomCenter, Quaternion.identity);
        }
    }
}
