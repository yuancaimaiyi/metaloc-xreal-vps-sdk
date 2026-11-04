using System;
using System.Runtime.InteropServices;
using UnityEngine;
using Unity.XR.XREAL;
using Unity.XR.XREAL.Enterprise;

namespace Metaloc.VPS
{
    /// <summary>
    /// Manages the XREAL grayscale camera: startup, intrinsic query, image capture, undistortion.
    /// One instance is created and owned by MetalocVPSManager.
    /// </summary>
    internal class MetalocGrayCameraCapture
    {
        public bool IsReady { get; private set; }

        // Mono-mode intrinsics (bound to m_Eye)
        public Vector2Int Resolution { get; private set; }
        public Vector2 FocalLength { get; private set; }
        public Vector2 PrincipalPoint { get; private set; }
        public Pose CameraToGlasses { get; private set; }
        public bool HasCameraToGlasses { get; private set; }

        // Stereo-mode intrinsics (both eyes, queried together). Available after Initialize() if useStereo = true.
        public Vector2Int Resolution_Left { get; private set; }
        public Vector2 FocalLength_Left { get; private set; }
        public Vector2 PrincipalPoint_Left { get; private set; }
        public Pose CameraToGlasses_Left { get; private set; }
        public Vector2Int Resolution_Right { get; private set; }
        public Vector2 FocalLength_Right { get; private set; }
        public Vector2 PrincipalPoint_Right { get; private set; }
        public Pose CameraToGlasses_Right { get; private set; }
        public bool HasStereoCalibration { get; private set; }

        private XREALGrayCameraTexture m_Texture;
        private readonly TargetEye m_Eye;
        private readonly bool m_Undistort;
        private readonly bool m_UseStereo;
        private byte[] m_UndistortBuffer;

        public MetalocGrayCameraCapture(TargetEye eye, bool undistort, bool useStereo = false)
        {
            m_Eye = eye;
            m_Undistort = undistort;
            m_UseStereo = useStereo;
        }

        public void Initialize()
        {
            m_Texture = XREALGrayCameraTexture.CreateSingleton();
            if (m_Texture == null)
            {
                Debug.LogError("[MetalocGrayCameraCapture] CreateSingleton returned null");
                return;
            }
            m_Texture.OnStartCapture += OnStart;
            m_Texture.OnStopCapture += OnStop;
            m_Texture.StartCapture();
        }

        public void Shutdown()
        {
            if (m_Texture == null) return;
            m_Texture.OnStartCapture -= OnStart;
            m_Texture.OnStopCapture -= OnStop;
            m_Texture.StopCapture();
            m_Texture = null;
        }

        /// <summary>
        /// Captures the current mono frame (bound to constructor m_Eye) as a JPEG byte array.
        /// Returns null if the camera is not ready. For stereo mode use CaptureEyeJPEG(eye).
        /// </summary>
        public byte[] CaptureJPEG(int jpegQuality = 95) => CaptureEyeJPEG(m_Eye, jpegQuality);

        /// <summary>
        /// Captures a specific eye's frame as a JPEG byte array. Used by stereo path.
        /// Call Left and Right in the same frame for a synchronized stereo pair; EncodeToJPG inside
        /// returns a fresh byte[] on each call so the shared undistort buffer can be reused safely.
        /// </summary>
        public byte[] CaptureEyeJPEG(TargetEye eye, int jpegQuality = 95)
        {
            if (!IsReady || m_Texture == null) return null;
            try
            {
                var textures = m_Texture.GetTextures();
                int idx = (eye == TargetEye.Left) ? 0 : 1;
                if (textures == null || textures.Length <= idx) return null;
                var src = textures[idx] as Texture2D;
                if (src == null) return null;

                RenderTexture rt = RenderTexture.GetTemporary(src.width, src.height, 0);
                Graphics.Blit(src, rt);
                Texture2D tmp = new Texture2D(src.width, src.height, TextureFormat.R8, false);
                RenderTexture.active = rt;
                tmp.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
                tmp.Apply();
                RenderTexture.active = null;
                RenderTexture.ReleaseTemporary(rt);

                byte[] raw = MetalocCoordUtils.MirrorHorizontal(tmp.GetRawTextureData(), src.width, src.height);
                UnityEngine.Object.DestroyImmediate(tmp);

                if (m_Undistort)
                    raw = Undistort(raw, eye);

                Texture2D jpgTex = new Texture2D(src.width, src.height, TextureFormat.R8, false);
                jpgTex.LoadRawTextureData(raw);
                byte[] jpeg = jpgTex.EncodeToJPG(jpegQuality);
                UnityEngine.Object.DestroyImmediate(jpgTex);
                return jpeg;
            }
            catch (Exception e)
            {
                Debug.LogError($"[MetalocGrayCameraCapture] CaptureEyeJPEG({eye}): {e.Message}");
                return null;
            }
        }

