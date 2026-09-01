using UnityEngine;
using Metaloc.VPS;

/// <summary>
/// Minimal example showing how to wire up MetalocVPSManager + MetalocARContentManager.
///
/// Setup (5 steps):
/// 1. Add this script, MetalocVPSManager, and MetalocARContentManager to one persistent GameObject.
/// 2. Create a MetalocVPSConfig  asset (Assets → Create → Metaloc → VPS Config).
///    Fill in vpsApiUrl, authToken, mapName, then drag it to MetalocVPSManager.Config.
/// 3. Create a MetalocSceneManifest asset (Assets → Create → Metaloc → Scene Manifest).
///    Add one entry per AR object (assetBundleName, prefabName, vpsPosition from the mapping tool).
///    Set assetBundleSubFolder to the folder name inside persistentDataPath that holds your bundles.
///    Drag the manifest to MetalocARContentManager.Manifest.
/// 4. Build your AR Prefabs → AssetBundles → push the bundle files to the device at
///    Android: /sdcard/Android/data/<package>/files/<assetBundleSubFolder>/
///    (adb push my_bundle /sdcard/Android/data/com.yourcompany.app/files/mymap/)
/// 5. Build and run — objects appear automatically once VPS localizes.
/// </summary>
public class MetalocVPSSample : MonoBehaviour
{
    private MetalocVPSManager m_VPS;

    private void Start()
    {
        m_VPS = GetComponent<MetalocVPSManager>();
        if (m_VPS == null) { Debug.LogError("[MetalocVPSSample] MetalocVPSManager not found"); return; }

        m_VPS.OnLocalized += OnLocalized;
        m_VPS.OnLocalizationFailed += OnLocalizationFailed;

        // MetalocARContentManager is also on this GO and subscribes to OnLocalized automatically.
        // All you need to do here is start localization.
        m_VPS.StartLocalization();
    }

    private void OnLocalized(VPSLocalizationResult result)
    {
        // MetalocARContentManager already moves your objects.
        // Add any extra game logic here, e.g. enable UI, play sound, etc.
        Debug.Log($"[Sample] Localized! score={result.rawResponse.score:F3}");
    }

    private void OnLocalizationFailed(string reason)
    {
        Debug.LogWarning($"[Sample] VPS failed: {reason}");
    }

    private void OnDestroy()
    {
        if (m_VPS != null)
        {
            m_VPS.OnLocalized -= OnLocalized;
            m_VPS.OnLocalizationFailed -= OnLocalizationFailed;
        }
    }
}
