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
- **Character controller, spawning, and weapons.** `PlayerController` does WASD with acceleration/deceleration. `PlayerAim` raycasts the mouse onto an AimPlane layer each frame and publishes the aim point, angle, and side; `CharacterFlip` and `WeaponAimRotator` consume that to mirror the sprite and rotate the gun as independent concerns. Cinemachine is installed and follows the player through `AimCameraTarget`, which pulls the camera toward the cursor. `PlayerSpawner` drops the player at the DungeonGraph Start room once generation finishes. `WeaponDefinition` ScriptableObjects drive fire rate, ammo, bloom/recoil, spread, multi-shot, and projectile behavior; `Weapon` fires them and `ProjectilePoolManager` pools every `Projectile` by prefab.

## Ongoing milestones

1. **Inventory, interaction, chests, and loot.** A 3-slot rotating weapon inventory shown bottom-right and cycled with the scroll wheel, a generic `Activate`-style interaction triggered by pressing E, a distance-based prompt UI over interactable world objects, and chests that roll weighted loot tables onto the floor. **Currently in progress.**
2. **Basic enemy and spawner.** Idle/Chase/Attack enemies driven by a simple state machine, spawned and pooled by an EnemySpawner, with performance as the top priority.
3. **SceneManager, main menu, lobby, and basic multiplayer.** A join-code lobby for up to 4 players built on Netcode for GameObjects + Unity Relay/Lobby, with character skin, starting weapon, and (tentative) map-vote selection. **Deliberately deferred** — everything before it is built single-player first, then made networked.

## Details

### 1. Inventory, interaction, chests, and loot

Built single-player. The reference implementation this was adapted from was a 3D Netcode project; the client/server splits, `NetworkBehaviour`s, and registry-id indirection were all stripped, and the 3D physics (arcing drops, holster anchors, billboarded prompts) became flat 2D equivalents.

- **Interaction contract:** an abstract `Interactable` MonoBehaviour with `CanInteract(GameObject)` and `Activate(GameObject)`. A base class rather than the `IInteractable` interface originally planned, because range, prompt, and registration are shared state every interactable needs — an interface would have meant duplicating all three on every implementer. `WeaponPickup` and `Chest` are the two subclasses today; a lever or a teleporter is two more overrides each.
- **Scanning:** `PlayerInteractor` sweeps the static `Interactable.All` list ten times a second with a squared-distance compare — no physics query, no per-frame cost, and no trigger collider needed on every prefab. Range lives on the interactable, so a chest can be reachable from further away than a dropped pistol without special casing.
- **Prompt UI:** `InteractionPromptView` on the authored `PromptAnchor` prefab — a world-space canvas with a glyph Image and a TMP label, lit only for the single nearest target so walking through a pile of loot never lights the whole pile. `InteractionPromptDefinition` assets hold the label and key sprite, so "Pick Up" is authored once and shared by every pickup.
- **Inventory:** `Inventory` holds a fixed array of `WeaponDefinition` slots (count from an `InventoryDefinition` asset, 3 today) plus the held index, and raises one `Changed` event. Pickup fills the first empty slot; when every slot is full, the **held** weapon is the one traded away and it drops on the floor, so the player always sees what they gave up. There is no separate drop key.
- **Cycling:** scroll wheel up/down, through `InventoryInput` and a `CycleSlot` action. Cycling skips empty slots and wraps.
- **Held weapon:** `WeaponHolder` keeps one instantiated `Weapon` per filled slot under the `Weapons` container and toggles `SetActive`, rather than instantiating on every scroll — so each weapon also keeps its own ammo count while stowed. Stowed weapons are hidden, not holstered: three guns strapped to a small top-down sprite reads as noise. Because a prefab cannot reference a scene object, `WeaponHolder` calls `Weapon.Initialize(PlayerAim)` at spawn.
- **HUD:** `InventoryBarView` binds to `Inventory.Local` and mirrors it into three authored `InventorySlotView` boxes (the `SlowViewDefault` prefab) anchored bottom-right. It is strictly a reader — it never changes a slot, which is why the highlight cannot lie about what is held.
- **Loot tables:** `LootTableDefinition` is a weighted pool whose rows are either weapons or **other loot tables**, so tiering is authoring rather than code — a gold chest is the same prefab pointing at a table whose rows are the rare and legendary pools. Weights are relative, not percentages, so adding a row never means editing the rows around it. Note this project is Unity 6000.0.67f1, which has **no native `Dictionary<,>` serialization**; the rows are serializable structs in a `List<>` whose fields are deliberately named `key`/`value` so the originally authored `.asset` YAML still loads.
- **Chests:** `Chest` is an `Interactable` and nothing more — two overrides. It rolls its table on open, fans the results out, and swaps a closed sprite for an open one. An opened chest returns false from `CanInteract`, which is all it takes for the prompt to stop appearing.
- **One spawn path:** every weapon that enters the world goes through `WeaponPickupSpawner.Spawn` — inventory trades and chest drops alike. That single seam is what makes adding a pool later a change to one file. Pickups are deliberately *not* pooled today: drops happen at human speed, unlike projectiles.

### 2. Basic enemy + EnemySpawner

- **Decision:** start with traditional GameObjects + object pooling + a simple state machine (Idle → Chase → Attack), not DOTS/ECS. Revisit ECS only if profiling later shows GameObject-based enemies are an actual bottleneck — no need to build for that possibility now.
- **EnemyDefinition:** a ScriptableObject for stats (health, damage, speed, detection/attack range) so new enemy types don't require new code.
- **EnemySpawner:** spawns pooled enemy instances rather than instantiating/destroying them; keep an eye on active enemy counts and avoid expensive per-enemy work (e.g. prefer cheap distance checks over physics raycasts where possible) since this milestone is explicitly performance-first.
- **Loot on death:** reuse `LootTableDefinition` and `WeaponPickupSpawner` from milestone 1 rather than growing a second drop path.

### 3. SceneManager, main menu, lobby, multiplayer

- **Networking stack (decided):** Netcode for GameObjects + Unity Relay + Unity Lobby service. None of these packages are installed yet — add `com.unity.netcode.gameobjects`, Relay, Lobby, and Authentication via Package Manager / Unity Dashboard services. The already-present `com.unity.multiplayer.center` package is just Unity's setup/recommendation tool, not the netcode itself.
- **Lobby flow:** host creates a lobby (gets a join code), other players join by entering it, up to 4 total. Each player picks a character skin, starting weapon, and votes on a map (tentative — confirm before building the voting UI itself).
- **Scene flow:** a small generic SceneManager/loader that moves players from Main Menu → Lobby → Gameplay scene, ideally driven by data (scene references/Addressables) rather than hardcoded scene name strings.
- **UI:** build menus and lobby screens by hand in the editor (Canvas + uGUI, since that's the UI package already in the project) rather than generating UI layouts from code.
- **Testing:** for local multiplayer testing before a dedicated server exists, look at Unity's Multiplayer Play Mode package (lets you run multiple simulated clients in one Editor).
- **What this milestone must revisit:** `Inventory.Local` (a static, which becomes per-client ownership), the fact that `Inventory` mutates itself directly (which becomes a server-authoritative path with client requests), and `Chest` open state plus pickup destruction (both must replicate, so two players cannot loot the same thing twice).
