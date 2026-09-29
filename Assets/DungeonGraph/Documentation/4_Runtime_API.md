# 4. Runtime API

[← Documentation Index](0_Documentation_Index.md)

## Contents

- [4.1 The DungeonGenerator component](#41-the-dungeongenerator-component)
- [4.2 Addressables setup](#42-addressables-setup)
- [4.3 Triggering generation from code](#43-triggering-generation-from-code)
- [4.4 Reading the result](#44-reading-the-result)
- [4.5 Complete example](#45-complete-example)
- [4.6 Memory and cleanup](#46-memory-and-cleanup)

Everything in this section is in the `DungeonGraph` namespace.

```csharp
using DungeonGraph;
```

---

## 4.1 The DungeonGenerator component

`DungeonGenerator` is the runtime entry point. Add it to a GameObject in the
scene that should build a dungeon.

| Inspector field | Type | Default | Meaning |
| --- | --- | --- | --- |
| **Dungeon Graph** | `DungeonGraphAsset` | none | The graph to build. Required. |
| **Generate On Start** | `bool` | true | Untick to trigger generation yourself. |

Everything else — floor, generation style (Organic or Grid), corridor tile,
width and type, and every tuning value — comes from the **graph's own settings**.
The Dungeon Tools panel saves them onto the graph asset whenever you change
them, so a runtime dungeon is built exactly as **Generate Dungeon** builds it in
the Editor. To tune a runtime dungeon, open its graph and adjust the panel.

The graph's **Floor** is used as the Addressables floor label, so it must match
the label on your room prefabs (the bundled rooms use `Floor_1` and `Floor_2`).
If a graph has never had a floor selected, generation stops with a Console error
telling you to pick one.

*Real-Time Simulation* is an Editor preview and is ignored at runtime; the
layout is always solved instantly.

### Scene requirements

The scene must contain a master tilemap. Drag
`Assets/DungeonGraph/Prefabs/Master_Tilemap.prefab` in, or add a
**Dungeon Master Tilemap** component to a Tilemap of your own.

Without one, rooms are still instantiated but their tiles are not merged and no
corridors are drawn. The Console explains this.

### Collision

The **Dungeon Master Tilemap** component builds the dungeon's walls for you. No
setup is needed, and it works the same on `Master_Tilemap.prefab` or on a Tilemap
of your own.

Two kinds of cell become solid wall:

- **Edge tiles** — every painted tile with an empty cell directly north, south,
  east or west. These are the tiles the bundled floor rule tiles draw as walls,
  so what looks like a wall is one.
- **The surrounding band** — every empty cell within **Wall Thickness** cells
  (default 2, diagonals included) of a painted tile.

The walls live on a child object named **Walls (Generated)**, which carries a
static Rigidbody 2D, a Tilemap Collider 2D and a Composite Collider 2D in
`Polygons` mode. Interior tiles have no collider, so objects on them move freely
and are stopped by the walls around every room and corridor.

**Corridor width.** A corridor's outer rows are edge tiles, so only its interior
is walkable. Corridors are stamped `2 × ⌊Width / 2⌋ + 1` tiles wide, leaving
`2 × ⌊Width / 2⌋ − 1` walkable: **Width 2 or 3 leaves a 1-tile lane, 4 or 5
leaves 3.** Width 1 corridors have no walkable lane. Make sure the lane is wider
than your player's collider.

Edges are found by emptiness, not by tile type. Where two different rule tiles
meet, for example a room painted with one tile and a corridor with another, each
may draw a wall sprite along the seam, but the seam stays walkable.

The walls are rebuilt after rooms are merged, after corridors are drawn, and on
**Clear**. The collider is ready the moment `OnGenerationComplete` fires, so
objects spawned there collide on their first physics step. The walls object is
never saved to the scene or prefab; it is rebuilt from the floor whenever the
scene loads. If you edit the master tilemap by hand, call
`DungeonMasterTilemap.RebuildWalls()` afterwards.

**Why solid walls:** an edge collider has no thickness. A body that crosses it
within one physics step is resolved out the *far* side, so small or fast bodies
pass straight through. A wall one or more cells thick always pushes a body back
toward the floor.

**For guaranteed collision,** set **Collision Detection** to `Continuous` on
every moving Rigidbody 2D that must never leave the dungeon — players,
projectiles, physics props. Continuous bodies cannot pass through static
colliders at any speed. Discrete bodies are stopped by the walls unless they
move more than about half the wall thickness in a single physics step; raise
**Wall Thickness** if you rely on Discrete.

Rooms carry no colliders of their own, and neither does the master tilemap.

---

## 4.2 Addressables setup

Runtime generation loads rooms through Addressables. Every room prefab must carry
**two** labels:

1. A **floor label** matching the graph's selected **Floor** — e.g. `Floor_1`.
2. A **room-type label** — one of `Start`, `End`, `Hub`, `Boss`, `Reward`,
   `Basic_Small`, `Basic_Medium`, `Basic_Large`, or the name of a custom type.

Rooms saved through `Tools > Dungeon Graph > Rooms > Save As…` are labelled
automatically.

**To label rooms manually,** open
`Window > Asset Management > Addressables > Groups`, select the prefabs, and
assign both labels in the Labels column.

**To repair a broken setup,** run
`Tools > Dungeon Graph > Setup > Reinitialize Addressables`. This re-scans the
floors folder, recreates every label, and re-registers every prefab it finds.

**Before building a player,** build the Addressables content:
`Window > Asset Management > Addressables > Groups > Build > New Build > Default Build Script`.
A player build with stale or missing Addressables content will log
"No prefabs found for labels …" at runtime.

---

## 4.3 Triggering generation from code

Three ways, depending on what you need back.

### Fire and forget

```csharp
generator.Generate();
```

Starts generation and returns immediately.

### Await the result

```csharp
private async void Start()
{
    DungeonGenerationResult result = await generator.GenerateDungeon();
    if (result == null)
    {
        Debug.LogError("Dungeon generation failed.");
        return;
    }
    // use result
}
```

`GenerateDungeon()` returns `Task<DungeonGenerationResult>`, or `null` on failure.

### Subscribe to events

```csharp
private void OnEnable()
{
    generator.OnGenerationComplete += HandleComplete;
    generator.OnGenerationFailed   += HandleFailed;
}

private void OnDisable()
{
    generator.OnGenerationComplete -= HandleComplete;
    generator.OnGenerationFailed   -= HandleFailed;
}

private void HandleComplete(DungeonGenerationResult result) { /* … */ }
private void HandleFailed() { /* … */ }
```

Best when several systems need to react to the same dungeon. Untick
**Generate On Start** and call `Generate()` once your listeners are attached, or
subscribe from `Awake`.

The most recent successful result is also available as `generator.LastResult`.

---

## 4.4 Reading the result

`DungeonGenerationResult` maps graph nodes back to the GameObjects they produced.

| Member | Type | Meaning |
| --- | --- | --- |
| `dungeonParent` | `GameObject` | The `Generated_Dungeon` root. |
| `startRoom` | `RoomNodeReference` | Convenience handle on the Start room. |
| `GetRoom(string typeName)` | `List<RoomResult>` | All rooms of a node type, case-insensitive. |
| `GetStartRoom()` | `List<RoomResult>` | Shorthand for `GetRoom("Start")`. |
| `GetEndRoom()` | `List<RoomResult>` | Shorthand for `GetRoom("End")`. |
| `GetBossRoom()` | `List<RoomResult>` | Shorthand for `GetRoom("Boss")`. |

`RoomResult` is a small struct:

| Member | Type | Meaning |
| --- | --- | --- |
| `gameObject` | `GameObject` | The spawned room. |
| `roomCenter` | `Vector3` | World-space center of the room's tile bounds. |

`RoomResult` converts implicitly to `GameObject`, so it can be passed straight to
`Instantiate`, `Destroy` and similar.

Pass the **node type name** to `GetRoom`. For built-ins that is the class name
minus "Node" — `Start`, `End`, `Hub`, `Boss`, `Reward`. For `BasicNode` it is
`Basic`. For custom types it is the name you gave the type, so
`GetRoom("Shop")` returns your shops.

---

## 4.5 Complete example

Generates a dungeon, spawns the player at the Start room, drops a treasure chest
in every Reward room, and places the level exit at the End room.

```csharp
using System.Collections.Generic;
using DungeonGraph;
using UnityEngine;

[RequireComponent(typeof(DungeonGenerator))]
public class LevelBuilder : MonoBehaviour
{
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject chestPrefab;
    [SerializeField] private GameObject exitPortalPrefab;

    private DungeonGenerator m_generator;

    private void Awake()
    {
        m_generator = GetComponent<DungeonGenerator>();
        m_generator.OnGenerationComplete += OnDungeonReady;
        m_generator.OnGenerationFailed   += OnDungeonFailed;
    }

    private void OnDestroy()
    {
        if (m_generator == null) return;
        m_generator.OnGenerationComplete -= OnDungeonReady;
        m_generator.OnGenerationFailed   -= OnDungeonFailed;
    }

    private void OnDungeonReady(DungeonGenerationResult result)
    {
        SpawnOne(playerPrefab,     result.GetRoom("Start"));
        SpawnAll(chestPrefab,      result.GetRoom("Reward"));
        SpawnOne(exitPortalPrefab, result.GetRoom("End"));

        Debug.Log($"Dungeon ready with {result.GetRoom("Basic").Count} basic rooms.");
    }

    private void OnDungeonFailed()
    {
        Debug.LogError("Dungeon generation failed — see the errors above.");
    }

    private static void SpawnOne(GameObject prefab, List<RoomResult> rooms)
    {
        if (prefab == null || rooms.Count == 0) return;
        Instantiate(prefab, rooms[0].roomCenter, Quaternion.identity);
    }

    private static void SpawnAll(GameObject prefab, List<RoomResult> rooms)
    {
        if (prefab == null) return;
        foreach (RoomResult room in rooms)
            Instantiate(prefab, room.roomCenter, Quaternion.identity, room.gameObject.transform);
    }
}
```

Add this alongside a `DungeonGenerator` and untick **Generate On Start** only if
you want to control the timing; with it ticked, `Awake` runs before `Start`, so
the subscription above is already in place.

### Regenerating

```csharp
public async void NextLevel()
{
    await m_generator.GenerateDungeon();
}
```

The generator deletes the previous `Generated_Dungeon` before building a new one.
Anything you parented under it is destroyed with it; anything you spawned
elsewhere is your responsibility to clean up.

---

## 4.6 Memory and cleanup

`DungeonGenerator` holds Addressables handles for every prefab list it loads and
releases them in `OnDestroy`. Prefabs are cached per room-type label, so
regenerating in the same scene does not reload them.

To release them earlier, destroy the generator GameObject or unload the scene.

Room GameObjects keep their `Tilemap` components after merging, but the renderers
are disabled — the tiles now live in the master tilemap. Keep the room objects if
you want to attach spawners or triggers to them; destroying them does not remove
their tiles from the master tilemap.

---

[← 3. Generation Styles](3_Generation_Styles.md) · [Documentation Index](0_Documentation_Index.md) · [Next: 5. Script Reference →](5_Script_Reference.md)
