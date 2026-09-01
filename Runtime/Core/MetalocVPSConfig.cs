using UnityEngine;

namespace Metaloc.VPS
{
    /// <summary>
    /// All runtime-configurable parameters for the Metaloc VPS service.
    /// Create via Assets → Create → Metaloc → VPS Config, then assign to MetalocVPSManager.
    /// </summary>
    [CreateAssetMenu(menuName = "Metaloc/VPS Config", fileName = "MetalocVPSConfig")]
    public class MetalocVPSConfig : ScriptableObject
    {
        [Header("Server Endpoints")]
        [Tooltip("Full URL for the standard VPS single-image endpoint")]
        public string vpsApiUrl = "https://vps.zybervision.cn/wayzoom/v1.2/vps/single";

        [Tooltip("Full URL for the QR-code assisted VPS endpoint")]
        public string qrcodeApiUrl = "https://vps.zybervision.cn/wayzoom/v1.2/vps/single/qrcode";

        [Header("Authentication")]
        [Tooltip("Bearer token for Authorization header")]
        public string authToken = "Bearer <your_token_here>";

        [Tooltip("Cookie header value (leave empty if not required)")]
        public string cookieHeader = "";

        [Header("Map")]
        [Tooltip("Map identifier registered on the Metaloc VPS server")]
        public string mapName = "your_map_name";

        [Header("Request Parameters")]
        [Tooltip("Device type identifier sent to VPS server (4 = XREAL Ultra)")]
        public int deviceType = 4;

        [Tooltip("Map orientation (1 = landscape capture, 2 = portrait capture)")]
        public int mapOrientation = 2;

        [Header("Localization Triggers")]
        [Tooltip("Time interval (seconds) between VPS requests during cold start (before first localization)")]
        public float coldStartIntervalSeconds = 5f;

        [Tooltip("Accumulated VIO displacement (metres) before a hot-start VPS request fires")]
        public float displacementTriggerThreshold = 5f;

        [Tooltip("Maximum time (seconds) between successful VPS updates during hot-start")]
        public float maxTimeBetweenVPS = 60f;

        [Tooltip("Minimum score to accept a 4DoF VPS result (6DoF skips this filter)")]
        [Range(0f, 1f)]
        public float scoreThreshold = 0.75f;

        [Header("Camera")]
        [Tooltip("Which grayscale camera to use for VPS image capture")]
        public TargetEye targetEye = TargetEye.Right;

        [Tooltip("Apply lens undistortion before sending the image to the server")]
        public bool enableUndistortion = true;

        [Header("Gyro Stability Gate")]
        [Tooltip("Angular velocity magnitude (rad/s) below which the device is considered stable")]
        public float gyroStabilityThreshold = 0.35f;

        [Tooltip("Number of consecutive stable gyro frames required before firing a request")]
        public int gyroStableFrameCount = 5;
    }

    public enum TargetEye { Left, Right }
}
