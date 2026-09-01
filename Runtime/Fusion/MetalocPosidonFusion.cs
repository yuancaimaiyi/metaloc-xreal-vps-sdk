using System.Runtime.InteropServices;
using UnityEngine;

namespace Metaloc.VPS
{
    // ── P/Invoke declarations (same ABI as the original PosidonInterface) ──

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct FusionStatus
    {
        [MarshalAs(UnmanagedType.I1)] public bool valid;
        [MarshalAs(UnmanagedType.I1)] public bool latest_is_outliner;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)] public double[] latest_translation;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public double[] latest_rotation;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)] public double[] translation_src2dst;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public double[] quaternion_src2dst;
        public double rotation_scale;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void FusionStatusCallback(FusionStatus status);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void FusionLogsCallback(string logs);

    internal static class PosidonNative
    {
#if UNITY_ANDROID
        private const string Lib = "multi_location_fusion";
        [DllImport(Lib)] public static extern int dummy(int a, int b);
        [DllImport(Lib)] public static extern bool init_system(string logPath);
        [DllImport(Lib)] public static extern bool feed_location_pair(double[] srcPos, double[] srcRot, double[] dstPos, double[] dstRot, bool isAndroid, bool sync);
        [DllImport(Lib)] public static extern void status_callback([MarshalAs(UnmanagedType.FunctionPtr)] FusionStatusCallback cb);
        [DllImport(Lib)] public static extern void logs_callback([MarshalAs(UnmanagedType.FunctionPtr)] FusionLogsCallback cb);
#else
        public static int dummy(int a, int b) { return 0; }
        public static bool init_system(string p) { Debug.LogWarning("[Posidon] Not on Android — skipping init"); return true; }
        public static bool feed_location_pair(double[] sp, double[] sr, double[] dp, double[] dr, bool a, bool s) { return true; }
        public static void status_callback(FusionStatusCallback cb) { }
        public static void logs_callback(FusionLogsCallback cb) { }
#endif
    }

    /// <summary>
    /// Wraps the Posidon multi-frame VIO/VPS fusion library.
    /// Call Initialize() once, then InsertMeasurement() for each accepted VPS result.
    /// The OnFusionAccepted callback fires synchronously from InsertMeasurement().
    /// </summary>
    internal class MetalocPosidonFusion
    {
        public delegate void FusionAccepted(Matrix4x4 vpsToUnityMatrix);
        public delegate void FusionRejected();

        public event FusionAccepted OnFusionAccepted;
        public event FusionRejected OnFusionRejected;

        private FusionStatusCallback m_StatusCb;
        private FusionLogsCallback m_LogsCb;

        // The callback is static so it can be pinned for native; we route it via the instance pointer.
        private static MetalocPosidonFusion s_instance;

        public bool Initialize(string logPath)
        {
            s_instance = this;
            PosidonNative.dummy(1, 2);
            bool ok = PosidonNative.init_system(logPath);
            if (!ok) { Debug.LogError("[MetalocPosidonFusion] init_system returned false"); return false; }

            m_StatusCb = OnNativeStatus;
            m_LogsCb = OnNativeLogs;
            PosidonNative.status_callback(m_StatusCb);
            PosidonNative.logs_callback(m_LogsCb);
            return true;
        }

        /// <summary>
        /// Feed a VPS measurement into the fusion engine.
        /// vioPos/vioRot: glasses pose in Unity VIO space at the moment the image was captured.
        /// vpsPos/vpsRot: camera pose in VPS world space returned by the server (left-hand Unity).
        /// </summary>
        public void InsertMeasurement(Vector3 vioPos, Quaternion vioRot, Vector3 vpsPos, Quaternion vpsRot)
        {
            double[] sp = { vioPos.x, vioPos.y, vioPos.z };
            double[] sr = { vioRot.x, vioRot.y, vioRot.z, vioRot.w };
            double[] dp = { vpsPos.x, vpsPos.y, vpsPos.z };
            double[] dr = { vpsRot.x, vpsRot.y, vpsRot.z, vpsRot.w };
            // is_android_device=false: XREAL VIO and VPS share the same coordinate frame; no extra 90° Z correction needed.
            PosidonNative.feed_location_pair(sp, sr, dp, dr, false, true);
        }

        [AOT.MonoPInvokeCallback(typeof(FusionStatusCallback))]
        private static void OnNativeStatus(FusionStatus status)
        {
            if (!status.valid || s_instance == null) return;

            if (status.latest_is_outliner)
            {
                Debug.LogWarning("[MetalocPosidonFusion] Outlier detected — measurement rejected");
                s_instance.OnFusionRejected?.Invoke();
                return;
            }

            // Posidon outputs T_vps←vio (src=VIO, dst=VPS). Invert to get T_unity←vps.
            Quaternion qSrc2Dst = new Quaternion(
                (float)status.quaternion_src2dst[0],
                (float)status.quaternion_src2dst[1],
                (float)status.quaternion_src2dst[2],
                (float)status.quaternion_src2dst[3]);
            Vector3 tSrc2Dst = new Vector3(
                (float)status.translation_src2dst[0],
                (float)status.translation_src2dst[1],
                (float)status.translation_src2dst[2]);

            Quaternion qInv = Quaternion.Inverse(qSrc2Dst);
            Vector3 tInv = qInv * (-tSrc2Dst);
            Matrix4x4 vpsToUnity = Matrix4x4.TRS(tInv, qInv, Vector3.one);

            s_instance.OnFusionAccepted?.Invoke(vpsToUnity);
        }

        [AOT.MonoPInvokeCallback(typeof(FusionLogsCallback))]
        private static void OnNativeLogs(string logs) { }
    }
}
