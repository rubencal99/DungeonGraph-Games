using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace DungeonGraph
{
    /// <summary>
    /// Spatial hash grid for collision detection and proximity queries
    /// </summary>
    public class SpatialHashGrid
    {
        private readonly Dictionary<Vector2Int, List<string>> grid;
        private readonly float cellSize;
        private Dictionary<string, Vector3> positions;
        private Dictionary<string, float> radii;

        public SpatialHashGrid(float cellSize)
        {
            this.cellSize = cellSize;
            this.grid = new Dictionary<Vector2Int, List<string>>();
        }

        public void UpdateGrid(Dictionary<string, Vector3> roomPositions, Dictionary<string, float> roomRadii)
        {
            grid.Clear();
            positions = roomPositions;
            radii = roomRadii;

            foreach (var kvp in roomPositions)
            {
                string nodeId = kvp.Key;
                Vector3 position = kvp.Value;
                float radius = roomRadii.ContainsKey(nodeId) ? roomRadii[nodeId] : 5f;

                foreach (var cell in GetOccupiedCells(position, radius))
                {
                    if (!grid.ContainsKey(cell))
                        grid[cell] = new List<string>();
                    grid[cell].Add(nodeId);
                }
            }
        }

        private List<Vector2Int> GetOccupiedCells(Vector3 position, float radius)
        {
            var cells = new List<Vector2Int>();
            int minX = Mathf.FloorToInt((position.x - radius) / cellSize);
            int maxX = Mathf.FloorToInt((position.x + radius) / cellSize);
            int minY = Mathf.FloorToInt((position.y - radius) / cellSize);
            int maxY = Mathf.FloorToInt((position.y + radius) / cellSize);

            for (int x = minX; x <= maxX; x++)
                for (int y = minY; y <= maxY; y++)
                    cells.Add(new Vector2Int(x, y));

            return cells;
        }

        public List<string> GetNearbyRooms(string nodeId)
        {
            if (!positions.ContainsKey(nodeId)) return new List<string>();

            Vector3 position = positions[nodeId];
            float radius = radii.ContainsKey(nodeId) ? radii[nodeId] : 5f;
            var nearbyRooms = new HashSet<string>();

            foreach (var cell in GetOccupiedCells(position, radius))
            {
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        var neighbor = new Vector2Int(cell.x + dx, cell.y + dy);
                        if (grid.ContainsKey(neighbor))
                            foreach (string roomId in grid[neighbor])
                                if (roomId != nodeId)
                                    nearbyRooms.Add(roomId);
                    }
            }

            return nearbyRooms.ToList();
        }
    }

    /// <summary>
    /// Organic dungeon generation using force-directed layout with spring physics.
    ///
    /// Room loading is intentionally decoupled via delegates so this class works
    /// in both the Unity Editor and standalone builds. Supply a RoomFactory and a
    /// BoundsProvider appropriate for your context:
    ///   - Editor: OrganicGenerationEditorBridge.CreateRoomFactory / CreateBoundsProvider
    ///   - Runtime/builds: load prefabs through addressables
    /// </summary>
    public static class OrganicGeneration
    {
        /// <summary>
        /// Instantiates a room prefab for the given node under <paramref name="parent"/>.
        /// Return null if no suitable prefab is available.
        /// </summary>
        public delegate Task<GameObject> AsyncRoomFactory(DungeonGraphNode node, Transform parent);

        /// <summary>
        /// Returns the world-space bounds of the room prefab for the given node.
        /// Used to calculate the initial placement radius before any rooms are instantiated.
        /// </summary>
        public delegate Task<Bounds> AsyncBoundsProvider(DungeonGraphNode node);

        // -------------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------------

        /// <summary>
        /// Full room-generation pipeline: processes spawn chances, runs the
        /// force-directed layout, snaps rooms to the tilemap grid, and merges
        /// room tilemaps into the master tilemap.
        /// </summary>
        public static async Task GenerateRooms(
            DungeonGraphAsset graph,
            AsyncRoomFactory roomFactory,
            AsyncBoundsProvider boundsProvider,
            Transform parent = null,
            float areaPlacementFactor = 2.0f,
            float repulsionFactor = 1.0f,
            int simulationIterations = 100,
            bool forceMode = false,
            float stiffnessFactor = 1.0f,
            float chaosFactor = 0.0f,
            bool realTimeSimulation = false,
            float simulationSpeed = 30f,
            float idealDistance = 20f,
            bool allowRoomOverlap = false,
            int maxRoomRegenerations = 3,
            int maxCorridorRegenerations = 3,
            RepulsionScalingMode repulsionScalingMode = RepulsionScalingMode.Default,
            float bendFactor = 0f,
            bool generateCorridorsAfterSimulation = false)
        {
            if (graph == null)
            {
                Debug.LogError("[OrganicGeneration] graph is null.");
                return;
            }

            ProcessSpawnChances(graph);

            if (parent == null)
            {
                var container = new GameObject("Generated_Dungeon");
                parent = container.transform;
            }

            // --- Measure bounds & initial placement radius ---
            var nodeBounds = new Dictionary<string, Bounds>();
            float totalArea = 0f;
            foreach (var node in graph.Nodes)
            {
                var bounds = await boundsProvider(node);
                nodeBounds[node.id] = bounds;
                totalArea += bounds.size.x * bounds.size.y;
            }
            float placementRadius = Mathf.Sqrt(totalArea * areaPlacementFactor) / 2f;

            // --- Graph distances (for repulsion scaling) ---
            var graphDistances = CalculateGraphDistances(graph);

            // --- Instantiate rooms at random positions ---
            var roomInstances = new Dictionary<string, GameObject>();
            var roomPositions = new Dictionary<string, Vector3>();
            bool hasOverlap = false;
            int regenerationAttempt = 0;

            do
            {
                if (regenerationAttempt > 0)
                {
                    foreach (var roomObj in roomInstances.Values)
                        if (roomObj != null) GameObject.DestroyImmediate(roomObj);
                    roomInstances.Clear();
                    roomPositions.Clear();
                }

                foreach (var node in graph.Nodes)
                {
                    float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                    float dist  = Random.Range(0f, placementRadius);
                    Vector3 initialPos = new Vector3(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist, 0f);

                    var roomObj = await SpawnRoom(node, parent, roomFactory);
                    if (roomObj == null) continue;

                    roomInstances[node.id] = roomObj;
                    roomPositions[node.id] = initialPos;
                }

                if (!realTimeSimulation || !Application.isPlaying)
                {
                    var adj = BuildAdjacency(graph);
                    var roomRadii = CalculateRoomRadii(roomInstances);
                    var velocities = roomPositions.Keys.ToDictionary(k => k, _ => Vector3.zero);

                    DungeonSimulationUtility.SimulateForces(
                        roomPositions, adj, graphDistances, roomRadii, velocities,
                        repulsionFactor, simulationIterations, forceMode,
                        stiffnessFactor, chaosFactor, idealDistance, repulsionScalingMode, bendFactor);

                    if (!allowRoomOverlap && regenerationAttempt < maxRoomRegenerations)
                    {
                        hasOverlap = CheckRoomOverlap(roomInstances, roomPositions);
                        if (hasOverlap) regenerationAttempt++;
                    }
                    else hasOverlap = false;
                }
                else
                {
                    hasOverlap = false;
                }
            }
            while (hasOverlap && regenerationAttempt <= maxRoomRegenerations);

            if (hasOverlap)
                Debug.LogWarning($"[OrganicGeneration] Max regenerations ({maxRoomRegenerations}) reached with overlap still present.");

            // If any nodes failed to spawn (e.g. missing Addressable label), remove them and their
            // connections from the graph copy so corridor generation never references a missing room.
            if (roomInstances.Count < graph.Nodes.Count)
            {
                int failedCount = graph.Nodes.Count - roomInstances.Count;
                var spawnedIds = new HashSet<string>(roomInstances.Keys);
                graph.Nodes.RemoveAll(n => !spawnedIds.Contains(n.id));
                graph.Connections.RemoveAll(c =>
                    !spawnedIds.Contains(c.inputPort.nodeId) ||
                    !spawnedIds.Contains(c.outputPort.nodeId));

                Debug.LogWarning($"[OrganicGeneration] {failedCount} node(s) failed to spawn — " +
                                 "check that Addressable labels match the floor label and room type. " +
                                 "Their connections have been removed to prevent corridor errors.");
            }

            // --- Real-time simulation path ---
            if (realTimeSimulation && Application.isPlaying)
            {
                ApplyInitialPositions(roomInstances, roomPositions);
                SetupVisualizerAndTilemapBeforeSimulation(graph, parent, roomInstances);

                var controller = parent.gameObject.AddComponent<DungeonSimulationController>();
                var simParams = new DungeonSimulationController.SimulationParameters
                {
                    areaPlacementFactor          = areaPlacementFactor,
                    repulsionFactor              = repulsionFactor,
                    simulationIterations         = simulationIterations,
                    forceMode                    = forceMode,
                    stiffnessFactor              = stiffnessFactor,
                    chaosFactor                  = chaosFactor,
                    simulationSpeed              = simulationSpeed,
                    idealDistance                = idealDistance,
                    allowRoomOverlap             = allowRoomOverlap,
                    maxRoomRegenerations         = maxRoomRegenerations,
                    maxCorridorRegenerations     = maxCorridorRegenerations,
                    repulsionScalingMode         = repulsionScalingMode,
                    bendFactor                   = bendFactor,
                    generateCorridorsAfterSimulation = generateCorridorsAfterSimulation
                };
                controller.StartSimulation(graph, roomInstances, roomPositions, nodeBounds, simParams);
                return; // PostSimulationSetup called by controller when done
            }

            // --- Instant mode: apply final positions ---
            ApplyFinalPositions(roomInstances, roomPositions);

            SetupVisualizerInstant(graph, parent, roomInstances);

            // Tilemap snap + merge
            var tilemapSystem = parent.gameObject.GetComponent<DungeonTilemapSystem>();
            if (tilemapSystem == null)
                tilemapSystem = parent.gameObject.AddComponent<DungeonTilemapSystem>();

            tilemapSystem.SnapRoomsToGrid(roomInstances);

            if (tilemapSystem.masterTilemap == null)
                tilemapSystem.FindMasterTilemap();

            if (tilemapSystem.masterTilemap != null)
                tilemapSystem.MergeRoomsToMasterTilemap(roomInstances);
            else
                Debug.LogWarning("[OrganicGeneration] Master tilemap not found, so room tilemaps were not merged. " +
                                 "Drag Prefabs/Master_Tilemap.prefab into the scene, or add a Dungeon Master Tilemap component to an existing Tilemap.");
        }

        /// <summary>
        /// Called by DungeonSimulationController when real-time simulation finishes.
        /// Snaps rooms to the grid, merges tilemaps, and optionally generates corridors.
        /// </summary>
        public static void PostSimulationSetup(DungeonGraphAsset graph, GameObject dungeonParent, bool generateCorridors)
        {
            if (graph == null || dungeonParent == null)
            {
                Debug.LogError("[OrganicGeneration] PostSimulationSetup: graph or parent is null.");
                return;
            }

            var roomInstances = new Dictionary<string, GameObject>();
            foreach (var roomRef in dungeonParent.GetComponentsInChildren<RoomNodeReference>())
                if (!string.IsNullOrEmpty(roomRef.nodeId))
                    roomInstances[roomRef.nodeId] = roomRef.gameObject;

            var tilemapSystem = dungeonParent.GetComponent<DungeonTilemapSystem>();
            if (tilemapSystem == null)
            {
                Debug.LogError("[OrganicGeneration] No DungeonTilemapSystem found during PostSimulationSetup.");
                return;
            }

            tilemapSystem.SnapRoomsToGrid(roomInstances);

            if (tilemapSystem.masterTilemap != null)
                tilemapSystem.MergeRoomsToMasterTilemap(roomInstances);
            else
                Debug.LogWarning("[OrganicGeneration] Master tilemap not found. " +
                                 "Drag Prefabs/Master_Tilemap.prefab into the scene, or add a Dungeon Master Tilemap component to an existing Tilemap.");

            if (generateCorridors && tilemapSystem.corridorTile != null && tilemapSystem.masterTilemap != null)
            {
                var simController = dungeonParent.GetComponent<DungeonSimulationController>();
                int maxCorridorRegen = simController?.Parameters?.maxCorridorRegenerations ?? 3;
                tilemapSystem.GenerateAllCorridorsWithOverlapPrevention(
                    graph, roomInstances, tilemapSystem.corridorType, maxCorridorRegen);
            }
            else if (generateCorridors && tilemapSystem.corridorTile == null)
            {
                Debug.LogWarning("[OrganicGeneration] Corridor tile not assigned. Skipping corridor generation.");
            }
        }

        /// <summary>
        /// Generates corridors for an existing Generated_Dungeon. Rooms must already be placed.
        /// </summary>
        public static void GenerateCorridors(DungeonGraphAsset graph, GameObject dungeonParent, int maxCorridorRegenerations = 3)
        {
            if (graph == null || dungeonParent == null)
            {
                Debug.LogError("[OrganicGeneration] GenerateCorridors: graph or dungeonParent is null.");
                return;
            }

            var tilemapSystem = dungeonParent.GetComponent<DungeonTilemapSystem>();
            if (tilemapSystem == null)
            {
                Debug.LogError("[OrganicGeneration] No DungeonTilemapSystem found on dungeon parent.");
                return;
            }

            var roomInstances = new Dictionary<string, GameObject>();
            foreach (var roomRef in dungeonParent.GetComponentsInChildren<RoomNodeReference>())
                if (!string.IsNullOrEmpty(roomRef.nodeId))
                    roomInstances[roomRef.nodeId] = roomRef.gameObject;

            if (roomInstances.Count == 0)
            {
                Debug.LogError("[OrganicGeneration] No RoomNodeReference components found. Generate rooms first.");
                return;
            }

            tilemapSystem.GenerateAllCorridorsWithOverlapPrevention(
                graph, roomInstances, tilemapSystem.corridorType, maxCorridorRegenerations);
        }

        // -------------------------------------------------------------------------
        // Private helpers
        // -------------------------------------------------------------------------

        /// <summary>
        /// Instantiates the room via the factory and adds the standard post-spawn components.
        /// Style-agnostic — also used by FloodFillGeneration. Belongs in a shared helper class
        /// once there are more than two generation styles.
        /// </summary>
        internal static async Task<GameObject> SpawnRoom(DungeonGraphNode node, Transform parent, AsyncRoomFactory factory)
        {
            var roomObj = await factory(node, parent);
            if (roomObj == null) return null;

            string simpleType = node.GetType().Name.Replace("Node", "");
            roomObj.name = $"{simpleType} Room";

            roomObj.GetComponent<RoomTemplate>()?.RepopulateExitsIfNeeded();

            var nodeRef = roomObj.AddComponent<RoomNodeReference>();
            nodeRef.nodeId       = node.id;
            nodeRef.nodeTypeName = simpleType;

            return roomObj;
        }

        private static void ApplyInitialPositions(
            Dictionary<string, GameObject> roomInstances,
            Dictionary<string, Vector3> roomPositions)
        {
            foreach (var kvp in roomPositions)
            {
                if (!roomInstances.TryGetValue(kvp.Key, out var roomObj)) continue;
                var tmpl = roomObj.GetComponent<RoomTemplate>();
                Vector3 centerOffset = tmpl != null ? tmpl.worldBounds.center : Vector3.zero;
                roomObj.transform.position = kvp.Value - centerOffset;
            }
        }

        private static void ApplyFinalPositions(
            Dictionary<string, GameObject> roomInstances,
            Dictionary<string, Vector3> roomPositions)
        {
            foreach (var kvp in roomPositions)
            {
                if (!roomInstances.TryGetValue(kvp.Key, out var roomObj)) continue;
                var tmpl = roomObj.GetComponent<RoomTemplate>();
                Vector3 centerOffset = tmpl != null ? tmpl.worldBounds.center : Vector3.zero;
                roomObj.transform.position = kvp.Value - centerOffset;
            }
        }

        /// <summary>
        /// Attaches the connection visualizer for a finished, non-animated layout.
        /// Style-agnostic — also used by FloodFillGeneration.
        /// </summary>
        internal static void SetupVisualizerInstant(
            DungeonGraphAsset graph,
            Transform parent,
            Dictionary<string, GameObject> roomInstances)
        {
            var visualizer = parent.gameObject.AddComponent<DungeonConnectionVisualizer>();
            visualizer.connections = new List<(GameObject, GameObject)>();
            visualizer.rooms       = new List<DungeonConnectionVisualizer.RoomInfo>();

            foreach (var node in graph.Nodes)
            {
                if (!roomInstances.ContainsKey(node.id)) continue;
                string typeName = node.GetType().Name.Replace("Node", "");
                Color color = node is CustomNode cn
                    ? cn.customColor
                    : DungeonConnectionVisualizer.GetColorForNodeType(typeName);
                visualizer.rooms.Add(new DungeonConnectionVisualizer.RoomInfo
                {
                    room     = roomInstances[node.id],
                    typeName = typeName,
                    color    = color
                });
            }

            foreach (var conn in graph.Connections)
            {
                if (roomInstances.TryGetValue(conn.inputPort.nodeId,  out var rA) &&
                    roomInstances.TryGetValue(conn.outputPort.nodeId, out var rB))
                    visualizer.connections.Add((rA, rB));
            }
        }

        private static void SetupVisualizerAndTilemapBeforeSimulation(
            DungeonGraphAsset graph,
            Transform parent,
            Dictionary<string, GameObject> roomInstances)
        {
            var tilemapSystem = parent.gameObject.GetComponent<DungeonTilemapSystem>();
            if (tilemapSystem == null)
                tilemapSystem = parent.gameObject.AddComponent<DungeonTilemapSystem>();

            if (tilemapSystem.masterTilemap == null)
                tilemapSystem.FindMasterTilemap();

            var visualizer = parent.gameObject.AddComponent<DungeonConnectionVisualizer>();
            visualizer.connections = new List<(GameObject, GameObject)>();
            visualizer.rooms       = new List<DungeonConnectionVisualizer.RoomInfo>();

            foreach (var node in graph.Nodes)
            {
                if (!roomInstances.ContainsKey(node.id)) continue;
                string typeName = node.GetType().Name.Replace("Node", "");
                Color color = node is CustomNode cn
                    ? cn.customColor
                    : DungeonConnectionVisualizer.GetColorForNodeType(typeName);
                visualizer.rooms.Add(new DungeonConnectionVisualizer.RoomInfo
                {
                    room     = roomInstances[node.id],
                    typeName = typeName,
                    color    = color
                });
            }

            foreach (var conn in graph.Connections)
            {
                if (roomInstances.TryGetValue(conn.inputPort.nodeId,  out var rA) &&
                    roomInstances.TryGetValue(conn.outputPort.nodeId, out var rB))
                    visualizer.connections.Add((rA, rB));
            }
        }

        private static Dictionary<string, float> CalculateRoomRadii(Dictionary<string, GameObject> roomInstances)
        {
            var radii = new Dictionary<string, float>();
            foreach (var kvp in roomInstances)
            {
                var tmpl = kvp.Value.GetComponent<RoomTemplate>();
                radii[kvp.Key] = tmpl != null
                    ? Mathf.Max(tmpl.worldBounds.size.x, tmpl.worldBounds.size.y) / 2f
                    : 5f;
            }
            return radii;
        }

        internal static void ProcessSpawnChances(DungeonGraphAsset graph)
        {
            if (graph?.Nodes == null || graph.Connections == null) return;

            var nodesToRemove    = new List<DungeonGraphNode>();
            var effectiveConns   = graph.Connections.ToList();

            foreach (var node in graph.Nodes)
            {
                if (node.spawnChance >= 100f) continue;

                var nodeConns = effectiveConns
                    .Where(c => c.inputPort.nodeId == node.id || c.outputPort.nodeId == node.id)
                    .ToList();

                if (nodeConns.Count > 2) continue;

                if (Random.Range(0f, 100f) >= node.spawnChance)
                {
                    nodesToRemove.Add(node);
                    foreach (var conn in nodeConns) effectiveConns.Remove(conn);

                    if (nodeConns.Count == 2)
                    {
                        string n1 = nodeConns[0].inputPort.nodeId == node.id
                            ? nodeConns[0].outputPort.nodeId : nodeConns[0].inputPort.nodeId;
                        string n2 = nodeConns[1].inputPort.nodeId == node.id
                            ? nodeConns[1].outputPort.nodeId : nodeConns[1].inputPort.nodeId;

                        bool linked = effectiveConns.Any(c =>
                            (c.inputPort.nodeId == n1 && c.outputPort.nodeId == n2) ||
                            (c.inputPort.nodeId == n2 && c.outputPort.nodeId == n1));

                        if (!linked && n1 != n2)
                            effectiveConns.Add(new DungeonGraphConnection(n1, 0, n2, 0));
                    }
                }
            }

            foreach (var node in nodesToRemove) graph.Nodes.Remove(node);

            var removedIds = new HashSet<string>(nodesToRemove.Select(n => n.id));
            graph.Connections.Clear();
            foreach (var conn in effectiveConns)
                if (!removedIds.Contains(conn.inputPort.nodeId) && !removedIds.Contains(conn.outputPort.nodeId))
                    graph.Connections.Add(conn);
        }

        internal static Dictionary<string, List<string>> BuildAdjacency(DungeonGraphAsset graph)
        {
            var adj = new Dictionary<string, List<string>>();
            foreach (var conn in graph.Connections)
            {
                string a = conn.inputPort.nodeId, b = conn.outputPort.nodeId;
                if (!adj.ContainsKey(a)) adj[a] = new List<string>();
                if (!adj.ContainsKey(b)) adj[b] = new List<string>();
                adj[a].Add(b);
                adj[b].Add(a);
            }
            return adj;
        }

        internal static Dictionary<(string, string), int> CalculateGraphDistances(DungeonGraphAsset graph)
        {
            var dist = new Dictionary<(string, string), int>();
            foreach (var a in graph.Nodes)
                foreach (var b in graph.Nodes)
                    dist[(a.id, b.id)] = a.id == b.id ? 0 : 999999;

            foreach (var conn in graph.Connections)
            {
                dist[(conn.inputPort.nodeId,  conn.outputPort.nodeId)] = 1;
                dist[(conn.outputPort.nodeId, conn.inputPort.nodeId)]  = 1;
            }

            foreach (var k in graph.Nodes)
                foreach (var i in graph.Nodes)
                    foreach (var j in graph.Nodes)
                    {
                        int via = dist[(i.id, k.id)] + dist[(k.id, j.id)];
                        if (via < dist[(i.id, j.id)]) dist[(i.id, j.id)] = via;
                    }

            return dist;
        }

        private static bool CheckRoomOverlap(
            Dictionary<string, GameObject> instances,
            Dictionary<string, Vector3> positions)
        {
            var keys = instances.Keys.ToList();
            for (int i = 0; i < keys.Count; i++)
                for (int j = i + 1; j < keys.Count; j++)
                {
                    var tA = instances[keys[i]].GetComponent<RoomTemplate>();
                    var tB = instances[keys[j]].GetComponent<RoomTemplate>();
                    if (tA == null || tB == null) continue;

                    var bA = new Bounds(positions[keys[i]], tA.worldBounds.size);
                    var bB = new Bounds(positions[keys[j]], tB.worldBounds.size);
                    if (bA.Intersects(bB)) return true;
                }
            return false;
        }
    }
}
