# Dungeon Graph — Documentation

**Version 1.0.0** · Unity 6000.0.67f1 or newer · 2D (URP or Built-in)

Dungeon Graph is an editor tool and runtime library for procedural 2D dungeon
generation. You author a graph of room types in a node editor, and the tool
instantiates your room prefabs, arranges them, bakes them into a single tilemap,
and connects them with generated corridors.

---

## Table of Contents

### 1. [Getting Started](1_Getting_Started.md)
   - 1.1 Requirements
   - 1.2 Required Unity packages
   - 1.3 Installing Dungeon Graph
   - 1.4 Verifying the installation
   - 1.5 Tutorial: your first dungeon in the Editor
   - 1.6 Tutorial: generating a dungeon at runtime
   - 1.7 What gets created in your scene

### 2. [Authoring Guide](2_Authoring_Guide.md)
   - 2.1 The graph editor window
   - 2.2 Creating and connecting nodes
   - 2.3 Built-in node types
   - 2.4 Spawn chance
   - 2.5 Custom node types
   - 2.6 Floors
   - 2.7 Authoring a room prefab
   - 2.8 Exits
   - 2.9 Saving and registering a room
   - 2.10 Using your own tile set

### 3. [Generation Styles](3_Generation_Styles.md)
   - 3.1 Choosing a style
   - 3.2 Organic (force-directed)
   - 3.3 Grid (flood-fill)
   - 3.4 Shared settings
   - 3.5 Corridors
   - 3.6 Tuning recipes

### 4. [Runtime API](4_Runtime_API.md)
   - 4.1 The DungeonGenerator component
   - 4.2 Addressables setup
   - 4.3 Triggering generation from code
   - 4.4 Reading the result
   - 4.5 Complete example
   - 4.6 Memory and cleanup

### 5. [Script Reference](5_Script_Reference.md)
   - 5.1 Runtime — data model
   - 5.2 Runtime — node types
   - 5.3 Runtime — generation
   - 5.4 Runtime — tilemap system
   - 5.5 Runtime — room components
   - 5.6 Runtime — entry points
   - 5.7 Editor — graph editor
   - 5.8 Editor — room authoring
   - 5.9 Editor — Addressables and floors

### 6. [Troubleshooting](6_Troubleshooting.md)
   - 6.1 Compile and import problems
   - 6.2 Nothing is generated
   - 6.3 Rooms overlap
   - 6.4 Corridors are missing or wrong
   - 6.5 Rooms render blank or magenta
   - 6.6 Runtime-only problems
   - 6.7 Diagnostic checklist

### 7. [Architecture](7_Architecture.md)
   - 7.1 Assemblies
   - 7.2 The generation pipeline
   - 7.3 Data flow
   - 7.4 Extending Dungeon Graph

### Appendix
   - [Third-Party Notices](../Third-Party%20Notices.txt)

---

## Where to Start

| If you want to… | Read |
| --- | --- |
| Get something on screen in 10 minutes | [1. Getting Started](1_Getting_Started.md) §1.5 |
| Draw your own dungeon layouts | [2. Authoring Guide](2_Authoring_Guide.md) §2.1–2.6 |
| Use your own art and room shapes | [2. Authoring Guide](2_Authoring_Guide.md) §2.7–2.10 |
| Control how rooms are arranged | [3. Generation Styles](3_Generation_Styles.md) |
| Generate dungeons in a build | [4. Runtime API](4_Runtime_API.md) |
| Look up a class or method | [5. Script Reference](5_Script_Reference.md) |
| Fix something that is broken | [6. Troubleshooting](6_Troubleshooting.md) |

---

## Support

Questions, bug reports and feature requests can be sent through the Unity Asset
Store product page for Dungeon Graph.
