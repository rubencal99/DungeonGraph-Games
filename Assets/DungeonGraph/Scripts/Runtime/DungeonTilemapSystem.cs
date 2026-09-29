using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace DungeonGraph
{
    public enum CorridorType
    {
        Direct,
        Angled,
        Both
    }

    /// <summary>
    /// Handles tilemap alignment, merging, and corridor generation for dungeons
    /// </summary>
    public class DungeonTilemapSystem : MonoBehaviour
    {
        [Header("Grid Settings")]
        public float gridCellSize = 1f; // Size of each grid cell in world units

        // Corridor settings are managed exclusively by the Dungeon Tools panel in the graph editor.
        // Hidden from the Inspector to avoid confusion with the authoritative EditorPrefs values.
        [HideInInspector] public TileBase corridorTile;
        [HideInInspector] public int corridorWidth = 2;
        [HideInInspector] public CorridorType corridorType = CorridorType.Direct;

        [Header("References")]
        public Tilemap masterTilemap; // The unified tilemap for all rooms

        // Track corridor tile positions for clearing
        private HashSet<Vector3Int> corridorTilePositions = new HashSet<Vector3Int>();

        /// <summary>
        /// Finds and assigns the master tilemap: the object carrying a
        /// <see cref="DungeonMasterTilemap"/> component, or failing that a legacy
        /// "Dungeon"-tagged object.
        /// </summary>
        /// <returns>True if a master tilemap was found and assigned.</returns>
        public bool FindMasterTilemap()
        {
            masterTilemap = DungeonMasterTilemap.Find();
            if (masterTilemap != null)
                return true;

            Debug.LogWarning(
                "[DungeonTilemapSystem] No master tilemap in the scene. Drag " +
                "Prefabs/Master_Tilemap.prefab into the scene, or add a Dungeon Master Tilemap " +
                "component to the Tilemap you want rooms baked into.");
            return false;
        }

        /// <summary>
        /// Snap all room positions to a universal grid
        /// </summary>
        public void SnapRoomsToGrid(Dictionary<string, GameObject> roomInstances)
        {
            //Debug.Log($"[DungeonTilemapSystem] SnapRoomsToGrid called with {roomInstances.Count} rooms");

            foreach (var kvp in roomInstances)
            {
                GameObject roomObj = kvp.Value;
                Vector3 currentPos = roomObj.transform.position;

                // Snap to grid
                Vector3 snappedPos = new Vector3(
                    Mathf.Round(currentPos.x / gridCellSize) * gridCellSize,
                    Mathf.Round(currentPos.y / gridCellSize) * gridCellSize,
                    0f
                );

                //Debug.Log($"[DungeonTilemapSystem] Snapping {roomObj.name}: {currentPos} -> {snappedPos}");
                roomObj.transform.position = snappedPos;
            }

            //Debug.Log($"[DungeonTilemapSystem] Snapped {roomInstances.Count} rooms to grid (cell size: {gridCellSize})");
        }

        /// <summary>
        /// Transfer all room tiles to a single master tilemap
        /// </summary>
        public void MergeRoomsToMasterTilemap(Dictionary<string, GameObject> roomInstances)
        {
            if (masterTilemap == null)
            {
                Debug.LogError("[DungeonTilemapSystem] Master tilemap is not assigned!");
                return;
            }

            // Clear the master tilemap
            masterTilemap.ClearAllTiles();

            int totalTilesCopied = 0;

            foreach (var kvp in roomInstances)
            {
                GameObject roomObj = kvp.Value;
                RoomTemplate roomTemplate = roomObj.GetComponent<RoomTemplate>();

                if (roomTemplate == null || roomTemplate.tilemaps == null)
                {
                    Debug.LogWarning($"[DungeonTilemapSystem] Room {kvp.Key} has no RoomTemplate or tilemaps");
                    continue;
                }

                // Copy tiles from each tilemap in the room to the master tilemap
                foreach (Tilemap sourceTilemap in roomTemplate.tilemaps)
                {
                    if (sourceTilemap == null) continue;

                    BoundsInt bounds = sourceTilemap.cellBounds;
                    TileBase[] tiles = sourceTilemap.GetTilesBlock(bounds);

                    // Calculate world offset between source tilemap and master tilemap
                    Vector3 worldOffset = sourceTilemap.transform.position - masterTilemap.transform.position;
                    Vector3Int cellOffset = masterTilemap.WorldToCell(worldOffset);

                    // Copy each tile
                    for (int x = 0; x < bounds.size.x; x++)
                    {
                        for (int y = 0; y < bounds.size.y; y++)
                        {
                            int index = x + y * bounds.size.x;
                            TileBase tile = tiles[index];

                            if (tile != null)
                            {
                                Vector3Int sourceCell = new Vector3Int(bounds.x + x, bounds.y + y, 0);
                                Vector3Int targetCell = sourceCell + cellOffset;

                                masterTilemap.SetTile(targetCell, tile);
                                totalTilesCopied++;
                            }
                        }
                    }

                    // Optionally disable the source tilemap to avoid visual duplication
                    sourceTilemap.gameObject.SetActive(false);
                }
            }

            RebuildWalls();

            //Debug.Log($"[DungeonTilemapSystem] Merged {totalTilesCopied} tiles from {roomInstances.Count} rooms to master tilemap");
        }

        /// <summary>
        /// Generate a direct corridor from one room center to another
        /// </summary>
        public void GenerateDirectCorridor(Vector3 roomACenter, Vector3 roomBCenter)
        {
            if (masterTilemap == null || corridorTile == null)
            {
                Debug.LogError("[DungeonTilemapSystem] Master tilemap or corridor tile not assigned!");
                return;
            }

            // Convert world positions to cell positions
            Vector3Int startCell = masterTilemap.WorldToCell(roomACenter);
            Vector3Int endCell = masterTilemap.WorldToCell(roomBCenter);

            // Use Bresenham's line algorithm to create a direct path
            List<Vector3Int> pathCells = GetLinePoints(startCell, endCell);

            // Draw corridor with specified width
            foreach (Vector3Int cell in pathCells)
            {
                DrawCorridorAtCell(cell, corridorWidth);
            }

            //Debug.Log($"[DungeonTilemapSystem] Generated direct corridor from {startCell} to {endCell} ({pathCells.Count} cells)");
        }

        /// <summary>
        /// Generate a right-angle corridor (L-shaped) from one room center to another
        /// </summary>
        public void GenerateRightCorridor(Vector3 roomACenter, Vector3 roomBCenter, bool horizontalFirst = true)
        {
            if (masterTilemap == null || corridorTile == null)
            {
                Debug.LogError("[DungeonTilemapSystem] Master tilemap or corridor tile not assigned!");
                return;
            }

            Vector3Int startCell = masterTilemap.WorldToCell(roomACenter);
            Vector3Int endCell = masterTilemap.WorldToCell(roomBCenter);

            // Calculate the corner point
            Vector3Int cornerCell;
            if (horizontalFirst)
            {
                // Go horizontal first, then vertical
                cornerCell = new Vector3Int(endCell.x, startCell.y, 0);
            }
            else
            {
                // Go vertical first, then horizontal
                cornerCell = new Vector3Int(startCell.x, endCell.y, 0);
            }

            // Generate two straight corridors
            List<Vector3Int> segment1 = GetLinePoints(startCell, cornerCell);
            List<Vector3Int> segment2 = GetLinePoints(cornerCell, endCell);

            // Draw both segments
            foreach (Vector3Int cell in segment1)
            {
                DrawCorridorAtCell(cell, corridorWidth);
            }

            foreach (Vector3Int cell in segment2)
            {
                DrawCorridorAtCell(cell, corridorWidth);
            }

            //Debug.Log($"[DungeonTilemapSystem] Generated right-angle corridor from {startCell} to {endCell} via {cornerCell}");
        }

        /// <summary>
        /// Draw a corridor tile at the specified cell with the given width
        /// </summary>
        private void DrawCorridorAtCell(Vector3Int centerCell, int width)
        {
            int halfWidth = width / 2;

            for (int dx = -halfWidth; dx <= halfWidth; dx++)
            {
                for (int dy = -halfWidth; dy <= halfWidth; dy++)
                {
                    Vector3Int cell = centerCell + new Vector3Int(dx, dy, 0);

                    // Only place corridor tile if there's no tile already (don't overwrite room tiles)
                    if (!masterTilemap.HasTile(cell))
                    {
                        masterTilemap.SetTile(cell, corridorTile);
                        corridorTilePositions.Add(cell); // Track corridor position
                    }
                }
            }
        }

        /// <summary>
        /// Clear all corridor tiles from the master tilemap
        /// </summary>
        public void ClearCorridors()
        {
            if (masterTilemap == null)
            {
                Debug.LogWarning("[DungeonTilemapSystem] Cannot clear corridors: master tilemap is null");
                return;
            }

            // Remove all corridor tiles
            foreach (var corridorPos in corridorTilePositions)
            {
                masterTilemap.SetTile(corridorPos, null);
            }

            // Clear the tracking set
            corridorTilePositions.Clear();

            RebuildWalls();

            //Debug.Log("[DungeonTilemapSystem] Cleared all corridor tiles");
        }

        /// <summary>
        /// Regenerates the wall collider around the master tilemap's current floor. See
        /// <see cref="DungeonMasterTilemap.RebuildWalls"/>.
        /// </summary>
        public void RebuildWalls()
        {
            if (masterTilemap == null) return;

            var marker = masterTilemap.GetComponent<DungeonMasterTilemap>();
            if (marker != null)
                marker.RebuildWalls();
        }

        /// <summary>
        /// Get line points using Bresenham's line algorithm
        /// </summary>
        private List<Vector3Int> GetLinePoints(Vector3Int start, Vector3Int end)
        {
            List<Vector3Int> points = new List<Vector3Int>();

            int x0 = start.x, y0 = start.y;
            int x1 = end.x, y1 = end.y;

            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                points.Add(new Vector3Int(x0, y0, 0));

                if (x0 == x1 && y0 == y1) break;

                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x0 += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }

            return points;
        }

        /// <summary>
        /// Generate corridors for all connections in the graph
        /// </summary>
        public void GenerateAllCorridors(DungeonGraphAsset graph, Dictionary<string, GameObject> roomInstances,
            CorridorType corridorType = CorridorType.Direct)
        {
            if (graph == null || graph.Connections == null)
            {
                Debug.LogError("[DungeonTilemapSystem] Graph or connections are null!");
                return;
            }

            int corridorCount = 0;

            foreach (var connection in graph.Connections)
            {
                string nodeAId = connection.inputPort.nodeId;
                string nodeBId = connection.outputPort.nodeId;

                if (!roomInstances.ContainsKey(nodeAId) || !roomInstances.ContainsKey(nodeBId))
                {
                    Debug.LogWarning($"[DungeonTilemapSystem] Connection references missing room: {nodeAId} <-> {nodeBId}");
                    continue;
                }

                GameObject roomA = roomInstances[nodeAId];
                GameObject roomB = roomInstances[nodeBId];

                RoomTemplate templateA = roomA.GetComponent<RoomTemplate>();
                RoomTemplate templateB = roomB.GetComponent<RoomTemplate>();

                if (templateA == null || templateB == null)
                {
                    Debug.LogWarning($"[DungeonTilemapSystem] Rooms missing RoomTemplate component");
                    continue;
                }

                // Get connection points (either closest exits or room centers)
                Vector3 pointA = GetBestConnectionPoint(roomA, templateA, roomB, templateB);
                Vector3 pointB = GetBestConnectionPoint(roomB, templateB, roomA, templateA);

                // Determine which corridor type to use for this connection
                bool useAngled = false;
                if (corridorType == CorridorType.Angled)
                {
                    useAngled = true;
                }
                else if (corridorType == CorridorType.Both)
                {
                    // 50/50 random chance between direct and angled
                    useAngled = Random.value < 0.5f;
                }
                // else Direct (useAngled = false)

                if (useAngled)
                {
                    // Alternate between horizontal-first and vertical-first for variety
                    bool horizontalFirst = (corridorCount % 2) == 0;
                    GenerateRightCorridor(pointA, pointB, horizontalFirst);
                }
                else
                {
                    GenerateDirectCorridor(pointA, pointB);
                }

                corridorCount++;
            }

            //Debug.Log($"[DungeonTilemapSystem] Generated {corridorCount} corridors");
        }

        /// <summary>
        /// Get the best connection point for a room - either the closest exit to the target room, or the room center
        /// </summary>
        private Vector3 GetBestConnectionPoint(GameObject sourceRoom, RoomTemplate sourceTemplate,
            GameObject targetRoom, RoomTemplate targetTemplate)
        {
            // If no exits are defined, use room center as fallback
            if (sourceTemplate.exits == null || sourceTemplate.exits.Length == 0)
            {
                Debug.LogWarning($"[DungeonTilemapSystem] Room {sourceRoom.name} has no exits array or empty exits array. Using room center.");
                return sourceRoom.transform.position + sourceTemplate.worldBounds.center;
            }

            // Get target center for distance calculations
            Vector3 targetCenter = targetRoom.transform.position + targetTemplate.worldBounds.center;

            // Find the closest exit to the target room
            Transform closestExit = null;
            float closestDistance = float.MaxValue;

            foreach (Transform exit in sourceTemplate.exits)
            {
                if (exit == null) continue; // Skip null references

                Vector3 exitWorldPos = exit.position;
                float distance = Vector3.Distance(exitWorldPos, targetCenter);

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestExit = exit;
                }
            }

            // If we found a valid exit, use it; otherwise fallback to room center
            if (closestExit != null)
            {
                //Debug.Log($"[DungeonTilemapSystem] Room {sourceRoom.name} using closest exit point at {closestExit.position}");
                return closestExit.position;
            }
            else
            {
                Debug.LogWarning($"[DungeonTilemapSystem] Room {sourceRoom.name} has exits array with {sourceTemplate.exits.Length} entries, but all are null. Using room center.");
                return sourceRoom.transform.position + sourceTemplate.worldBounds.center;
            }
        }

        /// <summary>
        /// Generate corridors for all connections with overlap prevention and regeneration.
        /// </summary>
        /// <remarks>
        /// For each connection, every corridor shape (Direct, Angled horizontal-first, Angled
        /// vertical-first) is tried at the current exit-point pairing before moving on to a
        /// different, less-ideal pairing — a bad shape choice should never be mistaken for "this
        /// doorway pair can't be routed" when a different shape at the very same doorway would
        /// have worked. Only once every shape has been tried at every available exit pairing
        /// (bounded by <paramref name="maxCorridorRegenerations"/>) does it fall back to drawing
        /// whichever candidate overlapped the least.
        /// </remarks>
        public void GenerateAllCorridorsWithOverlapPrevention(DungeonGraphAsset graph, Dictionary<string, GameObject> roomInstances,
            CorridorType corridorType = CorridorType.Direct, int maxCorridorRegenerations = 3)
        {
            if (graph == null || graph.Connections == null)
            {
                Debug.LogError("[DungeonTilemapSystem] Graph or connections are null!");
                return;
            }

            int corridorCount = 0;
            int unresolvedCount = 0;

            foreach (var connection in graph.Connections)
            {
                string nodeAId = connection.inputPort.nodeId;
                string nodeBId = connection.outputPort.nodeId;

                if (!roomInstances.ContainsKey(nodeAId) || !roomInstances.ContainsKey(nodeBId))
                {
                    Debug.LogWarning($"[DungeonTilemapSystem] Connection references missing room: {nodeAId} <-> {nodeBId}");
                    continue;
                }

                GameObject roomA = roomInstances[nodeAId];
                GameObject roomB = roomInstances[nodeBId];

                RoomTemplate templateA = roomA.GetComponent<RoomTemplate>();
                RoomTemplate templateB = roomB.GetComponent<RoomTemplate>();

                if (templateA == null || templateB == null)
                {
                    Debug.LogWarning($"[DungeonTilemapSystem] Rooms missing RoomTemplate component");
                    continue;
                }

                var shapeOrder = GetCorridorShapeTryOrder(corridorType, corridorCount);

                // How many distinct exit-point pairings to try, in addition to the "best" one
                // used as the final fallback pairing. Bounded by maxCorridorRegenerations so the
                // setting still controls how hard this searches, without capping it below trying
                // every shape at least once.
                int exitVariations = Mathf.Clamp(
                    Mathf.Max(CountValidExits(templateA), CountValidExits(templateB)),
                    1, Mathf.Max(1, maxCorridorRegenerations));

                (Vector3 pointA, Vector3 pointB, bool useAngled, bool horizontalFirst) bestCandidate = default;
                int bestOverlapCount = int.MaxValue;
                bool found = false;

                for (int variation = 0; variation < exitVariations && !found; variation++)
                {
                    Vector3 pointA = GetConnectionPointVariation(roomA, templateA, roomB, templateB, variation);
                    Vector3 pointB = GetConnectionPointVariation(roomB, templateB, roomA, templateA, variation);

                    foreach (var (useAngled, horizontalFirst) in shapeOrder)
                    {
                        List<Vector3Int> corridorCells = GetCorridorCells(pointA, pointB, useAngled, horizontalFirst);
                        int overlapCount = CountCorridorRoomOverlap(corridorCells, roomInstances, nodeAId, nodeBId);

                        if (overlapCount < bestOverlapCount)
                        {
                            bestOverlapCount = overlapCount;
                            bestCandidate = (pointA, pointB, useAngled, horizontalFirst);
                        }

                        if (overlapCount == 0)
                        {
                            found = true;
                            break;
                        }
                    }
                }

                if (bestCandidate.useAngled)
                    GenerateRightCorridor(bestCandidate.pointA, bestCandidate.pointB, bestCandidate.horizontalFirst);
                else
                    GenerateDirectCorridor(bestCandidate.pointA, bestCandidate.pointB);

                if (!found)
                {
                    unresolvedCount++;
                    Debug.LogWarning($"[DungeonTilemapSystem] Corridor between {roomA.name} and {roomB.name} still overlaps " +
                                      $"a room ({bestOverlapCount} cell(s)) after trying every corridor shape at every available " +
                                      "exit pairing. Drawing the least-overlapping option found.");
                }

                corridorCount++;
            }

            if (unresolvedCount > 0)
                Debug.LogWarning($"[DungeonTilemapSystem] {unresolvedCount} of {corridorCount} corridor(s) could not fully avoid overlapping a room.");

            // Corridors open new floor through what were wall cells.
            RebuildWalls();
        }

        /// <summary>
        /// Corridor shapes to try, in order, for one connection. The preferred shape(s) implied
        /// by <paramref name="corridorType"/> come first, but every shape is always included —
        /// never sticking with a preference that would force the corridor through a room when an
        /// unpreferred shape at the same doorway would avoid it.
        /// </summary>
        private List<(bool useAngled, bool horizontalFirst)> GetCorridorShapeTryOrder(CorridorType corridorType, int corridorIndex)
        {
            bool horizontalFirst = (corridorIndex % 2) == 0;
            var direct = (useAngled: false, horizontalFirst: true);
            var angledPreferred = (useAngled: true, horizontalFirst);
            var angledOther = (useAngled: true, horizontalFirst: !horizontalFirst);

            switch (corridorType)
            {
                case CorridorType.Angled:
                    return new List<(bool, bool)> { angledPreferred, angledOther, direct };
                case CorridorType.Both:
                    return Random.value < 0.5f
                        ? new List<(bool, bool)> { direct, angledPreferred, angledOther }
                        : new List<(bool, bool)> { angledPreferred, angledOther, direct };
                case CorridorType.Direct:
                default:
                    return new List<(bool, bool)> { direct, angledPreferred, angledOther };
            }
        }

        private static int CountValidExits(RoomTemplate template)
        {
            if (template.exits == null) return 0;
            int count = 0;
            foreach (var exit in template.exits)
                if (exit != null) count++;
            return count;
        }

        /// <summary>/
        /// Get a connection point with variation based on attempt number
        /// </summary>
        private Vector3 GetConnectionPointVariation(GameObject sourceRoom, RoomTemplate sourceTemplate,
            GameObject targetRoom, RoomTemplate targetTemplate, int variation)
        {
            // If no exits are defined, use room center
            if (sourceTemplate.exits == null || sourceTemplate.exits.Length == 0)
            {
                //Debug.LogWarning($"[DungeonTilemapSystem] Room {sourceRoom.name} has no exits array or empty exits array. Using room center.");
                return sourceRoom.transform.position + sourceTemplate.worldBounds.center;
            }

            // Get all valid exits
            List<Transform> validExits = new List<Transform>();
            foreach (var exit in sourceTemplate.exits)
            {
                if (exit != null)
                {
                    validExits.Add(exit);
                }
            }

            if (validExits.Count == 0)
            {
                Debug.LogWarning($"[DungeonTilemapSystem] Room {sourceRoom.name} has exits array with {sourceTemplate.exits.Length} entries, but all are null. Using room center.");
                return sourceRoom.transform.position + sourceTemplate.worldBounds.center;
            }

            //Debug.Log($"[DungeonTilemapSystem] Room {sourceRoom.name} using exit point (variation {variation}, {validExits.Count} valid exits available)");

            // For variation 0, use closest exit (default behavior)
            // For variation 1, use second-closest exit (if available)
            // For higher variations, cycle through available exits
            Vector3 targetPos = targetRoom.transform.position + targetTemplate.worldBounds.center;

            // Sort exits by distance to target
            validExits.Sort((a, b) =>
            {
                float distA = Vector3.Distance(a.position, targetPos);
                float distB = Vector3.Distance(b.position, targetPos);
                return distA.CompareTo(distB);
            });

            // Select exit based on variation
            int exitIndex = Mathf.Min(variation, validExits.Count - 1);
            return validExits[exitIndex].position;
        }

        /// <summary>
        /// Get all cells that would be occupied by a corridor without actually drawing it
        /// </summary>
        private List<Vector3Int> GetCorridorCells(Vector3 startPoint, Vector3 endPoint, bool useAngled, bool horizontalFirst)
        {
            List<Vector3Int> allCells = new List<Vector3Int>();

            Vector3Int startCell = masterTilemap.WorldToCell(startPoint);
            Vector3Int endCell = masterTilemap.WorldToCell(endPoint);

            if (useAngled)
            {
                // L-shaped corridor
                Vector3Int cornerCell = horizontalFirst
                    ? new Vector3Int(endCell.x, startCell.y, 0)
                    : new Vector3Int(startCell.x, endCell.y, 0);

                List<Vector3Int> segment1 = GetLinePoints(startCell, cornerCell);
                List<Vector3Int> segment2 = GetLinePoints(cornerCell, endCell);

                // Expand segments to corridor width
                foreach (Vector3Int cell in segment1)
                {
                    allCells.AddRange(GetCellsWithWidth(cell, corridorWidth));
                }
                foreach (Vector3Int cell in segment2)
                {
                    allCells.AddRange(GetCellsWithWidth(cell, corridorWidth));
                }
            }
            else
            {
                // Direct corridor
                List<Vector3Int> pathCells = GetLinePoints(startCell, endCell);
                foreach (Vector3Int cell in pathCells)
                {
                    allCells.AddRange(GetCellsWithWidth(cell, corridorWidth));
                }
            }

            return allCells;
        }

        /// <summary>
        /// Get all cells that would be covered by a corridor tile with the given width
        /// </summary>
        private List<Vector3Int> GetCellsWithWidth(Vector3Int centerCell, int width)
        {
            List<Vector3Int> cells = new List<Vector3Int>();
            int halfWidth = width / 2;

            for (int dx = -halfWidth; dx <= halfWidth; dx++)
            {
                for (int dy = -halfWidth; dy <= halfWidth; dy++)
                {
                    cells.Add(centerCell + new Vector3Int(dx, dy, 0));
                }
            }

            return cells;
        }

        /// <summary>
        /// Counts how many corridor cells fall inside a room's bounds, across every room in the
        /// dungeon except the two the corridor is actually connecting.
        /// </summary>
        /// <remarks>
        /// The two connected rooms are excluded outright rather than only exempting the exact
        /// doorway cells: real exit points commonly sit some distance inside a room's own bounds
        /// (behind a wall's thickness, or simply not flush with the boundary), so a narrower,
        /// doorway-only exemption flagged the corridor's own, entirely normal path out of its
        /// source/destination room as an "overlap" far more often than it caught anything wrong —
        /// pure noise. Overlap with every *other* room is still fully checked, and every corridor
        /// shape is still tried at every exit pairing (see GenerateAllCorridorsWithOverlapPrevention)
        /// before falling back to the least-overlapping option found.
        /// </remarks>
        private int CountCorridorRoomOverlap(List<Vector3Int> corridorCells, Dictionary<string, GameObject> roomInstances,
            string excludeRoomA, string excludeRoomB)
        {
            int overlapCount = 0;

            foreach (var kvp in roomInstances)
            {
                if (kvp.Key == excludeRoomA || kvp.Key == excludeRoomB)
                    continue;

                GameObject room = kvp.Value;
                RoomTemplate template = room.GetComponent<RoomTemplate>();
                if (template == null) continue;

                // Get room bounds in world space
                Bounds roomBounds = template.worldBounds;
                Vector3 roomWorldMin = room.transform.position + roomBounds.min;
                Vector3 roomWorldMax = room.transform.position + roomBounds.max;

                // Convert to cell coordinates
                Vector3Int roomCellMin = masterTilemap.WorldToCell(roomWorldMin);
                Vector3Int roomCellMax = masterTilemap.WorldToCell(roomWorldMax);

                foreach (Vector3Int corridorCell in corridorCells)
                {
                    if (corridorCell.x >= roomCellMin.x && corridorCell.x <= roomCellMax.x &&
                        corridorCell.y >= roomCellMin.y && corridorCell.y <= roomCellMax.y)
                    {
                        overlapCount++;
                    }
                }
            }

            return overlapCount;
        }
    }
}