        // ── private ──────────────────────────────────────────────────────────

        private void OnStart()
        {
            IsReady = true;
            RefreshIntrinsics();
            if (m_UseStereo) RefreshStereoCalibration();
        }

        private void OnStop() => IsReady = false;

        private void RefreshIntrinsics()
        {
            QueryEyeIntrinsicsAndPose(m_Eye,
                out Vector2Int size, out Vector2 fl, out Vector2 pp,
                out Pose camToGlasses, out bool hasPose);
            Resolution = size;
            FocalLength = fl;
            PrincipalPoint = pp;
            CameraToGlasses = camToGlasses;
            HasCameraToGlasses = hasPose;
            Debug.Log($"[MetalocGrayCameraCapture] {m_Eye} eye ready: {Resolution.x}x{Resolution.y} fx={FocalLength.x:F2} fy={FocalLength.y:F2}");
        }

        private void RefreshStereoCalibration()
        {
            QueryEyeIntrinsicsAndPose(TargetEye.Left,
                out Vector2Int sizeL, out Vector2 flL, out Vector2 ppL,
                out Pose cgL, out bool hasL);
            QueryEyeIntrinsicsAndPose(TargetEye.Right,
                out Vector2Int sizeR, out Vector2 flR, out Vector2 ppR,
                out Pose cgR, out bool hasR);
            Resolution_Left = sizeL; FocalLength_Left = flL; PrincipalPoint_Left = ppL; CameraToGlasses_Left = cgL;
            Resolution_Right = sizeR; FocalLength_Right = flR; PrincipalPoint_Right = ppR; CameraToGlasses_Right = cgR;
            HasStereoCalibration = hasL && hasR;
            if (HasStereoCalibration)
            {
                Debug.Log($"[MetalocGrayCameraCapture] Stereo L: {sizeL.x}x{sizeL.y} fx={flL.x:F2} camToGlasses.pos={cgL.position}");
                Debug.Log($"[MetalocGrayCameraCapture] Stereo R: {sizeR.x}x{sizeR.y} fx={flR.x:F2} camToGlasses.pos={cgR.position}");
            }
            else
            {
                Debug.LogError($"[MetalocGrayCameraCapture] Stereo calibration query failed: okL={hasL} okR={hasR}");
            }
        }

        private static void QueryEyeIntrinsicsAndPose(TargetEye eye,
            out Vector2Int size, out Vector2 fl, out Vector2 pp, out Pose camToGlasses, out bool hasPose)
        {
            XREALComponent component = (eye == TargetEye.Left)
                ? XREALComponent.XREAL_COMPONENT_GRAYSCALE_CAMERA_LEFT
                : XREALComponent.XREAL_COMPONENT_GRAYSCALE_CAMERA_RIGHT;

            size = Vector2Int.zero;
            XREALPlugin.GetDeviceResolution(component, ref size);

            Vector2 fl_tmp = Vector2.zero, pp_tmp = Vector2.zero;
            XREALPlugin.GetCameraIntrinsic(component, ref fl_tmp, ref pp_tmp);
            fl = fl_tmp;
            pp = (size.x > 0) ? new Vector2(size.x - pp_tmp.x, pp_tmp.y) : pp_tmp;

            Pose cg = new Pose();
            hasPose = XREALPlugin.GetDevicePoseFromHead(component, ref cg);
            if (hasPose) cg.rotation = Quaternion.Normalize(cg.rotation);
            camToGlasses = cg;
        }

        private byte[] Undistort(byte[] data, TargetEye eye)
        {
            if (m_UndistortBuffer == null || m_UndistortBuffer.Length != data.Length)
                m_UndistortBuffer = new byte[data.Length];
            IntPtr ptr = Marshal.AllocHGlobal(data.Length);
            Marshal.Copy(data, 0, ptr, data.Length);
            // XREAL official convention: Left=1, Right=2
            // (see Samples~/Enterprise/GrayCameraAndCapture/GrayCameraExample.cs in Enterprise SDK package)
            // cameraId=0 is NOT a valid ID — SDK returns err=UnSupported and the output buffer stays untouched,
            // resulting in a pure-black image.
            byte cameraId = (byte)((eye == TargetEye.Left) ? 1 : 2);
            XREALEnterprisePlugin.UndistortImage(cameraId, ptr, data.Length, m_UndistortBuffer);
            Marshal.FreeHGlobal(ptr);
            return m_UndistortBuffer;
        }
    }
}
