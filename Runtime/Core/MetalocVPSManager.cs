using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Management;
using UnityEngine.XR.ARSubsystems;
using AOT;

namespace Metaloc.VPS
{
    /// <summary>
    /// Main entry point for the Metaloc VPS SDK.
    ///
    /// Quick-start:
    /// 1. Create a MetalocVPSConfig asset (Assets → Create → Metaloc → VPS Config) and fill in your
    ///    server URL, auth token, and map name.
    /// 2. Add this component to a persistent GameObject in your scene.
    /// 3. Assign the config asset to the Config field.
    /// 4. Subscribe to OnLocalized to receive the VPS-to-Unity transform matrix.
    /// 5. Call StartLocalization() when your scene is ready.
    ///
    /// All networking parameters (URL, token, map name) are read from the Config asset at runtime.
    /// You can swap them without recompiling by editing the asset or calling SetMapName() at runtime.
    /// </summary>
    public class MetalocVPSManager : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────────

        [Tooltip("VPS configuration asset. Create via Assets → Create → Metaloc → VPS Config.")]
        public MetalocVPSConfig Config;

        // ── Public events ────────────────────────────────────────────────────

        /// <summary>
        /// Fires every time Posidon accepts a VPS measurement and the transform is updated.
        /// Provides the new VPS-to-Unity matrix and the raw server response.
        /// </summary>
        public event Action<VPSLocalizationResult> OnLocalized;

        /// <summary>Fires when a VPS request returns a server error or score rejection.</summary>
        public event Action<string> OnLocalizationFailed;

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>True after the first successful VPS localization (hot-start state).</summary>
        public bool IsLocalized => m_IsLocalized;

        /// <summary>Current VPS-to-Unity transform. Valid only after IsLocalized is true.</summary>
        public Matrix4x4 VpsToUnityMatrix => m_VpsToUnityMatrix;

        /// <summary>Override the map name at runtime without editing the config asset.</summary>
        public void SetMapName(string mapName)
        {
            if (m_Config == null) return;
            m_Config.mapName = mapName;
        }

        /// <summary>Begin polling for VPS requests. Call after scene is fully loaded.</summary>
        public void StartLocalization()
        {
            m_CanLocalize = true;
            Debug.Log("[MetalocVPSManager] Localization started");
        }

        /// <summary>Pause VPS polling without destroying any state.</summary>
        public void StopLocalization()
        {
            m_CanLocalize = false;
            Debug.Log("[MetalocVPSManager] Localization stopped");
        }

        /// <summary>
        /// Force an immediate VPS request on the next gyro-stable tick.
        /// Useful for manual retrigger buttons in your UI.
        /// </summary>
        public void TriggerVPSRequest()
        {
            if (m_IsLocalized) m_AccumulatedDisplacement = m_Config.displacementTriggerThreshold;
            else m_LastColdStartRequestTime = m_Config.coldStartIntervalSeconds;
        }

        /// <summary>
        /// Transform a point from VPS world space into Unity world space.
        /// Only valid after IsLocalized is true.
        /// </summary>
        public Vector3 VpsToUnity(Vector3 vpsPosition)
            => m_VpsToUnityMatrix.MultiplyPoint3x4(vpsPosition);

        // ── Private state ─────────────────────────────────────────────────────

        private MetalocVPSConfig m_Config;
        private MetalocGrayCameraCapture m_Camera;
        private MetalocPosidonFusion m_Fusion;
        private MetalocHttpClient m_Http;

        private Matrix4x4 m_VpsToUnityMatrix = Matrix4x4.identity;
        private Matrix4x4 m_PrevVpsToUnityMatrix = Matrix4x4.identity;
        private bool m_IsLocalized;

        private Pose m_VioSnapshotAtRequest;
        private bool m_RequestInFlight;
        private bool m_LastInsertAccepted;
        private bool m_LastWas6DoF;

        private bool m_CanLocalize;
        private float m_LastColdStartRequestTime;

