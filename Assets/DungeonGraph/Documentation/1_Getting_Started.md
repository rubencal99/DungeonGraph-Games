# 1. Getting Started

[← Documentation Index](0_Documentation_Index.md)

## Contents

- [1.1 Requirements](#11-requirements)
- [1.2 Required Unity packages](#12-required-unity-packages)
- [1.3 Installing Dungeon Graph](#13-installing-dungeon-graph)
- [1.4 Verifying the installation](#14-verifying-the-installation)
- [1.5 Tutorial: your first dungeon in the Editor](#15-tutorial-your-first-dungeon-in-the-editor)
- [1.6 Tutorial: generating a dungeon at runtime](#16-tutorial-generating-a-dungeon-at-runtime)
- [1.7 What gets created in your scene](#17-what-gets-created-in-your-scene)

---

## 1.1 Requirements

| Item | Requirement |
| --- | --- |
| Unity version | 6000.0.67f1 or newer |
| Render pipeline | Universal Render Pipeline or Built-in. No pipeline-specific code or shaders. |
| Project type | 2D. Rooms are built from Unity Tilemaps. |
| Scripting backend | Mono or IL2CPP |
| Platforms | Any platform Unity's Addressables package supports |

Dungeon Graph generates 2D tile-based dungeons. It does not generate 3D geometry,
meshes, navmeshes, enemies or gameplay logic.

---

## 1.2 Required Unity packages

**Dungeon Graph will not compile until these four packages are installed.** They
are not shipped inside the package; they are installed from the Unity Package
Manager. See [Third-Party Notices](../Third-Party%20Notices.txt) for licensing.

| Package | Package Manager ID | Minimum | Why it is needed |
| --- | --- | --- | --- |
| Addressables | `com.unity.addressables` | 1.19.0 | Loads room prefabs by floor and room-type label at runtime. |
| 2D Tilemap | `com.unity.2d.tilemap` | 1.0.0 | Rooms and corridors are painted onto Tilemaps. |
| 2D Tilemap Extras | `com.unity.2d.tilemap.extras` | 4.0.0 | Provides Rule Tile, used by the bundled floor tiles. |
| 2D Sprite | `com.unity.2d.sprite` | 1.0.0 | Sprite Editor, for slicing your own tile sheets. |

> **Note.** Unity's `.unitypackage` format cannot install package dependencies
> automatically. Dungeon Graph ships a setup wizard that detects the missing
> packages on first import and offers to install them for you — see §1.3.

---

## 1.3 Installing Dungeon Graph

Follow these steps in order.

1. **Create or open a 2D project** in Unity 6000.0.67f1 or newer.

2. **Import the package.**
   From the Asset Store window or `Assets > Import Package > Custom Package…`,
   import Dungeon Graph. Leave every item ticked.

3. **Install the required packages.**
   On the first import, a dialog titled *"Dungeon Graph — Missing Packages"*
   lists whatever is missing. Click **Install Now**. Unity installs the packages
   and recompiles.

   If you dismissed the dialog, or you want to run the check again:
   `Tools > Dungeon Graph > Setup > Check Dependencies`

   To install them by hand instead, open `Window > Package Manager`, switch the
   dropdown to **Unity Registry**, and install each package listed in §1.2, or use
   `Tools > Dungeon Graph > Setup > Open Package Manager`.

   > **Before this step, the only Dungeon Graph menu you will see is
   > `Tools > Dungeon Graph > Setup`.** That is by design, not a failed import.
   > The editor tools and the runtime generator are gated on Addressables, so
   > Unity skips compiling them rather than reporting reference errors. They
   > appear as soon as the packages are installed.

4. **Allow Addressables to be set up.**
   After the recompile, a dialog titled *"Dungeon Graph — Set Up Addressables"*
   appears. Dungeon Graph loads rooms through Addressables, whose configuration
   lives in `Assets/AddressableAssetsData` — a folder at your project root, outside
   the Dungeon Graph folder. Nothing outside the package is created without asking,
   so click **Set Up Addressables**.

   The Console then reports:

   ```
   [Dungeon Graph] Addressables setup complete.
   ```

   That means the room-type and floor labels were added and the sample rooms
   registered. If you choose **Not Now**, the graph editor still works, but rooms
   will not load at runtime until you run
   `Tools > Dungeon Graph > Setup > Reinitialize Addressables`.

   If your project already uses Addressables, no dialog appears — the rooms are
   registered into the configuration you already have.

5. **Confirm the Console is clean.** There should be no errors. If there are, go
   to [6. Troubleshooting](6_Troubleshooting.md) §6.1.

---

## 1.4 Verifying the installation

Check each of these:

- [ ] `Tools > Dungeon Graph` exists in the menu bar, with `Rooms` and `Setup` submenus.
- [ ] `Assets > Create > Dungeon Graph > New Graph` exists.
- [ ] `Window > Asset Management > Addressables > Groups` opens and shows groups
      named **Floor_1** and **Floor_2**.
- [ ] Double-clicking any asset in `Assets/DungeonGraph/Dungeon_Graphs/` opens the
      **Dungeon Graph** editor window rather than the Inspector.
- [ ] The Console has no errors.

---

## 1.5 Tutorial: your first dungeon in the Editor

This walkthrough uses the sample content. It takes about ten minutes.

### Step 1 — Create a scene

1. `File > New Scene`, choose **Basic 2D (URP)** or **Basic (Built-in)**, and save it
   as `Assets/DungeonGraph/Scenes/Demo.unity`.
2. Confirm the scene has a **Main Camera**.
3. Select the Main Camera and set:
   - **Projection** → `Orthographic`
   - **Size** → `40`
   - **Position** → `(0, 0, -10)`

   A dungeon of ten rooms is roughly 80 units across, so an orthographic size of
   40 frames it. Adjust after your first generation.

### Step 2 — Add the master tilemap

Every generated room is baked into one shared Tilemap called the *master tilemap*.

1. In the Project window, open `Assets/DungeonGraph/Prefabs/`.
2. Drag **Master_Tilemap.prefab** into the Hierarchy.
3. Leave its Transform at `(0, 0, 0)`.

   The prefab already carries a **Dungeon Master Tilemap** component. That
   component is how the generator finds it — you do not need to set any tag.

### Step 3 — Open a dungeon graph

1. In the Project window, open `Assets/DungeonGraph/Dungeon_Graphs/Starter_Floors/Floor_1/`.
2. Double-click **01 - Default.asset**.

   The **Dungeon Graph** window opens, showing a node graph: a Start node, some
   Basic and Hub rooms, a Reward room, a Boss room and an End node.

### Step 4 — Choose the floor

In the **Dungeon Tools** panel (top-left of the graph window):

1. Set **Floor** to `Floor_1`.

   The floor decides which folder of room prefabs is used.
   `Floor_1` maps to `Assets/DungeonGraph/Dungeon_Floors/Floor_1/`.

### Step 5 — Set the corridor tile

Still in **Dungeon Tools**, open **Basic Settings**:

1. Set **Corridor Tile** to `BasicFloorRuleTile`
   (found in `Assets/DungeonGraph/Tiles/`).
2. Leave **Corridor Width** at `2` and **Corridor Type** at `Direct`.

   Without a corridor tile, rooms are placed but nothing connects them.

### Step 6 — Generate

Click **Generate Dungeon**.

You should see, in the Scene view:

- A GameObject named **Generated_Dungeon** appear in the Hierarchy.
- Room prefabs instantiated beneath it and pushed apart into a layout.
- All room tiles merged into **Master_Tilemap**.
- Corridors drawn between connected rooms.

Press `F` with `Generated_Dungeon` selected to frame it in the Scene view.

### Step 7 — Iterate

- **Generate Dungeon** again → a different layout from the same graph.
- **Clear** → removes `Generated_Dungeon` and empties the master tilemap.
- **Generate Rooms** → placement only, no corridors.
- **Generate Corridors** → corridors only, on the rooms already placed.

A second layout style, **Grid**, is available from the tab next to Organic in
the Generation Style Settings section — it grows the dungeon outward from
Start one grid cell at a time instead of simulating physics. See
[3. Generation Styles](3_Generation_Styles.md).

### Step 8 — Save the scene

`File > Save`. The generated dungeon is a normal scene hierarchy and is saved with
the scene.

---

## 1.6 Tutorial: generating a dungeon at runtime

Editor generation bakes a dungeon you keep. Runtime generation builds a fresh one
every time the player starts a level.

### Step 1 — Prepare the scene

Start from the scene you built in §1.5, or a new one with a camera and a
**Master_Tilemap** instance.

If a `Generated_Dungeon` object is already in the scene from editor generation,
click **Clear** in the Dungeon Tools panel first. Runtime generation creates its
own.

### Step 2 — Add the generator

1. Create an empty GameObject and name it `Dungeon Generator`.
2. Add the **Dungeon Generator** component
   (`Add Component > Dungeon Graph > Dungeon Generator`, or search "DungeonGenerator").

### Step 3 — Configure it

In the Inspector:

| Field | Value |
| --- | --- |
| **Dungeon Graph** | `Starter_Floors/Floor_1/01 - Default` |
| **Generate On Start** | ✔ ticked |

That is all. The floor, corridor tile and every other setting come from the
graph itself — whatever you chose in the Dungeon Tools panel in §1.5 is saved on
the graph asset and used at runtime too.

> **Important.** The graph's **Floor** doubles as the Addressables label the
> rooms are loaded by, so it must match the label on your room prefabs. The
> bundled rooms use `Floor_1` and `Floor_2`.

### Step 4 — Play

Press Play. The Console logs the load, and a dungeon appears.

If you see `No prefabs found for labels 'Floor_1' + 'Start'`, the rooms are not
registered with Addressables. Run
`Tools > Dungeon Graph > Setup > Reinitialize Addressables` and try again.

### Step 5 — Spawn something in the dungeon

To place a player at the Start room, add the sample
**Runtime Dungeon Generator** component to the same GameObject and assign a
**Player** prefab. It waits for generation, then instantiates the prefab at the
center of the Start room.

For your own code, see [4. Runtime API](4_Runtime_API.md).

---

## 1.7 What gets created in your scene

After a generation run, the Hierarchy contains:

```
Master_Tilemap                 (you added this — holds every baked tile and the wall collider)
Generated_Dungeon              (created by the generator)
├── DungeonTilemapSystem       (component: grid snapping, merging, corridors)
├── DungeonConnectionVisualizer(component: Scene-view gizmos, editor only)
├── Room_F1_Start_1(Clone)     (component: RoomNodeReference, RoomTemplate)
├── Room_F1_Basic_Medium_3(Clone)
├── Room_F1_Hub_2(Clone)
└── …
```

- **Generated_Dungeon** is deleted and rebuilt on every run. Do not parent
  anything you want to keep underneath it.
- Each room keeps a **RoomNodeReference** recording which graph node produced it
  and its type name. This is how you find rooms afterwards — see
  [4. Runtime API](4_Runtime_API.md) §4.4.
- The room's own Tilemap renderers are **disabled** after merging, because their
  tiles now live in the master tilemap. The GameObjects stay so you can attach
  spawners, triggers and other gameplay objects to them.
- **Master_Tilemap** gains a **Walls (Generated)** child: solid wall colliders
  covering the outer edge tiles of every room and corridor (the ones drawn as
  walls) and the space beyond. Interior floor is open.
  They are rebuilt on every generation and never saved with the scene.
  See [4.1 Collision](4_Runtime_API.md#collision).

---

[← Documentation Index](0_Documentation_Index.md) · [Next: 2. Authoring Guide →](2_Authoring_Guide.md)
