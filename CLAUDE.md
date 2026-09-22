# DungeonGraph Games — Project Context

## Instructions

Talk plainly and keep sentences short. Use everyday analogies instead of technical jargon unless asked for detail. When something can be built by hand in the Unity Editor — a prefab, a scene, a UI layout, an animation, a ScriptableObject asset — prefer that over generating it through a script, and give the user the exact step-by-step editor process (menu paths, component names, field values) instead of writing a script that builds it for them. Reach for a script only for behavior, not for content a human should author and tweak visually.

Favor data-driven design: ScriptableObjects for anything tunable (weapon stats, enemy stats, character/map definitions), interfaces for swappable behavior (e.g. `IInteractable`, `IDamageable`), and generic systems that don't hardcode specific cases. Keep code simple — no speculative abstractions, no half-built features, no scaffolding for a problem that doesn't exist yet. Three similar lines beats a premature abstraction.

Performance is a first-class concern. Pool anything that spawns repeatedly (projectiles, enemies). Avoid per-frame allocations. Don't reach for exotic optimizations (DOTS/ECS, job system) until profiling actually shows a bottleneck — start simple, measure, then optimize.

`Assets/DungeonGraph` is a third-party Asset Store plugin (procedural 2D dungeon generation via a node-graph editor + Addressables + Tilemaps). Treat it as read-only vendored code — consume its public runtime API (see its own `Documentation/` folder) rather than editing its internals. All original game code and content lives under `Assets/Game`.

## Summary

DungeonGraph Games is a multiplayer game hub, built in Unity 6.6, meant to host several mini dungeon crawlers. Players land on a main menu, then create or join a lobby with a join code (up to 4 players), picking a character skin, starting weapon, and map before dropping in. Levels are procedurally generated using the DungeonGraph plugin, which turns an authored node graph into a tile-based dungeon. Core gameplay is a top-down twin-stick shooter: WASD movement, mouse aim, a rotating weapon inventory, and straight-line bullet-hell-style projectiles. The project is at its very beginning — almost everything below is planned, not yet built.

## Recently finished milestones

- **DungeonGraph plugin installed and verified.** The Asset Store dungeon generator is imported, its required packages (Addressables, 2D Tilemap, Tilemap Extras, 2D Sprite) are installed, and Addressables is configured. A sample dungeon generates correctly from a node graph, confirming the toolchain works end to end.
- **Base project scaffolding.** A Unity 6000.0.67f1 URP 2D project exists with the Input System package wired up, plus a minimal stub `PlayerController` (Rigidbody2D + WASD via an Input Action) as a starting point — not a finished controller.

## Ongoing milestones

1. **Character controller, spawning, and weapon system.** WASD movement with mouse aim and Cinemachine camera follow, a rotating weapon inventory driven by ScriptableObject weapon data, and pooled straight-line projectiles.
2. **SceneManager, main menu, lobby, and basic multiplayer.** A join-code lobby for up to 4 players built on Netcode for GameObjects + Unity Relay/Lobby, with character skin, starting weapon, and (tentative) map-vote selection.
3. **Inventory and interaction system.** A generic `Activate`-style interaction (pickup, lever, teleporter, anything) triggered by pressing E, with a distance-based prompt UI over interactable world objects.
4. **Basic enemy and spawner.** Idle/Chase/Attack enemies driven by a simple state machine, spawned and pooled by an EnemySpawner, with performance as the top priority.

## Details

### 1. Character controller, spawning, weapons

- **Movement:** extend the existing `Assets/Game/Scripts/PlayerController.cs` (Rigidbody2D + Input System) rather than replacing it wholesale. Gravity scale stays 0 (top-down).
- **Camera:** needs the **Cinemachine** package (not yet installed — add via Package Manager). Use a Cinemachine Camera following the player. Mouse aim should pull the camera slightly toward the cursor's world position — this is a soft offset/lookahead blended between the player position and the mouse world point, not a hard cut.
- **Mouse aim:** raycast/screen-to-world the mouse position each frame. Flip the character sprite (facing left/right) based on which side of the player the mouse is on, and rotate a separate "gun pivot" child object toward the aim point so the sprite flip and gun rotation are independent concerns.
- **Spawning:** use the DungeonGraph plugin's `RuntimeDungeonGenerator` sample flow (see its `Documentation/4_Runtime_API.md`) — it waits for generation, then can instantiate a prefab at the Start room's center. Follow that pattern for spawning each player's character.
- **Weapons (data-driven focus for this milestone):** a `WeaponDefinition` ScriptableObject holding recoil/bloom, spread, fire rate, ammo size, and damage. Weapons live in a rotating inventory (a fixed set of slots the player cycles through) — pick up, drop, swap, and rotate between them. Each weapon fires a projectile that travels in a straight line.
- **Projectiles:** pooled (use Unity's built-in `ObjectPool<T>` or a small generic pooler) rather than instantiated/destroyed per shot. Projectile prefabs should be authored by hand in the editor, referenced by the `WeaponDefinition`.

### 2. SceneManager, main menu, lobby, multiplayer

- **Networking stack (decided):** Netcode for GameObjects + Unity Relay + Unity Lobby service. None of these packages are installed yet — add `com.unity.netcode.gameobjects`, Relay, Lobby, and Authentication via Package Manager / Unity Dashboard services. The already-present `com.unity.multiplayer.center` package is just Unity's setup/recommendation tool, not the netcode itself.
- **Lobby flow:** host creates a lobby (gets a join code), other players join by entering it, up to 4 total. Each player picks a character skin, starting weapon, and votes on a map (tentative — confirm before building the voting UI itself).
- **Scene flow:** a small generic SceneManager/loader that moves players from Main Menu → Lobby → Gameplay scene, ideally driven by data (scene references/Addressables) rather than hardcoded scene name strings.
- **UI:** build menus and lobby screens by hand in the editor (Canvas + uGUI, since that's the UI package already in the project) rather than generating UI layouts from code.
- **Testing:** for local multiplayer testing before a dedicated server exists, look at Unity's Multiplayer Play Mode package (lets you run multiple simulated clients in one Editor).

### 3. Inventory and interaction system

- Reuses the rotating weapon inventory from Milestone 1 as its foundation.
- **Interaction contract:** a generic `IInteractable` interface with an `Activate()` method. Weapons on the floor, levers, teleporters, etc. all implement it — pressing E near one calls `Activate()`, and each object decides what that means. This keeps the input/prompt code identical no matter what's being interacted with.
- **Prompt UI:** a symbol + instruction popup that appears based on distance to the player (e.g. a trigger radius or distance check each frame), shown per-object above/near the interactable — likely a small world-space canvas or a pooled screen-space UI element that follows the target.

### 4. Basic enemy + EnemySpawner

- **Decision:** start with traditional GameObjects + object pooling + a simple state machine (Idle → Chase → Attack), not DOTS/ECS. Revisit ECS only if profiling later shows GameObject-based enemies are an actual bottleneck — no need to build for that possibility now.
- **EnemyDefinition:** a ScriptableObject for stats (health, damage, speed, detection/attack range) so new enemy types don't require new code.
- **EnemySpawner:** spawns pooled enemy instances rather than instantiating/destroying them; keep an eye on active enemy counts and avoid expensive per-enemy work (e.g. prefer cheap distance checks over physics raycasts where possible) since this milestone is explicitly performance-first.
