using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DungeonGraph
{
    /// <summary>
    /// ScriptableObject that stores all custom node type definitions.
    /// Persists between sessions and can be saved to disk.
    /// </summary>
    /// <remarks>
    /// This type lives in the runtime assembly because <see cref="CustomNode"/> reads it
    /// during generation. The authoring helpers below touch editor-only APIs and are
    /// therefore compiled out of player builds behind UNITY_EDITOR.
    /// </remarks>
    [CreateAssetMenu(menuName = "Dungeon Graph/Custom Node Type Registry")]
    public class CustomNodeTypeRegistry : ScriptableObject
    {
        /// <summary>Default location used when the registry has to be created from scratch.</summary>
        public const string DefaultRegistryPath =
            "Assets/DungeonGraph/Scripts/Runtime/DefaultCustomNodeTypeRegistry.asset";

        [SerializeField]
        private List<CustomNodeType> m_customNodeTypes = new List<CustomNodeType>();

        public List<CustomNodeType> customNodeTypes => m_customNodeTypes;

        /// <summary>
        /// Adds a new custom node type if the name doesn't already exist.
        /// </summary>
        /// <returns>True if added successfully, false if name already exists</returns>
        public bool AddCustomNodeType(string typeName, Color color)
        {
            if (string.IsNullOrWhiteSpace(typeName) || HasNodeType(typeName))
            {
                return false;
            }

            var newType = new CustomNodeType(typeName, color);
            m_customNodeTypes.Add(newType);
            MarkDirty();
            return true;
        }

        /// <summary>
        /// Checks if a node type with the given name already exists.
        /// </summary>
        public bool HasNodeType(string typeName)
        {
            return m_customNodeTypes.Any(t => t.typeName.Equals(typeName, System.StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Gets a custom node type by name.
        /// </summary>
        public CustomNodeType GetNodeType(string typeName)
        {
            return m_customNodeTypes.FirstOrDefault(t => t.typeName.Equals(typeName, System.StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Removes a custom node type by name.
        /// </summary>
        public bool RemoveNodeType(string typeName)
        {
            var nodeType = GetNodeType(typeName);
            if (nodeType != null)
            {
                m_customNodeTypes.Remove(nodeType);
                MarkDirty();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Flags the asset so the editor writes it back to disk. No-op in player builds.
        /// </summary>
        private void MarkDirty()
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>
        /// Gets the project-wide registry, creating it on first use.
        /// </summary>
        /// <remarks>
        /// In the editor the asset is located by type first, so the package folder can be
        /// renamed or moved without breaking custom node types. In a player build the asset
        /// database does not exist, so the already-loaded instance is returned instead and
        /// null is a valid answer when the project defines no custom types.
        /// </remarks>
        public static CustomNodeTypeRegistry GetOrCreateDefault()
        {
#if UNITY_EDITOR
            // Preferred: find the existing asset wherever the user has moved it to.
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:CustomNodeTypeRegistry");
            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                var found = UnityEditor.AssetDatabase.LoadAssetAtPath<CustomNodeTypeRegistry>(path);
                if (found != null)
                    return found;
            }

            // None in the project yet — create one at the default location.
            var registry = CreateInstance<CustomNodeTypeRegistry>();
            EnsureFolderExists(System.IO.Path.GetDirectoryName(DefaultRegistryPath).Replace('\\', '/'));
            UnityEditor.AssetDatabase.CreateAsset(registry, DefaultRegistryPath);
            UnityEditor.AssetDatabase.SaveAssets();
            return registry;
#else
            // Player builds: return whichever registry the build happened to include.
            var loaded = Resources.FindObjectsOfTypeAll<CustomNodeTypeRegistry>();
            return loaded.Length > 0 ? loaded[0] : null;
#endif
        }

#if UNITY_EDITOR
        /// <summary>Creates every missing folder along an "Assets/a/b/c" path.</summary>
        private static void EnsureFolderExists(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || UnityEditor.AssetDatabase.IsValidFolder(folderPath))
                return;

            string[] parts = folderPath.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!UnityEditor.AssetDatabase.IsValidFolder(next))
                    UnityEditor.AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
#endif
    }
}
