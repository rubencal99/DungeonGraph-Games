using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace DungeonGraph.Editor
{

public static class RoomAuthoringTools
{
    // ---------- PUBLIC MENUS ----------

    /// <summary>
    /// Quick-save: re-bakes the room and overwrites the existing prefab asset.
    /// If the room is not yet saved, opens Save As... instead.
    /// </summary>
    [MenuItem("Tools/Dungeon Graph/Rooms/Save")]
    public static void BakeSaveQuick()
    {
        var go = GetSelectedRoomRootOrWarn();
        if (!go) return;

        BakeRoom(go);

        if (!PrefabUtility.IsPartOfPrefabInstance(go))
        {
            // Not yet a prefab — open the full Save As dialog
            SaveRoomAsWindow.Show(go);
            return;
        }

        var prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
        string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(prefabRoot);

        if (string.IsNullOrEmpty(assetPath))
        {
            SaveRoomAsWindow.Show(go);
            return;
        }

        SavePrefab(go, assetPath);

        // Re-register labels in case the prefab moved or labels were removed
        AddressableRoomRegistrar.TryRegisterFromPath(assetPath);
    }

    /// <summary>
    /// Save As...: bakes the room, then opens the floor/type picker dialog.
    /// </summary>
    [MenuItem("Tools/Dungeon Graph/Rooms/Save As...")]
    public static void BakeSaveAs()
    {
        var go = GetSelectedRoomRootOrWarn();
        if (!go) return;

        BakeRoom(go);
        SaveRoomAsWindow.Show(go);
    }

    // ---------- CORE HELPERS ----------

    private static GameObject GetSelectedRoomRootOrWarn()
    {
        var go = Selection.activeGameObject;
        if (!go)
        {
            Debug.LogWarning("Select your Room root GameObject in the Hierarchy.");
            return null;
        }

        if (go.GetComponentInChildren<Grid>() == null ||
            go.GetComponentsInChildren<Tilemap>(true).Length == 0)
        {
            Debug.LogError("Selected object doesn't look like a room (need a Grid and at least one Tilemap).");
            return null;
        }

        return go;
    }

    private static void BakeRoom(GameObject go)
    {
        var tms = go.GetComponentsInChildren<Tilemap>(true);
        foreach (var tm in tms)
        {
            tm.CompressBounds();

            var cb = tm.cellBounds;
            if (cb.size.x == 0 || cb.size.y == 0)
            {
                RemoveEmptyTilemap(tm);
                continue;
            }

            for (int x = cb.xMin; x < cb.xMax; x++)
            for (int y = cb.yMin; y < cb.yMax; y++)
            {
                var p = new Vector3Int(x, y, 0);
                if (tm.GetTile(p) == null) continue;
                tm.SetTileFlags(p, TileFlags.LockColor | TileFlags.LockTransform);
            }
        }

        var rt = go.GetComponent<DungeonGraph.RoomTemplate>() ?? go.AddComponent<DungeonGraph.RoomTemplate>();
        rt.Recompute();
        PopulateExits(go, rt);
    }

    /// <summary>
    /// Removes an empty (no painted tiles) tilemap layer found during baking. If its GameObject
    /// holds only the tilemap itself, the whole GameObject goes; if a room author nested
    /// decoration, lighting, or spawner content under it, only the Tilemap/TilemapRenderer are
    /// stripped so that content survives baking rather than being silently deleted.
    /// </summary>
    private static void RemoveEmptyTilemap(Tilemap tm)
    {
        var go = tm.gameObject;
        if (!HasNonTilemapContent(go))
        {
            Object.DestroyImmediate(go);
            return;
        }

        Debug.LogWarning($"[RoomAuthoringTool] '{go.name}' has an empty tilemap but also holds other " +
                          "content (children or components) — removing only the tilemap layer, not the GameObject.");
        var renderer = go.GetComponent<TilemapRenderer>();
        if (renderer != null) Object.DestroyImmediate(renderer);
        Object.DestroyImmediate(tm);
    }

    private static bool HasNonTilemapContent(GameObject go)
    {
        if (go.transform.childCount > 0) return true;

        foreach (var c in go.GetComponents<Component>())
        {
            if (c == null) continue;
            if (c is Transform || c is Tilemap || c is TilemapRenderer) continue;
            return true;
        }
        return false;
    }

    private static void PopulateExits(GameObject go, DungeonGraph.RoomTemplate rt)
    {
        Transform exitsContainer = go.transform.Find("Exits");
        if (exitsContainer == null) return;

        int childCount = exitsContainer.childCount;
        if (childCount == 0)
        {
            Debug.LogWarning("[RoomAuthoringTool] Found 'Exits' container but it has no children.");
            rt.exits = new Transform[0];
            return;
        }

        rt.exits = new Transform[childCount];
        for (int i = 0; i < childCount; i++)
            rt.exits[i] = exitsContainer.GetChild(i);
    }

    internal static void SavePrefab(GameObject go, string file)
    {
        if (PrefabUtility.IsPartOfAnyPrefab(go))
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.UserAction);

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, file);
        if (prefab != null)
        {
            EditorUtility.SetDirty(prefab);
            AssetDatabase.SaveAssets();
        }
        else
        {
            Debug.LogError($"[RoomAuthoringTool] Failed to save prefab at: {file}");
        }
    }
}

} // namespace DungeonGraph.Editor
