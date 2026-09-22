# 3. Generation Styles

[← Documentation Index](0_Documentation_Index.md)

## Contents

- [3.1 Choosing a style](#31-choosing-a-style)
- [3.2 Organic (force-directed)](#32-organic-force-directed)
- [3.3 Grid (flood-fill)](#33-grid-flood-fill)
- [3.4 Shared settings](#34-shared-settings)
- [3.5 Corridors](#35-corridors)
- [3.6 Tuning recipes](#36-tuning-recipes)

---

## 3.1 Choosing a style

The **Generation Style Settings** section in the Dungeon Tools panel has a row
of tabs — one per style — instead of a dropdown. Click a tab to switch styles;
hover a tab to see a summary of how that style works. The tab you're on is
dark; the others sit at a lighter gray. The parameter section directly below
the tabs shares that same dark shade, so the selected tab and its unique
settings read as one connected block — Basic and Advanced Settings further
down stay at the panel's normal color regardless of which tab is selected.

**Organic** produces loose, cave-like layouts by simulating attraction along
graph connections and repulsion between all rooms. **Grid** produces compact,
grid-aligned layouts with short corridors by growing outward from the Start
room one grid cell at a time. Both are fully available in this release.

---

## 3.2 Organic (force-directed)

Rooms are scattered at random inside a circle, then a physics simulation pulls
connected rooms together and pushes all rooms apart until the layout settles.

### Parameters

| Parameter | Default | Meaning |
| --- | --- | --- |
| **Real-Time Simulation** | off | Animate the solve frame by frame in Play Mode instead of settling instantly. Organic-only — Grid always resolves instantly. |
| **Simulation Speed** | 10 | Physics iterations per second while animating. |
| **Area Placement Factor** | 2.0 | Radius of the circle rooms are scattered into, as a multiple of the total room area. Raise it for more initial spread on large graphs. |
| **Repulsion Factor** | 1.0 | Strength of the push between every pair of rooms. Raise it if rooms bunch up. |
| **Stiffness Factor** | 1.0 | Strength of the spring pulling connected rooms together. Raise it to shorten corridors; lower it for a looser layout. |
| **Simulation Iterations** | 100 | How many physics steps to run. More iterations means a more settled layout at the cost of time. Disabled when Force Mode is on. |

### Advanced

| Parameter | Default | Meaning |
| --- | --- | --- |
| **Repulsion Scaling** | Default | How repulsion scales with graph distance. `Default` = × distance, `Flat` = constant, `Inverse` = ÷ (1 + distance·0.5), `SqrtDamped` = × √distance. `Flat` gives more uniform spacing; `Default` spreads distant branches further apart. |
| **Bend Factor** | 0.0 | Breaks up straight chains. Applies a sideways nudge at any room with exactly two connections whose neighbours are close to a straight line. Raise it for winding, less grid-like corridors. |
| **Allow Room Overlap** | off | Skip the overlap check and accept whatever the layout produced. Organic-only — Grid never overlaps rooms by construction. |

### How it works

1. Cull nodes that fail their spawn chance, reconnecting neighbours.
2. Measure every room's bounds without instantiating it.
3. Compute all-pairs shortest paths through the graph (used to scale repulsion).
4. Instantiate rooms at random positions inside the placement circle.
5. Run the force simulation.
6. If rooms overlap and *Allow Room Overlap* is off, discard and re-roll, up to
   *Max Room Regenerations*.
7. Snap every room to the tile grid.
8. Merge all room tilemaps into the master tilemap.
9. Draw corridors along the graph connections.

The physics model, per iteration:

```
spring   = 0.01 · Stiffness · (distance − (radiusA + radiusB + IdealDistance))
repulsion = scaled(50 · Repulsion, graphDistance) / distance²
velocity *= 0.9                                        (damping)
```

---

## 3.3 Grid (flood-fill)

Starting from the Start room, each connected room claims a free cell on an
integer grid next to the room it branches from — the same room-placement
approach classic top-down dungeon crawlers use. Row/column spacing adapts to
the largest room on that line, so Small/Medium/Large (and custom) room sizes
can share one grid without clipping. See
[Flood Fill (Grid) generation](FloodFill_Generation.md) for the full
plain-language walkthrough — this section only summarizes it.

### Parameters

| Parameter | Default | Meaning |
| --- | --- | --- |
| **Max Corridor Length** | 40 | Longest edge-to-edge distance allowed between two connected rooms. Exceeding it on any connection triggers a re-roll. |
| **Max Backtrack Attempts** | 6 | Grid rings searched outward for a free cell when all 4 sides of a room are already occupied. |
| **Seed** | 0 | `0` = a new random layout every generation (the Console logs which seed was used). Any other value reproduces that exact layout every time. |
| **Randomize Seed** (button) | — | Rolls a fresh non-zero value into *Seed*, locking the layout so you can tweak other settings without it changing. |

### How it works

1. Cull nodes that fail their spawn chance, reconnecting neighbours.
2. Measure every room's bounds without instantiating it.
3. Seed a random number generator from *Seed* (or a fresh one if it's `0`).
4. Grow outward from Start on an integer grid: each room claims a free cell
   next to the room it branches from, using a direction order rolled fresh
   from that seed every generation.
5. Pack the grid into world space, sizing each row/column to its largest
   occupant, then re-center the whole layout on the origin.
6. If any connection needs a corridor longer than *Max Corridor Length*,
   discard the attempt and re-roll, up to *Max Room Regenerations* (or
   indefinitely under *Force Mode*), keeping the best attempt found.
7. Snap every room to the tile grid.
8. Merge all room tilemaps into the master tilemap.
9. Draw corridors along the graph connections (see [3.5](#35-corridors)).

---

## 3.4 Shared settings

These live under **Basic Settings** and apply to both styles, though a few
take on a different meaning per style (noted below). Settings that only make
sense for one style — Real-Time Simulation, Simulation Speed, and Allow Room
Overlap for Organic; Max Corridor Length, Max Backtrack Attempts, and Seed for
Grid — live in that style's own tab instead, so switching styles doesn't
surface controls that don't apply.

### Room settings

| Parameter | Default | Meaning |
| --- | --- | --- |
| **Ideal Distance** | 20 | Organic: spring rest length between connected rooms. Grid: gap left between the edges of grid-adjacent rooms. Raise it to spread the dungeon out, lower it to tighten corridors. |

### Corridor settings

| Parameter | Default | Meaning |
| --- | --- | --- |
| **Corridor Tile** | none | The tile corridors are painted with. **Corridors are not drawn until this is set.** |
| **Corridor Width** | 2 | Corridor width in tiles. |
| **Corridor Type** | Direct | `Direct` = straight line; `Angled` = L-shaped; `Both` = alternate per corridor. |

### Advanced

| Parameter | Default | Meaning |
| --- | --- | --- |
| **Force Mode** | off | Keep iterating until the layout stabilises rather than stopping at a fixed count. Organic caps at 2096 iterations; Grid at 64 layout attempts. Disables *Simulation Iterations* under Organic. |
| **Chaos Factor** | 0.0 | Organic: random jitter added to room velocities each iteration. Grid: how much each individual room's direction order deviates from the layout's shared random order. |
| **Max Room Regenerations** | 3 | Organic: full layout re-rolls allowed after a failed overlap check. Grid: re-rolls allowed when a connection's corridor would be too long. |
| **Max Corridor Regenerations** | 3 | Re-route attempts per corridor before accepting the result. |

---

## 3.5 Corridors

Corridors are painted onto the master tilemap after rooms are placed, along each
connection in the graph.

For each connection, the generator picks the exit on each room nearest the other
room, then draws between them:

- **Direct** — a Bresenham line, `Corridor Width` tiles thick.
- **Angled** — an L: one horizontal run and one vertical run.
- **Both** — alternates between the two.

Corridors never overwrite existing tiles, so a corridor never *replaces* a tile
that's already part of a room.

### Overlap avoidance

For each connection, every corridor shape (Direct, Angled horizontal-first,
Angled vertical-first) is tried at the current exit-point pairing before the
generator moves on to a different, less-ideal pairing. A bad shape choice is
never mistaken for "this doorway pair can't be routed" when a different shape
at that same doorway would have worked.

| Step | Strategy |
| --- | --- |
| Per exit pairing | Try Direct, Angled-horizontal, and Angled-vertical (order depends on *Corridor Type*: your chosen type is tried first, but every shape is always attempted). |
| Across pairings | Move to the next-closest exit pairing once all 3 shapes have been tried at the current one, up to *Max Corridor Regenerations* pairings. |
| If nothing avoided every room | Draw whichever (pairing, shape) combination overlapped the fewest cells, and log a warning. |

The overlap check only ever considers *other* rooms — the two rooms a corridor
is actually connecting are excluded from it, since their own exit points
routinely sit some distance inside their bounds (behind a wall's thickness, or
simply not flush with the edge), and checking against them flagged the
corridor's own, entirely normal path out of its source/destination room far
more often than it caught anything wrong. This means a corridor is guaranteed
not to cut through a room it *isn't* connected to, but is not separately
guarded against cutting through its own endpoint room's interior beyond the
doorway — keep exit placement sensible (facing roughly toward where connected
rooms are expected to sit) if that matters for your room shapes.

**Generate Corridors** re-runs only this stage on the rooms already in the scene,
which is useful for tuning corridor settings without re-rolling the layout.

---

## 3.6 Tuning recipes

**Rooms are too far apart / corridors too long**
Organic: raise **Stiffness Factor**, or lower **Ideal Distance**. Grid: lower
**Ideal Distance**, or raise **Max Corridor Length** if the warning is about a
specific over-budget connection rather than the general spacing.

**Rooms overlap** *(Organic)*
An overlap warning means "spend more search effort", not "impossible graph".
Try, in order:
1. Raise **Max Room Regenerations**.
2. Turn on **Force Mode**.
3. Raise **Repulsion Factor**.
4. Raise **Ideal Distance**.

**A connection keeps needing a too-long corridor** *(Grid)*
Raise **Max Corridor Length**, **Max Backtrack Attempts**, or **Max Room
Regenerations** (or enable **Force Mode**).

**Layout is too linear / snake-like** *(Organic)*
Raise **Bend Factor**.

**Layout is too spread out** *(Organic)*
Lower **Area Placement Factor** and **Repulsion Factor**. Set **Repulsion
Scaling** to `Flat` for more even spacing.

**Every run looks the same**
Organic: raise **Chaos Factor**. Grid: confirm **Seed** is `0` — a locked
non-zero seed reproduces the same layout by design; raise **Chaos Factor** for
more per-room variation within a layout otherwise. Both: add more prefab
variants per room type, and add nodes with a spawn chance below 100.

**Generation is slow** *(Organic)*
Lower **Simulation Iterations**. Turn off **Force Mode**. Turn off **Real-Time
Simulation**.

---

[← 2. Authoring Guide](2_Authoring_Guide.md) · [Documentation Index](0_Documentation_Index.md) · [Next: 4. Runtime API →](4_Runtime_API.md)
