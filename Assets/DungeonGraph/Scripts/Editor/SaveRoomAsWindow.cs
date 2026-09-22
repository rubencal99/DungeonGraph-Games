using System.IO;
using UnityEditor;
using UnityEngine;
using DungeonGraph;

namespace DungeonGraph.Editor
{
    /// <summary>
    /// Modal editor window that lets the user pick a floor and room type via dropdowns,
    /// then saves the room prefab to the correct Dungeon_Floors subfolder and registers
    /// it in Addressables with the appropriate labels.
    /// </summary>
    public class SaveRoomAsWindow : EditorWindow
    {
        private GameObject          m_roomGo;
        private RoomTypeInfo[]      m_roomTypes;
        private DungeonFloorConfig[] m_floors;
        private string[]            m_floorNames;
        private string[]            m_typeNames;

        private int  m_selectedFloorIndex = 0;
        private int  m_selectedTypeIndex  = 0;
        private string m_prefabName       = "Room";

        // -------------------------------------------------------------------------

        /// <summary>
        /// Opens the Save Room As window for <paramref name="roomGo"/>.
        /// The caller is responsible for baking the room before calling this.
        /// </summary>
        public static void Show(GameObject roomGo)
        {
            var window = GetWindow<SaveRoomAsWindow>(utility: true, title: "Save Room As", focus: true);
            window.Init(roomGo);
            window.minSize = new Vector2(380, 200);
            window.maxSize = new Vector2(380, 200);
            window.ShowUtility();
        }

        private void Init(GameObject roomGo)
        {
            m_roomGo    = roomGo;
            m_prefabName = roomGo != null ? roomGo.name : "Room";
            m_roomTypes  = AddressableRoomRegistrar.GetAllRoomTypes();
            m_floors     = DungeonFloorManager.GetAllFloors().ToArray();

            m_floorNames = new string[m_floors.Length];
            for (int i = 0; i < m_floors.Length; i++)
                m_floorNames[i] = m_floors[i].floorName;

            m_typeNames = new string[m_roomTypes.Length];
            for (int i = 0; i < m_roomTypes.Length; i++)
                m_typeNames[i] = m_roomTypes[i].displayName;

            // Pre-select floor and type by inferring from current path if already a prefab
            if (roomGo != null && PrefabUtility.IsPartOfPrefabInstance(roomGo))
            {
                string existingPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
                    PrefabUtility.GetOutermostPrefabInstanceRoot(roomGo));

                if (AddressableRoomRegistrar.TryGetLabelsFromPath(existingPath, out string floorLabel, out string typeLabel))
                {
                    for (int i = 0; i < m_floors.Length; i++)
                        if (m_floors[i].floorName == floorLabel) { m_selectedFloorIndex = i; break; }
                    for (int i = 0; i < m_roomTypes.Length; i++)
                        if (m_roomTypes[i].addressableLabel == typeLabel) { m_selectedTypeIndex = i; break; }
                }
            }
        }

        private void OnGUI()
        {
            if (m_roomGo == null)
            {
                EditorGUILayout.HelpBox("Room GameObject was lost. Close and retry.", MessageType.Error);
                return;
            }

            EditorGUILayout.Space(8);

            if (m_floors.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "No floors found in Assets/DungeonGraph/Dungeon_Floors.\n" +
                    "Create a floor first via the Dungeon Tools panel.",
                    MessageType.Warning);
                if (GUILayout.Button("Close")) Close();
                return;
            }

            EditorGUILayout.LabelField("Save Room", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            EditorGUI.BeginChangeCheck();

            m_selectedFloorIndex = EditorGUILayout.Popup("Floor", m_selectedFloorIndex, m_floorNames);
            m_selectedTypeIndex  = EditorGUILayout.Popup("Room Type", m_selectedTypeIndex, m_typeNames);
            m_prefabName         = EditorGUILayout.TextField("Prefab Name", m_prefabName);

            EditorGUILayout.Space(4);

            // Preview the derived save path
            string savePath = GetSavePath();
            EditorGUILayout.LabelField("Save Path", EditorStyles.miniLabel);
            EditorGUILayout.SelectableLabel(savePath, EditorStyles.helpBox, GUILayout.Height(28));

            EditorGUILayout.Space(8);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Cancel", GUILayout.Width(80))) Close();

            bool canSave = m_floors.Length > 0 && !string.IsNullOrWhiteSpace(m_prefabName);
            EditorGUI.BeginDisabledGroup(!canSave);
            if (GUILayout.Button("Save & Register", GUILayout.Width(120)))
            {
                SaveAndRegister();
                Close();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
        }

        private string GetSavePath()
        {
            if (m_floors.Length == 0 || m_roomTypes.Length == 0) return "";
            var floor    = m_floors[m_selectedFloorIndex];
            var roomType = m_roomTypes[m_selectedTypeIndex];
            string name  = string.IsNullOrWhiteSpace(m_prefabName) ? "Room" : m_prefabName.Trim();
            return $"{floor.folderPath}/{roomType.folderPath}/{name}.prefab";
        }

        private void SaveAndRegister()
        {
            var floor    = m_floors[m_selectedFloorIndex];
            var roomType = m_roomTypes[m_selectedTypeIndex];
            string name  = string.IsNullOrWhiteSpace(m_prefabName) ? "Room" : m_prefabName.Trim();

            string folderPath = $"{floor.folderPath}/{roomType.folderPath}";
            EnsureFolderExists(folderPath);

            string assetPath = $"{folderPath}/{name}.prefab";

            if (AssetDatabase.LoadAssetAtPath<Object>(assetPath) != null)
            {
                bool overwrite = EditorUtility.DisplayDialog(
                    "Overwrite Existing Room?",
                    $"A prefab already exists at:\n{assetPath}\n\nOverwrite it?",
                    "Overwrite",
                    "Cancel");

                if (!overwrite) return;
            }

            // Unpack any existing prefab connection so we get a clean asset
            if (PrefabUtility.IsPartOfAnyPrefab(m_roomGo))
                PrefabUtility.UnpackPrefabInstance(m_roomGo, PrefabUnpackMode.Completely, InteractionMode.UserAction);

            var prefab = PrefabUtility.SaveAsPrefabAsset(m_roomGo, assetPath);
            if (prefab == null)
            {
                Debug.LogError($"[SaveRoomAsWindow] Failed to save prefab at {assetPath}");
                return;
            }

            EditorUtility.SetDirty(prefab);
            AssetDatabase.SaveAssets();

            AddressableRoomRegistrar.RegisterRoomPrefab(assetPath, floor.floorName, roomType.addressableLabel);

            EditorGUIUtility.PingObject(prefab);
            Debug.Log($"[SaveRoomAsWindow] Saved and registered: {assetPath} " +
                      $"[{floor.floorName}, {roomType.addressableLabel}]");
        }

        private static void EnsureFolderExists(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;

            string parent = Path.GetDirectoryName(folderPath)!.Replace("\\", "/");
            string child  = Path.GetFileName(folderPath);

            EnsureFolderExists(parent);
            AssetDatabase.CreateFolder(parent, child);
        }
    }
}
