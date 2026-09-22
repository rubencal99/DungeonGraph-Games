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
| **Floor Label** | `string` | `Floor_1` | Addressables label identifying the room set. |
| **Corridor Tile** | `TileBase` | none | Corridors are skipped if this is null. |
| **Corridor Width** | `int` | 2 | Corridor width in tiles. |
| **Corridor Type** | `CorridorType` | Direct | `Direct`, `Angled` or `Both`. |
| **Max Corridor Regenerations** | `int` | 3 | Re-route attempts per corridor. |
| **Area Placement Factor** | `float` | 11.0 | See [3.2](3_Generation_Styles.md#32-organic-force-directed). |
| **Repulsion Factor** | `float` | 30.0 | " |
| **Stiffness Factor** | `float` | 20.0 | " |
| **Ideal Distance** | `float` | 20 | " |
| **Simulation Iterations** | `int` | 400 | " |
| **Chaos Factor** | `float` | 0 | " |
| **Bend Factor** | `float` | 0 | " |
| **Repulsion Scaling Mode** | `RepulsionScalingMode` | Default | " |
| **Allow Room Overlap** | `bool` | false | Skip the overlap check. |
| **Max Room Regenerations** | `int` | 4 | Full layout re-rolls on overlap. |
| **Generate On Start** | `bool` | true | Untick to trigger generation yourself. |

The runtime `DungeonGenerator` component always uses the **Organic** style.
**Grid**, the other style available in the Editor's Dungeon Tools panel, is
not yet wired up to the runtime generator.

> The Inspector values here are **separate** from the Dungeon Tools panel
> settings, which are stored in EditorPrefs. Tuning one does not change the other.

### Scene requirements

The scene must contain a master tilemap. Drag
`Assets/DungeonGraph/Prefabs/Master_Tilemap.prefab` in, or add a
**Dungeon Master Tilemap** component to a Tilemap of your own.

Without one, rooms are still instantiated but their tiles are not merged and no
corridors are drawn. The Console explains this.

---

## 4.2 Addressables setup

Runtime generation loads rooms through Addressables. Every room prefab must carry
**two** labels:

1. A **floor label** matching `Floor Label` on the component — e.g. `Floor_1`.
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
