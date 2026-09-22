# 6. Troubleshooting

[← Documentation Index](0_Documentation_Index.md)

## Contents

- [6.1 Compile and import problems](#61-compile-and-import-problems)
- [6.2 Nothing is generated](#62-nothing-is-generated)
- [6.3 Rooms overlap](#63-rooms-overlap)
- [6.4 Corridors are missing or wrong](#64-corridors-are-missing-or-wrong)
- [6.5 Rooms render blank or magenta](#65-rooms-render-blank-or-magenta)
- [6.6 Runtime-only problems](#66-runtime-only-problems)
- [6.7 Diagnostic checklist](#67-diagnostic-checklist)

---

## 6.1 Compile and import problems

### After importing, the Dungeon Graph window and menus are missing

The required packages are not installed yet. This is the expected first-import
state and is **not** an error — Dungeon Graph's editor and runtime assemblies
declare a define constraint on Addressables, so Unity skips compiling them
entirely rather than filling your Console with reference errors.

**Fix:** `Tools > Dungeon Graph > Setup > Check Dependencies` → **Install Now**.

That submenu is always present, because the setup assembly references nothing and
carries no constraints, so it compiles in every project state.

Once the packages finish installing, Unity recompiles and the full toolset
appears under `Tools > Dungeon Graph`.

See [1.2 Required Unity packages](1_Getting_Started.md#12-required-unity-packages).

### Even `Tools > Dungeon Graph` is missing

Something stopped Unity compiling altogether. Check the Console for the first
error — later ones are usually cascades from it — and confirm
`Assets/DungeonGraph/Setup/Editor/` exists in your project. If the import was
partial, reimport the package.

### `DungeonGenerator.prefab` shows a missing script

`DungeonGenerator` lives in an assembly gated on Addressables, so before the
packages are installed the component does not exist. Installing them restores it;
no data is lost.

### Rule Tile assets show as "Missing (Mono Script)"

The 2D Tilemap Extras package is not installed. Install
`com.unity.2d.tilemap.extras` (4.0.0 or newer), or run **Check Dependencies**.

### The setup dialog never appeared

It runs once per session and can be dismissed permanently.

**Fix:** `Tools > Dungeon Graph > Setup > Check Dependencies` re-enables and
re-runs it. `Tools > Dungeon Graph > Setup > Open Package Manager` jumps straight
to Package Manager if you would rather install by hand.

### Errors persist after installing the packages

**Fix:** `Assets > Reimport All`, or close Unity, delete the project's `Library`
folder, and reopen. Unity rebuilds the assembly graph from scratch.

---

## 6.2 Nothing is generated

### Nothing at all happens when I click Generate

| Check | How |
| --- | --- |
| Does the graph have a Start node? | Every graph needs exactly one. |
| Is a floor selected? | The **Floor** dropdown must not be empty. |
| Does that floor contain room prefabs? | Look in `Dungeon_Floors/{Floor}/Start/` etc. |
| Are there errors in the Console? | Fix the first one. |

### I clicked "Not Now" on the Addressables setup prompt

Nothing was created, by design — Dungeon Graph does not write outside its own
folder without asking, and Addressables stores its configuration in
`Assets/AddressableAssetsData` at your project root.

**Fix:** `Tools > Dungeon Graph > Setup > Reinitialize Addressables` and accept
the prompt. The graph editor works either way; only runtime room loading needs it.

### "No prefabs found for labels 'Floor_1' + 'Start'"

Room prefabs are not registered with Addressables, or carry the wrong labels.

**Fix:** `Tools > Dungeon Graph > Setup > Reinitialize Addressables`.

If that does not help, open
`Window > Asset Management > Addressables > Groups` and confirm each room prefab
has **both** a floor label and a room-type label. See
[4.2](4_Runtime_API.md#42-addressables-setup).

### Rooms appear but stay stacked at the origin

The simulation did not run. Check that **Simulation Iterations** is above zero, or
turn on **Force Mode**.

### Some nodes produce no room

Their prefab folder is empty, or the prefabs are unregistered. The Console names
the node type. Nodes that fail to instantiate are pruned from the run along with
their connections, so the rest of the dungeon still generates.

---

## 6.3 Rooms overlap

An overlap warning means the search needed more effort — not that the graph is
impossible. Work down this list:

1. Raise **Max Room Regenerations** (Basic Settings → Advanced).
2. Turn on **Force Mode**.
3. Raise **Repulsion Factor** and **Ideal Distance**.
4. Raise **Area Placement Factor** so rooms start further apart.

Very large rooms in a densely connected graph are the hardest case. If a layout
will not resolve, the quickest fix is usually a higher **Ideal Distance**.

---

## 6.4 Corridors are missing or wrong

### No corridors at all

**Corridor Tile is not set.** This is the most common cause. Set it in the Dungeon
Tools panel (Basic Settings → Corridor Settings), or on the `DungeonGenerator`
component at runtime. Corridors are silently skipped when it is null.

### Corridors run through rooms

By design — corridors never overwrite existing tiles, so a corridor crossing a
room leaves the room intact. To reduce crossings, raise **Max Corridor
Regenerations** or switch **Corridor Type** to `Angled`.

### Corridors connect to odd points on a room

The corridor attaches to the exit nearest the other room. If a room has no exits,
it falls back to the room center, which produces corridors that appear to start
inside the room.

**Fix:** add exits to the room prefab and re-save it
([2.8](2_Authoring_Guide.md#28-exits)).

### Corridors are too thin or too thick

**Corridor Width** is in tiles. It should be at least as wide as your doorway
openings.

---

## 6.5 Rooms render blank or magenta

### Tiles are invisible in a room prefab

The tile's sprite is missing, or the Tilemap's cached sprites are stale.

**Fix:** select the room's Tilemap and use the Tile Palette to repaint one cell,
which refreshes the tilemap. Or reassign the tile asset on the Rule Tile.

### The master tilemap is empty after generating

Rooms were placed but not merged. Check the Console for:

```
Master tilemap not found, so room tilemaps were not merged.
```

**Fix:** drag `Assets/DungeonGraph/Prefabs/Master_Tilemap.prefab` into the scene,
or add a **Dungeon Master Tilemap** component to the Tilemap you want rooms baked
into.

### Sprites are blurry or misaligned

Set **Pixels Per Unit** on your tile sprites to the exact pixel size of one tile,
and **Filter Mode** to `Point (no filter)`. See
[2.10](2_Authoring_Guide.md#210-using-your-own-tile-set).

---

## 6.6 Runtime-only problems

### Works in the Editor, produces nothing in a build

Addressables content was not built.

**Fix:** `Window > Asset Management > Addressables > Groups > Build > New Build > Default Build Script`,
then rebuild the player. Repeat after adding or moving room prefabs.

### `UnityException: Tag: Dungeon is not defined`

A scene or script from an older version is still looking up the master tilemap by
tag.

**Fix:** use `Master_Tilemap.prefab`, which carries the **Dungeon Master Tilemap**
component and needs no tag. If you must keep the tag-based workflow, run
`Tools > Dungeon Graph > Setup > Add "Dungeon" Tag`.

### The dungeon generates twice, or on top of an old one

The scene contains a dungeon baked in the Editor *and* a `DungeonGenerator` with
**Generate On Start** ticked.

**Fix:** click **Clear** in the Dungeon Tools panel before entering Play Mode, or
untick **Generate On Start**.

### Generation blocks the first frame

Addressables loading is asynchronous but room instantiation is not. On large
graphs, untick **Generate On Start** and call `Generate()` behind a loading
screen.

---

## 6.7 Diagnostic checklist

Work top to bottom. Most problems are resolved by the first four.

- [ ] Console has no errors.
- [ ] All four required packages are installed
      (`Tools > Dungeon Graph > Setup > Check Dependencies`).
- [ ] The scene has a master tilemap
      (`Master_Tilemap.prefab`, or a Tilemap with **Dungeon Master Tilemap**).
- [ ] **Corridor Tile** is assigned.
- [ ] The graph has exactly one Start node.
- [ ] A floor is selected, and that floor's folders contain room prefabs.
- [ ] Every room prefab has both an Addressables floor label and a room-type label.
- [ ] Every room prefab has a `RoomTemplate` with non-zero bounds
      (re-save it with `Tools > Dungeon Graph > Rooms > Save` if not).
- [ ] Rooms have at least as many exits as their busiest node has connections.
- [ ] For builds: Addressables content has been built since the last room change.

If a problem survives this list, note your Unity version, the package versions,
the exact Console output and the steps that reproduce it, and get in touch through
the Asset Store product page.

---

[← 5. Script Reference](5_Script_Reference.md) · [Documentation Index](0_Documentation_Index.md) · [Next: 7. Architecture →](7_Architecture.md)
