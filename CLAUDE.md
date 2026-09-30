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
- **Inventory, interaction, chests, and loot.** A 3-slot weapon inventory shown bottom-right and cycled with the scroll wheel, an `Interactable` base class triggered by pressing E, a nearest-target prompt over interactable world objects, and chests that roll nested weighted loot tables onto the floor. Design notes are kept under Details below because later milestones build on them.
- **Audio.** One pooled, budgeted `AudioManager` that stays cheap when dozens of cues fire at once, driven by `AudioCueDefinition` assets, with a hand-built mixer. Hooks for running, interacting, shooting, and pickup/swap. Reference notes under Details.

## Ongoing milestones

1. **Basic enemies and spawner.** A melee and a ranged enemy sharing one asset-driven Idle/Chase/Attack brain, spawned and pooled by an EnemySpawner, with performance as the top priority.
2. **SceneManager, main menu, lobby, and basic multiplayer.** A join-code lobby for up to 4 players built on Netcode for GameObjects + Unity Relay/Lobby, with character skin, starting weapon, and (tentative) map-vote selection. **Deliberately deferred** — everything before it is built single-player first, then made networked.

**Standing tasklist — Weapon polish.** Never "done". A running list of polish, extensions, and improvements to the weapon system, picked from between milestones. See Details.

## Details

### Inventory, interaction, chests, and loot (finished — reference notes)

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

### Audio (finished — reference notes)


The problem to solve is volume of sound, not variety. Four players on full-auto in a bullet-hell room can ask for hundreds of shots a second. Playing every one is expensive, and it sounds like mush anyway. The system decides which sounds are worth a voice.

Scripts live in `Scripts/Audio/`. Mixer, prefab, and cue assets were built by hand.

- **AudioCueDefinition:** a ScriptableObject per sound (`Assets > Create > Game > Audio Cue`): clip variations (random pick, optionally never the same twice in a row), and **Volume, Pitch, and Delay ranges rolled fresh every play** so repeats never sound identical. Also mixer group, spatial on/off with a distance range, priority (0–100), a **max instances** cap, and a **minimum interval**. The last two are what keep a minigun from stacking forty copies of one gunshot. Ranges draw as two-handled sliders through the `[MinMaxRange]` attribute (`Scripts/Editor/MinMaxRangeDrawer.cs`) — Unity has no built-in one.
- **One player, one pool:** `AudioManager` (a prefab dropped into each scene, like `ProjectilePoolManager`) builds a fixed set of `AudioSource` voices in `Awake` and exposes static `AudioManager.Play(cue, position)`. A null cue is silent, not an error, so unassigned slots are safe. No `Instantiate`, no `PlayClipAtPoint`, no allocation per play. Voice Count lives on the prefab and should match Project Settings > Audio > Max Real Voices (32 by default). Voices are tracked by clock (start + delay + length/pitch), not `AudioSource.isPlaying`, which is unreliable during a delayed start.
- **When the pool is full:** if a cue is at its own cap, its oldest copy restarts. Otherwise a free voice. Otherwise the lowest-priority, oldest voice is taken — unless everything playing matters more, in which case the new sound is dropped.
- **No loops yet.** Every current sound is a one-shot (footsteps included), so there are no play handles. Add a handle the day something genuinely loops (a minigun spin-up, ambience).
- **Mixer:** an `AudioMixer` built by hand — Master → Music, SFX → Player / Weapons / World, UI — with exposed volume parameters, so a settings menu later is a slider per parameter.
- **2D listener:** the camera sits at z = -10, so a listener on it hears everything 10 units too far away. The `AudioListener` lives on a child of Main Camera at local z = +10, which puts it on the gameplay plane and follows the camera with no code.
- **Hooks:**
  - *Shoot* — `WeaponDefinition.FireCue`, played by `Weapon.Fire` once per trigger pull, not per projectile.
  - *Pick up / swap* — one `WeaponDefinition.EquipCue`, played by `WeaponHolder` when a weapon comes into hand or into the pack. `WeaponHolder` already reacts to every inventory change, so one pickup can never play it twice.
  - *Interact* — optional `Activate Cue` on the `Interactable` base, played through `PlayActivateCue()` only after a subclass's `Activate` actually succeeds. The sound belongs to what you touched (chest creak vs. lever clunk), so it lives on the object, not on the player.
  - *Running* — `PlayerFootsteps`, a separate component that plays a step per stride of distance covered, so steps quicken and stop with the player.
  - *Reload and dodge* — **neither mechanic exists yet**, so there are no cue slots for them. Each is one cue field plus one `AudioManager.Play` line when the feature lands.
- **Multiplayer later:** audio stays client-local and is never networked itself. Each client plays sounds off the replicated events it already receives (a shot, a pickup), which is why hooks sit on gameplay events rather than on input.

### 1. Basic enemies + EnemySpawner

**Status: code written; editor setup (AI assets, prefabs, animations, layers) is done by hand.** Scripts live in `Scripts/Enemies/` and `Scripts/Enemies/AI/`.

