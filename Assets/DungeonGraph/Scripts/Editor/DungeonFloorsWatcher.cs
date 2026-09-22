using System;
using UnityEditor;

namespace DungeonGraph.Editor
{
    /// <summary>
    /// Watches the Dungeon_Floors directory for external changes (Project window
    /// folder creation/deletion/rename) and fires OnFloorsChanged so any open
    /// DungeonGraphView can refresh its floor dropdown.
    /// </summary>
    public class DungeonFloorsWatcher : AssetPostprocessor
    {
        private static string FLOORS_ROOT => DungeonGraphPaths.FloorsRoot;

        public static event Action OnFloorsChanged;

        static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (AnyDirectChildChanged(importedAssets) ||
                AnyDirectChildChanged(deletedAssets) ||
                AnyDirectChildChanged(movedAssets) ||
                AnyDirectChildChanged(movedFromAssetPaths))
            {
                // Defer past the current import transaction so the asset database
                // is fully settled before the UI tries to re-scan it.
                EditorApplication.delayCall += () => OnFloorsChanged?.Invoke();
            }
        }

        // Only react to direct children of FLOORS_ROOT (the floor folders themselves),
        // not to room prefabs or subfolders inside a floor.
        private static bool AnyDirectChildChanged(string[] paths)
        {
            string prefix = FLOORS_ROOT + "/";
            foreach (var path in paths)
            {
                if (!path.StartsWith(prefix)) continue;
                string remainder = path.Substring(prefix.Length);
                if (!remainder.Contains("/"))
                    return true;
            }
            return false;
        }
    }
}
