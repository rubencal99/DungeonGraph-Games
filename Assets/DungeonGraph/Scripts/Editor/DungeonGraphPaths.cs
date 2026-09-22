using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DungeonGraph.Editor
{
    /// <summary>
    /// Resolves the on-disk location of the Dungeon Graph package.
    /// </summary>
    /// <remarks>
    /// Every other editor script asks this class for its folders instead of hard-coding
    /// "Assets/DungeonGraph/...". That means the package keeps working after the user
    /// renames or relocates the folder, which is the single most common way a path-based
    /// tool breaks after import.
    ///
    /// The root is found by locating this script file itself and walking up two levels
    /// (Scripts/Editor -> Scripts -> package root). The result is cached until the next
    /// domain reload.
    /// </remarks>
    public static class DungeonGraphPaths
    {
        private const string k_FallbackRoot = "Assets/DungeonGraph";
        private const string k_StyleSheetName = "DungeonGraphEditor";

        private static string s_cachedRoot;

        /// <summary>Package root, e.g. "Assets/DungeonGraph". Never null.</summary>
        public static string PackageRoot
        {
            get
            {
                if (!string.IsNullOrEmpty(s_cachedRoot) && AssetDatabase.IsValidFolder(s_cachedRoot))
                    return s_cachedRoot;

                s_cachedRoot = ResolveRoot();
                return s_cachedRoot;
            }
        }

        /// <summary>Root folder holding one subfolder per floor.</summary>
        public static string FloorsRoot => PackageRoot + "/Dungeon_Floors";

        /// <summary>Folder holding the shared prefabs (Blank_Room, Master_Tilemap, DungeonGenerator).</summary>
        public static string PrefabsRoot => PackageRoot + "/Prefabs";

        /// <summary>Path to the Blank_Room template prefab.</summary>
        public static string BlankRoomTemplate => PrefabsRoot + "/Blank_Room.prefab";

        /// <summary>Loads the graph editor stylesheet, or returns null if it cannot be found.</summary>
        public static StyleSheet LoadEditorStyleSheet()
        {
            string direct = PackageRoot + "/Scripts/Editor/USS/" + k_StyleSheetName + ".uss";
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(direct);
            if (sheet != null)
                return sheet;

            // The folder layout changed or the file was moved — search the project for it.
            foreach (string guid in AssetDatabase.FindAssets(k_StyleSheetName + " t:StyleSheet"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) != k_StyleSheetName)
                    continue;

                sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                if (sheet != null)
                    return sheet;
            }

            Debug.LogWarning(
                "[Dungeon Graph] Could not find DungeonGraphEditor.uss. The graph editor will " +
                "open with default styling. Reimport the package to restore it.");
            return null;
        }

        /// <summary>
        /// Creates every missing folder along an "Assets/a/b/c" path. Returns false if the
        /// path is malformed.
        /// </summary>
        public static bool EnsureFolderExists(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath))
                return false;

            folderPath = folderPath.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(folderPath))
                return true;

            string[] parts = folderPath.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
                return false;

            string current = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }

            return AssetDatabase.IsValidFolder(folderPath);
        }

        private static string ResolveRoot()
        {
            // Locate this very script, then walk up out of Scripts/Editor.
            foreach (string guid in AssetDatabase.FindAssets("DungeonGraphPaths t:MonoScript"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith("/DungeonGraphPaths.cs"))
                    continue;

                // <root>/Scripts/Editor/DungeonGraphPaths.cs
                string editorDir = Path.GetDirectoryName(path).Replace('\\', '/');
                string scriptsDir = Path.GetDirectoryName(editorDir)?.Replace('\\', '/');
                string root = Path.GetDirectoryName(scriptsDir)?.Replace('\\', '/');

                if (!string.IsNullOrEmpty(root) && AssetDatabase.IsValidFolder(root))
                    return root;
            }

            return k_FallbackRoot;
        }
    }
}
