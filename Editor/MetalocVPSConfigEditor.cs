#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Metaloc.VPS.Editor
{
    [CustomEditor(typeof(MetalocVPSConfig))]
    public class MetalocVPSConfigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "Fill in your Metaloc VPS credentials here.\n" +
                "• vpsApiUrl / qrcodeApiUrl — server endpoints (can be changed without recompile)\n" +
                "• authToken — Bearer token supplied by Metaloc\n" +
                "• mapName   — the map identifier you registered on the VPS server",
                MessageType.Info);

            DrawDefaultInspector();

            EditorGUILayout.Space();
            var cfg = (MetalocVPSConfig)target;
            if (string.IsNullOrWhiteSpace(cfg.authToken) || cfg.authToken.StartsWith("Bearer <"))
            {
                EditorGUILayout.HelpBox("Auth token is not set. The VPS server will reject requests.", MessageType.Warning);
            }
            if (string.IsNullOrWhiteSpace(cfg.mapName) || cfg.mapName == "your_map_name")
            {
                EditorGUILayout.HelpBox("Map name is not set. Set it to the map ID registered on the server.", MessageType.Warning);
            }
        }
    }
}
#endif
