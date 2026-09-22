using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using DungeonGraph;

namespace DungeonGraph.Editor
{
    /// <summary>
    /// Describes a room type: its display name, subfolder path relative to a floor root,
    /// and the Addressable label used to identify it at runtime.
    /// </summary>
    [Serializable]
    public struct RoomTypeInfo
    {
        public string displayName;       // "Basic (Small)"
        public string folderPath;        // "Basic/Small"  (relative to floor root)
        public string addressableLabel;  // "Basic_Small"

        public RoomTypeInfo(string displayName, string folderPath, string addressableLabel)
        {
            this.displayName       = displayName;
            this.folderPath        = folderPath;
            this.addressableLabel  = addressableLabel;
        }

        public override string ToString() => displayName;
    }

    /// <summary>
    /// Editor utility for registering room prefabs in the Addressables system.
    /// Handles label creation, entry registration, and path-to-label inference.
    /// </summary>
    public static class AddressableRoomRegistrar
    {
        /// <summary>Root folder holding one subfolder per floor. Follows the package if it is moved.</summary>
        public static string FLOORS_ROOT => DungeonGraphPaths.FloorsRoot;

        // -------------------------------------------------------------------------
        // Room type catalogue
        // -------------------------------------------------------------------------

        private static readonly RoomTypeInfo[] k_StandardTypes =
        {
            new RoomTypeInfo("Start",          "Start",        "Start"),
            new RoomTypeInfo("End",            "End",          "End"),
            new RoomTypeInfo("Hub",            "Hub",          "Hub"),
            new RoomTypeInfo("Boss",           "Boss",         "Boss"),
            new RoomTypeInfo("Reward",         "Reward",       "Reward"),
            new RoomTypeInfo("Basic (Small)",  "Basic/Small",  "Basic_Small"),
            new RoomTypeInfo("Basic (Medium)", "Basic/Medium", "Basic_Medium"),
            new RoomTypeInfo("Basic (Large)",  "Basic/Large",  "Basic_Large"),
            new RoomTypeInfo("Debug",          "Debug",        "Debug"),
        };

        /// <summary>Returns all room types: standard built-in types plus registered custom node types.</summary>
        public static RoomTypeInfo[] GetAllRoomTypes()
        {
            var result = new List<RoomTypeInfo>(k_StandardTypes);

            var registry = CustomNodeTypeRegistry.GetOrCreateDefault();
            if (registry != null)
            {
                foreach (var ct in registry.customNodeTypes)
                {
                    result.Add(new RoomTypeInfo(ct.typeName, ct.typeName, ct.typeName));
                }
            }

            return result.ToArray();
        }

        // -------------------------------------------------------------------------
        // Addressables registration
        // -------------------------------------------------------------------------

        /// <summary>Returns true if an Addressable group named <paramref name="floorName"/> already exists.</summary>
        public static bool FloorGroupExists(string floorName)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            return settings != null && settings.FindGroup(floorName) != null;
        }

