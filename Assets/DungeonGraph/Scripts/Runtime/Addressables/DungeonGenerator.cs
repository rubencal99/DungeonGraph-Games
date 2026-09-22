using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.Tilemaps;

namespace DungeonGraph
{
    /// <summary>
    /// Drop into a scene and assign a DungeonGraphAsset plus a floor label to generate
    /// a dungeon at runtime using Addressables.
    ///
    /// Room prefabs must be marked Addressable with two labels:
    ///   1. Floor label   -- matches m_floorLabel (e.g. "Floor_1")
    ///   2. Room type label -- one of: Start, End, Hub, Boss, Reward, Basic_Small,
    ///                        Basic_Medium, Basic_Large, or a custom type name
    ///
    /// Subscribe to OnGenerationComplete to receive the result and spawn your player.
    /// Call Generate() manually if m_generateOnStart is false.
    /// </summary>
    public class DungeonGenerator : MonoBehaviour
    {
        [Header("Graph")]
        [SerializeField] private DungeonGraphAsset m_dungeonGraph;

        [Header("Floor")]
        [Tooltip("Addressable label assigned to all prefabs for this floor (e.g. 'Floor_1').")]
        [SerializeField] private string m_floorLabel = "Floor_1";

        [Header("Corridors")]
        [SerializeField] private TileBase m_corridorTile;
        [SerializeField] private int m_corridorWidth = 2;
        [SerializeField] private CorridorType m_corridorType = CorridorType.Direct;
        [SerializeField] private int m_maxCorridorRegenerations = 3;

        [Header("Physics Simulation")]
        [SerializeField] private float m_areaPlacementFactor  = 11.0f;
        [SerializeField] private float m_repulsionFactor      = 30.0f;
        [SerializeField] private float m_stiffnessFactor      = 20.0f;
        [SerializeField] private float m_idealDistance        = 20f;
        [SerializeField] private int   m_simulationIterations = 400;
        [SerializeField] private float m_chaosFactor          = 0f;
        [SerializeField] private float m_bendFactor           = 0f;
        [SerializeField] private RepulsionScalingMode m_repulsionScalingMode = RepulsionScalingMode.Default;

        [Header("Overlap Prevention")]
        [SerializeField] private bool m_allowRoomOverlap     = false;
        [SerializeField] private int  m_maxRoomRegenerations = 4;

        [Header("Startup")]
        [Tooltip("Automatically generate on Start. Disable to call Generate() manually.")]
        [SerializeField] private bool m_generateOnStart = true;

        // -------------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------------

        /// <summary>Raised when dungeon generation finishes successfully.</summary>
        public event Action<DungeonGenerationResult> OnGenerationComplete;

        /// <summary>Raised when generation fails (check the log for details).</summary>
        public event Action OnGenerationFailed;

        /// <summary>The result of the most recent successful generation.</summary>
        public DungeonGenerationResult LastResult { get; private set; }

        /// <summary>Trigger dungeon generation. Safe to call from user scripts.</summary>
        /// <remarks>
        /// Ignored if a generation is already running — call again once
        /// <see cref="OnGenerationComplete"/> or <see cref="OnGenerationFailed"/> has fired.
        /// </remarks>
        public void Generate()
        {
            if (m_isGenerating)
            {
                Debug.LogWarning("[DungeonGenerator] Generation is already in progress; " +
                                 "this call was ignored. Wait for OnGenerationComplete before " +
                                 "generating again.", this);
                return;
            }

            StartCoroutine(GenerateCoroutine());
        }