        private float m_AccumulatedDisplacement;
        private Vector3 m_PrevTickPos;
        private bool m_DisplacementTrackingActive;
        private float m_LastVPSSuccessTime = -999f;

        private TrackingState m_PrevTrackingState = TrackingState.Tracking;
        private int m_StableGyroFrames;

        private UnityEngine.InputSystem.Gyroscope m_Gyroscope;

        // Static back-pointer so the static Posidon callback can dispatch to the correct instance.
        private static MetalocVPSManager s_instance;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            if (Config == null)
            {
                Debug.LogError("[MetalocVPSManager] Config is not assigned. Assign a MetalocVPSConfig asset in the Inspector.");
                enabled = false;
                return;
            }
            m_Config = Config;
            s_instance = this;
        }

        private void Start()
        {
            // Gray camera
            m_Camera = new MetalocGrayCameraCapture(m_Config.targetEye, m_Config.enableUndistortion);
            m_Camera.Initialize();

            // Posidon fusion
            m_Fusion = new MetalocPosidonFusion();
            m_Fusion.OnFusionAccepted += HandleFusionAccepted;
            m_Fusion.OnFusionRejected += HandleFusionRejected;
            string logPath = Application.persistentDataPath;
            m_Fusion.Initialize(logPath);

            // HTTP client
            m_Http = gameObject.AddComponent<MetalocHttpClient>();
            m_Http.Initialize(m_Config);

            // Input system gyroscope
            var gyro = UnityEngine.InputSystem.Gyroscope.current;
            if (gyro != null) { InputSystem.EnableDevice(gyro); m_Gyroscope = gyro; }

            // Location (for GNSS data in requests)
            if (Input.location.isEnabledByUser)
                Input.location.Start(1f);
        }

        private void Update()
        {
            if (!m_CanLocalize) return;
            CheckAndSendVPSRequest();
        }

        private void OnDestroy()
        {
            m_Camera?.Shutdown();
            StopLocalization();
        }

        // ── VPS trigger logic ─────────────────────────────────────────────────

        private void CheckAndSendVPSRequest()
        {
            TrackingState ts = GetTrackingState();

            if (ts == TrackingState.None) { m_PrevTrackingState = ts; return; }
            if (!m_IsLocalized && ts == TrackingState.Limited) { m_PrevTrackingState = ts; return; }

            // SLAM recovery jump → force immediate re-localization to correct drift
            if (m_IsLocalized && m_PrevTrackingState != TrackingState.Tracking && ts == TrackingState.Tracking)
            {
                m_AccumulatedDisplacement = m_Config.displacementTriggerThreshold;
                m_LastVPSSuccessTime = -999f;
                Debug.Log("[MetalocVPSManager] SLAM recovered → forcing re-localization");
            }
            m_PrevTrackingState = ts;

            if (!m_IsLocalized)
            {
                // Cold start: fire on timer
                m_LastColdStartRequestTime += Time.deltaTime;
                if (m_LastColdStartRequestTime >= m_Config.coldStartIntervalSeconds)
                    CheckGyroAndFire(resetColdTimer: true);
            }
            else
            {
                // Hot start: fire on displacement or time
                long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (MetalocHeadPoseHelper.GetPoseAtTime((ulong)nowMs, out Vector3 curPos, out _))
                {
                    if (m_DisplacementTrackingActive)
                        m_AccumulatedDisplacement += Vector3.Distance(curPos, m_PrevTickPos);
                    m_DisplacementTrackingActive = true;
                    m_PrevTickPos = curPos;
                }

                bool byDistance = m_AccumulatedDisplacement >= m_Config.displacementTriggerThreshold;
                bool byTime = (Time.time - m_LastVPSSuccessTime) > m_Config.maxTimeBetweenVPS;
                if (byDistance || byTime)
                    CheckGyroAndFire(resetColdTimer: false);
            }
        }

        private void CheckGyroAndFire(bool resetColdTimer)
        {
            if (m_RequestInFlight) return;
            float rotMag = m_Gyroscope?.angularVelocity.ReadValue().magnitude ?? 0f;
            if (rotMag < m_Config.gyroStabilityThreshold)
            {
                m_StableGyroFrames++;
                if (m_StableGyroFrames >= m_Config.gyroStableFrameCount)
                {
                    if (resetColdTimer) m_LastColdStartRequestTime = 0;
                    m_StableGyroFrames = 0;
                    StartCoroutine(BuildAndSendRequest());
                }
            }
            else
            {
                m_StableGyroFrames = 0;
            }
        }

        // ── Request building ──────────────────────────────────────────────────

        private IEnumerator BuildAndSendRequest()
        {
            // Snapshot timestamp before GPU work to align VIO query to the actual image moment
            long imgTs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            byte[] jpeg = m_Camera.CaptureJPEG();
            if (jpeg == null) { Debug.LogWarning("[MetalocVPSManager] CaptureJPEG returned null"); yield break; }

            if (!MetalocHeadPoseHelper.GetPoseAtTime((ulong)imgTs, out Vector3 glassesPos, out Quaternion glassesRot))
            {
                Debug.LogWarning("[MetalocVPSManager] VIO query failed — skipping request");
                yield break;
            }

            m_VioSnapshotAtRequest = new Pose(glassesPos, glassesRot);

            // Apply camera→glasses extrinsic offset
            Vector3 camPos = m_Camera.HasCameraToGlasses
                ? glassesPos + glassesRot * m_Camera.CameraToGlasses.position
                : glassesPos;
            Quaternion camRot = m_Camera.HasCameraToGlasses
                ? glassesRot * m_Camera.CameraToGlasses.rotation
                : glassesRot;

            Pose unityCam = new Pose(camPos, camRot);
            Pose rhCam = MetalocCoordUtils.LeftHandToRightHand(unityCam);

            // Limited SLAM → 6DoF (identity priorRotation, server falls back to classic PnP)
            bool use6DoF = (GetTrackingState() == TrackingState.Limited);
            m_LastWas6DoF = use6DoF;

            VPSRequestInfo info = new VPSRequestInfo
            {
                timestamp = imgTs,
                focalLength = m_Camera.FocalLength,
                resolution = m_Camera.Resolution,
                principalPoint = m_Camera.PrincipalPoint,
                unityPosition = camPos,
                unityRotation = camRot,
                priorPosition = rhCam.position,
                priorRotation = use6DoF ? new Quaternion(0f, 0f, 0f, 1f) : rhCam.rotation,
                deviceOrientation = Input.deviceOrientation,
                gnss = BuildGnssData(),
                compass = BuildCompassData(),
                trackingState = m_IsLocalized ? 1 : 0
            };

            m_RequestInFlight = true;
            m_Http.SendVPSRequest(info, jpeg, OnVPSSuccess, OnVPSError);
        }

        // ── Response handling ─────────────────────────────────────────────────

        private void OnVPSSuccess(string responseJson)
        {
            m_RequestInFlight = false;
            VPSResponseData data = null;
            try { data = JsonUtility.FromJson<VPSResponseData>(responseJson); }
            catch (Exception e) { Debug.LogError($"[MetalocVPSManager] JSON parse error: {e.Message}"); return; }

            if (data == null || data.causeValue != VPSCauseValue.Success)
            {
                string reason = data?.description ?? "unknown";
                Debug.LogWarning($"[MetalocVPSManager] VPS rejected: {reason}");
                OnLocalizationFailed?.Invoke(reason);
                return;
            }

            // 4DoF result: apply score threshold; 6DoF result: trust Posidon to filter
            if (!m_LastWas6DoF && data.score < m_Config.scoreThreshold)
            {
                Debug.LogWarning($"[MetalocVPSManager] Score {data.score:F3} below threshold {m_Config.scoreThreshold}");
                OnLocalizationFailed?.Invoke($"Low score: {data.score:F3}");
                return;
            }

            // Convert server result (right-hand) to Unity left-hand
            Pose cameraInVps_LH = MetalocCoordUtils.RightHandToLeftHand(new Pose(data.translation, data.rotation));

            // Apply inverse camera→glasses extrinsic to get glasses pose in VPS space
            Pose glassesInVps_LH = cameraInVps_LH;
            if (m_Camera.HasCameraToGlasses)
            {
                Pose glassesToCamera = MetalocCoordUtils.InversePose(m_Camera.CameraToGlasses);
                glassesInVps_LH = MetalocCoordUtils.MultiplyPose(cameraInVps_LH, glassesToCamera);
            }

            // Candidate VPS→Unity matrix (may be overridden by Posidon)
            m_PrevVpsToUnityMatrix = m_VpsToUnityMatrix;
            m_VpsToUnityMatrix =
                Matrix4x4.TRS(m_VioSnapshotAtRequest.position, m_VioSnapshotAtRequest.rotation, Vector3.one)
                * Matrix4x4.TRS(glassesInVps_LH.position, glassesInVps_LH.rotation, Vector3.one).inverse;
            m_IsLocalized = true;
            m_LastInsertAccepted = false;

            // Feed to Posidon — fires OnFusionAccepted / OnFusionRejected synchronously
            m_Fusion.InsertMeasurement(
                m_VioSnapshotAtRequest.position, m_VioSnapshotAtRequest.rotation,
                glassesInVps_LH.position, glassesInVps_LH.rotation);

            // If Posidon rejected and we were already localized, roll back the candidate matrix
            if (!m_LastInsertAccepted && m_IsLocalized)
            {
                m_VpsToUnityMatrix = m_PrevVpsToUnityMatrix;
                Debug.LogWarning("[MetalocVPSManager] Posidon rejected → matrix rolled back");
                return;
            }

            OnLocalized?.Invoke(new VPSLocalizationResult
            {
                vpsToUnityMatrix = m_VpsToUnityMatrix,
                rawResponse = data
            });
        }

        private void OnVPSError(string error, long code)
        {
            m_RequestInFlight = false;
            Debug.LogError($"[MetalocVPSManager] HTTP error {code}: {error}");
            OnLocalizationFailed?.Invoke($"HTTP {code}: {error}");
        }

        // ── Posidon callbacks (called synchronously from InsertMeasurement) ───

        private void HandleFusionAccepted(Matrix4x4 vpsToUnity)
        {
            m_VpsToUnityMatrix = vpsToUnity;
            m_LastInsertAccepted = true;
            m_AccumulatedDisplacement = 0f;
            m_DisplacementTrackingActive = false;
            m_StableGyroFrames = 0;
            m_LastVPSSuccessTime = Time.time;
            Debug.Log($"[MetalocVPSManager] Posidon accepted fusion, t={vpsToUnity.GetColumn(3)}");
        }

        private void HandleFusionRejected()
        {
            m_LastInsertAccepted = false;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static TrackingState GetTrackingState()
        {
            var sub = XRGeneralSettings.Instance?.Manager?.activeLoader
                          ?.GetLoadedSubsystem<XRSessionSubsystem>();
            return sub?.trackingState ?? TrackingState.None;
        }

        private VPSGnssData BuildGnssData()
        {
            if (Input.location.status == LocationServiceStatus.Running)
            {
                var loc = Input.location.lastData;
                return new VPSGnssData { isValid = true, latitude = loc.latitude, longitude = loc.longitude };
            }
            return new VPSGnssData { isValid = false };
        }

        private VPSCompassData BuildCompassData()
        {
            return new VPSCompassData
            {
                isValid = Input.compass.enabled,
                trueHeading = Input.compass.trueHeading,
                headingAccuracy = Input.compass.headingAccuracy
            };
        }
    }
}
