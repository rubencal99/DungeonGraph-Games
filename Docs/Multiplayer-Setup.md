# Multiplayer setup (Milestone 2)

The code is written. Everything below is editor work, in order. Each part can be checked before the next.

Prefab paths assume `Assets/Game/Prefabs/`. "Add" means **Add Component** and type the name.

---

## 1. Packages and services

1. **Window > Package Manager > Unity Registry.** Install:
   - **Netcode for GameObjects**
   - **Multiplayer Services** (`com.unity.services.multiplayer`). This includes Lobby, Relay, and join codes.
   - **Multiplayer Play Mode**, for testing several players in one editor.
2. **Edit > Project Settings > Services.** Link the project to a Unity Cloud project (create one if asked).
3. In the Unity Cloud dashboard for that project, open **Multiplayer** and make sure **Relay** and **Lobby** are enabled.
4. Wait for scripts to compile. The Console should be clean before you go on.

## 2. Pause key

1. Open `Assets/InputSystem_Actions.inputactions`.
2. In the **UI** map (not Player), add an action **Pause**, type **Button**.
3. Bind it to **Escape [Keyboard]** and **Start [Gamepad]**. Click **Save Asset**.

It has to live in UI: pausing switches the Player map off, and a key in that map couldn't unpause.

## 3. Data assets

**Characters.** Do this once per dino (doux, mort, tard, vita):
1. Right-click `Animations/Default_Movement.controller` > **Create > Animator Override Controller**. Name it `Override_Doux` (etc.).
2. Select it and drag that dino's Idle / Run / Hurt clips over the base clips.
3. **Assets > Create > Game > Character Definition**, named `Character_Doux`. Set Display Name, a Portrait sprite (one frame of the sheet), and Animator = the override.

**Catalog.**
1. Create the folder `Assets/Game/Resources`.
2. In it, **Assets > Create > Game > Game Catalog**. The name must be exactly `GameCatalog`.
3. Fill in:
   - **Weapons:** every WeaponDefinition asset, enemy guns included.
   - **Characters:** the four Character Definitions, in the order the lobby arrows should cycle.
   - **Enemies:** `Enemy_Melee`, `Enemy_Ranged`.

## 4. Network prefabs

Make a folder `Prefabs/Network`.

**LobbyPlayer.** Create an empty GameObject "LobbyPlayer". Add **NetworkObject** and **LobbyPlayer**. Drag it into `Prefabs/Network`, then delete it from the scene.

**LobbyState.** Same steps: empty "LobbyState", add **NetworkObject** and **LobbyState**. Leave Countdown Seconds at 5, save as a prefab, delete it from the scene.

**Core.** This is the one persistent object.
1. Create an empty GameObject "Core". Add **NetworkManager**.
2. In NetworkManager, click **Select Transport** > **UnityTransport**.
3. Set **Player Prefab** = LobbyPlayer. Leave **Enable Scene Management** ticked.
4. Open **Network Prefabs Lists** (NGO made a `DefaultNetworkPrefabs` asset). Make sure it contains LobbyPlayer, LobbyState, Player, the weapon pickup prefab, Enemy_Melee, and Enemy_Ranged.
5. Add **GameSceneManager**:
   - Main Menu Scene = `MainMenu`
   - Gameplay Scene = `Demo 1` (or whatever your level scene is called)
   - LobbyState Prefab = LobbyState
   - Pause Action = **UI/Pause**
   - Gameplay Action Map = `Player`
6. Save it as a prefab in `Prefabs/Network`, then delete it from the scene.

**Player** (`Player.prefab`). Open the prefab.
- On the **Player** root, add:
  - **NetworkObject**
  - **OwnerNetworkTransform**: under Syncing, tick Position X and Y only. Untick Position Z, all Rotation, and all Scale. Keep Interpolate ticked.
  - **NetworkRigidbody2D**
  - **HealthNetwork**
  - **ShotNetwork**
  - **AimNetwork**: Aim = the root's PlayerAim.
  - **InventoryNetwork**: Inventory = the `InventoryContainer/Inventory` child's Inventory.
  - **PlayerShooter**: Weapon Holder = the `Inventory` child's WeaponHolder, Fire Action = **Player/Attack**.
  - **PlayerNetwork**:
    - Owner Only (6 entries): PlayerController, PlayerAim, PlayerShooter, PlayerInteractor, PlayerFootsteps (all on the root), and InventoryInput (on the Inventory child).
    - Aim = PlayerAim. Inventory = the Inventory child. Animator = the Character child's Animator.
- On the **Character** child, add **OwnerNetworkAnimator**. Its Animator slot fills itself.
- *Optional name tag:* right-click Player > **3D Object > Text - TextMeshPro**. Name it "NameLabel", set Position (0, 1.2, 0), Font Size 3, alignment Center, and on its renderer pick a Sorting Layer above the characters. Drag it into PlayerNetwork's **Name Label**.