        /// <summary>
        /// Generates the dungeon and returns a <see cref="DungeonGenerationResult"/> when complete.
        /// Awaitable from async MonoBehaviour methods (e.g. <c>async void Start()</c>).
        /// Returns null if generation failed.
        /// </summary>
        public Task<DungeonGenerationResult> GenerateDungeon()
        {
            var tcs = new TaskCompletionSource<DungeonGenerationResult>();

            void OnComplete(DungeonGenerationResult result)
            {
                OnGenerationComplete -= OnComplete;
                OnGenerationFailed   -= OnFailed;
                tcs.TrySetResult(result);
            }
            void OnFailed()
            {
                OnGenerationComplete -= OnComplete;
                OnGenerationFailed   -= OnFailed;
                tcs.TrySetResult(null);
            }

            OnGenerationComplete += OnComplete;
            OnGenerationFailed   += OnFailed;

            // If a run is already in flight (typically because Generate On Start fired first),
            // just await that one rather than requesting a second.
            if (!m_isGenerating) Generate();

            return tcs.Task;
        }

        /// <summary>True while a generation is running.</summary>
        public bool IsGenerating => m_isGenerating;

        // -------------------------------------------------------------------------
        // Internal
        // -------------------------------------------------------------------------

        private readonly Dictionary<string, IList<GameObject>> m_prefabCache = new Dictionary<string, IList<GameObject>>();
        private readonly List<AsyncOperationHandle> m_handles = new List<AsyncOperationHandle>();

        /// <summary>
        /// Guards against overlapping runs. Two concurrent generations would race over the
        /// shared "Generated_Dungeon" object and each other's Addressables handles, which
        /// produces a dungeon that appears and is then torn down by the other run.
        /// </summary>
        private bool m_isGenerating;

        private void Start()
        {
            if (m_generateOnStart) Generate();
        }

        private void OnDestroy()
        {
            ReleaseHandles();
        }

        private IEnumerator GenerateCoroutine()
        {
            m_isGenerating = true;

            var task = GenerateAsync();
            yield return new WaitUntil(() => task.IsCompleted);

            m_isGenerating = false;

            if (task.IsFaulted)
            {
                Debug.LogError("[DungeonGenerator] Generation failed: " + task.Exception?.Flatten().InnerException);
                OnGenerationFailed?.Invoke();
            }
        }

        private async Task<DungeonGenerationResult> GenerateAsync()
        {
            if (m_dungeonGraph == null)
            {
                Debug.LogError("[DungeonGenerator] No DungeonGraphAsset assigned.");
                OnGenerationFailed?.Invoke();
                return null;
            }

            // Confirm the floor label resolves to something BEFORE tearing anything down.
            // Without this check a misconfigured Addressables setup destroys the existing
            // dungeon and clears the master tilemap, then fails to load a replacement —
            // leaving the player staring at an empty scene.
            if (!await FloorContentIsAvailable())
            {
                OnGenerationFailed?.Invoke();
                return null;
            }

            ReleaseHandles();

            var existing = GameObject.Find("Generated_Dungeon");
            if (existing != null) DestroyImmediate(existing);

            var graph = ScriptableObject.Instantiate(m_dungeonGraph);
            graph.Init();

            await OrganicGeneration.GenerateRooms(
                graph,
                CreateRoomFactory(),
                CreateBoundsProvider(),
                parent:                   null,
                areaPlacementFactor:      m_areaPlacementFactor,
                repulsionFactor:          m_repulsionFactor,
                simulationIterations:     m_simulationIterations,
                forceMode:                false,
                stiffnessFactor:          m_stiffnessFactor,
                chaosFactor:              m_chaosFactor,
                realTimeSimulation:       false,
                simulationSpeed:          30f,
                idealDistance:            m_idealDistance,
                allowRoomOverlap:         m_allowRoomOverlap,
                maxRoomRegenerations:     m_maxRoomRegenerations,
                maxCorridorRegenerations: m_maxCorridorRegenerations,
                repulsionScalingMode:     m_repulsionScalingMode,
                bendFactor:               m_bendFactor,
                generateCorridorsAfterSimulation: false);

            var dungeonParent = GameObject.Find("Generated_Dungeon");

            if (dungeonParent != null && m_corridorTile != null)
            {
                var tilemapSystem = dungeonParent.GetComponent<DungeonTilemapSystem>();
                if (tilemapSystem != null)
                {
                    tilemapSystem.corridorTile  = m_corridorTile;
                    tilemapSystem.corridorWidth = m_corridorWidth;
                    tilemapSystem.corridorType  = m_corridorType;
                }
                OrganicGeneration.GenerateCorridors(graph, dungeonParent, m_maxCorridorRegenerations);
            }

            LastResult = DungeonGenerationResult.Build(dungeonParent);

            ScriptableObject.DestroyImmediate(graph);
            OnGenerationComplete?.Invoke(LastResult);
            return LastResult;
        }

