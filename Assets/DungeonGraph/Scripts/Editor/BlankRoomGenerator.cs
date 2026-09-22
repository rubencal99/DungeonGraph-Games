using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;

namespace DungeonGraph.Editor
{
    public static class BlankRoomGenerator
    {
        private static string TEMPLATE_PATH => DungeonGraphPaths.BlankRoomTemplate;

        [MenuItem("Assets/Create/Dungeon Graph/Blank Room", false, 0)]
        public static void CreateBlankRoom()
        {
            GameObject room = null;

            // Try to load the template prefab
            GameObject templatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TEMPLATE_PATH);

            if (templatePrefab != null)
            {
                // Instantiate from template
                room = (GameObject)PrefabUtility.InstantiatePrefab(templatePrefab);
                //Debug.Log($"[BlankRoomGenerator] Using Blank_Room template from {TEMPLATE_PATH}");
            }
            else
            {
                // Fall back to procedural generation
                Debug.LogWarning($"[BlankRoomGenerator] Blank_Room template not found at {TEMPLATE_PATH}, using procedural generation");

                // Create root Room object
                room = new GameObject("Room");
                room.AddComponent<RoomTemplate>();

                // Create Grid child
                GameObject grid = new GameObject("Grid");
                grid.transform.SetParent(room.transform);

                Grid gridComponent = grid.AddComponent<Grid>();
                gridComponent.cellSize = new Vector3(1, 1, 0);

                Rigidbody2D rb = grid.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Static;

                CompositeCollider2D compositeCollider = grid.AddComponent<CompositeCollider2D>();
                compositeCollider.geometryType = CompositeCollider2D.GeometryType.Polygons;

                // Create Exits child
                GameObject exits = new GameObject("Exits");
                exits.transform.SetParent(room.transform);

                // Create Tilemap_Floor child (child of Grid)
                GameObject tilemapFloor = new GameObject("Tilemap_Floor");
                tilemapFloor.transform.SetParent(grid.transform);

                Tilemap tilemap = tilemapFloor.AddComponent<Tilemap>();
                TilemapRenderer tilemapRenderer = tilemapFloor.AddComponent<TilemapRenderer>();
                tilemapRenderer.sortingOrder = 0;

                // Optionally add TilemapCollider2D for collision (common in dungeon rooms)
                TilemapCollider2D tilemapCollider = tilemapFloor.AddComponent<TilemapCollider2D>();
                tilemapCollider.compositeOperation = Collider2D.CompositeOperation.Merge;
            }

            // Get the active folder path in the Project window
            string path = GetActiveFolderPath();

            // Create unique prefab name
            string prefabName = "Room.prefab";
            string uniquePath = AssetDatabase.GenerateUniqueAssetPath(path + "/" + prefabName);

            // Save as prefab
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(room, uniquePath);

            // Clean up the scene instance
            GameObject.DestroyImmediate(room);

            // Select the newly created prefab
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);

            // Auto-register in Addressables if saved inside Dungeon_Floors
            AddressableRoomRegistrar.TryRegisterFromPath(uniquePath);

            Debug.Log($"[BlankRoomGenerator] Created blank room prefab at: {uniquePath}");
        }

        /// <summary>
        /// The folder a new blank room should be written to: whatever is selected in the
        /// Project window, falling back to the package's own floors root.
        /// </summary>
        /// <remarks>
        /// The fallback deliberately never resolves to "Assets". Writing a loose Room.prefab
        /// into the project root scatters package content outside its own folder, which is
        /// both untidy for the user and an Asset Store submission violation.
        /// </remarks>
        private static string GetActiveFolderPath()
        {
            foreach (Object obj in Selection.GetFiltered(typeof(Object), SelectionMode.Assets))
            {
                string selected = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(selected))
                    continue;

                if (System.IO.File.Exists(selected))
                    selected = selected.Substring(0, selected.LastIndexOf('/'));

                // Only honour a selection that is a real folder below the project root.
                if (AssetDatabase.IsValidFolder(selected) && !PathsAreEqual(selected, "Assets"))
                    return selected;

                break;
            }

            string fallback = DungeonGraphPaths.FloorsRoot;
            DungeonGraphPaths.EnsureFolderExists(fallback);
            return AssetDatabase.IsValidFolder(fallback) ? fallback : DungeonGraphPaths.PackageRoot;
        }

        private static bool PathsAreEqual(string a, string b)
        {
            return string.Equals(a.TrimEnd('/'), b.TrimEnd('/'), System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