**Enemies** (`Enemy_Melee` and `Enemy_Ranged`). Open each prefab.
- On the root, add:
  - **NetworkObject**
  - **NetworkTransform**: same syncing as the player (Position X and Y only).
  - **NetworkRigidbody2D**
  - **EnemyNetwork**
  - **HealthNetwork**
  - **AimNetwork**: Aim = EnemyAim.
  - On the ranged enemy only: **ShotNetwork**.
- On the **Character** child (the one with the Animator), add **NetworkAnimator**.

**Weapon pickup** (the prefab assigned in WeaponPickupSpawner). Add:
- **NetworkObject**
- **WeaponPickupNetwork**
- **NetworkTransform** (Position X and Y only)
- **NetworkRigidbody2D**

## 5. Gameplay scene (`Demo 1`)

1. Select **DungeonGenerator**:
   - Untick **Generate On Start**. DungeonNetwork triggers it now.
   - Add **NetworkObject**.
   - Add **DungeonNetwork**: Generator = this object's DungeonGenerator, Graph = `01 - RewardChain` (the same graph). The graph must stay on the **Grid** style, which is the one that takes a seed.
   - Add **LevelProgress**: Dungeon = DungeonNetwork.
   - On **PlayerSpawner**, fill the new **Dungeon** slot with DungeonNetwork. Check the other slots are still filled.
2. **WeaponPickupSpawner** object: add **NetworkObject**.
3. Make sure the scene has: the EnemyPoolManager prefab, the AudioManager prefab, a Main Camera (with CinemachineBrain), and an **EventSystem** (GameObject > UI > Event System) for the menu buttons.
4. Delete any hand-placed Player, CinemachineCamera, or AimCameraTarget. PlayerSpawner makes these per player now.
5. **Spawners in rooms:** open each room prefab that should have enemies (e.g. the Basic rooms under `DungeonGraph/Dungeon_Floors/Floor_1`). Drag the `EnemySpawner` prefab in and shape its collider over the room's floor. The level is won when every spawner in the dungeon has fired and every enemy is dead.

## 6. Build profile

**File > Build Profiles > Scene List**: `MainMenu` first, then `Demo 1`.

Always press Play from **MainMenu**. The level scene can't start on its own any more, because it needs a session.

---

## 7. UI: main menu and lobby (MainMenu scene)

### What you're building

One canvas with two pages. Only one page is visible at a time, like two slides in a deck. The title and background sit outside both pages, so they never change.

```
Main Menu (Canvas)            ← MainMenuView: picks which page is showing
├─ Background                   always visible
├─ Title                        always visible
├─ MainPanel                    PAGE 1, starts ON
│  ├─ HostButton
│  ├─ JoinCodeInput
│  ├─ JoinButton
│  ├─ QuitButton
│  └─ StatusLabel
└─ LobbyPanel                   PAGE 2, starts OFF  ← LobbyView: runs this page
   ├─ JoinCodeLabel
   ├─ PlayerList                  (Vertical Layout Group)
   │  └─ LobbyPlayerRow ×4         ← each has LobbyPlayerRowView
   ├─ Profile
   │  ├─ NameInput
   │  ├─ CharacterPicker            (Horizontal Layout Group)
   │  │  ├─ PrevCharacter
   │  │  ├─ CharacterPortrait
   │  │  └─ NextCharacter
   │  └─ CharacterName
   ├─ ReadyButton
   │  └─ Text (TMP)                 the label that flips "Ready" / "Not Ready"
   ├─ CountdownLabel
   └─ LeaveButton

Bootstrap (separate, outside the canvas)  ← GameBootstrap
```

Three rules explain where each script goes:

1. **A script that hides something can't live on the thing it hides.** A switched-off object stops running its scripts. MainMenuView swaps the two pages, so it sits on the Canvas above them, which never switches off.
2. **Each page's script lives on that page.** LobbyView only matters while the lobby is showing, so it sits on LobbyPanel and sleeps when the page is hidden.
3. **Scripts find things only through slots you drag in.** Nothing is looked up by name, so you can rename or rearrange objects freely, as long as the slots stay filled.

The rows are display-only. LobbyView hands row 1 the first player, row 2 the second, and so on, then hides the rows nobody is using. The layout group closes the gap.

**Bootstrap.**
1. GameObject > Create Empty > "Bootstrap".
2. Add **GameBootstrap**. Core Prefab = Core.

**Canvas.** Use the existing "Main Menu" canvas.
1. On **Canvas Scaler**, set UI Scale Mode to **Scale With Screen Size**, Reference Resolution to 1920 × 1080, and Match to 0.5.
2. Add **MainMenuView** to the canvas. Fill its slots last.

**Main panel** (what you see first):
1. Right-click the canvas > Create Empty, named "MainPanel". Anchor it to the center, size 500 × 500.
2. Add **Vertical Layout Group**: Spacing 16, Child Alignment Middle Center, tick Control Child Size Width and Height.
3. Under it, create:
   - **UI > Button - TextMeshPro** "HostButton", text "Host".
   - **UI > Input Field - TextMeshPro** "JoinCodeInput". Placeholder "Join code"; Content Type **Alphanumeric**; Character Limit 8.
   - **Button** "JoinButton", text "Join".
   - **Button** "QuitButton", text "Quit".
   - **UI > Text - TextMeshPro** "StatusLabel": empty text, font size 24, wrapping on.
