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
    ///
    /// This component also owns the dungeon's collision (see <see cref="RebuildWalls"/>): a
    /// band of solid wall cells around the floor, on a generated child tilemap.
    /// </remarks>
    [AddComponentMenu("Dungeon Graph/Dungeon Master Tilemap")]
    [RequireComponent(typeof(Tilemap))]
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class DungeonMasterTilemap : MonoBehaviour
    {
        /// <summary>Legacy tag kept for backwards compatibility with pre-existing scenes.</summary>
        public const string LegacyTag = "Dungeon";

        /// <summary>Name of the generated child that carries the wall collider.</summary>
        public const string WallsObjectName = "Walls (Generated)";

        [Tooltip("How many cells of solid wall surround the floor. Thicker walls stop faster " +
                 "bodies that use Discrete collision detection; Continuous bodies never pass through.")]
        [SerializeField, Range(1, 4)] private int m_wallThickness = 2;

        private Tilemap m_tilemap;
        private Tilemap m_wallTilemap;
        private TilemapCollider2D m_wallCollider;
        private CompositeCollider2D m_wallComposite;

        // Shared, never saved: the wall tilemap is derived data, rebuilt from the floor on load.
        private static Tile s_wallTile;

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

        /// <summary>The generated tilemap holding the wall cells, or null before the first rebuild.</summary>
        public Tilemap WallTilemap => m_wallTilemap;

        private void OnEnable()
        {
            // Walls are never serialized, so a scene holding an editor-baked dungeon gets them
            // back here on load, in the Editor and in Play Mode alike.
            RebuildWalls();
        }

        /// <summary>
        /// Rebuilds the dungeon's collision from the tiles currently on the master tilemap. The
        /// generation pipeline calls this after merging rooms, drawing corridors and clearing;
        /// call it yourself after editing the master tilemap by hand.
        /// </summary>
        /// <remarks>
        /// Every empty cell within <c>wallThickness</c> cells of a floor tile (diagonals
        /// included) gets an invisible wall tile, and so does every edge floor tile — one with an
        /// empty cell directly north, south, east or west, which the floor rule tiles draw as a
        /// wall. They merge into one solid <see cref="CompositeCollider2D"/> in Polygons mode.
        /// Only interior floor has no collider.
        ///
        /// Solid walls, rather than an edge outline around the floor, are what make this
        /// reliable: an edge has no thickness, so a body whose center crosses it in one physics
        /// step is resolved out the far side. A wall a cell or more thick always pushes a
        /// penetrating body back toward the floor.
        ///
        /// The collider is regenerated immediately, not at the next physics step, so bodies
        /// spawned from <c>OnGenerationComplete</c> collide on their first step.
        /// </remarks>
        public void RebuildWalls()
        {
            var floor = Tilemap;
            if (floor == null) return;

            EnsureWallObject();
            m_wallTilemap.ClearAllTiles();

            // Not CompressBounds(): this also runs on scene load, and must not dirty the scene.
            BoundsInt fb = floor.cellBounds;
            if (fb.size.x > 0 && fb.size.y > 0)
            {
                int t = m_wallThickness;
                var floorBlock = new BoundsInt(fb.xMin, fb.yMin, 0, fb.size.x, fb.size.y, 1);
                TileBase[] floorTiles = floor.GetTilesBlock(floorBlock);

                int w = fb.size.x + 2 * t;
                int h = fb.size.y + 2 * t;
                var walls = new TileBase[w * h];

                // Mark every cell near the floor, then carve the interior floor back out.
                for (int y = 0; y < fb.size.y; y++)
                for (int x = 0; x < fb.size.x; x++)
                {
                    if (floorTiles[x + y * fb.size.x] == null) continue;
                    for (int dy = -t; dy <= t; dy++)
                    for (int dx = -t; dx <= t; dx++)
                        walls[(x + t + dx) + (y + t + dy) * w] = s_wallTile;
                }

                // Edge floor tiles (an empty cell directly N/S/E/W) are drawn as walls by the
                // floor rule tiles, so they stay solid. Only interior floor is walkable.
                bool IsFloor(int x, int y) =>
                    x >= 0 && y >= 0 && x < fb.size.x && y < fb.size.y &&
                    floorTiles[x + y * fb.size.x] != null;

                for (int y = 0; y < fb.size.y; y++)
                for (int x = 0; x < fb.size.x; x++)
                {
                    if (IsFloor(x, y) && IsFloor(x, y + 1) && IsFloor(x, y - 1) &&
                        IsFloor(x + 1, y) && IsFloor(x - 1, y))
                        walls[(x + t) + (y + t) * w] = null;
                }

                m_wallTilemap.SetTilesBlock(new BoundsInt(fb.xMin - t, fb.yMin - t, 0, w, h, 1), walls);
            }

            m_wallCollider.ProcessTilemapChanges();
            m_wallComposite.GenerateGeometry();
        }

        /// <summary>
        /// Finds or creates the wall child. It is DontSave: never written to the scene or the
        /// prefab, and recreated by <see cref="OnEnable"/>.
        /// </summary>
        private void EnsureWallObject()
        {
            if (s_wallTile == null)
            {
                s_wallTile = ScriptableObject.CreateInstance<Tile>();
                s_wallTile.name = "Dungeon Wall (Generated)";
                s_wallTile.hideFlags = HideFlags.HideAndDontSave;
                // Grid = the whole cell is solid, whatever the sprite. The sprite is never
                // drawn (the wall tilemap has no renderer); it only guarantees a shape.
                s_wallTile.colliderType = Tile.ColliderType.Grid;
                s_wallTile.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4),
                                                  new Vector2(0.5f, 0.5f), 4f);
                s_wallTile.sprite.hideFlags = HideFlags.HideAndDontSave;
            }

            if (m_wallTilemap != null) return;

            Transform existing = transform.Find(WallsObjectName);
            GameObject walls = existing != null ? existing.gameObject : null;
            if (walls == null)
            {
                walls = new GameObject(WallsObjectName);
                walls.hideFlags = HideFlags.DontSave;
                walls.layer = gameObject.layer;
                walls.transform.SetParent(transform, false);
            }

            m_wallTilemap = GetOrAdd<Tilemap>(walls);

            var body = GetOrAdd<Rigidbody2D>(walls);
            body.bodyType = RigidbodyType2D.Static;

            m_wallCollider = GetOrAdd<TilemapCollider2D>(walls);
            m_wallCollider.compositeOperation = Collider2D.CompositeOperation.Merge;

            m_wallComposite = GetOrAdd<CompositeCollider2D>(walls);
            m_wallComposite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            m_wallComposite.generationType = CompositeCollider2D.GenerationType.Synchronous;
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
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
