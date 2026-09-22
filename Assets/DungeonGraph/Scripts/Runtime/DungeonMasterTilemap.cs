using UnityEngine;
using UnityEngine.Tilemaps;

namespace DungeonGraph
{
    /// <summary>
    /// Marks the Tilemap that generated rooms and corridors are baked into.
    /// </summary>
    /// <remarks>
    /// Earlier versions identified the master tilemap by the "Dungeon" tag alone. Tags live
    /// in ProjectSettings and are not carried by a .unitypackage, so on a fresh import the
    /// tag did not exist and <c>GameObject.FindGameObjectWithTag</c> threw. This component
    /// travels with the prefab, so it always survives import. The tag is still honoured as
    /// a fallback for scenes authored against the old workflow.
    ///
    /// Put this on the same GameObject as the master <see cref="Tilemap"/>.
    /// </remarks>
    [AddComponentMenu("Dungeon Graph/Dungeon Master Tilemap")]
    [RequireComponent(typeof(Tilemap))]
    [DisallowMultipleComponent]
    public class DungeonMasterTilemap : MonoBehaviour
    {
        /// <summary>Legacy tag kept for backwards compatibility with pre-existing scenes.</summary>
        public const string LegacyTag = "Dungeon";

        private Tilemap m_tilemap;

        /// <summary>The Tilemap on this GameObject.</summary>
        public Tilemap Tilemap
        {
            get
            {
                if (m_tilemap == null)
                    m_tilemap = GetComponent<Tilemap>();
                return m_tilemap;
            }
        }

        /// <summary>
        /// Finds the master tilemap in the currently loaded scenes.
        /// </summary>
        /// <returns>The master <see cref="Tilemap"/>, or null if the scene has none.</returns>
        /// <remarks>
        /// Resolution order:
        /// <list type="number">
        ///   <item><description>A GameObject carrying this component.</description></item>
        ///   <item><description>A GameObject tagged "Dungeon" (only if that tag is defined).</description></item>
        /// </list>
        /// Never throws, whatever the project's tag configuration looks like.
        /// </remarks>
        public static Tilemap Find()
        {
            var marker = FindMarker();
            if (marker != null && marker.Tilemap != null)
                return marker.Tilemap;

            var tagged = FindByLegacyTag();
            if (tagged != null)
            {
                var tilemap = tagged.GetComponent<Tilemap>();
                if (tilemap != null)
                    return tilemap;
            }

            return null;
        }

        /// <summary>Returns the marker component in the loaded scenes, including inactive objects.</summary>
        public static DungeonMasterTilemap FindMarker()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<DungeonMasterTilemap>(FindObjectsInactive.Include);
#else
            var all = Resources.FindObjectsOfTypeAll<DungeonMasterTilemap>();
            foreach (var candidate in all)
            {
                // Skip prefab assets — only scene instances count.
                if (candidate.gameObject.scene.IsValid())
                    return candidate;
            }
            return null;
#endif
        }

        /// <summary>
        /// Looks up the legacy "Dungeon"-tagged object without throwing when the tag is
        /// undefined, which is the normal state of a freshly imported project.
        /// </summary>
        private static GameObject FindByLegacyTag()
        {
            try
            {
                return GameObject.FindGameObjectWithTag(LegacyTag);
            }
            catch (UnityException)
            {
                // Tag is not defined in this project. Not an error — the marker component
                // is the supported path and the caller reports a friendly message.
                return null;
            }
        }
    }
}
