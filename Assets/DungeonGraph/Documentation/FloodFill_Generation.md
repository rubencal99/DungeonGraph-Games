# Grid (flood-fill) generation

[← 3. Generation Styles](3_Generation_Styles.md) · [Documentation Index](0_Documentation_Index.md)

## Contents

- [How it works, in plain terms](#how-it-works-in-plain-terms)
- [Step-by-step walkthrough](#step-by-step-walkthrough)
- [Parameters](#parameters)
- [Shared settings it reuses](#shared-settings-it-reuses)
- [Corridors](#corridors)
- [Tuning recipes](#tuning-recipes)

---

## How it works, in plain terms

Picture a sheet of graph paper. You write **Start** in one square, then grow
outward one connected room at a time: whichever room you just placed, you look
at the four squares touching it — north, east, south, west — and drop the next
room it connects to into whichever of those is still empty. If all four are
already taken, you spiral outward, ring by ring, until you find an open square.
This is the same room-placement trick classic top-down dungeon crawlers use —
every room sits directly next to the room it branches from, so the map reads
like city blocks instead of a scattered pile.

Graph paper squares are all the same size, but rooms aren't — some are broom
closets, some are ballrooms. So once every room has claimed a square, a second
pass looks at each row and each column of the grid and stretches it to fit
whatever it's holding — like a bookshelf where each **shelf** is only as tall
as its tallest book, and each **column of shelves** is only as wide as its
widest book. A small room sitting in a row with a big one just gets centered
in the extra space, instead of being stretched or clipped.

Because a room is only ever placed directly beside the room it connects to,
the hallway between them is short by construction — like an apartment
building where every unit's front door faces its neighbor's, not some door
three floors away. The one time this can go wrong is when a room has more
connections than it has open sides to place them on; when that happens, the
whole layout is reshuffled and tried again, up to a limited number of
retries, keeping whichever attempt produced the fewest overly-long hallways.

Which direction — north, east, south, or west — gets tried first is rolled
fresh from a random seed every time you generate, not fixed. A fixed
preference would mean almost every room's first open neighbor is checked in
the same direction first, so the whole map would grow the same way every
single time, and Start would always end up pinned near whichever corner is
opposite that drift. Rolling a new order each generation (and centering the
finished layout on the origin afterward) means both the shape and Start's
place within it genuinely change from one generation to the next.

---

## Step-by-step walkthrough

1. **Cull spawn-chance failures** and reconnect their neighbors — shared with
   every other style.
2. **Measure every room's real size** without instantiating it, so later
   steps know how much space each one actually needs.
3. **Roll this run's seed** (or use the one you typed into *Seed*) and use it
   to build a self-contained random number generator, independent of Unity's
   global random state, so a given seed always reproduces the same layout.
4. **Roll a random north/east/south/west preference order** for this attempt
   from that seed — this is what makes the shape (and Start's place within
   it) different every generation instead of the grid always growing the
   same way.
5. **Seed the grid at Start** (grid square `(0, 0)`), then grow outward: each
   newly placed room looks at its still-unplaced connections and drops each
   one into a free square immediately north, east, south, or west of it,
   trying directions in this attempt's rolled order (see *Chaos Factor* for
   how much each individual room deviates from it).
6. **If all four sides are occupied**, search outward in expanding square
   rings (up to *Max Backtrack Attempts* rings) for the nearest free square
   instead of giving up on that room.
7. **Seed any leftover disconnected cluster** (a set of rooms with no path
   back to Start) near the main cluster, so nothing is silently dropped.
8. **Pack the grid into world space**: measure the tallest and widest room on
   each row and column, then lay the whole grid out using those sizes plus a
   fixed gap (*Ideal Distance*) between lines, and re-center the finished
   layout on the world origin. This is the pass that makes Small/Medium/Large
   — and arbitrary custom-node — footprints share one grid without clipping.
9. **Check every graph connection** against *Max Corridor Length*. If any
   connected pair would need a longer hallway than that, discard this attempt
   and roll a new one from step 4 onward (up to *Max Room Regenerations*
   retries, or indefinitely under *Force Mode*), keeping whichever attempt
   had the fewest violations.
10. **Instantiate the winning attempt's rooms** at their computed positions,
    and log the seed that produced them.
11. **Snap to the tile grid and merge** every room's tiles into the master
    tilemap — identical to Organic.
12. **Draw corridors** along the graph's connections, via the same
    Generate Corridors / Generate Dungeon buttons as every other style. Since
    connected rooms are already aligned north/east/south/west of each other,
    the existing straight/angled corridor drawer produces short, clean
    hallways with no new drawing logic required.

---

## Parameters

Shown under **Grid Parameters** when this style is selected.

| Parameter | Default | Meaning |
| --- | --- | --- |
| **Max Corridor Length** | 40 | Longest edge-to-edge distance allowed between two connected rooms. Exceeding it on any connection triggers a re-roll. Raise it for sprawling graphs with high-degree hub rooms; lower it to force a tighter, more compact map. |
| **Max Backtrack Attempts** | 6 | How many rings outward to search for a free grid square when all 4 sides of a room are already occupied, before that room is deferred to a last-resort placement pass. Raise it on dense graphs where rooms frequently box each other in. |
| **Seed** | 0 | `0` = pick a new random layout every time you generate (check the Console after generating to see which seed was actually used). Any other value reproduces that exact layout every time. |
| **Randomize Seed** (button) | — | Rolls a fresh non-zero value into *Seed*, so repeated generations stay locked to that one layout instead of picking a new one each time — useful for tuning other settings against a fixed shape. |

## Shared settings it reuses

These live under **Basic Settings** and already exist for Organic — Grid gives
them the meaning below instead of duplicating its own copies.

| Parameter | Meaning for Grid |
| --- | --- |
| **Ideal Distance** | The gap left between the edges of grid-adjacent rooms — the "shelf spacing" in the bookshelf analogy above. |
| **Chaos Factor** | How much each individual room's direction order deviates from the run's shared random order (see *Seed*). `0` = every room uses the same rolled order; `1` = every room's order is independently reshuffled. Both extremes already vary between generations — this controls *consistency of grain* within one layout, not whether it varies run to run. |
| **Force Mode** | Keep re-rolling the layout until every connection fits inside *Max Corridor Length*, ignoring the *Max Room Regenerations* cap. |
| **Max Room Regenerations** | How many full re-rolls are allowed when a connection's corridor would be too long. (Its Organic meaning — re-rolling on room *overlap* — doesn't apply here, since Grid never overlaps rooms by construction.) |
| **Max Corridor Regenerations** | Passed straight through to the corridor-drawing stage, same as every other style. |

**Not used by this style:** *Allow Room Overlap* (rooms never overlap — each
one owns a grid square), *Real-Time Simulation* / *Simulation Speed*
(placement is an instant discrete process, not an animated physics solve).

---

## Corridors

Corridor drawing is a separate stage from placement — same as Organic — and
uses the exact same code (`DungeonTilemapSystem`'s direct/angled line
drawer). No Grid-specific corridor logic exists, because none is needed:
since placement only ever puts a room directly north/east/south/west
of its neighbor, the straight-line drawer already produces short, axis-aligned
hallways. See [3.5 Corridors](3_Generation_Styles.md#35-corridors) for the
overlap-avoidance/regeneration details, which apply unchanged.

---

## Tuning recipes

**A connection keeps needing a "too long" corridor**
Raise **Max Corridor Length**, or raise **Max Backtrack Attempts** so blocked
rooms search farther before settling for a distant square, or raise
**Max Room Regenerations** (or enable **Force Mode**) to try more full layouts.

**Rooms of very different sizes look oddly padded**
Expected: a Small room sharing a row/column with a Large one is centered in
the extra space rather than stretched. If it bothers you visually, keep
Small/Medium/Large variants of a given room type closer in footprint.

**I want to keep re-generating without losing a layout I liked**
Click **Randomize Seed**, then generate. As long as *Seed* stays non-zero,
every subsequent generation reproduces that exact layout — safe to tweak
*Ideal Distance*, corridor settings, or prefab variants without the shape
changing underneath you. Set *Seed* back to `0` to return to a fresh layout
every time.

**Every run looks the same**
Confirm **Seed** is `0` (a locked non-zero seed always reproduces the same
layout by design). Otherwise, raise **Chaos Factor** for more per-room
variation within a layout, and add more prefab variants per room type.

**A hub room with many connections keeps forcing long corridors**
This is the structural limit of a 4-connected grid — a room can only have 4
immediate neighbors. Reduce that node's connection count, or accept the
occasional longer corridor by raising **Max Corridor Length**.

---

[← 3. Generation Styles](3_Generation_Styles.md) · [Documentation Index](0_Documentation_Index.md)