        // -------------------------------------------------------------------------
        // Factory / BoundsProvider
        // -------------------------------------------------------------------------

        private OrganicGeneration.AsyncRoomFactory CreateRoomFactory()
        {
            var usedIndices = new Dictionary<string, List<int>>();

            return async (node, parent) =>
            {
                string typeLabel = GetTypeLabel(node);
                var prefabs = await LoadPrefabsForType(typeLabel);
                if (prefabs == null || prefabs.Count == 0) return null;

                if (!usedIndices.TryGetValue(typeLabel, out var used))
                    usedIndices[typeLabel] = used = new List<int>();

                var available = Enumerable.Range(0, prefabs.Count).Where(i => !used.Contains(i)).ToList();
                if (available.Count == 0) { used.Clear(); available = Enumerable.Range(0, prefabs.Count).ToList(); }

                int idx = available[UnityEngine.Random.Range(0, available.Count)];
                used.Add(idx);

                return Instantiate(prefabs[idx], Vector3.zero, Quaternion.identity, parent);
            };
        }

        private OrganicGeneration.AsyncBoundsProvider CreateBoundsProvider()
        {
            return async (node) =>
            {
                string typeLabel = GetTypeLabel(node);
                var prefabs = await LoadPrefabsForType(typeLabel);
                if (prefabs != null && prefabs.Count > 0)
                {
                    var tmpl = prefabs[0].GetComponent<RoomTemplate>();
                    if (tmpl != null && tmpl.worldBounds.size.magnitude > 0.1f)
                        return tmpl.worldBounds;
                }
                return new Bounds(Vector3.zero, new Vector3(10f, 10f, 1f));
            };
        }

        // -------------------------------------------------------------------------
        // Addressables loading
        // -------------------------------------------------------------------------

        /// <summary>
        /// Returns true if the Addressables catalogue knows about any asset carrying the
        /// configured floor label.
        /// </summary>
        /// <remarks>
        /// The overwhelmingly common cause of a failure here is Addressables content that was
        /// never built, or a group that lost its schemas and was therefore excluded from the
        /// build. Both produce an empty catalogue for the label with no exception, so this
        /// check exists to turn a silent "nothing happened" into an actionable message.
        /// </remarks>
        private async Task<bool> FloorContentIsAvailable()
        {
            if (string.IsNullOrWhiteSpace(m_floorLabel))
            {
                Debug.LogError("[DungeonGenerator] Floor Label is empty. Set it to the Addressables " +
                               "label on your room prefabs, e.g. \"Floor_1\".", this);
                return false;
            }

            var handle = Addressables.LoadResourceLocationsAsync(m_floorLabel);
            await handle.Task;

            bool available = handle.Status == AsyncOperationStatus.Succeeded &&
                             handle.Result != null && handle.Result.Count > 0;

            if (!available)
            {
                Debug.LogError(
                    $"[DungeonGenerator] No Addressable content found for floor label '{m_floorLabel}'. " +
                    "Nothing was generated and the scene was left untouched.\n" +
                    "Check, in order:\n" +
                    "  1. Addressables content has been built: Window > Asset Management > " +
                    "Addressables > Groups > Build > New Build > Default Build Script.\n" +
                    "  2. The group for this floor has schemas. A group with none is silently " +
                    "excluded from the build; run Tools > Dungeon Graph > Setup > Reinitialize " +
                    "Addressables to repair it, then rebuild content.\n" +
                    "  3. Floor Label matches the label on your room prefabs exactly.", this);
            }

            Addressables.Release(handle);
            return available;
        }

