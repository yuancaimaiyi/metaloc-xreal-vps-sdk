using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;

namespace Metaloc.VPS
{
    /// <summary>
    /// Sends multipart HTTP requests to the Metaloc VPS server.
    /// All endpoints, tokens, and field values come from MetalocVPSConfig — nothing is hardcoded.
    /// Attach this to the same GameObject as MetalocVPSManager.
    /// </summary>
    internal class MetalocHttpClient : MonoBehaviour
    {
        private MetalocVPSConfig m_Config;
        private string m_UUID;

        public void Initialize(MetalocVPSConfig config)
        {
            m_Config = config;
            m_UUID = SystemInfo.deviceUniqueIdentifier;
        }

        public void SendVPSRequest(VPSRequestInfo info, byte[] imageJpeg,
                                   Action<string> onSuccess, Action<string, long> onError)
        {
            StartCoroutine(VPSRequestCoroutine(info, imageJpeg, onSuccess, onError));
        }

        public void SendStereoVPSRequest(StereoVPSRequestInfo info,
                                         byte[] leftJpeg, byte[] rightJpeg,
                                         Action<string> onSuccess, Action<string, long> onError)
        {
            StartCoroutine(StereoVPSRequestCoroutine(info, leftJpeg, rightJpeg, onSuccess, onError));
        }

        private IEnumerator VPSRequestCoroutine(VPSRequestInfo info, byte[] imageJpeg,
                                                 Action<string> onSuccess, Action<string, long> onError)
        {
            string json = BuildVPSJson(info);
            Debug.Log($"[MetalocHttpClient] POST {m_Config.vpsApiUrl}\n{json}");

            var fields = new Dictionary<string, string> { { "data", json } };
            yield return PostMultipart(m_Config.vpsApiUrl, fields,
                                       "file", imageJpeg, "image.jpg", "image/jpeg",
                                       onSuccess, onError);
        }

        private string BuildVPSJson(VPSRequestInfo info)
        {
            // Use InvariantCulture so float values always use '.' as decimal separator
            // regardless of the device's regional settings.
            var ic = CultureInfo.InvariantCulture;
            string F(float v) => v.ToString("G", ic);

            return "{"
                + $"\"mapName\":\"{m_Config.mapName}\","
                + $"\"timestamp\":{info.timestamp},"
                + $"\"equipmentInfo\":{{\"uuid\":\"{m_UUID}\"}},"
                + $"\"deviceType\":{m_Config.deviceType},"
                + $"\"mapOrientation\":{m_Config.mapOrientation},"
                + $"\"focalLength\":{{\"x\":{F(info.focalLength.x)},\"y\":{F(info.focalLength.y)}}},"
                + $"\"resolution\":{{\"x\":{info.resolution.x},\"y\":{info.resolution.y}}},"
                + $"\"principalPoint\":{{\"x\":{F(info.principalPoint.x)},\"y\":{F(info.principalPoint.y)}}},"
                + $"\"unityPosition\":{{\"x\":{F(info.unityPosition.x)},\"y\":{F(info.unityPosition.y)},\"z\":{F(info.unityPosition.z)}}},"
                + $"\"unityRotation\":{{\"x\":{F(info.unityRotation.x)},\"y\":{F(info.unityRotation.y)},\"z\":{F(info.unityRotation.z)},\"w\":{F(info.unityRotation.w)}}},"
                + $"\"priorPosition\":{{\"x\":{F(info.priorPosition.x)},\"y\":{F(info.priorPosition.y)},\"z\":{F(info.priorPosition.z)}}},"
                + $"\"priorRotation\":{{\"x\":{F(info.priorRotation.x)},\"y\":{F(info.priorRotation.y)},\"z\":{F(info.priorRotation.z)},\"w\":{F(info.priorRotation.w)}}},"
                + $"\"deviceOrientation\":{(int)info.deviceOrientation},"
                + $"\"gnss\":{{\"isValid\":{(info.gnss?.isValid == true ? "true" : "false")},\"latitude\":{F(info.gnss?.latitude ?? 0)},\"longitude\":{F(info.gnss?.longitude ?? 0)}}},"
                + $"\"compass\":{{\"isValid\":{(info.compass?.isValid == true ? "true" : "false")},\"trueHeading\":{F(info.compass?.trueHeading ?? 0)}}},"
                + $"\"trackingState\":{info.trackingState}"
                + "}";
        }

