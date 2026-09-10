using UnityEngine;
using Metaloc.VPS;

/// <summary>
/// Minimal example: drop this script onto ANY empty GameObject in a scene that has
/// XREAL Rig set up (Main Camera + XREAL Session). RequireComponent auto-adds the
/// VPS manager and AR content manager. You still need to:
///
/// 1. Create a MetalocVPSConfig asset  (Assets → Create → Metaloc → VPS Config)
///    and assign it to MetalocVPSManager.Config in the Inspector.
/// 2. Create a MetalocSceneManifest asset (Assets → Create → Metaloc → Scene Manifest)
///    with your AR object list, and assign to MetalocARContentManager.Manifest.
/// 3. adb push your AssetBundles to
///    /sdcard/Android/data/&lt;package&gt;/files/&lt;assetBundleSubFolder&gt;/
/// 4. Build APK and run — objects appear once VPS localizes.
///
/// Prefer the one-click setup: Menu → Metaloc → Create Sample Setup In Scene.
/// </summary>
[RequireComponent(typeof(MetalocVPSManager), typeof(MetalocARContentManager))]
public class MetalocVPSSample : MonoBehaviour
{
    private MetalocVPSManager m_VPS;

    private void Start()
    {
        m_VPS = GetComponent<MetalocVPSManager>();
        m_VPS.OnLocalized += OnLocalized;
        m_VPS.OnLocalizationFailed += OnLocalizationFailed;
        m_VPS.StartLocalization();
    }

    private void OnLocalized(VPSLocalizationResult result)
    {
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
