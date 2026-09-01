using System;
using System.Collections;
using System.Collections.Generic;
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
            // Manual JSON assembly avoids a hard dependency on LitJson / Newtonsoft.
            return "{"
                + $"\"mapName\":\"{m_Config.mapName}\","
                + $"\"timestamp\":{info.timestamp},"
                + $"\"equipmentInfo\":{{\"uuid\":\"{m_UUID}\"}},"
                + $"\"deviceType\":{m_Config.deviceType},"
                + $"\"mapOrientation\":{m_Config.mapOrientation},"
                + $"\"focalLength\":{{\"x\":{info.focalLength.x},\"y\":{info.focalLength.y}}},"
                + $"\"resolution\":{{\"x\":{info.resolution.x},\"y\":{info.resolution.y}}},"
                + $"\"principalPoint\":{{\"x\":{info.principalPoint.x},\"y\":{info.principalPoint.y}}},"
                + $"\"unityPosition\":{{\"x\":{info.unityPosition.x},\"y\":{info.unityPosition.y},\"z\":{info.unityPosition.z}}},"
                + $"\"unityRotation\":{{\"x\":{info.unityRotation.x},\"y\":{info.unityRotation.y},\"z\":{info.unityRotation.z},\"w\":{info.unityRotation.w}}},"
                + $"\"priorPosition\":{{\"x\":{info.priorPosition.x},\"y\":{info.priorPosition.y},\"z\":{info.priorPosition.z}}},"
                + $"\"priorRotation\":{{\"x\":{info.priorRotation.x},\"y\":{info.priorRotation.y},\"z\":{info.priorRotation.z},\"w\":{info.priorRotation.w}}},"
                + $"\"deviceOrientation\":{(int)info.deviceOrientation},"
                + $"\"gnss\":{{\"isValid\":{(info.gnss?.isValid == true ? "true" : "false")},\"latitude\":{info.gnss?.latitude ?? 0},\"longitude\":{info.gnss?.longitude ?? 0}}},"
                + $"\"compass\":{{\"isValid\":{(info.compass?.isValid == true ? "true" : "false")},\"trueHeading\":{info.compass?.trueHeading ?? 0}}},"
                + $"\"trackingState\":{info.trackingState}"
                + "}";
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
