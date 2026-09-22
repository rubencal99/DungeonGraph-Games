using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace DungeonGraph
{
    /// <summary>
    /// Grid flood-fill dungeon generation: starting from the Start room, every connected room
    /// claims a free cell on an integer grid adjacent to the room it branches from — the classic
    /// Binding of Isaac / Enter the Gungeon room-placement algorithm.
    ///
    /// Unlike a fixed-size lattice, each grid row/column is sized to whichever room on that line
    /// is largest, so Small/Medium/Large rooms (and arbitrary custom-node footprints) can share
    /// the same grid without clipping or wasting space: placement happens on a purely topological
    /// integer grid first, then that grid is packed into world space in a second pass once every
    /// room's real size is known.
    ///
    /// Corridor length is a first-class constraint: after packing, every graph connection is
    /// checked against maxCorridorLength, and the whole placement is re-rolled (up to
    /// maxRoomRegenerations times, keeping the best attempt seen) if any connection would need a
    /// corridor longer than that budget.
    ///
    /// Room loading is decoupled via delegates for the same reason as OrganicGeneration, so this
    /// class works in both the Unity Editor and standalone builds:
    ///   - Editor: OrganicGenerationEditorBridge.CreateRoomFactory / CreateBoundsProvider
    ///   - Runtime/builds: load prefabs through Addressables
    /// </summary>
    /// <remarks>
    /// Displayed to users as "Grid" in the Dungeon Tools panel — the class kept its original
    /// "Flood Fill" internal name.
    /// </remarks>
    public static class FloodFillGeneration
    {
        /// <summary>
        /// Instantiates a room prefab for the given node under <paramref name="parent"/>.
        /// Return null if no suitable prefab is available.
        /// </summary>
        public delegate Task<GameObject> AsyncRoomFactory(DungeonGraphNode node, Transform parent);

        /// <summary>
        /// Returns the world-space bounds of the room prefab for the given node.
        /// Used to size the grid row/column that node's cell falls on before it is instantiated.
        /// </summary>
        public delegate Task<Bounds> AsyncBoundsProvider(DungeonGraphNode node);

        private static readonly Vector2Int[] kBaseDirections =
        {
            new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(-1, 0)
        };

        /// <summary>Fallback footprint for a node with no usable bounds.</summary>
        private static readonly Vector3 kFallbackRoomSize = new Vector3(10f, 10f, 0f);

        /// <summary>Row/column size used for an empty grid line that falls between occupied ones.</summary>
        private const float kFallbackLineSize = 10f;

        // -------------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------------

        /// <summary>
        /// Full room-generation pipeline: processes spawn chances, flood-fills the graph onto an
        /// integer grid, packs that grid into world space, then snaps rooms to the tilemap grid
        /// and merges them into the master tilemap. Corridors are drawn separately via
        /// <see cref="GenerateCorridors"/>, same as Organic.
        /// </summary>
        /// <param name="graph">Graph to generate from. Pass a copy — this is mutated.</param>
        /// <param name="roomFactory">Instantiates a room prefab for a node.</param>
        /// <param name="boundsProvider">Returns room bounds without instantiating.</param>
        /// <param name="parent">Root to parent rooms under. Created if null.</param>
        /// <param name="idealDistance">Gap left between the edges of grid-adjacent rooms.</param>
        /// <param name="maxCorridorLength">
        /// Longest edge-to-edge distance allowed between two connected rooms. A placement where
        /// any connection exceeds this triggers a re-roll (see maxRoomRegenerations).
        /// </param>
        /// <param name="maxBacktrackAttempts">
        /// Rings searched outward for a free cell when all 4 immediate neighbors of a room are
        /// already occupied, before that node is deferred to a last-resort placement pass.
        /// </param>
        /// <param name="forceMode">Keep re-rolling the layout until every connection fits inside maxCorridorLength, ignoring maxRoomRegenerations.</param>
        /// <param name="chaosFactor">
        /// 0..1 extra per-room randomness on top of the run's base direction order (see seed):
        /// 0 = every room shares the same (still randomly chosen) N/E/S/W preference, 1 = every
        /// room's preference is independently reshuffled.
        /// </param>
        /// <param name="seed">
        /// Seeds this run's random layout. 0 (the default) picks a new seed automatically, so the
        /// physical layout differs every generation. Any other value reproduces the exact same
        /// layout every time — the seed actually used is logged after generation.
        /// </param>
        /// <param name="maxRoomRegenerations">Full layout re-rolls allowed when a connection's corridor would be too long.</param>
        /// <param name="maxCorridorRegenerations">Corridor re-route attempts, passed through to the tilemap system.</param>
        public static async Task GenerateRooms(
            DungeonGraphAsset graph,
            AsyncRoomFactory roomFactory,
            AsyncBoundsProvider boundsProvider,
            Transform parent = null,
            float idealDistance = 20f,
            float maxCorridorLength = 60f,
            int maxBacktrackAttempts = 6,
            bool forceMode = false,
            float chaosFactor = 0f,
            int seed = 0,
            int maxRoomRegenerations = 3,
            int maxCorridorRegenerations = 3)
        {
            if (graph == null)
            {
                Debug.LogError("[FloodFillGeneration] graph is null.");
                return;
            }

            var startNode = graph.GetStartNode();
            if (startNode == null)
            {
                Debug.LogError("[FloodFillGeneration] No Start node found in this graph.");
                return;
            }

            // Shared with Organic — this project's equivalent of classic node injection
            // probabilities.
            OrganicGeneration.ProcessSpawnChances(graph);

            // Spawn chance processing can, in principle, remove the Start node itself.
            startNode = graph.GetStartNode();
            if (startNode == null)
            {
                Debug.LogError("[FloodFillGeneration] Start node was removed by spawn-chance processing; cannot seed placement.");
                return;
            }

            if (parent == null)
            {
                var container = new GameObject("Generated_Dungeon");
                parent = container.transform;
            }

            var adjacency = OrganicGeneration.BuildAdjacency(graph);

            // --- Measure bounds up front so grid rows/columns can be sized before any prefab is
            //     instantiated (same reasoning as Organic's up-front bounds pass). ---
            var nodeBounds = new Dictionary<string, Bounds>();
            foreach (var node in graph.Nodes)
                nodeBounds[node.id] = await boundsProvider(node);

            // --- Seed a self-contained RNG for this run. Using System.Random instead of
            //     UnityEngine.Random keeps this class's randomness reproducible from one seed
            //     without disturbing the engine's global random state (which prefab-variant
            //     selection and spawn-chance rolls also draw from). 0 means "pick a fresh seed
            //     automatically", so the layout differs every generation by default. ---
            int effectiveSeed = seed != 0 ? seed : unchecked((int)System.DateTime.Now.Ticks);
            var rng = new System.Random(effectiveSeed);

            // --- Try placements until every connection fits inside maxCorridorLength, keeping
            //     whichever attempt had the fewest violations if the budget runs out. ---
            int maxAttempts = forceMode ? 64 : Mathf.Max(1, maxRoomRegenerations + 1);
            Dictionary<string, Vector2Int> bestCellOf = null;
            Dictionary<string, Vector3> bestWorldPositions = null;
            int bestViolations = int.MaxValue;
            int attemptsRun = 0;

            while (attemptsRun < maxAttempts)
            {
                var cellOf = PlaceOnLogicalGrid(graph, adjacency, startNode.id, maxBacktrackAttempts, chaosFactor, rng);
                var worldPositions = PackGridToWorldSpace(cellOf, nodeBounds, idealDistance);
                int violations = CountLongCorridors(graph, worldPositions, nodeBounds, maxCorridorLength);

                if (violations < bestViolations)
                {
                    bestViolations = violations;
                    bestCellOf = cellOf;
                    bestWorldPositions = worldPositions;
                }

                attemptsRun++;
                if (violations == 0) break;
            }

            if (bestViolations > 0)
            {
                Debug.LogWarning($"[FloodFillGeneration] {bestViolations} connection(s) still need a corridor longer " +
                                  $"than Max Corridor Length ({maxCorridorLength}) after {attemptsRun} attempt(s). " +
                                  "Consider raising Max Corridor Length, Max Backtrack Attempts, or Max Room Regenerations.");
            }

            Debug.Log($"[FloodFillGeneration] Layout seed: {effectiveSeed}. Enter this into the Seed field " +
                      "(Flood Fill Parameters) to reproduce this exact layout.");

            // --- Instantiate rooms at their packed positions. ---
            var roomInstances = new Dictionary<string, GameObject>();
            foreach (var node in graph.Nodes)
            {
                if (!bestCellOf.TryGetValue(node.id, out _)) continue;

                var roomObj = await OrganicGeneration.SpawnRoom(node, parent, (n, p) => roomFactory(n, p));
                if (roomObj == null) continue;

                var tmpl = roomObj.GetComponent<RoomTemplate>();
                Vector3 centerOffset = tmpl != null ? tmpl.worldBounds.center : Vector3.zero;
                roomObj.transform.position = bestWorldPositions[node.id] - centerOffset;

                roomInstances[node.id] = roomObj;
            }

            // If any nodes failed to spawn (e.g. missing Addressable label), remove them and
            // their connections so corridor generation never references a missing room — same
            // safety net Organic uses.
            if (roomInstances.Count < graph.Nodes.Count)
            {
                int failedCount = graph.Nodes.Count - roomInstances.Count;
                var spawnedIds = new HashSet<string>(roomInstances.Keys);
                graph.Nodes.RemoveAll(n => !spawnedIds.Contains(n.id));
                graph.Connections.RemoveAll(c =>
                    !spawnedIds.Contains(c.inputPort.nodeId) ||
                    !spawnedIds.Contains(c.outputPort.nodeId));

                Debug.LogWarning($"[FloodFillGeneration] {failedCount} node(s) failed to spawn — " +
                                 "check that Addressable labels match the floor label and room type. " +
                                 "Their connections have been removed to prevent corridor errors.");
            }

            OrganicGeneration.SetupVisualizerInstant(graph, parent, roomInstances);

            var tilemapSystem = parent.gameObject.GetComponent<DungeonTilemapSystem>();
            if (tilemapSystem == null)
                tilemapSystem = parent.gameObject.AddComponent<DungeonTilemapSystem>();

            tilemapSystem.SnapRoomsToGrid(roomInstances);

            if (tilemapSystem.masterTilemap == null)
                tilemapSystem.FindMasterTilemap();

            if (tilemapSystem.masterTilemap != null)
                tilemapSystem.MergeRoomsToMasterTilemap(roomInstances);
            else
                Debug.LogWarning("[FloodFillGeneration] Master tilemap not found, so room tilemaps were not merged. " +
                                 "Drag Prefabs/Master_Tilemap.prefab into the scene, or add a Dungeon Master Tilemap component to an existing Tilemap.");
        }

        /// <summary>
        /// Generates corridors for an existing Generated_Dungeon. Rooms must already be placed.
        ///
        /// The corridor stage is style-agnostic — it reads RoomNodeReference components off the
        /// dungeon parent and hands them to DungeonTilemapSystem. Because the layout stage above
        /// only ever places a room directly north/east/south/west of its neighbor, the resulting
        /// gaps are already short and axis-aligned, so the existing direct/angled corridor drawer
        /// needs no changes to produce clean, short corridors here.
        /// </summary>
        public static void GenerateCorridors(DungeonGraphAsset graph, GameObject dungeonParent, int maxCorridorRegenerations = 3)
        {
            OrganicGeneration.GenerateCorridors(graph, dungeonParent, maxCorridorRegenerations);
        }

        // -------------------------------------------------------------------------
        // Phase 1: topology — flood-fill placement on an integer grid
        // -------------------------------------------------------------------------

        /// <summary>
        /// Walks the graph outward from <paramref name="startNodeId"/>, assigning each node a
        /// free integer cell adjacent to the node it branches from. Any node not reachable from
        /// Start (a separate connected component) is seeded near the existing cluster instead of
        /// being silently dropped.
        /// </summary>
        /// <remarks>
        /// The direction each room tries first is a fresh random permutation every call (see
        /// <paramref name="rng"/>), not a fixed N/E/S/W order. A fixed order would almost always
        /// place a room's first unplaced neighbor in whichever direction is tried first — since
        /// that direction is essentially always free early in the fill — so the whole layout
        /// would drift the same way every time and Start would end up pinned in whichever corner
        /// of the resulting bounding box is opposite that drift.
        /// </remarks>
        private static Dictionary<string, Vector2Int> PlaceOnLogicalGrid(
            DungeonGraphAsset graph,
            Dictionary<string, List<string>> adjacency,
            string startNodeId,
            int maxBacktrackAttempts,
            float chaosFactor,
            System.Random rng)
        {
            var cellOf = new Dictionary<string, Vector2Int>();
            var occupied = new Dictionary<Vector2Int, string>();
            var visited = new HashSet<string>();
            var deferred = new List<string>();

            // One random base order for the whole attempt (re-rolled fresh on every retry via
            // the shared rng); chaosFactor then controls how much each individual room deviates
            // from it. This keeps chaosFactor's original meaning (0 = consistent preference
            // across the layout, 1 = independently chaotic per room) without ever falling back
            // to a hardcoded, direction-biased order.
            Vector2Int[] baseDirectionOrder = ShuffleDirections(kBaseDirections, rng);

            void SeedAndFlood(string seedId, Vector2Int seedCell)
            {
                if (occupied.ContainsKey(seedCell))
                    seedCell = FindNearestFreeCell(seedCell, occupied, 64) ?? seedCell;

                cellOf[seedId] = seedCell;
                occupied[seedCell] = seedId;
                visited.Add(seedId);

                var frontier = new Queue<string>();
                frontier.Enqueue(seedId);

                while (frontier.Count > 0)
                {
                    string current = frontier.Dequeue();
                    Vector2Int currentCell = cellOf[current];
                    if (!adjacency.TryGetValue(current, out var neighbors)) continue;

                    var directions = ApplyChaosJitter(baseDirectionOrder, chaosFactor, rng);

                    foreach (var neighborId in neighbors)
                    {
                        if (visited.Contains(neighborId)) continue;
                        visited.Add(neighborId);

                        bool placed = false;
                        foreach (var dir in directions)
                        {
                            var candidate = currentCell + dir;
                            if (occupied.ContainsKey(candidate)) continue;

                            occupied[candidate] = neighborId;
                            cellOf[neighborId] = candidate;
                            frontier.Enqueue(neighborId);
                            placed = true;
                            break;
                        }

                        if (placed) continue;

                        var ringCandidate = FindNearestFreeCell(currentCell, occupied, maxBacktrackAttempts);
                        if (ringCandidate.HasValue)
                        {
                            occupied[ringCandidate.Value] = neighborId;
                            cellOf[neighborId] = ringCandidate.Value;
                            frontier.Enqueue(neighborId);
                        }
                        else
                        {
                            deferred.Add(neighborId);
                        }
                    }
                }
            }

            SeedAndFlood(startNodeId, Vector2Int.zero);

            // Any node not reached from Start belongs to a separate component — seed it near the
            // existing cluster rather than dropping it.
            foreach (var node in graph.Nodes)
            {
                if (visited.Contains(node.id)) continue;
                var seedCell = FindNearestFreeCell(Vector2Int.zero, occupied, 128) ?? Vector2Int.zero;
                SeedAndFlood(node.id, seedCell);
            }

            // Force-place anything that still couldn't find a spot (pathological, tightly
            // boxed-in cases only).
            foreach (var nodeId in deferred)
            {
                if (cellOf.ContainsKey(nodeId)) continue;
                var candidate = FindNearestFreeCell(Vector2Int.zero, occupied, 256) ?? Vector2Int.zero;
                occupied[candidate] = nodeId;
                cellOf[nodeId] = candidate;
            }

            return cellOf;
        }

        /// <summary>
        /// Expands outward in square rings from <paramref name="anchor"/> and returns the first
        /// free cell found, or null if none exists within <paramref name="maxSearchRadius"/> rings.
        /// </summary>
        private static Vector2Int? FindNearestFreeCell(Vector2Int anchor, Dictionary<Vector2Int, string> occupied, int maxSearchRadius)
        {
            for (int radius = 1; radius <= maxSearchRadius; radius++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != radius) continue; // ring perimeter only

                        var candidate = anchor + new Vector2Int(dx, dy);
                        if (!occupied.ContainsKey(candidate)) return candidate;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Fisher-Yates shuffle using the run's seeded RNG — this is what gives every generation
        /// (and every retry attempt within one generation) its own random direction preference,
        /// instead of a hardcoded order that would drift the layout the same way every time.
        /// </summary>
        private static Vector2Int[] ShuffleDirections(Vector2Int[] baseDirections, System.Random rng)
        {
            var dirs = (Vector2Int[])baseDirections.Clone();
            for (int i = dirs.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (dirs[i], dirs[j]) = (dirs[j], dirs[i]);
            }
            return dirs;
        }

        /// <summary>
        /// Starts from the run's base direction order and, per room, probabilistically reshuffles
        /// it further. 0 = every room uses the same base order. 1 = every room's preference is
        /// independently reshuffled.
        /// </summary>
        private static Vector2Int[] ApplyChaosJitter(Vector2Int[] baseOrder, float chaosFactor, System.Random rng)
        {
            if (chaosFactor <= 0f) return baseOrder;

            var dirs = (Vector2Int[])baseOrder.Clone();
            for (int i = dirs.Length - 1; i > 0; i--)
            {
                if (rng.NextDouble() > chaosFactor) continue;
                int j = rng.Next(i + 1);
                (dirs[i], dirs[j]) = (dirs[j], dirs[i]);
            }
            return dirs;
        }

        // -------------------------------------------------------------------------
        // Phase 2: packing — lay the integer grid out in world space
        // -------------------------------------------------------------------------

        /// <summary>
        /// Converts integer cells into world positions. Each grid row/column is sized to the
        /// largest room assigned to it (falling back to a small default for an empty line that
        /// falls between occupied ones), so Small/Medium/Large rooms can share the grid without
        /// clipping. Rooms smaller than their line's size are centered within it.
        /// </summary>
        /// <remarks>
        /// The finished layout is re-centered on the world origin (see centerOffset below)
        /// instead of being left corner-anchored at the packing cursor's (0, 0) start, so it sits
        /// the same way Organic's layout does rather than always growing up-and-right from
        /// a fixed corner of world space.
        /// </remarks>
        private static Dictionary<string, Vector3> PackGridToWorldSpace(
            Dictionary<string, Vector2Int> cellOf,
            Dictionary<string, Bounds> nodeBounds,
            float idealDistance)
        {
            var worldPositions = new Dictionary<string, Vector3>();
            if (cellOf.Count == 0) return worldPositions;

            int minGX = cellOf.Values.Min(c => c.x), maxGX = cellOf.Values.Max(c => c.x);
            int minGY = cellOf.Values.Min(c => c.y), maxGY = cellOf.Values.Max(c => c.y);

            var columnWidth = new Dictionary<int, float>();
            var rowHeight = new Dictionary<int, float>();

            foreach (var kvp in cellOf)
            {
                Vector3 size = nodeBounds.TryGetValue(kvp.Key, out var b) && b.size.sqrMagnitude > 0.0001f
                    ? b.size
                    : kFallbackRoomSize;
                var cell = kvp.Value;

                if (!columnWidth.TryGetValue(cell.x, out var w) || size.x > w) columnWidth[cell.x] = size.x;
                if (!rowHeight.TryGetValue(cell.y, out var h) || size.y > h) rowHeight[cell.y] = size.y;
            }

            var columnStart = new Dictionary<int, float>();
            float cursorX = 0f;
            for (int gx = minGX; gx <= maxGX; gx++)
            {
                columnStart[gx] = cursorX;
                float width = columnWidth.TryGetValue(gx, out var w) ? w : kFallbackLineSize;
                cursorX += width + idealDistance;
            }

            var rowStart = new Dictionary<int, float>();
            float cursorY = 0f;
            for (int gy = minGY; gy <= maxGY; gy++)
            {
                rowStart[gy] = cursorY;
                float height = rowHeight.TryGetValue(gy, out var h) ? h : kFallbackLineSize;
                cursorY += height + idealDistance;
            }

            // cursorX/cursorY each include one trailing idealDistance gap that was added after
            // the last column/row but never followed by anything — subtract it back off before
            // using the cursor as the layout's total span.
            var centerOffset = new Vector3((cursorX - idealDistance) * 0.5f, (cursorY - idealDistance) * 0.5f, 0f);

            foreach (var kvp in cellOf)
            {
                var cell = kvp.Value;
                float width = columnWidth.TryGetValue(cell.x, out var w) ? w : kFallbackLineSize;
                float height = rowHeight.TryGetValue(cell.y, out var h) ? h : kFallbackLineSize;

                float centerX = columnStart[cell.x] + width * 0.5f;
                float centerY = rowStart[cell.y] + height * 0.5f;
                worldPositions[kvp.Key] = new Vector3(centerX, centerY, 0f) - centerOffset;
            }

            return worldPositions;
        }

        /// <summary>
        /// Counts graph connections whose packed rooms are farther apart (edge-to-edge, using
        /// each room's bounding radius as an approximation — same simplification Organic's own
        /// overlap/repulsion checks use) than <paramref name="maxCorridorLength"/> allows.
        /// </summary>
        private static int CountLongCorridors(
            DungeonGraphAsset graph,
            Dictionary<string, Vector3> worldPositions,
            Dictionary<string, Bounds> nodeBounds,
            float maxCorridorLength)
        {
            int violations = 0;
            foreach (var conn in graph.Connections)
            {
                string a = conn.inputPort.nodeId, b = conn.outputPort.nodeId;
                if (!worldPositions.TryGetValue(a, out var posA) || !worldPositions.TryGetValue(b, out var posB)) continue;

                float radiusA = nodeBounds.TryGetValue(a, out var boundsA)
                    ? Mathf.Max(boundsA.size.x, boundsA.size.y) / 2f : 5f;
                float radiusB = nodeBounds.TryGetValue(b, out var boundsB)
                    ? Mathf.Max(boundsB.size.x, boundsB.size.y) / 2f : 5f;

                float edgeToEdge = Mathf.Max(0f, Vector3.Distance(posA, posB) - radiusA - radiusB);
                if (edgeToEdge > maxCorridorLength) violations++;
            }
            return violations;
        }
    }
}
