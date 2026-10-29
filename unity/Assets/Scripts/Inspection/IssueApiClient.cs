using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Inspection
{
    public sealed class IssueApiClient
    {
        public IEnumerator CreateIssue(string baseUrl, IssueCreateRequest body, Action<IssueDto> onOk, Action<string> onError)
        {
            var url = baseUrl + "/api/issues";
            var json = JsonUtility.ToJson(body);
            using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                var bytes = Encoding.UTF8.GetBytes(json);
                req.uploadHandler = new UploadHandlerRaw(bytes);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = 15;
                yield return req.SendWebRequest();
                Debug.Log("POST " + url + " -> " + req.responseCode + " " + req.downloadHandler.text);
                if (req.result != UnityWebRequest.Result.Success)
                {
                    onError(ReadError(req));
                    yield break;
                }

                var issue = JsonUtility.FromJson<IssueDto>(req.downloadHandler.text);
                if (issue == null || string.IsNullOrEmpty(issue.id))
                {
                    onError("后端没有返回问题 id。");
                    yield break;
                }
                onOk(issue);
            }
        }

        public IEnumerator CheckHealth(string baseUrl, Action onOk, Action<string> onError)
        {
            var url = baseUrl + "/health";
            using (var req = UnityWebRequest.Get(url))
            {
                req.timeout = 8;
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    onError(ReadError(req));
                    yield break;
                }

                var body = JsonUtility.FromJson<HealthBody>(req.downloadHandler.text);
                if (body != null && body.status == "ok")
                {
                    onOk();
                    yield break;
                }
                onError(body != null && !string.IsNullOrEmpty(body.error) ? body.error : "健康检查未返回 ok。");
            }
        }

        static string ReadError(UnityWebRequest req)
        {
            var raw = req.downloadHandler != null ? req.downloadHandler.text : "";
            if (!string.IsNullOrEmpty(raw))
            {
                var parsed = JsonUtility.FromJson<ApiErrorBody>(raw);
                if (parsed != null && !string.IsNullOrEmpty(parsed.error))
                    return parsed.error;
            }
            if (!string.IsNullOrEmpty(req.error))
                return req.error;
            return "请求失败 (" + req.responseCode + ")";
        }
    }
}