        private async Task<IList<GameObject>> LoadPrefabsForType(string typeLabel)
        {
            if (m_prefabCache.TryGetValue(typeLabel, out var cached))
                return cached;

            var keys = new List<object> { m_floorLabel, typeLabel };
            var handle = Addressables.LoadAssetsAsync<GameObject>(keys, null, Addressables.MergeMode.Intersection);
            await handle.Task;

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"[DungeonGenerator] Addressables load failed for labels '{m_floorLabel}' + '{typeLabel}'.");
                Addressables.Release(handle);
                return null;
            }

            if (handle.Result.Count == 0)
            {
                Debug.LogError($"[DungeonGenerator] No prefabs found for labels '{m_floorLabel}' + '{typeLabel}'. " +
                               "Assign both labels to room prefabs in the Addressables Groups window.");
                Addressables.Release(handle);
                return null;
            }

            m_handles.Add(handle);
            m_prefabCache[typeLabel] = handle.Result;
            return handle.Result;
        }

        private void ReleaseHandles()
        {
            foreach (var h in m_handles) Addressables.Release(h);
            m_handles.Clear();
            m_prefabCache.Clear();
        }

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        private static string GetTypeLabel(DungeonGraphNode node)
        {
            if (node is BasicNode bn)
                return $"Basic_{bn.size}";
            if (node is CustomNode cn)
                return cn.customTypeName;
            return node.GetType().Name.Replace("Node", "");
        }
    }

    /// <summary>A generated room paired with its world-space center.</summary>
    public readonly struct RoomResult
    {
        /// <summary>The spawned room GameObject.</summary>
        public readonly GameObject gameObject;

        /// <summary>World-space center of the room's tile bounds at the time of generation.</summary>
        public readonly Vector3 roomCenter;

        internal readonly string nodeTypeName;

        internal RoomResult(GameObject go, Vector3 center, string typeName)
        {
            gameObject   = go;
            roomCenter   = center;
            nodeTypeName = typeName;
        }

        public static implicit operator GameObject(RoomResult r) => r.gameObject;
    }

    /// <summary>Data returned to subscribers of DungeonGenerator.OnGenerationComplete.</summary>
    public class DungeonGenerationResult
    {
        /// <summary>The root GameObject containing all spawned rooms.</summary>
        public GameObject dungeonParent;

        /// <summary>The RoomNodeReference on the Start room (kept for compatibility).</summary>
        public RoomNodeReference startRoom;

        private List<RoomResult> m_rooms = new();

        internal static DungeonGenerationResult Build(GameObject parent)
        {
            var result = new DungeonGenerationResult { dungeonParent = parent };
            if (parent != null)
            {
                var refs = parent.GetComponentsInChildren<RoomNodeReference>();
                result.m_rooms = refs.Select(r => new RoomResult(r.gameObject, ComputeCenter(r), r.nodeTypeName)).ToList();
                result.startRoom = refs.FirstOrDefault(r => r.nodeTypeName == "Start");
            }
            return result;
        }

        private static Vector3 ComputeCenter(RoomNodeReference r)
        {
            var tmpl = r.GetComponent<RoomTemplate>();
            return tmpl != null ? r.transform.position + tmpl.worldBounds.center : r.transform.position;
        }

        /// <summary>Returns all spawned Start rooms.</summary>
        public List<RoomResult> GetStartRoom() => GetRoom("Start");

        /// <summary>Returns all spawned End rooms.</summary>
        public List<RoomResult> GetEndRoom() => GetRoom("End");

        /// <summary>Returns all spawned Boss rooms.</summary>
        public List<RoomResult> GetBossRoom() => GetRoom("Boss");

        /// <summary>
        /// Returns all spawned rooms whose node type matches <paramref name="typeName"/> (case-insensitive).
        /// Use the node type name as configured in the graph — e.g. GetRoom("Upgrade") returns all Upgrade rooms.
        /// </summary>
        public List<RoomResult> GetRoom(string typeName) =>
            m_rooms
                .Where(r => string.Equals(r.nodeTypeName, typeName, StringComparison.OrdinalIgnoreCase))
                .ToList();
    }
}
