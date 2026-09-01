using System;
using System.Collections.Generic;
using UnityEngine;

namespace Metaloc.VPS
{
    /// <summary>
    /// Describes one AR object that will be placed in the scene after VPS localization.
    /// Fill these in your MetalocSceneManifest asset, or build the list at runtime.
    /// </summary>
    [Serializable]
    public class MetalocARContentEntry
    {
        [Tooltip("Unique id. Must match the Prefab/AB asset name exactly.")]
        public string id;

        [Tooltip("AssetBundle file name (without path). E.g. 'my_model_bundle'")]
        public string assetBundleName;

        [Tooltip("Prefab name inside the AssetBundle. E.g. 'MyBuilding'")]
        public string prefabName;

        [Tooltip("Object position in VPS world coordinates (same frame as the mapping tool output)")]
        public Vector3 vpsPosition;

        [Tooltip("Object euler rotation in VPS world coordinates")]
        public Vector3 vpsEulerAngles;

        [Tooltip("Local scale to apply after instantiation")]
        public Vector3 localScale = Vector3.one;
    }

    /// <summary>
    /// ScriptableObject listing every AR object for one scene.
    /// Create via Assets → Create → Metaloc → Scene Manifest.
    /// </summary>
    [CreateAssetMenu(menuName = "Metaloc/Scene Manifest", fileName = "MetalocSceneManifest")]
    public class MetalocSceneManifest : ScriptableObject
    {
        [Tooltip("Root folder on the device that holds all AssetBundle files for this scene.\n" +
                 "Android: Application.persistentDataPath + '/' + this value\n" +
                 "Leave empty to load from StreamingAssets instead.")]
        public string assetBundleSubFolder = "";

        [Tooltip("All AR content entries for this scene")]
        public List<MetalocARContentEntry> entries = new List<MetalocARContentEntry>();
    }
}
