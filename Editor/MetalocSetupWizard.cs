#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Metaloc.VPS;

namespace Metaloc.VPS.Editor
{
    /// <summary>
    /// One-click Editor helper that provisions a working MetalocVPS GameObject in the
    /// current scene, so first-time integrators don't have to hand-wire components.
    /// </summary>
    public static class MetalocSetupWizard
    {
        private const string kMenu = "Metaloc/Create Sample Setup In Scene";
        private const string kGoName = "MetalocVPS";

        [MenuItem(kMenu, priority = 10)]
        public static void CreateSetupInScene()
        {
            var existing = GameObject.Find(kGoName);
            if (existing != null)
            {
                Selection.activeGameObject = existing;
                EditorGUIUtility.PingObject(existing);
                EditorUtility.DisplayDialog(
                    "Metaloc VPS",
                    $"'{kGoName}' already exists in the current scene. Selected it in the Hierarchy.",
                    "OK");
                return;
            }

            var go = new GameObject(kGoName);
            go.AddComponent<MetalocVPSManager>();
            go.AddComponent<MetalocARContentManager>();

            Undo.RegisterCreatedObjectUndo(go, "Create Metaloc VPS Setup");
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);

            EditorUtility.DisplayDialog(
                "Metaloc VPS",
                "Created '" + kGoName + "' with MetalocVPSManager + MetalocARContentManager.\n\n" +
                "Next:\n" +
                "1. Assets → Create → Metaloc → VPS Config, fill in URL/token/mapName, drag onto MetalocVPSManager.Config.\n" +
                "2. Assets → Create → Metaloc → Scene Manifest, add your AR entries, drag onto MetalocARContentManager.Manifest.\n" +
                "3. Ensure your scene has the XREAL Camera Rig (Main Camera under XREAL Session).\n" +
                "4. Build & Run.",
                "Got it");
        }
    }
}
#endif
