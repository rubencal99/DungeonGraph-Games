using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DungeonGraph.Editor
{
    /// <summary>
    /// Provides editor-side factory delegates for OrganicGeneration.GenerateRooms.
    /// Uses AssetDatabase (editor-only) to load room prefabs from a floor folder.
    /// </summary>
    public static class OrganicGenerationEditorBridge
    {
        /// <summary>
        /// Returns a RoomFactory that loads prefabs from the given AssetDatabase floor folder.
        /// The factory tracks which prefabs have been used per type to maximise variety.
        /// </summary>
        public static OrganicGeneration.AsyncRoomFactory CreateRoomFactory(string floorPath)
        {
            var usedByFolder = new Dictionary<string, List<string>>();

            return (DungeonGraphNode node, Transform parent) =>
            {
                string folderPath = GetFolderPath(node, floorPath);
                string[] guids    = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });

                if (guids.Length == 0)
                {
                    Debug.LogError($"[OrganicGenerationEditorBridge] No prefabs found in {folderPath}");
                    return Task.FromResult<GameObject>(null);
                }

                string[] allPaths = guids.Select(AssetDatabase.GUIDToAssetPath).ToArray();

                if (!usedByFolder.TryGetValue(folderPath, out var used))
                    usedByFolder[folderPath] = used = new List<string>();

                var available = allPaths.Where(p => !used.Contains(p)).ToList();
                if (available.Count == 0)
                {
                    used.Clear();
                    available = allPaths.ToList();
                }

                string prefabPath = available[Random.Range(0, available.Count)];
                used.Add(prefabPath);

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    Debug.LogError($"[OrganicGenerationEditorBridge] Could not load prefab at {prefabPath}");
                    return Task.FromResult<GameObject>(null);
                }

                GameObject roomObj = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                if (parent != null) roomObj.transform.SetParent(parent);
                return Task.FromResult(roomObj);
            };
        }

        /// <summary>
        /// Returns a BoundsProvider that reads the RoomTemplate.worldBounds from a prefab
        /// in the given AssetDatabase floor folder without instantiating it.
        /// </summary>
        public static OrganicGeneration.AsyncBoundsProvider CreateBoundsProvider(string floorPath)
        {
            return (DungeonGraphNode node) =>
            {
                string folderPath = GetFolderPath(node, floorPath);
                string[] guids    = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });

                if (guids.Length > 0)
                {
                    string prefabPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                    var    prefab     = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    if (prefab != null)
                    {
                        var tmpl = prefab.GetComponent<RoomTemplate>();
                        if (tmpl != null && tmpl.worldBounds.size.magnitude > 0.1f)
                            return Task.FromResult(tmpl.worldBounds);
                    }
                }

                return Task.FromResult(new Bounds(Vector3.zero, new Vector3(10f, 10f, 1f)));
            };
        }

        // -------------------------------------------------------------------------

        private static string GetFolderPath(DungeonGraphNode node, string floorPath)
        {
            string basePath  = string.IsNullOrEmpty(floorPath) ? "Assets/DungeonGraph/Rooms" : floorPath;
            string typeName  = node.GetType().Name;

            if (typeName == "BasicNode")
            {
                string size  = "Small";
                var sizeField = node.GetType().GetField("size");
                if (sizeField != null) size = sizeField.GetValue(node)?.ToString() ?? "Small";
                return $"{basePath}/Basic/{size}";
            }

            if (node is CustomNode cn)
                return $"{basePath}/{cn.customTypeName}";

            return $"{basePath}/{typeName.Replace("Node", "")}";
        }
    }
}
