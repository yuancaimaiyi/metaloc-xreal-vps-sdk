using System;
using UnityEngine;

namespace Metaloc.VPS
{
    [Serializable]
    public class VPSGnssData
    {
        public bool isValid;
        public float latitude;
        public float longitude;
        public float altitude;
        public float horizontalAccuracy;
        public float verticalAccuracy;
    }

    [Serializable]
    public class VPSCompassData
    {
        public bool isValid;
        public float trueHeading;
        public float headingAccuracy;
    }

    /// <summary>All data fields sent per VPS HTTP request.</summary>
    public class VPSRequestInfo
    {
        public long timestamp;
        public Vector2 focalLength;
        public Vector2Int resolution;
        public Vector2 principalPoint;
        public Vector3 unityPosition;
        public Quaternion unityRotation;
        public Vector3 priorPosition;
        public Quaternion priorRotation;
        public DeviceOrientation deviceOrientation;
        public VPSGnssData gnss;
        public VPSCompassData compass;
        /// <summary>0 = cold start, 1 = hot start (priorRotation Yaw is valid in VPS world frame)</summary>
        public int trackingState;
    }

    /// <summary>All data fields sent per stereo VPS HTTP request (body = left eye).</summary>
    public class StereoVPSRequestInfo
    {
        public long timestamp;

        // Left eye intrinsics (body frame)
        public Vector2 focalLength_left;
        public Vector2Int resolution_left;
        public Vector2 principalPoint_left;

        // Right eye intrinsics
        public Vector2 focalLength_right;
        public Vector2Int resolution_right;
        public Vector2 principalPoint_right;

        /// <summary>Unity-space pose (left-hand). Used for debug/logging on server side.</summary>
        public Vector3 unityPosition;
        public Quaternion unityRotation;

        /// <summary>Left eye VPS prior pose (right-hand). priorRotation = identity when VIO Limited.</summary>
        public Vector3 priorPosition;
        public Quaternion priorRotation;

        /// <summary>
        /// Stereo rig extrinsic: T_rightcam_leftcam in right-hand frame.
        /// Semantics: pt_rightcam = T_rightcam_leftcam · pt_leftcam (left-to-right frame transform).
        /// For ~62mm baseline, rightToLeftPosition.x ≈ -0.062 (negative, counter-intuitive).
        /// </summary>
        public Vector3 rightToLeftPosition;
        public Quaternion rightToLeftRotation;

        public DeviceOrientation deviceOrientation;
        public VPSGnssData gnss;
        public VPSCompassData compass;
        /// <summary>0 = cold start, 1 = hot start</summary>
        public int trackingState;
    }

    public enum VPSCauseValue
    {
        Success = 0,
        LocalizationFailed = 1,
        OutOfLocalizableArea = 2,
        InvalidParameter = 3,
        IllegalAppKey = 401,
        UnauthorizedAppKey = 403,
        ParseError = 404,
        InternalServerError = 500,
    }

    /// <summary>Parsed server response for a VPS request.</summary>
    [Serializable]
    public class VPSResponseData
    {
        public VPSCauseValue causeValue;
        public string description;
        public long timestamp;
        /// <summary>Camera position in VPS world frame (right-hand).</summary>
        public Vector3 translation;
        /// <summary>Camera rotation in VPS world frame (right-hand).</summary>
        public Quaternion rotation;
        public float score;
        public string mapName;
        public string mapID;
    }

    /// <summary>Result delivered to the developer after Posidon fusion accepts a VPS measurement.</summary>
    public class VPSLocalizationResult
    {
        /// <summary>Transform matrix from VPS world space to Unity world space.</summary>
        public Matrix4x4 vpsToUnityMatrix;
        /// <summary>Raw server response that produced this result.</summary>
        public VPSResponseData rawResponse;
    }
}
