# 5. Script Reference

[← Documentation Index](0_Documentation_Index.md)

Every runtime type is in the `DungeonGraph` namespace; every editor type is in
`DungeonGraph.Editor`.

## Contents

- [5.1 Runtime — data model](#51-runtime--data-model)
- [5.2 Runtime — node types](#52-runtime--node-types)
- [5.3 Runtime — generation](#53-runtime--generation)
- [5.4 Runtime — tilemap system](#54-runtime--tilemap-system)
- [5.5 Runtime — room components](#55-runtime--room-components)
- [5.6 Runtime — entry points](#56-runtime--entry-points)
- [5.7 Editor — graph editor](#57-editor--graph-editor)
- [5.8 Editor — room authoring](#58-editor--room-authoring)
- [5.9 Editor — Addressables and floors](#59-editor--addressables-and-floors)

### Assemblies

| Assembly | Location | In player builds |
| --- | --- | --- |
| `DungeonGraph.Runtime` | `Scripts/Runtime/` | Yes |
| `DungeonGraph.Addressables` | `Scripts/Runtime/Addressables/` | Yes |
| `DungeonGraph.Editor` | `Scripts/Editor/` | No |
| `DungeonGraph.Setup.Editor` | `Setup/Editor/` | No |

`DungeonGraph.Runtime` has no package dependencies, so the components on your
room prefabs stay intact even if Addressables is not installed.

---

## 5.1 Runtime — data model

### 5.1.1 `DungeonGraphAsset` : ScriptableObject

Stores a graph. Create with `Assets > Create > Dungeon Graph > New Graph`.

| Member | Description |
| --- | --- |
| `nodes` | `List<DungeonGraphNode>`, `[SerializeReference]` so polymorphic subclasses survive serialization. |
| `connections` | `List<DungeonGraphConnection>`. |
| `Init()` | Builds the internal GUID → node lookup. **Call before `GetNode` or `GetNodeFromOutput`.** The generation pipeline calls it automatically. |
| `GetStartNode()` | Returns the first `StartNode`. Multiple Start nodes are undefined behaviour. |
| `GetNode(string id)` | Node by GUID. |
| `GetNodeFromOutput(string nodeId, int portIndex)` | The node connected to a given output port. |

### 5.1.2 `DungeonGraphNode`

Base class for all node types. A plain serializable class, not a MonoBehaviour.

| Member | Description |
| --- | --- |
| `id` | GUID assigned at construction; the stable identity key. |
| `position` | `Rect` — the node's position on the editor canvas. |
| `spawnChance` | `float` 0–100, clamped on set. See [2.4](2_Authoring_Guide.md#24-spawn-chance). |
| `OnProcess(graph)` | Walks to the next node by output port. Used by `DungeonGraphObject`, not by generation. |

Subclasses add their own data and carry `[NodeInfo]` to register with the editor
search menu.

### 5.1.3 `DungeonGraphConnection` (struct)

An edge. Holds two `DungeonGraphConnectionPort` values (`inputPort`,
`outputPort`), each a `nodeId` plus a `portIndex`.

The generation pipeline treats connections as **undirected** — `BuildAdjacency`
adds both directions — so the input/output split has no effect on layout.

### 5.1.4 `DungeonFloorConfig`

Plain data: a floor's display name and its asset folder path. Returned by
`DungeonFloorManager.GetAllFloors()`. Not persisted on its own.

### 5.1.5 `CustomNodeType`

Serializable data for a user-created node type: `typeName`, `color`, `guid`.

### 5.1.6 `CustomNodeTypeRegistry` : ScriptableObject

Project-wide registry of custom node types, stored by default at
`Scripts/Runtime/DefaultCustomNodeTypeRegistry.asset`.

| Member | Description |
| --- | --- |
| `customNodeTypes` | `List<CustomNodeType>`. |
| `GetOrCreateDefault()` | The standard accessor. In the editor it finds the asset by type — so the package folder can be moved — and creates one if none exists. |
| `AddCustomNodeType(name, color)` | Adds a type. Returns false if the name is taken (case-insensitive). |
| `GetNodeType(name)` / `HasNodeType(name)` / `RemoveNodeType(name)` | Lookup and removal. |

Editor-only APIs inside this class are compiled out of player builds.

---

## 5.2 Runtime — node types

All extend `DungeonGraphNode` and carry `[NodeInfo(title, menuItem)]`, which
registers them in the search menu and drives their CSS class in the graph view.

The prefab subfolder is derived from the C# type name with `"Node"` stripped.

| Type | Prefab folder | Notes |
| --- | --- | --- |
| `StartNode` | `{Floor}/Start/` | Dungeon entrance. One per graph. |
| `EndNode` | `{Floor}/End/` | Dungeon exit. Several allowed. |
| `HubNode` | `{Floor}/Hub/` | High-connectivity junction. |
| `BossNode` | `{Floor}/Boss/` | Boss encounter. |
| `RewardNode` | `{Floor}/Reward/` | Treasure room. |
| `BasicNode` | `{Floor}/Basic/{Size}/` | Has a `RoomSize` enum (`Small`, `Medium`, `Large`) exposed on the node via `[ExposedProperty]`. |
| `CustomNode` | `{Floor}/{TypeName}/` | Stores `customTypeName`, `customTypeGuid` and `customColor`, copied from the registry by `Initialize()`. |
| `DebugLogNode` | — | Logs a message during `OnProcess`. No effect on generation. |

### Attributes

| Attribute | Purpose |
| --- | --- |
| `NodeInfoAttribute(title, menuItem, hasFlowInput, hasFlowOutput)` | Registers a node type with the editor. `menuItem` sets its place in the search tree, e.g. `"Rooms/Hub"`. |
| `ExposedPropertyAttribute` | Marks a field to be drawn on the node body in the graph editor. |

---

## 5.3 Runtime — generation

### 5.3.1 `GenerationStyle` (enum)

| Value | Meaning |
| --- | --- |
| `Organic` (0) | Force-directed layout. |
| `FloodFill` (2) | Grid flood-fill layout. Displayed to users as "Grid" — the enum member and backing class kept their original internal name. |

### 5.3.2 `OrganicGeneration` (static)

The force-directed pipeline. Lives in the runtime assembly so the editor tools and
runtime MonoBehaviours share one implementation.

Room loading is injected through two delegates, which is what keeps the algorithm
independent of Addressables and of the AssetDatabase:

| Delegate | Signature |
| --- | --- |
| `AsyncRoomFactory` | `Task<GameObject> (DungeonGraphNode node, Transform parent)` |
| `AsyncBoundsProvider` | `Task<Bounds> (DungeonGraphNode node)` |

| Method | Description |
| --- | --- |
| `GenerateRooms(graph, roomFactory, boundsProvider, …)` | The full pipeline. If `realTimeSimulation` is on and the application is playing, hands off to `DungeonSimulationController` and returns early. Nodes that fail to instantiate are pruned from the graph copy, along with their connections, before corridors are drawn. |
| `GenerateCorridors(graph, dungeonParent, maxCorridorRegenerations)` | Corridors only. Gathers rooms by scanning for `RoomNodeReference` under `dungeonParent`. |
| `PostSimulationSetup(graph, dungeonParent, generateCorridors)` | Called by `DungeonSimulationController` when animated placement finishes: snap, merge, optionally draw corridors. |

`SpatialHashGrid`, a grid-bucketed spatial index, is declared in this file and
available for proximity queries.

### 5.3.3 `FloodFillGeneration` (static)

The grid flood-fill pipeline (displayed to users as "Grid"). Its public surface
mirrors `OrganicGeneration` — `GenerateRooms`, `GenerateCorridors`, and the same
two delegates (each style declares its own delegate types, so callers adapt by
lambda) — so callers can swap styles freely. There is no animated-placement
path; `GenerateRooms` always resolves instantly.

Placement happens in two phases: `PlaceOnLogicalGrid` grows outward from Start
on a purely topological integer grid (no world positions yet), then
`PackGridToWorldSpace` sizes each row/column to its largest occupant and lays
the grid out in world space, re-centering the result on the origin. A
`System.Random` seeded from the `seed` parameter (or the system clock, if
`seed` is 0) drives every random decision in the layout, independent of
`UnityEngine.Random`'s global state — this is what makes a given seed
reproduce an identical layout, and what makes `seed: 0` (the default) yield a
different layout on every generation.

Every graph connection is checked against `maxCorridorLength` after packing;
if any connection exceeds it, the whole placement is re-rolled (up to
`maxRoomRegenerations` times, or 64 under Force Mode), keeping whichever
attempt had the fewest over-length connections.

`GenerateCorridors` forwards to `OrganicGeneration`'s — the corridor stage is
style-agnostic, and because placement only ever puts a room directly
north/east/south/west of its neighbor, the existing straight/angled corridor
drawer needs no changes to produce short, clean hallways here.

See [3.3](3_Generation_Styles.md#33-grid-flood-fill) and
[Flood Fill (Grid) generation](FloodFill_Generation.md) for the plain-language
walkthrough.

### 5.3.4 `DungeonSimulationUtility` (static)

The physics model, shared by instant and animated modes.

| Method | Description |
| --- | --- |
| `SimulateForces(…)` | Runs the whole loop for N iterations, or until convergence in Force Mode. Modifies positions and velocities in place. |
| `CalculateForcesForIteration(…)` | One iteration's forces, without applying them. Used by the animated path. |

`RepulsionScalingMode` controls how repulsion varies with graph distance:

| Mode | Scaled strength |
| --- | --- |
| `Default` | `strength × graphDistance` |
| `Flat` | `strength` |
| `Inverse` | `strength / (1 + graphDistance × 0.5)` |
| `SqrtDamped` | `strength × √graphDistance` |

### 5.3.5 `DungeonSimulationController` : MonoBehaviour

Drives animated placement. Added to the dungeon parent by `OrganicGeneration`,
receives its parameters through `StartSimulation()`, runs one physics iteration
per frame, and lerps room positions toward their targets.

On completion it checks for overlap; if rooms overlap and the regeneration budget
is not spent, it re-randomises and restarts. When satisfied it calls
`OrganicGeneration.PostSimulationSetup` and destroys itself.

The component is ephemeral — its presence indicates a simulation is still running.

---

## 5.4 Runtime — tilemap system

### 5.4.1 `DungeonTilemapSystem` : MonoBehaviour

Grid alignment, tilemap merging and corridor drawing. Attached to the dungeon
parent during generation.

| Member | Description |
| --- | --- |
| `gridCellSize` | Cell size used for snapping. Default 1.0. |
| `masterTilemap` | Target tilemap. Resolved automatically if unset. |
| `FindMasterTilemap()` | Finds and assigns the master tilemap. Returns false and logs guidance if the scene has none. |
| `SnapRoomsToGrid(rooms)` | Rounds each room's position to the nearest `gridCellSize` boundary. |
| `MergeRoomsToMasterTilemap(rooms)` | Clears the master tilemap, then copies each room's tiles into it block by block with the world offset applied. Source renderers are disabled afterwards. |
| `GenerateDirectCorridor(start, end)` | Bresenham line, `corridorWidth` tiles thick. Does not overwrite existing tiles. |
| `GenerateRightCorridor(start, end, horizontalFirst)` | L-shaped path, drawn as two direct segments. |
| `GenerateAllCorridorsWithOverlapPrevention(graph, rooms, type, maxRegen)` | Draws every corridor, retrying with different exits and corridor types on overlap. |
| `GetBestConnectionPoint(…)` | The exit on one room nearest the center of another. Falls back to the room center when the exits array is empty. |

`corridorTile`, `corridorWidth` and `corridorType` are `[HideInInspector]`; they
are set from the Dungeon Tools panel or from `DungeonGenerator`.

### 5.4.2 `CorridorType` (enum)

`Direct`, `Angled`, `Both`.

### 5.4.3 `DungeonMasterTilemap` : MonoBehaviour

Marks the Tilemap that generated rooms and corridors are baked into. Put it on the
same GameObject as the master `Tilemap`; `Master_Tilemap.prefab` already has it.

| Member | Description |
| --- | --- |
| `Tilemap` | The Tilemap on this GameObject. |
| `Find()` (static) | Returns the scene's master tilemap, or null. Checks for this component first, then falls back to a `"Dungeon"`-tagged object for scenes built against the older workflow. Never throws, whatever the project's tags look like. |
| `FindMarker()` (static) | The marker component itself, including inactive objects. |

---

## 5.5 Runtime — room components

### 5.5.1 `RoomTemplate` : MonoBehaviour

On every room prefab. Stores spatial metadata the pipeline reads *without*
instantiating the prefab.

| Member | Description |
| --- | --- |
| `worldBounds` | World-space bounding box of the room's tiles. |
| `sizeInCells` | Width and height in tile units. |
| `tilemaps` | Every `Tilemap` found in children. |
| `exits` | `Transform[]` marking doorways. |
| `Recompute()` | Run during baking. Compresses each child tilemap and computes the union bounding box in world space, so tilemaps with different local origins combine correctly. |
| `RepopulateExitsIfNeeded()` | Fills `exits` from a child named `Exits`. Called during instantiation and from `Awake` in Play Mode, so prefabs authored outside the standard workflow still work. |

### 5.5.2 `RoomNodeReference` : MonoBehaviour

Added to every room at instantiation. Stores `nodeId` (the graph node's GUID) and
`nodeTypeName` (e.g. `"Basic"`, `"Hub"`).

This is how the pipeline — and your code — maps scene objects back to graph nodes.

### 5.5.3 `DungeonConnectionVisualizer` : MonoBehaviour

Editor-only Scene-view gizmos: room bounds as filled cubes in their node-type
colour, plus connection lines with a midpoint sphere so short links stay visible.
Purely visual; removing it has no effect on generation.

`GetColorForNodeType(name)` returns the colour for a type name, matching the graph
editor's scheme, and grey for anything unrecognised.

---

## 5.6 Runtime — entry points

### 5.6.1 `DungeonGenerator` : MonoBehaviour

Runtime generation via Addressables. Full documentation in
[4. Runtime API](4_Runtime_API.md).

| Member | Description |
| --- | --- |
| `Generate()` | Starts generation. |
| `GenerateDungeon()` | `Task<DungeonGenerationResult>`; null on failure. |
| `OnGenerationComplete` | `event Action<DungeonGenerationResult>`. |
| `OnGenerationFailed` | `event Action`. |
| `LastResult` | The most recent successful result. |

Deep-copies the graph asset before generating, so the source asset is never
mutated. The Addressables room factory tracks which prefabs it has used per type
to maximise variety, resetting when all have been used.

### 5.6.2 `DungeonGenerationResult` / `RoomResult`

See [4.4 Reading the result](4_Runtime_API.md#44-reading-the-result).

### 5.6.3 `RuntimeDungeonGenerator` : MonoBehaviour

A worked example: awaits `DungeonGenerator.GenerateDungeon()`, then instantiates a
player prefab at the Start room's center. Copy and adapt it; production code
should call `DungeonGenerator` directly.

### 5.6.4 `DungeonGraphObject` : MonoBehaviour

Sequential graph traversal: instantiates the graph, calls `Init()`, finds the
Start node, then calls `OnProcess` on each node until none is returned. It does
not instantiate rooms or perform layout. Useful as a lightweight way to walk a
graph for your own purposes.

---

## 5.7 Editor — graph editor

### 5.7.1 `DungeonGraphEditorWindow` : EditorWindow

Hosts the graph view. `Open()` reuses an existing window for the same asset or
docks a new one beside the Scene view. Owns the `SerializedObject` and saves on
focus loss, focus gain and destroy, so a recompile cannot lose work.

### 5.7.2 `DungeonGraphView` : GraphView

The main view. Draws nodes and edges, builds the Dungeon Tools and Node Settings
panels, and dispatches generation to the selected style.

Every generation parameter is persisted in `EditorPrefs` under the
`DungeonGraph.*` prefix. Each style keeps its own keys, so switching styles does
not lose tuning. `RebuildStyleParameters()` rebuilds the style-specific block on
every style change.

### 5.7.3 `DungeonGraphEditorNode` : Node

One node's visual. Uses a center-jack layout: the default port containers are
hidden and a circular drag target is inserted over the node body, holding one
input and one output port stacked to fill it, so the whole circle is clickable.

Fields marked `[ExposedProperty]` are drawn with a `PropertyField` bound to the
node's serialized data, located by matching node GUID within the asset's node
array.

### 5.7.4 `DungeonGraphWindowSearchProvider` : ISearchWindowProvider

Populates the right-click Create Node menu. Scans loaded assemblies for types
carrying `[NodeInfo]`, then appends custom types from the registry under
`Custom/`. A `+ Create New Node Type` entry opens `CreateCustomNodeTypeWindow`.

### 5.7.5 `DungeonGraphAssetEditor` : Editor

Inspector for `DungeonGraphAsset`. Adds an `[OnOpenAsset]` callback so
double-clicking opens the graph editor.

### 5.7.6 `PortTypes`

Defines the `FlowPort` marker type shared by every port. GraphView needs a common
type object to decide port compatibility; one suffices because all ports here are
undirected.

### 5.7.7 `DungeonGraphPaths` (static)

Resolves the package's location on disk by finding its own script file and walking
up. Every other editor script asks this class for its folders rather than
hard-coding `Assets/DungeonGraph/…`, so the package keeps working after being
renamed or moved.

| Member | Description |
| --- | --- |
| `PackageRoot` | e.g. `Assets/DungeonGraph`. Never null. |
| `FloorsRoot`, `PrefabsRoot`, `BlankRoomTemplate` | Derived paths. |
| `LoadEditorStyleSheet()` | Loads the graph editor USS, searching the project if the expected path misses. Returns null rather than throwing. |
| `EnsureFolderExists(path)` | Creates every missing folder along an `Assets/a/b/c` path. |

---

## 5.8 Editor — room authoring

### 5.8.1 `RoomAuthoringTool` (static)

Menu items `Tools > Dungeon Graph > Rooms > Save` and `Save As…`. The baking
process is described in [2.9](2_Authoring_Guide.md#29-saving-and-registering-a-room).

### 5.8.2 `SaveRoomAsWindow` : EditorWindow

The Save As dialog. Floor and room-type dropdowns, a name field and a live path
preview. On confirm it creates any missing folders, unpacks an existing prefab
connection, saves the prefab, and registers it with Addressables under both
labels. If a prefab already exists at the target path it asks first.

### 5.8.3 `BlankRoomGenerator` (static)

`Assets > Create > Dungeon Graph > Blank Room`. Instantiates the Blank_Room
template, falling back to building a Grid, a `Floor` tilemap and an `Exits`
container procedurally if the template is missing.

### 5.8.4 `DungeonTilemapSystemEditor` : Editor

Inspector for `DungeonTilemapSystem`. Adds a **Merge Rooms to Master Tilemap**
button that merges every `RoomTemplate` in the scene — a manual fallback, since
generation normally handles merging.

---

## 5.9 Editor — Addressables and floors

### 5.9.1 `AddressableRoomRegistrar` (static)

All Addressables registration.

| Method | Description |
| --- | --- |
| `GetAllRoomTypes()` | Every `RoomTypeInfo` — the standard types plus registered custom types. |
| `GetOrCreateFloorGroup(floorName)` | Finds or creates the floor's Addressable group, copying schemas from the default group so it inherits the same build settings. |
| `RegisterRoomPrefab(assetPath, floorLabel, typeLabel)` | Marks a prefab Addressable, moves it into the floor group and assigns both labels. |
| `UnregisterRoomPrefab(assetPath)` | Removes the prefab by GUID. |
| `TryGetLabelsFromPath(assetPath, …)` | Infers floor and type labels from a path. Handles both `Floor_1/Start/…` → `Start` and `Floor_1/Basic/Small/…` → `Basic_Small`. |
| `TryRegisterFromPath(assetPath)` | Infers labels, then registers. |

`RoomTypeInfo` is a struct of `displayName`, `folderPath` and `addressableLabel`.

### 5.9.2 `DungeonGraphAddressablesSetup` (static, `[InitializeOnLoad]`)

Runs once per editor session. Ensures the Addressable Asset Settings exist, all
labels are present, and every prefab in the floors root is registered. Gated on
`SessionState` so it does not repeat on each recompile.

`Tools > Dungeon Graph > Setup > Reinitialize Addressables` clears the gate and
re-runs the full scan.

### 5.9.3 `DungeonFloorManager` (static)

| Method | Description |
| --- | --- |
| `GetAllFloors()` | Scans the floors root and returns a `DungeonFloorConfig` per subdirectory. |
| `CreateNewFloor(name)` | Creates the floor folder, every standard room-type subfolder, a subfolder per custom type, a blank room in each, and registers them all. |
| `CreateNodeFolderInAllFloors(typeName)` | Adds a subfolder for a custom type to every existing floor, with a blank room in each. |

### 5.9.4 `DungeonFloorsWatcher` : AssetPostprocessor

Fires `OnFloorsChanged` when a **direct child** of the floors root is created,
deleted, moved or renamed. Changes inside a floor are ignored.
`DungeonGraphView` subscribes so the floor dropdown refreshes live.

### 5.9.5 `CreateFloorWindow` / `CreateCustomNodeTypeWindow` : EditorWindow

Small modal dialogs. `CreateCustomNodeTypeWindow` validates the type name against
the registry as you type and disables Create on a duplicate.

### 5.9.6 `DungeonGraphSetupWizard` (static, `[InitializeOnLoad]`)

In `DungeonGraph.Setup.Editor`, an assembly that deliberately references nothing
and declares no define constraint — so it compiles even when the packages the rest
of Dungeon Graph needs are absent.

Detects missing packages at **compile time**, through `DUNGEONGRAPH_HAS_*` symbols
supplied by its own `versionDefines`, rather than an asynchronous `Client.List`
that can fail or arrive late during a first import. Offers to install whatever is
missing via `Client.AddAndRemove`.

Menu items, all always available:

| Menu | Purpose |
| --- | --- |
| `Tools > Dungeon Graph > Setup > Check Dependencies` | Re-run the check and clear "Don't Ask Again". |
| `Tools > Dungeon Graph > Setup > Open Package Manager` | Jump to Package Manager to install by hand. |
| `Tools > Dungeon Graph > Setup > Add "Dungeon" Tag` | Recreate the legacy tag for older scenes. |

---

[← 4. Runtime API](4_Runtime_API.md) · [Documentation Index](0_Documentation_Index.md) · [Next: 6. Troubleshooting →](6_Troubleshooting.md)
