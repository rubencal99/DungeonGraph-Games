using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace DungeonGraph.Editor
{
    /// <summary>
    /// Registers the bundled room prefabs with Addressables so the runtime generator can
    /// load them by floor and room-type label.
    /// </summary>
    /// <remarks>
    /// Addressables stores its configuration in <c>Assets/AddressableAssetsData</c> — a folder
    /// at the project root, outside this package. Creating it is therefore never done silently:
    /// a project that has not opted into Addressables is left exactly as it was, and the user is
    /// asked before anything outside the Dungeon Graph folder is written.
    ///
    /// Once Addressables is initialised, registering rooms only adds labels and groups to data
    /// the project already owns, so that runs without prompting.
    /// </remarks>
    [InitializeOnLoad]
    static class DungeonGraphAddressablesSetup
    {
        static readonly string k_SessionKey = "DungeonGraph.AddressablesSetup." + Application.dataPath.GetHashCode();
        const string k_DeclinedKey = "DungeonGraph.AddressablesSetupDeclined";

        static DungeonGraphAddressablesSetup()
        {
            if (!SessionState.GetBool(k_SessionKey, false))
                EditorApplication.delayCall += RunAutomaticSetup;
        }

        /// <summary>
        /// Runs once per editor session. Only touches the project if Addressables is already
        /// initialised; otherwise it asks first.
        /// </summary>
        static void RunAutomaticSetup()
        {
            SessionState.SetBool(k_SessionKey, true);

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings != null)
            {
                Register(settings, verbose: false);
                return;
            }

            if (EditorPrefs.GetBool(k_DeclinedKey, false))
                return;

            if (!AskToInitialiseAddressables())
            {
                EditorPrefs.SetBool(k_DeclinedKey, true);
                Debug.Log("[Dungeon Graph] Skipped Addressables setup. The sample rooms will not " +
                          "load at runtime until you run Tools > Dungeon Graph > Setup > " +
                          "Reinitialize Addressables.");
                return;
            }

            settings = AddressableAssetSettingsDefaultObject.GetSettings(create: true);
            if (settings == null)
            {
                Debug.LogError("[Dungeon Graph] Failed to create Addressable Asset Settings.");
                return;
            }

            Register(settings, verbose: true);
        }

        /// <summary>
        /// Re-scans the floors folder and re-registers every room prefab. Repairs a broken or
        /// missing Addressables configuration.
        /// </summary>
        [MenuItem("Tools/Dungeon Graph/Setup/Reinitialize Addressables")]
        public static void ForceSetup()
        {
            SessionState.EraseBool(k_SessionKey);
            EditorPrefs.DeleteKey(k_DeclinedKey);

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                if (!AskToInitialiseAddressables())
                    return;

                settings = AddressableAssetSettingsDefaultObject.GetSettings(create: true);
                if (settings == null)
                {
                    Debug.LogError("[Dungeon Graph] Failed to create Addressable Asset Settings.");
                    return;
                }
            }

            Register(settings, verbose: true);
        }

        /// <summary>
        /// Asks permission before creating Addressables' project-root data folder.
        /// </summary>
        static bool AskToInitialiseAddressables()
        {
            return EditorUtility.DisplayDialog(
                "Dungeon Graph — Set Up Addressables",
                "Dungeon Graph loads room prefabs through Addressables, which is not yet " +
                "initialised in this project.\n\n" +
                "Setting it up creates the folder:\n\n" +
                "    Assets/AddressableAssetsData\n\n" +
                "This is Unity's standard Addressables location and sits outside the Dungeon " +
                "Graph folder, so you are asked first.\n\n" +
                "Without it, the graph editor still works, but generated rooms will not load " +
                "at runtime. You can do this later from " +
                "Tools > Dungeon Graph > Setup > Reinitialize Addressables.",
                "Set Up Addressables",
                "Not Now");
        }

        static void Register(AddressableAssetSettings settings, bool verbose)
        {
            EnsureLabels(settings);
            RegisterExistingRooms();

            if (verbose)
                Debug.Log("[Dungeon Graph] Addressables setup complete.");
        }

        static void EnsureLabels(AddressableAssetSettings settings)
        {
            // Standard room-type labels
            foreach (var info in AddressableRoomRegistrar.GetAllRoomTypes())
                settings.AddLabel(info.addressableLabel, postEvent: false);

            // Floor labels — one per subdirectory of the floors root
            if (!AssetDatabase.IsValidFolder(AddressableRoomRegistrar.FLOORS_ROOT))
                return;

            string fullPath = Path.GetFullPath(AddressableRoomRegistrar.FLOORS_ROOT);
            foreach (string dir in Directory.GetDirectories(fullPath))
                settings.AddLabel(Path.GetFileName(dir), postEvent: false);
        }

        static void RegisterExistingRooms()
        {
            if (!AssetDatabase.IsValidFolder(AddressableRoomRegistrar.FLOORS_ROOT))
                return;

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { AddressableRoomRegistrar.FLOORS_ROOT });
            foreach (string guid in guids)
                AddressableRoomRegistrar.TryRegisterFromPath(AssetDatabase.GUIDToAssetPath(guid));

            AssetDatabase.SaveAssets();
        }
    }
}
