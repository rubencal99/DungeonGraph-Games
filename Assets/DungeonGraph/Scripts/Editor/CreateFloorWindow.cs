using UnityEngine;
using UnityEditor;

namespace DungeonGraph.Editor
{
    /// <summary>
    /// Editor window for creating new dungeon floors.
    /// </summary>
    public class CreateFloorWindow : EditorWindow
    {
        private string m_floorName = "";
        private string m_helpText = "";
        private System.Func<string, bool> m_onCreateCallback;

        /// <summary>
        /// Opens the Create Floor window. <paramref name="onCreateCallback"/> receives the entered
        /// name and should return true to close the window, or false to keep it open
        /// (e.g. when validation fails and an error dialog was already shown).
        /// </summary>
        public static void ShowWindow(System.Func<string, bool> onCreateCallback,
            string windowTitle = "Create New Dungeon Floor",
            string helpText = "A new floor will be created with all standard and custom node type folders populated with blank rooms.")
        {
            var window = GetWindow<CreateFloorWindow>(true, windowTitle, true);
            window.m_floorName = "";
            window.m_helpText = helpText;
            window.m_onCreateCallback = onCreateCallback;
            window.minSize = new Vector2(350, 180);
            window.maxSize = new Vector2(350, 180);
            window.ShowUtility();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Dungeon Floor Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUILayout.BeginVertical("box");

            m_floorName = EditorGUILayout.TextField("Floor Name:", m_floorName);

            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(m_helpText, MessageType.Info);

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Buttons
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Cancel", GUILayout.Width(80)))
            {
                Close();
            }

            EditorGUI.BeginDisabledGroup(string.IsNullOrWhiteSpace(m_floorName));
            if (GUILayout.Button("Create", GUILayout.Width(80)))
            {
                bool success = m_onCreateCallback == null || m_onCreateCallback.Invoke(m_floorName);
                if (success) Close();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
        }
    }
}
