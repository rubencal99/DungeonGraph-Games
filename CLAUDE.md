# DungeonGraph Games — Project Context

## Instructions

Talk plainly and keep sentences short. Use everyday analogies instead of technical jargon unless asked for detail. When something can be built by hand in the Unity Editor — a prefab, a scene, a UI layout, an animation, a ScriptableObject asset — prefer that over generating it through a script, and give the user the exact step-by-step editor process (menu paths, component names, field values) instead of writing a script that builds it for them. Reach for a script only for behavior, not for content a human should author and tweak visually.

Favor data-driven design: ScriptableObjects for anything tunable (weapon stats, enemy stats, character/map definitions), interfaces for swappable behavior (e.g. `IInteractable`, `IDamageable`), and generic systems that don't hardcode specific cases. Keep code simple — no speculative abstractions, no half-built features, no scaffolding for a problem that doesn't exist yet. Three similar lines beats a premature abstraction.

Performance is a first-class concern. Pool anything that spawns repeatedly (projectiles, enemies). Avoid per-frame allocations. Don't reach for exotic optimizations (DOTS/ECS, job system) until profiling actually shows a bottleneck — start simple, measure, then optimize.

`Assets/DungeonGraph` is a third-party Asset Store plugin (procedural 2D dungeon generation via a node-graph editor + Addressables + Tilemaps). Treat it as read-only vendored code — consume its public runtime API (see its own `Documentation/` folder) rather than editing its internals. All original game code and content lives under `Assets/Game`.

## Summary

DungeonGraph Games is a multiplayer game hub, built in Unity 6.6, meant to host several mini dungeon crawlers. Players land on a main menu, then create or join a lobby with a join code (up to 4 players), typing a name and picking a character before readying up and dropping in together. Levels are procedurally generated using the DungeonGraph plugin, which turns an authored node graph into a tile-based dungeon. Core gameplay is a top-down twin-stick shooter: WASD movement, mouse aim, a rotating weapon inventory, and straight-line bullet-hell-style projectiles. The project is at its very beginning — almost everything below is planned, not yet built.

## Recently finished milestones

- **DungeonGraph plugin installed and verified.** The Asset Store dungeon generator is imported, its required packages (Addressables, 2D Tilemap, Tilemap Extras, 2D Sprite) are installed, and Addressables is configured. A sample dungeon generates correctly from a node graph, confirming the toolchain works end to end.
- **Base project scaffolding.** A Unity 6000.0.67f1 URP 2D project exists with the Input System package wired up, plus a minimal stub `PlayerController` (Rigidbody2D + WASD via an Input Action) as a starting point — not a finished controller.
- **Character controller, spawning, and weapons.** `PlayerController` does WASD with acceleration/deceleration. `PlayerAim` raycasts the mouse onto an AimPlane layer each frame and publishes the aim point, angle, and side; `CharacterFlip` and `WeaponAimRotator` consume that to mirror the sprite and rotate the gun as independent concerns. Cinemachine is installed and follows the player through `AimCameraTarget`, which pulls the camera toward the cursor. `PlayerSpawner` drops the player at the DungeonGraph Start room once generation finishes. `WeaponDefinition` ScriptableObjects drive fire rate, ammo, bloom/recoil, spread, multi-shot, and projectile behavior; `Weapon` fires them and `ProjectilePoolManager` pools every `Projectile` by prefab.
- **Inventory, interaction, chests, and loot.** A 3-slot weapon inventory shown bottom-right and cycled with the scroll wheel, an `Interactable` base class triggered by pressing E, a nearest-target prompt over interactable world objects, and chests that roll nested weighted loot tables onto the floor. Design notes are kept under Details below because later milestones build on them.
- **Audio.** One pooled, budgeted `AudioManager` that stays cheap when dozens of cues fire at once, driven by `AudioCueDefinition` assets, with a hand-built mixer. Hooks for running, interacting, shooting, and pickup/swap. Reference notes under Details.

## Ongoing milestones

1. **Basic enemies and spawner.** A melee and a ranged enemy sharing one asset-driven Idle/Chase/Attack brain, spawned and pooled by an EnemySpawner, with performance as the top priority.
2. **SceneManager, main menu, lobby, and basic multiplayer.** A join-code lobby for up to 4 players built on Netcode for GameObjects + Multiplayer Services (Lobby/Relay), with a typed name, character pick, and ready-up countdown; synced co-op play in a shared dungeon until every spawner is cleared, then a Level Complete screen. Code written; editor setup in `Docs/Multiplayer-Setup.md`.

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

