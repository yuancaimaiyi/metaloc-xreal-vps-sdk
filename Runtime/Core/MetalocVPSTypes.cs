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
