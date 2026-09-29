using UnityEditor;
using UnityEngine;

namespace DungeonGraph.Editor
{
    [CustomEditor(typeof(DungeonTilemapSystem))]
    public class DungeonTilemapSystemEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            DungeonTilemapSystem tilemapSystem = (DungeonTilemapSystem)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Tilemap Operations", EditorStyles.boldLabel);

            // Button to merge rooms to master tilemap
            if (GUILayout.Button("Merge Rooms to Master Tilemap", GUILayout.Height(30)))
            {
                if (tilemapSystem.masterTilemap == null)
                {
                    EditorUtility.DisplayDialog("Error", "Please assign a Master Tilemap first!", "OK");
                }
                else
                {
                    // Find all room instances in the scene
                    var roomInstances = new System.Collections.Generic.Dictionary<string, GameObject>();
                    var roomTemplates = FindObjectsByType<RoomTemplate>(FindObjectsSortMode.None);

                    for (int i = 0; i < roomTemplates.Length; i++)
                    {
                        // Use the room's index as key since we don't have node IDs in this context
                        roomInstances[i.ToString()] = roomTemplates[i].gameObject;
                    }

                    tilemapSystem.MergeRoomsToMasterTilemap(roomInstances);
                    EditorUtility.DisplayDialog("Success",
                        $"Merged {roomTemplates.Length} rooms to master tilemap!", "OK");
                }
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Corridor Generation", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "Corridor settings (tile, width, type) are configured in the Dungeon Tools panel " +
                "of the Dungeon Graph editor and applied automatically at generation time.",
                MessageType.Info
            );

            EditorGUILayout.Space(5);

            // Display runtime-relevant settings only
            EditorGUILayout.LabelField("Current Settings:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Grid Cell Size: {tilemapSystem.gridCellSize}");
            EditorGUILayout.LabelField($"Master Tilemap: {(tilemapSystem.masterTilemap != null ? tilemapSystem.masterTilemap.name : "Not Assigned")}");
        }
    }
}