### Camera shake (reference notes)

Scripts live in `Scripts/Camera/`.

- **CameraShakeDefinition:** a ScriptableObject per shake (`Assets > Create > Game > Camera Shake`): a Duration, plus an Amplitude curve and a Frequency curve on a 0–1 timeline. Curve heights are the Cinemachine noise gains directly.
- **One listener:** `CameraShake` sits on the CinemachineCamera prefab next to `CinemachineBasicMultiChannelPerlin` and drives its Amplitude/Frequency Gain. Callers use static `CameraShake.Play(shake)`, a static event every enabled camera subscribes to, so nothing needs a reference to the runtime-spawned camera. A null shake does nothing.
- **Overlap:** the strongest active shake at each moment drives both gains; shakes are not summed, so full-auto holds a steady shake. Re-requesting a shake that is already playing restarts it. When the last one ends, the gains return to whatever the Perlin component was set to in the Inspector.
- **Hooks:** *Fire* — `WeaponDefinition.FireShake`, played by `PlayerShooter` (not `Weapon`), so enemy guns and teammates' guns never shake your screen. *Damage* — `CameraShakeOnDamage` on the Player, listening to `Health.Damaged`. Explosions, environment, and interaction are one `CameraShake.Play` call each when they exist.
- **Not built yet:** distance falloff (a far explosion shaking less). Add a position parameter the day explosions exist.

### 1. Basic enemies + EnemySpawner

**Status: code written; editor setup (AI assets, prefabs, animations, layers) is done by hand.** Scripts live in `Scripts/Enemies/` and `Scripts/Enemies/AI/`.

