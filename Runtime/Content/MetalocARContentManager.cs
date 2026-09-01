using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Metaloc.VPS
{
    /// <summary>
    /// Loads AssetBundle-packaged AR content and places it in the scene after VPS localization.
    ///
    /// Workflow for the developer:
    ///   1. Build AR content as Prefabs in Unity.
    ///   2. Package each Prefab into an AssetBundle (Build → AssetBundles).
    ///   3. Put the generated bundle files into one folder on the device
    ///      (e.g. push to Android: persistentDataPath/{mapName}/).
    ///   4. Create a MetalocSceneManifest asset listing each entry:
    ///         id, assetBundleName, prefabName, vpsPosition, vpsEulerAngles
    ///      — vpsPosition/vpsEulerAngles come from the Metaloc Mapping Studio export.
    ///   5. Assign MetalocVPSManager + MetalocARContentManager to the same GameObject.
    ///      Assign your manifest to the Manifest field.
    ///   6. Done — objects appear automatically after first VPS localization.
    /// </summary>
    public class MetalocARContentManager : MonoBehaviour
    {
        [Tooltip("Scene manifest describing every AR object and its VPS-space position.")]
        public MetalocSceneManifest Manifest;

        [Tooltip("Smoothly Lerp objects to their new position on re-localization (hot-start correction). " +
                 "First localization always jumps instantly regardless.")]
        public bool smoothCorrection = true;

        [Tooltip("Lerp duration in seconds for smooth correction.")]
        public float smoothDuration = 0.5f;

        // ── private ──────────────────────────────────────────────────────────

        private MetalocVPSManager m_Manager;
        private Transform m_WorldRoot;
        private bool m_IsFirstLocalization = true;

        // id → loaded GameObject
        private readonly Dictionary<string, GameObject> m_Objects = new Dictionary<string, GameObject>();
        // bundleName → loaded AssetBundle
        private readonly Dictionary<string, AssetBundle> m_Bundles = new Dictionary<string, AssetBundle>();

        private string m_BundleBasePath;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Start()
        {
            m_Manager = GetComponent<MetalocVPSManager>();
            if (m_Manager == null)
            {
                Debug.LogError("[MetalocARContentManager] MetalocVPSManager not found on the same GameObject.");
                return;
            }
            if (Manifest == null)
            {
                Debug.LogError("[MetalocARContentManager] Manifest is not assigned.");
                return;
            }

            m_WorldRoot = new GameObject("MetalocWorldRoot").transform;
            DontDestroyOnLoad(m_WorldRoot.gameObject);

            // Resolve bundle base path
            string sub = Manifest.assetBundleSubFolder;
#if UNITY_ANDROID && !UNITY_EDITOR
            m_BundleBasePath = string.IsNullOrEmpty(sub)
                ? Application.streamingAssetsPath
                : Path.Combine(Application.persistentDataPath, sub);
#else
            m_BundleBasePath = string.IsNullOrEmpty(sub)
                ? Application.streamingAssetsPath
                : Path.Combine(Application.streamingAssetsPath, sub);
#endif

            Debug.Log($"[MetalocARContentManager] Bundle base path: {m_BundleBasePath}");

            // Pre-load all bundles and instantiate objects (hidden until localized)
            StartCoroutine(PreloadContent());

            m_Manager.OnLocalized += OnLocalized;
        }

        private void OnDestroy()
        {
            if (m_Manager != null) m_Manager.OnLocalized -= OnLocalized;
            foreach (var go in m_Objects.Values)
                if (go != null) Destroy(go);
            m_Objects.Clear();
            UnloadAllBundles();
            if (m_WorldRoot != null) Destroy(m_WorldRoot.gameObject);
        }

        // ── Pre-load ──────────────────────────────────────────────────────────

        private IEnumerator PreloadContent()
        {
            foreach (var entry in Manifest.entries)
            {
                yield return LoadEntry(entry);
            }
            Debug.Log($"[MetalocARContentManager] Pre-load done: {m_Objects.Count} objects ready (hidden)");
        }

        private IEnumerator LoadEntry(MetalocARContentEntry entry)
        {
            if (!m_Bundles.TryGetValue(entry.assetBundleName, out AssetBundle bundle))
            {
                string bundlePath = Path.Combine(m_BundleBasePath, entry.assetBundleName);
                var bundleReq = AssetBundle.LoadFromFileAsync(bundlePath);
                yield return bundleReq;

                bundle = bundleReq.assetBundle;
                if (bundle == null)
                {
                    Debug.LogError($"[MetalocARContentManager] Failed to load bundle: {bundlePath}");
                    yield break;
                }
                m_Bundles[entry.assetBundleName] = bundle;
            }

            var assetReq = bundle.LoadAssetAsync<GameObject>(entry.prefabName);
            yield return assetReq;

            var prefab = assetReq.asset as GameObject;
            if (prefab == null)
            {
                Debug.LogError($"[MetalocARContentManager] Prefab '{entry.prefabName}' not found in bundle '{entry.assetBundleName}'");
                yield break;
            }

            var go = Instantiate(prefab, m_WorldRoot);
            go.name = entry.id;
            go.transform.localScale = entry.localScale;
            go.SetActive(false);   // hidden until VPS localizes
            m_Objects[entry.id] = go;
        }

        // ── VPS callback ──────────────────────────────────────────────────────

        private void OnLocalized(VPSLocalizationResult result)
        {
            bool firstTime = m_IsFirstLocalization;
            m_IsFirstLocalization = false;

            foreach (var entry in Manifest.entries)
            {
                if (!m_Objects.TryGetValue(entry.id, out GameObject go) || go == null) continue;

                Vector3 worldPos = result.vpsToUnityMatrix.MultiplyPoint3x4(entry.vpsPosition);
                Quaternion worldRot = result.vpsToUnityMatrix.rotation * Quaternion.Euler(entry.vpsEulerAngles);

                if (firstTime || !smoothCorrection)
                {
                    go.transform.position = worldPos;
                    go.transform.rotation = worldRot;
                }
                else
                {
                    StartCoroutine(SmoothMove(go.transform, worldPos, worldRot, smoothDuration));
                }

                if (!go.activeSelf) go.SetActive(true);
            }
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Reload the scene: destroy all current objects, reload bundles, hide until next VPS.
        /// Call this when switching maps.
        /// </summary>
        public void ReloadScene()
        {
            foreach (var go in m_Objects.Values)
                if (go != null) Destroy(go);
            m_Objects.Clear();
            UnloadAllBundles();
            m_IsFirstLocalization = true;
            StartCoroutine(PreloadContent());
        }

        // ── private helpers ───────────────────────────────────────────────────

        private IEnumerator SmoothMove(Transform t, Vector3 targetPos, Quaternion targetRot, float duration)
        {
            if (t == null) yield break;
            Vector3 startPos = t.position;
            Quaternion startRot = t.rotation;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (t == null) yield break;
                float p = elapsed / duration;
                t.position = Vector3.Lerp(startPos, targetPos, p);
                t.rotation = Quaternion.Slerp(startRot, targetRot, p);
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (t != null)
            {
                t.position = targetPos;
                t.rotation = targetRot;
            }
        }

        private void UnloadAllBundles()
        {
            foreach (var bundle in m_Bundles.Values)
                bundle?.Unload(false);
            m_Bundles.Clear();
        }
    }
}
