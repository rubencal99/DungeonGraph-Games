using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DungeonGraph.Editor
{
    /// <summary>
    /// Manages dungeon floors and their folder structure.
    /// Scans the Dungeon_Floors directory and provides utilities for floor creation.
    /// </summary>
    public static class DungeonFloorManager
    {
        private static string FLOORS_ROOT_PATH => DungeonGraphPaths.FloorsRoot;

        // Standard node type folders that should exist in each floor
        private static readonly string[] STANDARD_NODE_FOLDERS = new string[]
        {
            "Start",
            "Basic/Small",
            "Basic/Medium",
            "Basic/Large",
            "Hub",
            "End",
            "Boss",
            "Reward",
            "Debug"
        };

        /// <summary>
        /// Gets all available dungeon floors by scanning the Dungeon_Floors directory.
        /// </summary>
        public static List<DungeonFloorConfig> GetAllFloors()
        {
            List<DungeonFloorConfig> floors = new List<DungeonFloorConfig>();

            // Ensure root directory exists
            if (!AssetDatabase.IsValidFolder(FLOORS_ROOT_PATH))
            {
                Debug.LogWarning($"[DungeonFloorManager] Floors root directory does not exist: {FLOORS_ROOT_PATH}");
                return floors;
            }

            // Get all subdirectories in the Dungeon_Floors folder
            string fullPath = Path.Combine(Application.dataPath, FLOORS_ROOT_PATH.Replace("Assets/", ""));
            if (!Directory.Exists(fullPath))
            {
                Debug.LogWarning($"[DungeonFloorManager] Floors directory does not exist: {fullPath}");
                return floors;
            }

            string[] floorDirectories = Directory.GetDirectories(fullPath);
            foreach (string floorDir in floorDirectories)
            {
                string floorName = Path.GetFileName(floorDir);
                string relativePath = $"{FLOORS_ROOT_PATH}/{floorName}";
                floors.Add(new DungeonFloorConfig(floorName, relativePath));
            }

            return floors;
        }

        /// <summary>
        /// Creates a new dungeon floor with the specified name.
        /// Automatically creates all standard node type folders and populates them with blank rooms.
        /// </summary>
        public static bool CreateNewFloor(string floorName)
        {
            if (string.IsNullOrWhiteSpace(floorName))
            {
                Debug.LogError("[DungeonFloorManager] Floor name cannot be empty");
                return false;
            }

            // Block creation if an Addressable group with this name already exists —
            // group names mirror floor names and are the authoritative duplicate check.
            if (AddressableRoomRegistrar.FloorGroupExists(floorName))
            {
                EditorUtility.DisplayDialog(
                    "Duplicate Floor Name",
                    $"An Addressable group named \"{floorName}\" already exists.\n\nChoose a different floor name.",
                    "OK");
                return false;
            }

            // Ensure root directory exists
            EnsureRootDirectoryExists();

            // Create floor directory
            string floorPath = $"{FLOORS_ROOT_PATH}/{floorName}";
            if (AssetDatabase.IsValidFolder(floorPath))
            {
                EditorUtility.DisplayDialog(
                    "Floor Already Exists",
                    $"A floor folder named \"{floorName}\" already exists.\n\nChoose a different floor name.",
                    "OK");
                return false;
            }

            AssetDatabase.CreateFolder(FLOORS_ROOT_PATH, floorName);
            Debug.Log($"[DungeonFloorManager] Created floor: {floorName} at {floorPath}");

            // Create all standard node type folders
            CreateStandardNodeFolders(floorPath);

            // Get custom node types and create folders for them
            var customNodeRegistry = CustomNodeTypeRegistry.GetOrCreateDefault();
            foreach (var customType in customNodeRegistry.customNodeTypes)
            {
                CreateNodeFolderInFloor(floorPath, customType.typeName);
            }

            AssetDatabase.Refresh();

            // Register the floor label and all blank rooms created for this floor
            RegisterFloorInAddressables(floorPath, floorName);

            return true;
        }

        /// <summary>
        /// Creates an empty folder inside the Dungeon_Floors root with no subfolders or room prefabs.
        /// Useful for manually-managed floors.
        /// </summary>
        public static bool CreateEmptyFloor(string floorName)
        {
            if (string.IsNullOrWhiteSpace(floorName))
            {
                Debug.LogError("[DungeonFloorManager] Floor name cannot be empty");
                return false;
            }

            EnsureRootDirectoryExists();

            string floorPath = $"{FLOORS_ROOT_PATH}/{floorName}";
            if (AssetDatabase.IsValidFolder(floorPath))
            {
                Debug.LogError($"[DungeonFloorManager] Folder already exists: {floorName}");
                return false;
            }

            AssetDatabase.CreateFolder(FLOORS_ROOT_PATH, floorName);
            AssetDatabase.Refresh();
            return true;
        }

        /// <summary>
        /// Deletes a floor folder and all its contents from the project.
        /// </summary>
        public static bool DeleteFloor(string floorPath)
        {
            if (!AssetDatabase.IsValidFolder(floorPath))
            {
                Debug.LogError($"[DungeonFloorManager] Floor folder does not exist: {floorPath}");
                return false;
            }

            bool success = AssetDatabase.DeleteAsset(floorPath);
            if (success)
                AssetDatabase.Refresh();
            else
                Debug.LogError($"[DungeonFloorManager] Failed to delete floor: {floorPath}");

            return success;
        }

        /// <summary>
        /// Creates a new node type folder in all existing floors.
        /// Called when a new custom node type is created.
        /// </summary>
        public static void CreateNodeFolderInAllFloors(string nodeTypeName)
        {
            var floors = GetAllFloors();
            foreach (var floor in floors)
            {
                CreateNodeFolderInFloor(floor.folderPath, nodeTypeName);
            }

            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Creates a node type folder in a specific floor and generates a blank room.
        /// </summary>
        private static void CreateNodeFolderInFloor(string floorPath, string nodeTypeName)
        {
            string nodeFolderPath = $"{floorPath}/{nodeTypeName}";

            // Create folder if it doesn't exist
            if (!AssetDatabase.IsValidFolder(nodeFolderPath))
            {
                string parentFolder = Path.GetDirectoryName(nodeFolderPath).Replace("\\", "/");
                string folderName = Path.GetFileName(nodeFolderPath);

                // Ensure parent exists
                EnsureFolderExists(parentFolder);

                AssetDatabase.CreateFolder(parentFolder, folderName);
                Debug.Log($"[DungeonFloorManager] Created node folder: {nodeFolderPath}");
            }

            string assetPath = GenerateBlankRoomInFolder(nodeFolderPath);
            if (!string.IsNullOrEmpty(assetPath))
                AddressableRoomRegistrar.TryRegisterFromPath(assetPath);
        }

        /// <summary>
        /// Creates all standard node type folders in a floor.
        /// </summary>
        private static void CreateStandardNodeFolders(string floorPath)
        {
            foreach (string nodeFolder in STANDARD_NODE_FOLDERS)
            {
                string fullNodePath = $"{floorPath}/{nodeFolder}";
                string[] pathParts = nodeFolder.Split('/');

                string currentPath = floorPath;
                foreach (string part in pathParts)
                {
                    string nextPath = $"{currentPath}/{part}";
                    if (!AssetDatabase.IsValidFolder(nextPath))
                    {
                        AssetDatabase.CreateFolder(currentPath, part);
                    }
                    currentPath = nextPath;
                }

                // Generate a blank room in this folder
                GenerateBlankRoomInFolder(fullNodePath);
            }
        }

        /// <summary>
        /// Generates a blank room prefab in the specified folder.
        /// First tries to use the Blank_Room template from Assets/DungeonGraph/Prefabs.
        /// Falls back to procedural generation if template is not found.
        /// </summary>
        private static string GenerateBlankRoomInFolder(string folderPath)
        {
            string TEMPLATE_PATH = DungeonGraphPaths.BlankRoomTemplate;
            GameObject room = null;

            GameObject templatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TEMPLATE_PATH);

            if (templatePrefab != null)
            {
                room = (GameObject)PrefabUtility.InstantiatePrefab(templatePrefab);
            }
            else
            {
                Debug.LogWarning($"[DungeonFloorManager] Blank_Room template not found at {TEMPLATE_PATH}, using procedural generation");

                room = new GameObject("Room");
                room.AddComponent<RoomTemplate>();

                GameObject grid = new GameObject("Grid");
                grid.transform.SetParent(room.transform);

                Grid gridComponent = grid.AddComponent<Grid>();
                gridComponent.cellSize = new Vector3(1, 1, 0);

                // No colliders on rooms: collision lives on the master tilemap.

                GameObject exits = new GameObject("Exits");
                exits.transform.SetParent(room.transform);

                GameObject tilemapFloor = new GameObject("Tilemap_Floor");
                tilemapFloor.transform.SetParent(grid.transform);

                UnityEngine.Tilemaps.Tilemap tilemap = tilemapFloor.AddComponent<UnityEngine.Tilemaps.Tilemap>();
                UnityEngine.Tilemaps.TilemapRenderer tilemapRenderer = tilemapFloor.AddComponent<UnityEngine.Tilemaps.TilemapRenderer>();
                tilemapRenderer.sortingOrder = 0;
            }

            string uniquePath = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/Room.prefab");

            PrefabUtility.SaveAsPrefabAsset(room, uniquePath);
            GameObject.DestroyImmediate(room);

            return uniquePath;
        }

        /// <summary>
        /// Registers the floor label in Addressables and labels every blank room just created.
        /// </summary>
        private static void RegisterFloorInAddressables(string floorPath, string floorName)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { floorPath });
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                AddressableRoomRegistrar.TryRegisterFromPath(assetPath);
            }
        }

        /// <summary>
        /// Ensures the Dungeon_Floors root directory exists.
        /// </summary>
        private static void EnsureRootDirectoryExists()
        {
            if (AssetDatabase.IsValidFolder(FLOORS_ROOT_PATH))
                return;

            // The floors root must be created inside the package. This previously created
            // "Assets/Dungeon_Floors" at the project root, which scattered package content
            // outside its own folder and left the real root still missing.
            if (!DungeonGraphPaths.EnsureFolderExists(FLOORS_ROOT_PATH))
                Debug.LogError($"[DungeonFloorManager] Could not create the floors root at {FLOORS_ROOT_PATH}.");
        }

        /// <summary>
        /// Recursively ensures a folder path exists, creating parent folders as needed.
        /// </summary>
        private static void EnsureFolderExists(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            string parentFolder = Path.GetDirectoryName(folderPath).Replace("\\", "/");
            string folderName = Path.GetFileName(folderPath);

            if (!AssetDatabase.IsValidFolder(parentFolder))
            {
                EnsureFolderExists(parentFolder);
            }

            AssetDatabase.CreateFolder(parentFolder, folderName);
        }

        /// <summary>
        /// Gets the room folder path for a specific node type on a specific floor.
        /// </summary>
        public static string GetRoomFolderPath(string floorPath, string nodeTypeName, string sizeCategory = null)
        {
            if (nodeTypeName == "BasicNode" && !string.IsNullOrEmpty(sizeCategory))
            {
                return $"{floorPath}/Basic/{sizeCategory}";
            }
            else
            {
                string typeFolder = nodeTypeName.Replace("Node", "");
                return $"{floorPath}/{typeFolder}";
            }
        }
    }
}