- **Decision:** traditional GameObjects + object pooling + a simple state machine, not DOTS/ECS. Revisit ECS only if profiling shows GameObject enemies are an actual bottleneck.
- **Brain as assets:** `AIState` ScriptableObjects hold a list of `AIAction` assets (run every frame, in order) and a list of transitions. A transition is a set of `AICondition` assets that must all pass, each with a **Negate** tick box, plus a target state. OR = two transitions to the same state. Adding a behavior is one small `AIAction` / `AICondition` subclass plus an asset; nothing else changes. Actions and conditions are shared assets and must never store per-enemy data — that lives on `EnemyBrain` (target, cooldown, current state).
- **Current assets:** actions `MoveToTarget`, `FaceTarget`, `AttackTarget`; conditions `TargetInRange` (Detection / Attack / LoseTarget, read from each enemy's `EnemyDefinition`, so one asset serves every enemy) and `HasLineOfSight` (one `Linecast` against wall layers). Tree: Idle → Chase when in Detection range and in sight; Chase → Attack when in Attack range and in sight; Chase → Idle when outside LoseTarget range; Attack → Chase when out of Attack range or out of sight.
- **Two speeds:** `EnemyBrain` *thinks* (finds the nearest player from `PlayerController.All`, checks transitions) every 0.1 s with a random per-enemy offset, and *does* (runs actions) every frame. Conditions run at think rate, so a raycast in one is fine.
- **One tree, two enemies:** `AttackTarget` calls whatever `IEnemyAttack` the prefab carries. `MeleeAttack` (on the Animator's object) fires an "Attack" trigger; an Animation Event on the impact frame calls `DealHit`, one non-allocating `OverlapCircle`. `RangedAttack` pulls the trigger on a real `Weapon` — same prefab, definition, projectiles, and cues the player uses.
- **Shared aim:** `AimSource` is the base of `PlayerAim` (mouse) and `EnemyAim` (told by `FaceTarget`). `Weapon`, `WeaponAimRotator`, and `CharacterFlip` only read `AimSource`, which is why an enemy can hold a gun. `Weapon` never reads input; `PlayerShooter` owns the player's Attack action and calls `Weapon.TryFire()` on `WeaponHolder.Held`. Enemy guns tick **Infinite Ammo** until reloading exists.
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

**Status: code written; packages, prefabs, scenes, and UI are set up by hand — follow `Docs/Multiplayer-Setup.md`.** Scripts live in `Scripts/Session/`, `Scripts/Network/`, `Scripts/Level/`, and `Scripts/UI/`.

- **Stack:** Netcode for GameObjects + **Multiplayer Services** (`com.unity.services.multiplayer`), whose Sessions API bundles Lobby, Relay, and join codes and starts Netcode itself. `GameSession` is the only file that talks to it. In editor/dev builds each process signs in with its own profile so Multiplayer Play Mode players are distinct accounts.
- **Authority ("co-op trust"):** each player's own machine decides their movement, aim, inventory, shooting, and whether they got hit. The host decides enemies, loot, chests, spawners, the countdown, and scene changes. No anti-cheat, by design.
- **Persistent objects:** `GameBootstrap` (in MainMenu) creates the **Core** prefab once — `NetworkManager` + `GameSceneManager` — so returning to the menu never duplicates it. `LobbyPlayer` is the NGO Player Prefab (name, character, ready) and lives for the whole session; `LobbyState` (the 5 s countdown, stored as a server end time) is spawned by the host and also persists.
- **Scene flow:** `GameSceneManager` holds scene names in the Inspector. In-session moves (`StartGameplay`, `ReturnToLobby`) go through NGO's scene manager so everyone follows the host; `LeaveToMainMenu`/`QuitGame` leave the session then load locally. An unexpected disconnect (host left) sends you to the menu with a message. Pause is a local overlay that switches off the **Player** action map — the world keeps running. Components no longer Enable/Disable shared input actions themselves (project-wide actions are on by default), because a teammate's copy switching off would kill your input.
- **Ready rules:** ready is host-written (owners ask via RPC) so the host can clear everyone's flag when a level starts. Any unready player (including a new joiner) cancels the countdown. The session is locked while a level runs; no mid-game joins. Duplicate characters are allowed. Starting weapon and map vote were dropped.
- **Same dungeon everywhere:** `DungeonNetwork` has the host pick a seed; every machine pins the graph's `floodFillSeed` and `UnityEngine.Random` to it, generates locally, then restores both. Requires the graph's **Grid** style. Clients report a room fingerprint and the host logs any mismatch. Avatars spawn only after every machine has finished. Things inside room prefabs (chests, spawners) can't be network objects, so they're addressed by index in the generated hierarchy — `DungeonNetwork` relays "open chest #N".
- **Per-object glue:** `PlayerNetwork` switches off its **Owner Only** input components on teammates' copies, applies the lobby character/name, sets `Inventory.Local`, and announces the local player so `PlayerSpawner` builds that machine's own camera rig. `AimNetwork` syncs aim points (which drives flip and gun angle for free), `InventoryNetwork` mirrors slots and the held slot, and `OwnerNetworkTransform`/`OwnerNetworkAnimator` sync movement and animation. Enemies use host-authority NetworkTransform/NetworkAnimator, and `EnemyNetwork` turns brain and motor off on clients. Melee swings go through `NetworkAnimator.SetTrigger`.
- **Projectiles are never network objects.** `Weapon.Fire` sends one `ShotNetwork` message per trigger pull (origin, angle, bloom, seed) and every machine replays it with `Weapon.SpawnShot`, using a private xorshift so the spread matches exactly.
- **Damage routing:** `HealthNetwork` decides which copy of a hit counts — a player's shot counts on the shooter's machine, anything else on the victim's — then forwards it to whoever owns the health. So `Damaged`/`Died` fire only on the owner, which is also why camera shake and enemy death/loot happen once. Friendly fire is off through the physics matrix (PlayerProjectile × Player).
- **Shared loot:** `WeaponPickupSpawner` is a NetworkBehaviour; non-hosts forward spawn requests. Pressing E on a pickup asks the host, who despawns it and sends the weapon to the first asker (`PlayerNetwork.GiveWeaponRpc`).
- **Enemies:** `EnemyPoolManager` registers an NGO prefab handler per enemy in the `GameCatalog`, so every machine pools its own copies. `EnemySpawner` (hand-placed in room prefabs) acts on the host only and checks player positions against its collider — no physics trigger, because teammates' copies are moved by the network, not physics.
- **Level loop:** `LevelProgress` (host) completes the level when `EnemySpawner.AllTriggered` and `Enemy.ActiveCount == 0`, flipping a shared flag that shows `LevelCompleteView` for everyone. Only the host can Return to Lobby. Player death is still unhandled.
- **IDs:** `GameCatalog` (a Resources asset) maps weapons, characters, and enemies to list indices so they can cross the network. Every new weapon, character, or enemy must be added to it.
- **Not built yet:** host migration, reconnecting, offline play without a session, footstep sounds for teammates, synced hit feedback.