        private IEnumerator StereoVPSRequestCoroutine(StereoVPSRequestInfo info,
                                                       byte[] leftJpeg, byte[] rightJpeg,
                                                       Action<string> onSuccess, Action<string, long> onError)
        {
            string json = BuildStereoVPSJson(info);
            Debug.Log($"[MetalocHttpClient] POST {m_Config.stereoApiUrl}\n{json}");

            var fields = new Dictionary<string, string> { { "data", json } };
            yield return PostMultipartStereo(m_Config.stereoApiUrl, fields,
                                             leftJpeg, rightJpeg, onSuccess, onError);
        }

        private string BuildStereoVPSJson(StereoVPSRequestInfo info)
        {
            var ic = CultureInfo.InvariantCulture;
            string F(float v) => v.ToString("G", ic);

            // Mirror the server contract. mapOrientation/deviceType come from config.
            // Left-eye intrinsics also duplicated at top-level (focalLength/resolution/principalPoint)
            // so the stereo endpoint stays binary-compatible with the mono schema's required fields.
            return "{"
                + $"\"mapName\":\"{m_Config.mapName}\","
                + $"\"timestamp\":{info.timestamp},"
                + $"\"equipmentInfo\":{{\"uuid\":\"{m_UUID}\"}},"
                + $"\"deviceType\":{m_Config.deviceType},"
                + $"\"mapOrientation\":{m_Config.mapOrientation},"
                + $"\"focalLength\":{{\"x\":{F(info.focalLength_left.x)},\"y\":{F(info.focalLength_left.y)}}},"
                + $"\"resolution\":{{\"x\":{info.resolution_left.x},\"y\":{info.resolution_left.y}}},"
                + $"\"principalPoint\":{{\"x\":{F(info.principalPoint_left.x)},\"y\":{F(info.principalPoint_left.y)}}},"
                + $"\"focalLength_left\":{{\"x\":{F(info.focalLength_left.x)},\"y\":{F(info.focalLength_left.y)}}},"
                + $"\"resolution_left\":{{\"x\":{info.resolution_left.x},\"y\":{info.resolution_left.y}}},"
                + $"\"principalPoint_left\":{{\"x\":{F(info.principalPoint_left.x)},\"y\":{F(info.principalPoint_left.y)}}},"
                + $"\"focalLength_right\":{{\"x\":{F(info.focalLength_right.x)},\"y\":{F(info.focalLength_right.y)}}},"
                + $"\"resolution_right\":{{\"x\":{info.resolution_right.x},\"y\":{info.resolution_right.y}}},"
                + $"\"principalPoint_right\":{{\"x\":{F(info.principalPoint_right.x)},\"y\":{F(info.principalPoint_right.y)}}},"
                + $"\"unityPosition\":{{\"x\":{F(info.unityPosition.x)},\"y\":{F(info.unityPosition.y)},\"z\":{F(info.unityPosition.z)}}},"
                + $"\"unityRotation\":{{\"x\":{F(info.unityRotation.x)},\"y\":{F(info.unityRotation.y)},\"z\":{F(info.unityRotation.z)},\"w\":{F(info.unityRotation.w)}}},"
                + $"\"priorPosition\":{{\"x\":{F(info.priorPosition.x)},\"y\":{F(info.priorPosition.y)},\"z\":{F(info.priorPosition.z)}}},"
                + $"\"priorRotation\":{{\"x\":{F(info.priorRotation.x)},\"y\":{F(info.priorRotation.y)},\"z\":{F(info.priorRotation.z)},\"w\":{F(info.priorRotation.w)}}},"
                + $"\"rightToLeftPosition\":{{\"x\":{F(info.rightToLeftPosition.x)},\"y\":{F(info.rightToLeftPosition.y)},\"z\":{F(info.rightToLeftPosition.z)}}},"
                + $"\"rightToLeftRotation\":{{\"x\":{F(info.rightToLeftRotation.x)},\"y\":{F(info.rightToLeftRotation.y)},\"z\":{F(info.rightToLeftRotation.z)},\"w\":{F(info.rightToLeftRotation.w)}}},"
                + $"\"deviceOrientation\":{(int)info.deviceOrientation},"
                + $"\"gnss\":{{\"isValid\":{(info.gnss?.isValid == true ? "true" : "false")},\"latitude\":{F(info.gnss?.latitude ?? 0)},\"longitude\":{F(info.gnss?.longitude ?? 0)}}},"
                + $"\"compass\":{{\"isValid\":{(info.compass?.isValid == true ? "true" : "false")},\"trueHeading\":{F(info.compass?.trueHeading ?? 0)}}},"
                + $"\"trackingState\":{info.trackingState}"
                + "}";
        }

