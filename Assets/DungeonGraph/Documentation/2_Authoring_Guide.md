# 2. Authoring Guide

[← Documentation Index](0_Documentation_Index.md)

## Contents

- [2.1 The graph editor window](#21-the-graph-editor-window)
- [2.2 Creating and connecting nodes](#22-creating-and-connecting-nodes)
- [2.3 Built-in node types](#23-built-in-node-types)
- [2.4 Spawn chance](#24-spawn-chance)
- [2.5 Custom node types](#25-custom-node-types)
- [2.6 Floors](#26-floors)
- [2.7 Authoring a room prefab](#27-authoring-a-room-prefab)
- [2.8 Exits](#28-exits)
- [2.9 Saving and registering a room](#29-saving-and-registering-a-room)
- [2.10 Using your own tile set](#210-using-your-own-tile-set)

---

## 2.1 The graph editor window

Create a graph with `Assets > Create > Dungeon Graph > New Graph`, then
double-click it to open the editor.

The window has three parts:

| Part | Purpose |
| --- | --- |
| **Canvas** | The node graph. Pan with middle-mouse or Alt+drag; zoom with the scroll wheel. |
| **Dungeon Tools** panel | Generation buttons, floor selection, and every generation parameter. |
| **Node Settings** panel | Appears when exactly one node is selected. Holds that node's spawn chance. |

The graph asset is saved when the window loses focus, regains focus, or closes.
An asterisk in the window title means there are unsaved changes.

---

## 2.2 Creating and connecting nodes

**To create a node:** right-click on empty canvas. A search window opens listing
every node type. Type to filter, then press Enter or click the entry. The node
appears where you right-clicked.

**To connect two nodes:** drag from the circular port in the middle of one node
onto the middle of another. The whole circle is a drag target.

**To delete:** select and press `Delete`.
**To copy:** `Ctrl+C` / `Ctrl+V`.

Connections are **undirected**. Dungeon Graph draws them with an arrowhead
because Unity's GraphView requires an input and an output side, but the
generation pipeline treats every connection as a two-way link. `A → B` and
`B → A` produce identical dungeons.

### Rules the generator expects

1. **Exactly one Start node.** If a graph has several, only the first is used.
2. **The graph should be connected.** Disconnected islands are laid out, but
   nothing joins them, so you get two separate dungeons in one scene.
3. **Give rooms an exit for each connection where you can.** Corridors attach to
   the nearest exit, so a room with four connections looks best with four doorways.
   Rooms with no exits still work — corridors fall back to the room center.

---

## 2.3 Built-in node types

Each node type loads its room prefabs from a matching subfolder of the selected
floor.

| Node | Menu path | Prefab folder | Notes |
| --- | --- | --- | --- |
| **Start** | `Rooms/Start` | `{Floor}/Start/` | Dungeon entrance. One per graph. |
| **End** | `Rooms/End` | `{Floor}/End/` | Dungeon exit. Several allowed. |
| **Hub** | `Rooms/Hub` | `{Floor}/Hub/` | Intended for high-connectivity junctions. |
| **Basic** | `Rooms/Basic` | `{Floor}/Basic/{Size}/` | General traversal room. Has a **Size** dropdown: Small, Medium, Large. |
| **Boss** | `Rooms/Boss` | `{Floor}/Boss/` | Boss encounter. |
| **Reward** | `Rooms/Reward` | `{Floor}/Reward/` | Treasure or reward room. |
| **Debug Log** | `Debug/Debug Log Console` | — | Utility node. Logs a message during graph traversal. Ignored by the generation pipeline. |

When several prefabs exist in a folder, the generator picks among them and tracks
which it has used, so a run prefers unused variants before repeating one.

---

## 2.4 Spawn chance

Select a single node to reveal the **Node Settings** panel and its **Spawn
Chance** slider (0–100, default 100).

At generation time, any node below 100 is rolled against a random value. If the
roll fails, the node is removed from that run.

**Reconnection rule.** When a culled node had **exactly two** connections, its two
neighbours are joined directly so the graph stays contiguous. A node with three
or more connections cannot be removed cleanly, so its spawn chance is ignored and
the Node Settings panel shows a warning.

Use spawn chance for optional side rooms — a Reward room hanging off a corridor,
a shop that only sometimes appears.

---

## 2.5 Custom node types

Custom node types let you add room categories of your own — `Shop`, `Armory`,
`Puzzle`, `Secret`.

**To create one:**

1. Right-click on the canvas to open the node search window.
2. Choose **+ Create New Node Type** at the bottom of the list.
3. Enter a name and pick a colour. The name is validated against existing types.
4. Click **Create**.

This does three things:

- Adds the type to `DefaultCustomNodeTypeRegistry.asset`.
- Creates a `{TypeName}/` folder inside **every** existing floor.
- Adds an Addressables label with the same name.

New custom nodes then appear in the search window under **Custom/**.

The package ships with two sample custom types, `Shop` and `Upgrade`, to show the
mechanism.

> **Naming.** The type name becomes a folder name and an Addressables label, so
> avoid slashes, leading/trailing spaces and characters illegal in file names.

---

## 2.6 Floors

A **floor** is one folder of room prefabs — one visual and structural theme.

```
Assets/DungeonGraph/Dungeon_Floors/
├── Floor_1/
│   ├── Start/        Basic/Small/     Hub/       Reward/
│   ├── End/          Basic/Medium/    Boss/      Shop/  Upgrade/
└── Floor_2/
    └── …
```

**To create a floor:** click **Create New Floor** in the Dungeon Tools panel and
enter a name. Dungeon Graph creates the folder, every standard room-type
subfolder, a subfolder for each custom type, a blank placeholder room in each,
and registers all of them with Addressables under a label matching the floor name.

**To use a floor:** pick it from the **Floor** dropdown before generating. At
runtime, set the **Floor Label** field on the DungeonGenerator component instead.

Graphs and floors are independent. Any graph can be generated against any floor —
the graph describes structure, the floor supplies the rooms. The sample graphs
under `Dungeon_Graphs/Starter_Floors/Floor_3` … `Floor_5` are layout templates;
generate them against `Floor_1` or `Floor_2`, or create matching floors of your own.

---

## 2.7 Authoring a room prefab

Rooms are ordinary Unity Tilemap objects with one extra component. You can build
them in any scene; `Assets/DungeonGraph/Scenes/RoomStudio.unity` is provided as a
dedicated workspace.

### Step 1 — Create the room object

`Assets > Create > Dungeon Graph > Blank Room`

This instantiates `Prefabs/Blank_Room.prefab` into the scene, which contains:

```
Room
├── Grid
│   └── Floor          (Tilemap + TilemapRenderer)
└── Exits              (empty container)
```

### Step 2 — Paint the room

1. Open `Window > 2D > Tile Palette`.
2. Select the **Floor** tilemap as the active target.
3. Paint the room's floor area.

Guidelines:

- **Keep rooms rectangular** where you can. Doorway direction is inferred from
  each exit's position relative to the room's bounding box, and that inference is
  exact for rectangles.
- **Whole cells only.** Do not offset the Grid or Tilemap by a fraction of a cell.
  Rooms are snapped to whole grid cells at generation time; a half-cell offset
  will shift the room off its doorways.
- **Multiple tilemaps are fine.** Add more Tilemap children under the Grid for
  walls, decoration or a background layer. All of them are merged.

### Step 3 — Add exits

See [2.8](#28-exits) below.

### Step 4 — Save

See [2.9](#29-saving-and-registering-a-room) below.

---

## 2.8 Exits

An **exit** marks a doorway — the point where a corridor may attach.

**To add exits:**

1. Select the **Exits** child of your room.
2. Create an empty GameObject as its child for each doorway.
3. Position each one at the doorway location, on the room's edge.

The name of each exit object does not matter. The container **must** be named
`Exits` (matching is case-insensitive) and must be a direct child of the room root.

```
Room
├── Grid
│   └── Floor
└── Exits
    ├── North
    ├── East
    ├── South
    └── West
```

### Guidelines

- **One exit per wall, four total,** covers almost every graph. A node can have
  at most as many connections as its room has exits.
- **Put exits on cell boundaries**, at the middle of the doorway opening.
- **Exits are positions, not directions.** Which wall an exit belongs to is
  worked out at generation time from its position relative to the room's bounds.
  Place exits near the edge they represent, not near the room center.
- **Rooms without exits still generate.** Corridors fall back to the room center,
  which makes them appear to start inside the room. Adding exits is what makes
  corridors attach cleanly at doorways.

---

## 2.9 Saving and registering a room

With the room's **root** GameObject selected in the Hierarchy:

**`Tools > Dungeon Graph > Rooms > Save As…`**

A dialog opens:

| Field | Meaning |
| --- | --- |
| **Floor** | Which floor folder to save into. |
| **Room Type** | Which room-type subfolder. Includes your custom types. |
| **Name** | Prefab file name. |
| **Path preview** | The full asset path that will be written. |

Click **Save**. Dungeon Graph then:

1. **Compresses** every child tilemap's bounds, trimming empty margin cells.
2. **Deletes** any tilemap that is empty after compression.
3. **Locks** colour and transform flags on every occupied cell, so per-cell
   overrides survive the merge into the master tilemap.
4. **Adds or refreshes** the `RoomTemplate` component and recomputes its
   world-space bounds, cell size and tilemap list.
5. **Collects exits** from the `Exits` child.
6. **Writes the prefab** to `Dungeon_Floors/{Floor}/{Type}/{Name}.prefab`.
7. **Registers it with Addressables**, adding the floor label (e.g. `Floor_1`) and
   the room-type label (e.g. `Basic_Small`).

Both labels are required for the room to be found at runtime.

**`Tools > Dungeon Graph > Rooms > Save`** re-bakes an existing room prefab in
place, keeping its path and labels. Use this after editing a room you have already
saved. If the selection is not yet a prefab, it opens the Save As dialog instead.

> **Always save rooms through this menu.** Dragging a room into the Project window
> creates a prefab with stale bounds, no exits array, and no Addressables labels.

---

## 2.10 Using your own tile set

The bundled tiles (`Assets/DungeonGraph/Tiles/Tile_1`…`Tile_4`) are plain solid
colours. They exist so the samples render; replace them with your own art.

1. **Import your tile sheet** into the project as a PNG.
2. Set **Texture Type** to `Sprite (2D and UI)`.
3. For a sheet of several tiles, set **Sprite Mode** to `Multiple` and slice it in
   the Sprite Editor.
4. Set **Pixels Per Unit** to the pixel size of one tile — 16 for 16×16 art, 32 for
   32×32. **This must be exact**, or rooms will not align to the grid.
5. Set **Filter Mode** to `Point (no filter)` and **Compression** to `None` for
   crisp pixel art.
6. Create tiles from the sprites: select them and use
   `Assets > Create > 2D > Tiles > Rule Tile` (or drag them into a Tile Palette).
7. Paint your rooms with the new tiles, then **Save** each room (§2.9) so its
   bounds are recomputed.
8. Set **Corridor Tile** in the Dungeon Tools panel to your new tile.

You can delete `Assets/DungeonGraph/Tiles/` entirely once your rooms no longer
reference it.

---

[← 1. Getting Started](1_Getting_Started.md) · [Documentation Index](0_Documentation_Index.md) · [Next: 3. Generation Styles →](3_Generation_Styles.md)