4. Give each child a **Layout Element** with Preferred Height 60 (the status label: 80).

**Lobby player row prefab:**
1. Create Empty "LobbyPlayerRow", size 600 × 80.
2. Add **Horizontal Layout Group**: Spacing 16, Child Alignment Middle Left, tick Control Child Size.
3. Children:
   - **UI > Image** "Portrait": Layout Element 64 × 64, Preserve Aspect on.
   - **Text - TextMeshPro** "Name": Layout Element Flexible Width 1.
   - **Image** "ReadyMark": 40 × 40, green, or a tick sprite.
4. Add **LobbyPlayerRowView** to the row and drag in Portrait, Name, and ReadyMark.
5. Drag the row into `Prefabs/UI`, then delete it from the scene.

**Lobby panel:**
1. Create Empty "LobbyPanel" (full screen stretch). **Untick it** so it starts hidden.
2. Children, laid out however you like:
   - **Text - TextMeshPro** "JoinCodeLabel": large font. Players read the code out loud from here.
   - Empty "PlayerList" with a **Vertical Layout Group** (Spacing 8). Under it, 4 × LobbyPlayerRow.
   - Empty "Profile" with:
     - **Input Field - TextMeshPro** "NameInput": Placeholder "Your name", Character Limit 16.
     - A row with **Button** "PrevCharacter" ("<"), **Image** "CharacterPortrait" (128 × 128, Preserve Aspect), and **Button** "NextCharacter" (">").
     - **Text - TextMeshPro** "CharacterName".
   - **Button** "ReadyButton". LobbyView flips its child text between "Ready" and "Not Ready".
   - **Text - TextMeshPro** "CountdownLabel": empty text, large font.
   - **Button** "LeaveButton", text "Leave".
3. Add **LobbyView** to LobbyPanel and drag everything in. Fill Rows top to bottom. Ready Button Label is ReadyButton's child text.

**Wire MainMenuView:** Main Panel, Lobby Panel, the three buttons, Join Code Input, and Status Label.

## 8. UI: pause and level complete (gameplay scene)

Use the HUD canvas the inventory bar is on. Both menus follow the same pattern: an always-on parent holds the script, and a child panel is what shows and hides.

**Pause menu:**
1. Create Empty "PauseMenu" (stretch full screen). Add **PauseMenuView**.
2. Child **UI > Image** "Panel": stretch full screen, black at 70% alpha. **Untick it.**
3. Under Panel, create Empty "Buttons" (centered, **Vertical Layout Group**, Spacing 16) with:
   - **Text - TextMeshPro** "Paused"
   - **Button** "ResumeButton" ("Resume")
   - **Button** "MainMenuButton" ("Main Menu")
   - **Button** "ExitButton" ("Exit Game")
4. Drag Panel and the three buttons into PauseMenuView.

**Level complete:**
1. Create Empty "LevelComplete" (stretch full screen), placed **below** PauseMenu in the Hierarchy so it draws on top. Add **LevelCompleteView**.
2. Child **Image** "Panel": full screen, black at 80% alpha. **Untick it.**
3. Under Panel, a centered **Vertical Layout Group** with:
   - **Text - TextMeshPro** "Level Complete!"
   - **Button** "ReturnToLobbyButton" ("Return to Lobby")
   - **Text - TextMeshPro** "WaitingLabel" ("Waiting for host…", small, grey)
   - **Button** "MainMenuButton" ("Main Menu")
   - **Button** "ExitButton" ("Exit Game")
4. Drag Panel, the three buttons, and WaitingLabel into LevelCompleteView.

---

## 9. Testing

1. **Window > Multiplayer > Multiplayer Play Mode**. Tick **Player 2** (and 3 and 4 if you like) and wait for the windows to start.
2. Open **MainMenu** and press Play. Every player window enters Play mode together.
3. In the main editor: **Host**. The lobby shows a code.
4. In Player 2: type the code and **Join**. Both lobbies should list both players.
5. Change a name or character in one window. It updates in the other within a moment.
6. Press **Ready** in both. After 5 seconds both load the level. Un-readying during the countdown cancels it.
7. In the level, check:
   - You see each other move, aim, flip, and shoot.
   - Each window has its own camera.
   - Opening a chest drops the loot once, and both see it.
   - A pickup goes to whoever presses E first.
   - Enemies chase and shoot either player.
   - Clearing every spawner shows Level Complete in both windows.
8. Host presses **Return to Lobby**: both land back in the lobby, unready.

Things to watch in the Console:
- **"built a different dungeon"**: the seed trick failed for that client. Tell me what changed (graph style, a new random call during generation).
- **"is not in the GameCatalog"**: add that asset to the catalog.