- **Decision:** traditional GameObjects + object pooling + a simple state machine, not DOTS/ECS. Revisit ECS only if profiling shows GameObject enemies are an actual bottleneck.
- **Brain as assets:** `AIState` ScriptableObjects hold a list of `AIAction` assets (run every frame, in order) and a list of transitions. A transition is a set of `AICondition` assets that must all pass, each with a **Negate** tick box, plus a target state. OR = two transitions to the same state. Adding a behavior is one small `AIAction` / `AICondition` subclass plus an asset; nothing else changes. Actions and conditions are shared assets and must never store per-enemy data — that lives on `EnemyBrain` (target, cooldown, current state).
- **Current assets:** actions `MoveToTarget`, `FaceTarget`, `AttackTarget`; conditions `TargetInRange` (Detection / Attack / LoseTarget, read from each enemy's `EnemyDefinition`, so one asset serves every enemy) and `HasLineOfSight` (one `Linecast` against wall layers). Tree: Idle → Chase when in Detection range and in sight; Chase → Attack when in Attack range and in sight; Chase → Idle when outside LoseTarget range; Attack → Chase when out of Attack range or out of sight.
- **Two speeds:** `EnemyBrain` *thinks* (finds the nearest player from `PlayerController.All`, checks transitions) every 0.1 s with a random per-enemy offset, and *does* (runs actions) every frame. Conditions run at think rate, so a raycast in one is fine.
- **One tree, two enemies:** `AttackTarget` calls whatever `IEnemyAttack` the prefab carries. `MeleeAttack` (on the Animator's object) fires an "Attack" trigger; an Animation Event on the impact frame calls `DealHit`, one non-allocating `OverlapCircle`. `RangedAttack` pulls the trigger on a real `Weapon` — same prefab, definition, projectiles, and cues the player uses.
- **Shared aim:** `AimSource` is the base of `PlayerAim` (mouse) and `EnemyAim` (told by `FaceTarget`). `Weapon`, `WeaponAimRotator`, and `CharacterFlip` only read `AimSource`, which is why an enemy can hold a gun. `Weapon` no longer reads input; `WeaponHolder` owns the player's Attack action and calls `Weapon.TryFire()`. Enemy guns tick **Infinite Ammo** until reloading exists.
- **Components on an enemy root:** `Enemy` (definition, health reset on spawn, death → loot + back to pool), `Health`, `EnemyBrain`, `EnemyMotor` (Rigidbody2D eased toward speed from the definition), `EnemyAim`, plus one `IEnemyAttack`.
- **EnemyDefinition:** start state, health, speed/acceleration, detection/attack/lose-target ranges, melee damage, attack cooldown, death loot table + drop chance.
- **Health:** `Health` is the shared `IDamageable` for player and enemies. It only counts down and raises `Damaged` / `Died`. **The player has no death handling yet.**
- **Friendly fire:** handled by the physics layer matrix, not code — an EnemyProjectile layer that does not collide with Enemy.
- **Not built yet:** pathfinding (enemies chase in a straight line), a Disabled/stunned condition (one `AICondition` reading a flag, added when stuns exist), enemy attack/hurt/death sounds, hit feedback.
- **Loot on death:** reuses `LootTableDefinition` and `WeaponPickupSpawner` — no second drop path.

### Weapon polish (standing tasklist)

Not a milestone to finish. Add items as they come up; tick them off as they ship.

- [ ] **Audio** — shoot, reload, pickup/swap cues (the Audio milestone built the system; this list tracks per-weapon tuning and new cues after it).
- [ ] **Muzzle flash** — a short sprite or light flash at the barrel on each shot.
- [ ] **Particles** — impact sparks where projectiles hit, shell casings, pooled like projectiles.
- [ ] **Reloading** — the mechanic itself (magazine vs. reserve ammo, reload time from `WeaponDefinition`, reload key), plus a HUD indicator.
- [ ] **Animations** — recoil kick, reload, and equip/swap animations, authored in the editor.

### 2. SceneManager, main menu, lobby, multiplayer

- **Networking stack (decided):** Netcode for GameObjects + Unity Relay + Unity Lobby service. None of these packages are installed yet — add `com.unity.netcode.gameobjects`, Relay, Lobby, and Authentication via Package Manager / Unity Dashboard services. The already-present `com.unity.multiplayer.center` package is just Unity's setup/recommendation tool, not the netcode itself.
- **Lobby flow:** host creates a lobby (gets a join code), other players join by entering it, up to 4 total. Each player picks a character skin, starting weapon, and votes on a map (tentative — confirm before building the voting UI itself).
- **Scene flow:** a small generic SceneManager/loader that moves players from Main Menu → Lobby → Gameplay scene, ideally driven by data (scene references/Addressables) rather than hardcoded scene name strings.
- **UI:** build menus and lobby screens by hand in the editor (Canvas + uGUI, since that's the UI package already in the project) rather than generating UI layouts from code.
- **Testing:** for local multiplayer testing before a dedicated server exists, look at Unity's Multiplayer Play Mode package (lets you run multiple simulated clients in one Editor).
- **What this milestone must revisit:** `Inventory.Local` (a static, which becomes per-client ownership), `PlayerController.All` and enemy targeting (enemy AI should run on the server only), the fact that `Inventory` mutates itself directly (which becomes a server-authoritative path with client requests), and `Chest` open state plus pickup destruction (both must replicate, so two players cannot loot the same thing twice).