        private IEnumerator PostMultipartStereo(string url,
                                                 Dictionary<string, string> fields,
                                                 byte[] leftBytes, byte[] rightBytes,
                                                 Action<string> onSuccess, Action<string, long> onError)
        {
            WWWForm form = new WWWForm();
            if (fields != null)
                foreach (var kv in fields) form.AddField(kv.Key, kv.Value);
            if (leftBytes != null)  form.AddBinaryData("left_image",  leftBytes,  "left.jpg",  "image/jpeg");
            if (rightBytes != null) form.AddBinaryData("right_image", rightBytes, "right.jpg", "image/jpeg");

            using (var req = UnityWebRequest.Post(url, form))
            {
                if (!string.IsNullOrEmpty(m_Config.authToken))
                    req.SetRequestHeader("authorization", m_Config.authToken);
                if (!string.IsNullOrEmpty(m_Config.cookieHeader))
                    req.SetRequestHeader("Cookie", m_Config.cookieHeader);
                req.timeout = 30;

                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    onSuccess?.Invoke(req.downloadHandler.text);
                }
                else
                {
                    string body = req.downloadHandler?.text ?? "";
                    Debug.LogError($"[MetalocHttpClient] stereo {req.responseCode} {req.error} — {body}");
                    onError?.Invoke(req.error, req.responseCode);
                }
            }
        }

        private IEnumerator PostMultipart(string url,
                                          Dictionary<string, string> fields,
                                          string fileField, byte[] fileBytes,
                                          string fileName, string mimeType,
                                          Action<string> onSuccess, Action<string, long> onError)
        {
            WWWForm form = new WWWForm();
            if (fields != null)
                foreach (var kv in fields) form.AddField(kv.Key, kv.Value);
            if (fileBytes != null && !string.IsNullOrEmpty(fileField))
                form.AddBinaryData(fileField, fileBytes, fileName, mimeType);

            using (var req = UnityWebRequest.Post(url, form))
            {
                if (!string.IsNullOrEmpty(m_Config.authToken))
                    req.SetRequestHeader("authorization", m_Config.authToken);
                if (!string.IsNullOrEmpty(m_Config.cookieHeader))
                    req.SetRequestHeader("Cookie", m_Config.cookieHeader);
                req.timeout = 30;

                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    onSuccess?.Invoke(req.downloadHandler.text);
                }
                else
                {
                    string body = req.downloadHandler?.text ?? "";
                    Debug.LogError($"[MetalocHttpClient] {req.responseCode} {req.error} — {body}");
                    onError?.Invoke(req.error, req.responseCode);
                }
            }
        }
    }
}
