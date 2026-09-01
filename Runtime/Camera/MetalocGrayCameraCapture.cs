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
        public Vector2Int Resolution { get; private set; }
        public Vector2 FocalLength { get; private set; }
        public Vector2 PrincipalPoint { get; private set; }
        public Pose CameraToGlasses { get; private set; }
        public bool HasCameraToGlasses { get; private set; }

        private XREALGrayCameraTexture m_Texture;
        private readonly TargetEye m_Eye;
        private readonly bool m_Undistort;
        private byte[] m_UndistortBuffer;

        public MetalocGrayCameraCapture(TargetEye eye, bool undistort)
        {
            m_Eye = eye;
            m_Undistort = undistort;
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
        /// Captures the current frame as a JPEG byte array.
        /// Returns null if the camera is not ready.
        /// </summary>
        public byte[] CaptureJPEG(int jpegQuality = 95)
        {
            if (!IsReady || m_Texture == null) return null;
            try
            {
                var textures = m_Texture.GetTextures();
                int idx = (m_Eye == TargetEye.Left) ? 0 : 1;
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
                    raw = Undistort(raw);

                Texture2D jpgTex = new Texture2D(src.width, src.height, TextureFormat.R8, false);
                jpgTex.LoadRawTextureData(raw);
                byte[] jpeg = jpgTex.EncodeToJPG(jpegQuality);
                UnityEngine.Object.DestroyImmediate(jpgTex);
                return jpeg;
            }
            catch (Exception e)
            {
                Debug.LogError($"[MetalocGrayCameraCapture] CaptureJPEG: {e.Message}");
                return null;
            }
        }

        // ── private ──────────────────────────────────────────────────────────

        private void OnStart()
        {
            IsReady = true;
            RefreshIntrinsics();
        }

        private void OnStop() => IsReady = false;

        private void RefreshIntrinsics()
        {
            XREALComponent component = (m_Eye == TargetEye.Left)
                ? XREALComponent.XREAL_COMPONENT_GRAYSCALE_CAMERA_LEFT
                : XREALComponent.XREAL_COMPONENT_GRAYSCALE_CAMERA_RIGHT;

            Vector2Int size = Vector2Int.zero;
            XREALPlugin.GetDeviceResolution(component, ref size);
            Resolution = size;

            Vector2 fl = Vector2.zero, pp = Vector2.zero;
            XREALPlugin.GetCameraIntrinsic(component, ref fl, ref pp);
            FocalLength = fl;
            PrincipalPoint = (size.x > 0) ? new Vector2(size.x - pp.x, pp.y) : pp;

            Pose camToGlasses = new Pose();
            if (XREALPlugin.GetDevicePoseFromHead(component, ref camToGlasses))
            {
                camToGlasses.rotation = Quaternion.Normalize(camToGlasses.rotation);
                CameraToGlasses = camToGlasses;
                HasCameraToGlasses = true;
            }

            Debug.Log($"[MetalocGrayCameraCapture] {m_Eye} eye ready: {Resolution.x}x{Resolution.y} fx={FocalLength.x:F2} fy={FocalLength.y:F2}");
        }

        private byte[] Undistort(byte[] data)
        {
            if (m_UndistortBuffer == null || m_UndistortBuffer.Length != data.Length)
                m_UndistortBuffer = new byte[data.Length];
            IntPtr ptr = Marshal.AllocHGlobal(data.Length);
            Marshal.Copy(data, 0, ptr, data.Length);
            byte cameraId = (byte)((m_Eye == TargetEye.Left) ? 0 : 1);
            XREALEnterprisePlugin.UndistortImage(cameraId, ptr, data.Length, m_UndistortBuffer);
            Marshal.FreeHGlobal(ptr);
            return m_UndistortBuffer;
        }
    }
}
