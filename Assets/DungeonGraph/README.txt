================================================================================
DUNGEON GRAPH  v1.0.0
Node-based procedural 2D dungeon generation for Unity
================================================================================

Thank you for downlaoding Dungeon Graph.

--------------------------------------------------------------------------------
START HERE
--------------------------------------------------------------------------------

  Documentation/0_Documentation_Index.md

That file is the table of contents for the whole manual. If you only read one
page, read:

  Documentation/1_Getting_Started.md

It covers requirements, installation, and a ten-minute tutorial that produces a
working dungeon.

--------------------------------------------------------------------------------
REQUIRED UNITY PACKAGES  -  READ THIS FIRST
--------------------------------------------------------------------------------

Dungeon Graph WILL NOT COMPILE until these four packages are installed. They are
not shipped inside this package; Unity's .unitypackage format cannot install
dependencies automatically.

    com.unity.addressables         (1.19.0 or newer)
    com.unity.2d.tilemap           (1.0.0 or newer)
    com.unity.2d.tilemap.extras    (4.0.0 or newer)
    com.unity.2d.sprite            (1.0.0 or newer)

On first import, a dialog offers to install whatever is missing. Click
"Install Now".

If you dismissed it, run:

    Tools > Dungeon Graph > Setup > Check Dependencies

Or install them yourself from Window > Package Manager > Unity Registry.

--------------------------------------------------------------------------------
WHAT THIS PACKAGE WRITES TO YOUR PROJECT
--------------------------------------------------------------------------------

Everything Dungeon Graph ships lives under Assets/DungeonGraph, and it does not
create files anywhere else in your project without asking first.

Two optional, explicitly-confirmed exceptions:

  Assets/AddressableAssetsData
      Unity's standard Addressables configuration folder. Dungeon Graph loads
      room prefabs through Addressables, so it offers to initialise this on
      first import. Decline and the graph editor still works; only runtime room
      loading is affected. You can set it up later from
      Tools > Dungeon Graph > Setup > Reinitialize Addressables.

  ProjectSettings/TagManager.asset
      Only if you run Tools > Dungeon Graph > Setup > Add "Dungeon" Tag. This is
      never done automatically and is only needed for scenes authored against a
      previous version; current code finds the master tilemap by component.

--------------------------------------------------------------------------------
REQUIREMENTS
--------------------------------------------------------------------------------

    Unity            6000.0.67f1 or newer
    Project type     2D (rooms are built from Unity Tilemaps)
    Render pipeline  URP or Built-in - no pipeline-specific code or shaders
    Platforms        anything Addressables supports

--------------------------------------------------------------------------------
WHAT IS IN THIS PACKAGE
--------------------------------------------------------------------------------

    Documentation/       The manual. Start at 0_Documentation_Index.md
    Scripts/Runtime/     Data model, generation algorithms, tilemap system
    Scripts/Editor/      Graph editor window, room authoring tools
    Setup/Editor/        First-run dependency wizard
    Dungeon_Graphs/      Sample dungeon layouts (Floors 1-5)
    Dungeon_Floors/      Sample room prefabs (Floor_1, Floor_2)
    Prefabs/             Master_Tilemap, Blank_Room, DungeonGenerator
    Tiles/               Plain placeholder tiles and Rule Tiles
    Scenes/              RoomStudio - a workspace for authoring rooms
    References/          Package icon

The bundled tiles are deliberately plain solid colours. They exist so the sample
rooms render out of the box; they are placeholders, not production art. See
Documentation/2_Authoring_Guide.md section 2.10 for swapping in your own.

--------------------------------------------------------------------------------
MENU REFERENCE
--------------------------------------------------------------------------------

    Assets > Create > Dungeon Graph > New Graph
    Assets > Create > Dungeon Graph > Blank Room
    Tools  > Dungeon Graph > Rooms > Save
    Tools  > Dungeon Graph > Rooms > Save As...
    Tools  > Dungeon Graph > Setup > Check Dependencies
    Tools  > Dungeon Graph > Setup > Reinitialize Addressables
    Tools  > Dungeon Graph > Setup > Add "Dungeon" Tag

--------------------------------------------------------------------------------
LICENSING
--------------------------------------------------------------------------------

Everything in this folder is original work by the publisher, Ruben Calderon, 
distributed under the Unity Asset Store EULA. This package contains no third-
party fonts, audio, images, models or source code.

See "Third-Party Notices.txt" in this folder for the full statement, including
the Unity packages this asset depends on but does not redistribute.

--------------------------------------------------------------------------------
SUPPORT
--------------------------------------------------------------------------------

Questions, bug reports and feature requests: use the Unity Asset Store product
page for Dungeon Graph.

If you are reporting a problem, Documentation/6_Troubleshooting.md section 6.7
lists the details that make it quickest to diagnose.

================================================================================
