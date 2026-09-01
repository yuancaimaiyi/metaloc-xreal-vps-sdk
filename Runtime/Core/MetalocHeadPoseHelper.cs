using System;
using System.Runtime.InteropServices;
using UnityEngine;
using Unity.XR.XREAL.Enterprise;

namespace Metaloc.VPS
{
    /// <summary>
    /// Queries the XREAL Enterprise SDK for VIO head-pose at a specific timestamp.
    /// Uses a right-hand → left-hand conversion so the result is in Unity world space.
    /// </summary>
    public static class MetalocHeadPoseHelper
    {
        private static readonly Matrix4x4 k_UnityWorld_T_GLWorld = Matrix4x4.Scale(new Vector3(1, 1, -1));

        /// <summary>
        /// Returns the glasses pose (Unity left-hand) at the given UTC millisecond timestamp.
        /// </summary>
        public static bool GetPoseAtTime(ulong timestampMs, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            try
            {
                float[] apiPose = new float[16];
                if (!XREALEnterprisePlugin.GetHeadPoseAtTime(timestampMs, apiPose))
                    return false;

                Matrix4x4 glPose = MemoryMarshal.Cast<float, Matrix4x4>(apiPose)[0];
                Matrix4x4 unityPose = k_UnityWorld_T_GLWorld * glPose * k_UnityWorld_T_GLWorld.inverse;
                position = unityPose.GetColumn(3);
                rotation = unityPose.rotation;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[MetalocHeadPoseHelper] {e.Message}");
                return false;
            }
        }
    }
}
