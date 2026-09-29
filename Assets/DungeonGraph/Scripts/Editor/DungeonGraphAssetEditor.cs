using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace DungeonGraph.Editor
{
    [CustomEditor(typeof(DungeonGraphAsset))]
    public class DungeonGraphAssetEditor : UnityEditor.Editor
    {
        // Unity 6.3 replaced instance IDs with EntityId in the OnOpenAsset signature
        // (the int overloads become compile errors in 6.6).
        [OnOpenAsset]
#if UNITY_6000_3_OR_NEWER
        public static bool OnOpenAsset(EntityId entityId, int index)
        {
            Object asset = EditorUtility.EntityIdToObject(entityId);
#else
        public static bool OnOpenAsset(int instanceId, int index)
        {
            Object asset = EditorUtility.InstanceIDToObject(instanceId);
#endif
            if (asset == null)
            {
                return false;
            }

            if (asset is DungeonGraphAsset dungeonGraph)
            {
                DungeonGraphEditorWindow.Open(dungeonGraph);
                return true;
            }
            return false;
        }
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("Open"))
            {
                DungeonGraphEditorWindow.Open((DungeonGraphAsset)target);
            }
        }
    }
}