        /// <summary>
        /// Returns the Addressable group for <paramref name="floorName"/>, creating it if it doesn't exist.
        /// The new group copies schemas from the default group so it inherits the same build settings.
        /// </summary>
        public static AddressableAssetGroup GetOrCreateFloorGroup(string floorName)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) return null;

            var group = settings.FindGroup(floorName);
            if (group != null)
            {
                EnsureGroupHasSchemas(settings, group);
                return group;
            }

            group = settings.CreateGroup(floorName, setAsDefaultGroup: false, readOnly: false,
                                         postEvent: false, schemasToCopy: settings.DefaultGroup?.Schemas);
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.GroupAdded, group, postEvent: true);
            return group;
        }

        /// <summary>
        /// Repairs a group that has lost its schemas by copying them from the default group.
        /// </summary>
        /// <remarks>
        /// A group with no schemas is silently excluded from the Addressables build — Unity
        /// warns "&lt;group&gt; does not have any associated AddressableAssetGroupSchemas" at build
        /// time, and at runtime every load for that group's labels returns nothing. Groups can
        /// end up in this state through merge conflicts, hand-edited asset files, or an
        /// interrupted import, and until now nothing put them back, because the group already
        /// existed so creation was skipped.
        /// </remarks>
        private static void EnsureGroupHasSchemas(AddressableAssetSettings settings, AddressableAssetGroup group)
        {
            if (group.Schemas != null && group.Schemas.Count > 0)
                return;

            var template = settings.DefaultGroup;
            if (template == null || template == group || template.Schemas == null || template.Schemas.Count == 0)
            {
                Debug.LogWarning(
                    $"[Dungeon Graph] Addressable group '{group.Name}' has no schemas and the default " +
                    "group has none to copy. Open Window > Asset Management > Addressables > Groups, " +
                    "select the group, and add a Content Packing & Loading schema. Until then its " +
                    "rooms will not be included in a build.");
                return;
            }

            // The instance overload copies the schema's values, so the repaired group inherits
            // the default group's build and load paths rather than starting from blank defaults.
            foreach (var schema in template.Schemas)
            {
                if (schema != null)
                    group.AddSchema(schema, postEvent: false);
            }

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.GroupSchemaAdded, group, postEvent: true);
            EditorUtility.SetDirty(group);

            Debug.Log($"[Dungeon Graph] Repaired Addressable group '{group.Name}': " +
                      $"restored {group.Schemas.Count} schema(s) from '{template.Name}'. " +
                      "Rebuild Addressables content before making a player build.");
        }

        /// <summary>
        /// Marks the prefab at <paramref name="assetPath"/> as Addressable, places it in the
        /// floor-named Addressable group (creating that group if needed), and assigns
        /// <paramref name="floorLabel"/> and <paramref name="typeLabel"/> labels to it.
        /// </summary>
        public static void RegisterRoomPrefab(string assetPath, string floorLabel, string typeLabel)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError("[AddressableRoomRegistrar] Addressable Asset Settings not found. " +
                               "Create them via Window > Asset Management > Addressables > Groups.");
                return;
            }

            // Ensure both labels exist in the settings
            settings.AddLabel(floorLabel, postEvent: false);
            settings.AddLabel(typeLabel,  postEvent: false);

            string guid  = AssetDatabase.AssetPathToGUID(assetPath);
            var    group = GetOrCreateFloorGroup(floorLabel) ?? settings.DefaultGroup;
            var    entry = settings.CreateOrMoveEntry(guid, group, readOnly: false, postEvent: false);

            entry.address = assetPath;
            entry.SetLabel(floorLabel, enable: true, postEvent: false);
            entry.SetLabel(typeLabel,  enable: true, postEvent: false);

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, postEvent: true);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Removes the prefab at <paramref name="assetPath"/> from Addressables (if registered).
        /// </summary>
        public static void UnregisterRoomPrefab(string assetPath)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) return;

            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            settings.RemoveAssetEntry(guid, postEvent: true);
            AssetDatabase.SaveAssets();
        }

        // -------------------------------------------------------------------------
        // Path inference
        // -------------------------------------------------------------------------

        /// <summary>
        /// Attempts to derive floor and type labels from a prefab path inside Dungeon_Floors.
        /// Returns false if the path is outside the floors root or has unrecognised structure.
        ///
        /// Expected patterns:
        ///   FLOORS_ROOT/Floor_1/Start/Room.prefab       → floor="Floor_1"  type="Start"
        ///   FLOORS_ROOT/Floor_1/Basic/Small/Room.prefab → floor="Floor_1"  type="Basic_Small"
        /// </summary>
        public static bool TryGetLabelsFromPath(string assetPath, out string floorLabel, out string typeLabel)
        {
            floorLabel = typeLabel = null;

            string prefix = FLOORS_ROOT + "/";
            if (!assetPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;

            // Strip floors root and filename
            string relative  = assetPath.Substring(prefix.Length);     // "Floor_1/Basic/Small/Room.prefab"
            string withoutFile = Path.GetDirectoryName(relative)?.Replace("\\", "/"); // "Floor_1/Basic/Small"
            if (string.IsNullOrEmpty(withoutFile)) return false;

            string[] parts = withoutFile.Split('/');
            // parts[0] = floor name, parts[1..] = type path segments
            if (parts.Length < 2) return false;

            floorLabel = parts[0];

            int typeParts = parts.Length - 1;
            if (typeParts == 1)
                typeLabel = parts[1];                   // "Start", "Hub", "Boss" …
            else if (typeParts == 2)
                typeLabel = $"{parts[1]}_{parts[2]}";   // "Basic_Small", "Basic_Medium" …
            else
                return false;

            return !string.IsNullOrEmpty(floorLabel) && !string.IsNullOrEmpty(typeLabel);
        }

        /// <summary>
        /// Convenience overload: registers a prefab by inferring its labels from its path.
        /// Does nothing (with a log) if the path is not inside FLOORS_ROOT.
        /// </summary>
        public static void TryRegisterFromPath(string assetPath)
        {
            if (!TryGetLabelsFromPath(assetPath, out string floorLabel, out string typeLabel))
            {
                Debug.Log($"[AddressableRoomRegistrar] Skipping auto-label: '{assetPath}' is not inside {FLOORS_ROOT}.");
                return;
            }
            RegisterRoomPrefab(assetPath, floorLabel, typeLabel);
        }
    }
}
