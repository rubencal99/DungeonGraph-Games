using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace DungeonGraph.Setup
{
    /// <summary>
    /// First-run dependency check for Dungeon Graph.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This assembly references nothing and declares no define constraints, so it compiles
    /// in every project state — including the one it exists to fix, where the packages the
    /// rest of Dungeon Graph needs are absent.
    /// </para>
    /// <para>
    /// Detection is done at <em>compile time</em> through the versionDefines in this
    /// assembly's .asmdef, not through an asynchronous Package Manager query. Unity resolves
    /// versionDefines from the project manifest before compiling, so the symbols below are an
    /// exact, synchronous statement of which packages are installed. An async
    /// <c>Client.List</c> can fail or arrive late during the very first import, which is
    /// precisely when this check matters most.
    /// </para>
    /// </remarks>
    [InitializeOnLoad]
    public static class DungeonGraphSetupWizard
    {
        private const string k_SessionKey = "DungeonGraph.DependencyCheck";
        private const string k_SkipKey    = "DungeonGraph.SkipDependencyCheck";

        private static AddAndRemoveRequest s_installRequest;

        static DungeonGraphSetupWizard()
        {
            if (SessionState.GetBool(k_SessionKey, false) || EditorPrefs.GetBool(k_SkipKey, false))
                return;

            SessionState.SetBool(k_SessionKey, true);

            if (MissingPackages().Length > 0)
                EditorApplication.delayCall += () => PromptInstall(MissingPackages());
        }

        // ---------------------------------------------------------------------
        // Detection
        // ---------------------------------------------------------------------

        /// <summary>Packages Dungeon Graph cannot compile or run without, that are not installed.</summary>
        private static PackageRequirement[] MissingPackages()
        {
            var missing = new List<PackageRequirement>();

#if !DUNGEONGRAPH_HAS_ADDRESSABLES
            missing.Add(new PackageRequirement(
                "com.unity.addressables",
                "Addressables",
                "Loads room prefabs by floor and room-type label. The graph editor and the runtime generator both need it."));
#endif
#if !DUNGEONGRAPH_HAS_TILEMAP
            missing.Add(new PackageRequirement(
                "com.unity.2d.tilemap",
                "2D Tilemap",
                "Rooms and corridors are painted onto Tilemaps."));
#endif
#if !DUNGEONGRAPH_HAS_TILEMAP_EXTRAS
            missing.Add(new PackageRequirement(
                "com.unity.2d.tilemap.extras",
                "2D Tilemap Extras",
                "Provides Rule Tile, used by the bundled floor tiles."));
#endif
#if !DUNGEONGRAPH_HAS_SPRITE
            missing.Add(new PackageRequirement(
                "com.unity.2d.sprite",
                "2D Sprite",
                "Sprite Editor support for slicing your own tile sheets."));
#endif
            return missing.ToArray();
        }

        // ---------------------------------------------------------------------
        // Menu entry points
        // ---------------------------------------------------------------------

        [MenuItem("Tools/Dungeon Graph/Setup/Check Dependencies", false, 0)]
        public static void CheckDependenciesMenu()
        {
            EditorPrefs.DeleteKey(k_SkipKey);

            var missing = MissingPackages();
            if (missing.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "Dungeon Graph",
                    "All required packages are installed.\n\n" +
                    "Addressables\n2D Tilemap\n2D Tilemap Extras\n2D Sprite",
                    "OK");
                return;
            }

            PromptInstall(missing);
        }

        [MenuItem("Tools/Dungeon Graph/Setup/Open Package Manager", false, 1)]
        public static void OpenPackageManagerMenu()
        {
            UnityEditor.PackageManager.UI.Window.Open("com.unity.addressables");
        }

        [MenuItem("Tools/Dungeon Graph/Setup/Add \"Dungeon\" Tag", false, 20)]
        public static void AddDungeonTagMenu()
        {
            if (EnsureDungeonTag())
                Debug.Log("[Dungeon Graph] The \"Dungeon\" tag is present.");
            else
                Debug.LogWarning("[Dungeon Graph] Could not add the \"Dungeon\" tag. " +
                                 "Add it manually in Project Settings > Tags and Layers.");
        }

        // ---------------------------------------------------------------------
        // Install
        // ---------------------------------------------------------------------

        private static void PromptInstall(PackageRequirement[] missing)
        {
            if (missing.Length == 0 || s_installRequest != null)
                return;

            string list = string.Join("\n", missing.Select(m => $"  • {m.displayName}\n      {m.reason}"));

            int choice = EditorUtility.DisplayDialogComplex(
                "Dungeon Graph — Missing Packages",
                "Dungeon Graph needs the following packages, which are not installed in this project:\n\n" +
                list +
                "\n\nUntil they are installed, Dungeon Graph's editor tools and runtime generator " +
                "are not compiled, so its menus and windows will not appear.",
                "Install Now",
                "Not Now",
                "Don't Ask Again");

            switch (choice)
            {
                case 0:
                    Install(missing);
                    break;
                case 2:
                    EditorPrefs.SetBool(k_SkipKey, true);
                    break;
            }
        }

        private static void Install(PackageRequirement[] missing)
        {
            string[] ids = missing.Select(m => m.id).ToArray();
            Debug.Log("[Dungeon Graph] Installing: " + string.Join(", ", ids));

            s_installRequest = Client.AddAndRemove(packagesToAdd: ids);
            EditorApplication.update += PollInstall;
        }

        private static void PollInstall()
        {
            if (s_installRequest == null)
            {
                EditorApplication.update -= PollInstall;
                return;
            }

            if (!s_installRequest.IsCompleted)
                return;

            EditorApplication.update -= PollInstall;

            var request = s_installRequest;
            s_installRequest = null;

            if (request.Status == StatusCode.Success)
            {
                Debug.Log("[Dungeon Graph] Packages installed. Unity will recompile, after which " +
                          "the Dungeon Graph tools appear under Tools > Dungeon Graph.");
            }
            else
            {
                Debug.LogError("[Dungeon Graph] Package installation failed: " + request.Error?.message +
                               "\nInstall them manually via Window > Package Manager > Unity Registry.");
            }
        }

        // ---------------------------------------------------------------------
        // Legacy tag support
        // ---------------------------------------------------------------------

        /// <summary>
        /// Adds the "Dungeon" tag if the project does not define it.
        /// </summary>
        /// <remarks>
        /// Only ever called from its menu item. Tags live in ProjectSettings, outside this
        /// package, so writing one changes the user's project and must be explicitly requested
        /// rather than performed as a side effect of import or package installation.
        ///
        /// Nothing in Dungeon Graph needs the tag any more — the master tilemap is found via
        /// the DungeonMasterTilemap component. It remains only so scenes authored against the
        /// original tag-based workflow keep working.
        /// </remarks>
        /// <returns>True if the tag exists after this call.</returns>
        public static bool EnsureDungeonTag()
        {
            const string tag = "Dungeon";

            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
                return false;

            var tagManager = new SerializedObject(assets[0]);
            var tags = tagManager.FindProperty("tags");
            if (tags == null || !tags.isArray)
                return false;

            for (int i = 0; i < tags.arraySize; i++)
            {
                if (tags.GetArrayElementAtIndex(i).stringValue == tag)
                    return true;
            }

            tags.InsertArrayElementAtIndex(tags.arraySize);
            tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
            tagManager.ApplyModifiedProperties();
            return true;
        }

        // ---------------------------------------------------------------------

        private readonly struct PackageRequirement
        {
            public readonly string id;
            public readonly string displayName;
            public readonly string reason;

            public PackageRequirement(string id, string displayName, string reason)
            {
                this.id          = id;
                this.displayName = displayName;
                this.reason      = reason;
            }
        }
    }
}
