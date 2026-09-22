# 7. Architecture

[← Documentation Index](0_Documentation_Index.md)

For readers extending Dungeon Graph or integrating it into a larger system.

## Contents

- [7.1 Assemblies](#71-assemblies)
- [7.2 The generation pipeline](#72-the-generation-pipeline)
- [7.3 Data flow](#73-data-flow)
- [7.4 Extending Dungeon Graph](#74-extending-dungeon-graph)

---

## 7.1 Assemblies

| Assembly | Folder | Platforms | References | Define constraint |
| --- | --- | --- | --- | --- |
| `DungeonGraph.Runtime` | `Scripts/Runtime/` | All | *(none)* | *(none)* |
| `DungeonGraph.Addressables` | `Scripts/Runtime/Addressables/` | All | Runtime, Addressables | `DUNGEONGRAPH_ADDRESSABLES` |
| `DungeonGraph.Editor` | `Scripts/Editor/` | Editor | Runtime, Addressables, Addressables.Editor | `DUNGEONGRAPH_ADDRESSABLES` |
| `DungeonGraph.Setup.Editor` | `Setup/Editor/` | Editor | *(none)* | *(none)* |

Three of these choices are deliberate and worth understanding before you
rearrange anything.

**`DungeonGraph.Runtime` references nothing.** The generation algorithm, the data
model, the tilemap system and every component that lives on a room prefab are in
here. Because it has no package dependencies and no constraint, it compiles in
every project state — so room prefabs keep their components even before the
dependencies are installed.

**The two Addressables-dependent assemblies are gated on a define constraint.**
Each declares a `versionDefines` entry mapping `com.unity.addressables` to
`DUNGEONGRAPH_ADDRESSABLES`, then constrains itself on that same symbol. Unity
resolves version defines from the project manifest *before* compiling, so when
the package is absent the symbol is undefined, the constraint fails, and Unity
**skips the assembly silently**.

That silence is the entire point. Without the constraint, an assembly that
references a missing package raises an assembly-resolution error, and while the
compilation pipeline is in a failed state Unity does not complete a domain reload
— so no `[InitializeOnLoad]` runs and no `[MenuItem]` registers, in *any*
assembly. A package that cannot compile on import therefore cannot ship its own
recovery path. Constraining the dependent assemblies keeps the project healthy so
the rest of the package can still run. This is the same pattern Unity's own
optional-dependency packages use.

**`DungeonGraph.Setup.Editor` references nothing and constrains nothing**, so it
always compiles — including in the exact situation it exists to fix. It detects
missing packages at compile time through its own `versionDefines`
(`DUNGEONGRAPH_HAS_*`) rather than an asynchronous `Client.List`, which can fail
or arrive late during a first import.

**Generation lives in the runtime assembly** so the editor tools and the runtime
component share one implementation rather than two that drift apart.

---

## 7.2 The generation pipeline

Every style presents the same `GenerateRooms`/`GenerateCorridors` pair, so a
caller can swap between them without changing anything else:

```csharp
Task GenerateRooms(graph, roomFactory, boundsProvider, …);
void GenerateCorridors(graph, dungeonParent, maxCorridorRegenerations);
```

Organic additionally exposes `PostSimulationSetup(graph, dungeonParent,
generateCorridors)`, called by `DungeonSimulationController` when animated
placement finishes. Grid has no animated-placement path — `GenerateRooms`
always resolves instantly — so it has no equivalent.

### Dependency injection through delegates

The pipeline never loads an asset itself. Two delegates supply rooms:

| Delegate | Signature | Editor implementation | Runtime implementation |
| --- | --- | --- | --- |
| `AsyncRoomFactory` | `Task<GameObject>(node, parent)` | `OrganicGenerationEditorBridge`, backed by `AssetDatabase` | `DungeonGenerator`, backed by `Addressables.LoadAssetsAsync` |
| `AsyncBoundsProvider` | `Task<Bounds>(node)` | " | " |

This is what lets one algorithm serve both contexts. It is also the extension
point: supply your own factory to load rooms from anywhere — a resource pack, a
network payload, a procedurally built prefab.

### The Organic pipeline

1. **`ProcessSpawnChances`** — cull nodes below their threshold, reconnecting
   neighbours when the culled node had exactly two connections.
2. **Measure bounds** for every node through `AsyncBoundsProvider`, before
   anything is instantiated.
3. **Floyd–Warshall** all-pairs shortest paths, used to scale repulsion by graph
   distance.
4. **Instantiate** rooms through `AsyncRoomFactory` at random positions inside a
   circle.
5. **Simulate** spring and repulsion forces, instantly or frame by frame.
6. **Check overlap**; re-roll up to `maxRoomRegenerations` if any is found and
   overlap is not allowed.
7. **Snap** rooms to the tile grid.
8. **Merge** every room tilemap into the master tilemap.
9. **Draw corridors** along the graph's connections.

### The Grid pipeline

`FloodFillGeneration` (displayed to users as "Grid" — see
[3.3](3_Generation_Styles.md#33-grid-flood-fill)) is the worked example of a
second style plugging into the same `GenerateRooms`/`GenerateCorridors` pair.

Steps 1, 2, 8 and 9 are shared with Organic. In between:

3. **Build adjacency** and seed a `System.Random` from the `seed` parameter
   (or the system clock, if `seed` is 0) — self-contained, so it never
   disturbs `UnityEngine.Random`'s global state.
4. **Place on a logical grid**: grow outward from Start, each room claiming a
   free integer cell next to the room it branches from. Direction order is a
   fresh random permutation per attempt, drawn from that seed. Any node not
   reachable from Start (a separate connected component) is seeded near the
   existing cluster.
5. **Pack into world space**: size each row/column to its largest occupant,
   lay the grid out with a fixed gap between lines, then re-center the whole
   result on the origin.
6. **Check corridor lengths**: if any connection would need a corridor longer
   than `maxCorridorLength`, discard the attempt and re-roll from step 4, up
   to `maxRoomRegenerations` times (64 under Force Mode), keeping whichever
   attempt had the fewest over-length connections.
7. **Instantiate** the winning attempt's rooms at their computed positions.

Overlap handling differs in kind, not degree. Organic detects room overlap
*after the fact* and re-rolls the whole layout. Grid never overlaps rooms at
all — placement is cell-occupancy-based, not continuous-position-based — so
its regeneration loop exists purely to bound corridor length, not overlap.

---

## 7.3 Data flow

**Graph authoring** writes to the asset through Unity's serialization:

```
DungeonGraphView → DungeonGraphEditorNode → DungeonGraphAsset
```

**Room authoring** writes prefabs to disk and registers them:

```
RoomAuthoringTool / SaveRoomAsWindow → PrefabUtility → AddressableRoomRegistrar
```

**Editor generation** reads the graph and floor folder:

```
DungeonGraphView
  → OrganicGeneration | FloodFillGeneration    (via OrganicGenerationEditorBridge)
    → DungeonSimulationUtility  (instant)
      | DungeonSimulationController (animated)
    → DungeonTilemapSystem      (snap, merge, corridors)
```

**Runtime generation** reads the graph and floor label:

```
DungeonGenerator.GenerateDungeon()
  → OrganicGeneration                          (via Addressables delegates)
    → the same tilemap pipeline
```

### Folder convention

```
Assets/DungeonGraph/Dungeon_Floors/{Floor}/{RoomType}/MyRoom.prefab
```

`AddressableRoomRegistrar` infers both labels straight from this path, so the
folder layout *is* the configuration. A `BasicNode` sized Medium resolves to
`{Floor}/Basic/Medium/`; a custom type named `Trap` resolves to `{Floor}/Trap/`.

---

## 7.4 Extending Dungeon Graph

### Adding a node type in code

```csharp
using DungeonGraph;

[NodeInfo("Armory", "Rooms/Armory", hasFlowInput: true, hasFlowOutput: true)]
public class ArmoryNode : DungeonGraphNode
{
    [ExposedProperty]
    public int weaponCount = 3;
}
```

It appears in the search menu automatically — the search provider scans loaded
assemblies for `[NodeInfo]`. Create `{Floor}/Armory/` and put room prefabs in it.

For most cases the in-editor **+ Create New Node Type** flow
([2.5](2_Authoring_Guide.md#25-custom-node-types)) is simpler; write a class when
the node needs its own fields.

### Loading rooms from somewhere else

Supply your own factory:

```csharp
OrganicGeneration.AsyncRoomFactory factory = async (node, parent) =>
{
    GameObject prefab = await MyLoader.LoadRoomAsync(node.GetType().Name);
    return Object.Instantiate(prefab, Vector3.zero, Quaternion.identity, parent);
};

OrganicGeneration.AsyncBoundsProvider bounds = async (node) =>
{
    return await MyLoader.GetBoundsAsync(node.GetType().Name);
};

await OrganicGeneration.GenerateRooms(graph, factory, bounds, /* … */);
```

Neither the pipeline nor the tilemap system cares where the prefab came from.

### Reacting to generation

Every room carries a `RoomNodeReference`, so post-processing is a scan:

```csharp
foreach (var room in dungeonParent.GetComponentsInChildren<RoomNodeReference>())
{
    switch (room.nodeTypeName)
    {
        case "Boss":   SpawnBoss(room.transform);   break;
        case "Reward": SpawnTreasure(room.transform); break;
    }
}
```

At runtime, `DungeonGenerationResult` does this for you — see
[4.4](4_Runtime_API.md#44-reading-the-result).

### Writing a third generation style

Implement the three entry points in
[7.2](#72-the-generation-pipeline), add a value to `GenerationStyle`, and add a
branch in `DungeonGraphView.RebuildStyleParameters()` for its parameter block.
`ProcessSpawnChances`, `BuildAdjacency`, `SpawnRoom` and the corridor stage are
`internal` on `OrganicGeneration` and can be reused directly.

---

[← 6. Troubleshooting](6_Troubleshooting.md) · [Documentation Index](0_Documentation_Index.md)
