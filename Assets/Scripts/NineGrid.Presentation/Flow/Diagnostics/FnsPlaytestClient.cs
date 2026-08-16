using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace NineGrid.Flow.Diagnostics
{
    /// <summary>
    /// 试玩反馈上报：登录 Fast Note Sync（须 <c>X-Client: WebGui</c>），
    /// 在协作目录下为每次 Bug / 意见各新建一篇笔记（不续写、不传 log）。
    /// 明文 HTTP 依赖 Player Settings <c>insecureHttpOption=AlwaysAllowed</c>；
    /// Release 包若仍是 DevelopmentOnly，<c>SendWebRequest</c> 会抛
    /// <c>Insecure connection not allowed</c>，界面会一直停在「正在提交…」。
    /// </summary>
    public static class FnsPlaytestClient
    {
        public const string DefaultBaseUrl = "http://8.156.36.219:9000";
        public const string DefaultVault = "MyNote";
        public const string CollaborationFolder = "游戏开发项目/九宫格登神/游戏开发协作";
        public const string WebGuiClient = "WebGui";
        public const string SecretFileName = "fns-playtest.secret.json";

        private static string sCachedToken = string.Empty;
        private static string sBaseUrl = DefaultBaseUrl;
        private static string sVault = DefaultVault;
        private static string sCredentials = string.Empty;
        private static string sPassword = string.Empty;
        private static bool sSecretLoaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            sCachedToken = string.Empty;
            sBaseUrl = DefaultBaseUrl;
            sVault = DefaultVault;
            sCredentials = string.Empty;
            sPassword = string.Empty;
            sSecretLoaded = false;
        }

        public readonly struct SubmitResult
        {
            public readonly bool Success;
            public readonly string Message;
            public readonly string NotePath;

            public SubmitResult(bool success, string message, string notePath = "")
            {
                Success = success;
                Message = message ?? string.Empty;
                NotePath = notePath ?? string.Empty;
            }
        }

        public static IEnumerator SubmitBug(string problemText, Action<SubmitResult> done)
        {
            yield return SubmitNewNote(
                "玩家Bug",
                "玩家 Bug",
                problemText,
                "请先填写遇到的问题再提交。",
                done);
        }

        public static IEnumerator SubmitSuggestion(string suggestionText, Action<SubmitResult> done)
        {
            yield return SubmitNewNote(
                "玩家建议",
                "玩家建议",
                suggestionText,
                "请先填写意见或建议再提交。",
                done);
        }

        private static IEnumerator SubmitNewNote(
            string filePrefix,
            string title,
            string bodyText,
            string emptyMessage,
            Action<SubmitResult> done)
        {
            var body = (bodyText ?? string.Empty).Trim();
            if (body.Length == 0)
            {
                done?.Invoke(new SubmitResult(false, emptyMessage));
                yield break;
            }

            var stamp = DateTime.Now;
            var path = CollaborationFolder + "/" + filePrefix + "_"
                + stamp.ToString("yyyyMMdd-HHmmss") + "_"
                + DiagTraceManualSnapshot.SanitizeForFileName(body, 24) + ".md";
            var markdown = BuildStandaloneNote(title, stamp, body);

            yield return EnsureToken(error =>
            {
                done?.Invoke(new SubmitResult(false, error));
            });
            if (string.IsNullOrEmpty(sCachedToken))
            {
                yield break;
            }

            string createError = null;
            yield return PutNote(path, markdown, err => createError = err);
            if (createError != null)
            {
                done?.Invoke(new SubmitResult(false, "笔记写入失败：" + createError));
                yield break;
            }

            done?.Invoke(new SubmitResult(true, title + "已提交。", path));
        }

        private static IEnumerator EnsureToken(Action<string> onError)
        {
            if (!string.IsNullOrEmpty(sCachedToken))
            {
                yield break;
            }

            LoadSecretIfNeeded();
            if (string.IsNullOrEmpty(sCredentials) || string.IsNullOrEmpty(sPassword))
            {
                onError?.Invoke("缺少 FNS 登录凭据（StreamingAssets/" + SecretFileName + "）。");
                yield break;
            }

            var payload = "{\"credentials\":\"" + JsonEscape(sCredentials)
                + "\",\"password\":\"" + JsonEscape(sPassword) + "\"}";
            string body = null;
            string transportError = null;
            yield return PostJson("/api/user/login", payload, withToken: false, (text, err) =>
            {
                body = text;
                transportError = err;
            });
            if (transportError != null)
            {
                onError?.Invoke(transportError);
                yield break;
            }

            var parsed = JsonUtility.FromJson<LoginResponse>(body ?? "{}");
            if (parsed == null || parsed.code < 1 || parsed.data == null || string.IsNullOrEmpty(parsed.data.token))
            {
                onError?.Invoke("FNS 登录失败：" + ExtractMessage(body, parsed));
                yield break;
            }

            sCachedToken = parsed.data.token;
        }

        private static IEnumerator PutNote(string path, string content, Action<string> onError)
        {
            var payload = "{\"vault\":\"" + JsonEscape(sVault)
                + "\",\"path\":\"" + JsonEscape(path)
                + "\",\"content\":\"" + JsonEscape(content) + "\"}";
            string body = null;
            string transportError = null;
            yield return PostJson("/api/note", payload, withToken: true, (text, err) =>
            {
                body = text;
                transportError = err;
            });
            if (transportError != null)
            {
                onError?.Invoke(transportError);
                yield break;
            }

            var parsed = JsonUtility.FromJson<SimpleResponse>(body ?? "{}");
            if (parsed == null || parsed.code < 1)
            {
                onError?.Invoke(ExtractMessage(body, parsed));
            }
        }

        private static IEnumerator PostJson(
            string apiPath,
            string json,
            bool withToken,
            Action<string, string> done)
        {
            var url = sBaseUrl.TrimEnd('/') + apiPath;
            var bytes = Encoding.UTF8.GetBytes(json ?? "{}");
            var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            req.uploadHandler = new UploadHandlerRaw(bytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.timeout = 20;
            req.useHttpContinue = false;
            req.disposeUploadHandlerOnDispose = true;
            req.disposeDownloadHandlerOnDispose = true;
            req.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
            req.SetRequestHeader("X-Client", WebGuiClient);
            if (withToken && !string.IsNullOrEmpty(sCachedToken))
            {
                req.SetRequestHeader("token", sCachedToken);
            }

            Debug.Log("[FnsPlaytest] POST " + url + " bytes=" + bytes.Length);
            UnityWebRequestAsyncOperation op;
            try
            {
                op = req.SendWebRequest();
            }
            catch (InvalidOperationException ex)
            {
                req.Dispose();
                done?.Invoke(null, MapTransportError(ex.Message));
                yield break;
            }

            var startedAt = Time.realtimeSinceStartup;
            while (op != null && !op.isDone)
            {
                if (Time.realtimeSinceStartup - startedAt >= 20f)
                {
                    req.Abort();
                    req.Dispose();
                    done?.Invoke(null, "网络超时，笔记库没有在 20 秒内响应。");
                    yield break;
                }

                yield return null;
            }

            try
            {
                var text = req.downloadHandler != null ? req.downloadHandler.text : string.Empty;
                Debug.Log("[FnsPlaytest] POST done result=" + req.result + " code=" + req.responseCode
                    + " err=" + req.error + " bodyChars=" + (text != null ? text.Length : 0));
                if (req.result != UnityWebRequest.Result.Success && string.IsNullOrEmpty(text))
                {
                    done?.Invoke(null, MapTransportError(req.error));
                    yield break;
                }

                done?.Invoke(text, null);
            }
            finally
            {
                req.Dispose();
            }
        }

        private static void LoadSecretIfNeeded()
        {
            if (sSecretLoaded)
            {
                return;
            }

            sSecretLoaded = true;
            try
            {
                var path = Path.Combine(Application.streamingAssetsPath, SecretFileName);
                if (!File.Exists(path))
                {
                    Debug.LogWarning("[FnsPlaytest] 未找到 " + path);
                    return;
                }

                var dto = JsonUtility.FromJson<SecretDto>(File.ReadAllText(path, Encoding.UTF8));
                if (dto == null)
                {
                    return;
                }

                if (!string.IsNullOrWhiteSpace(dto.baseUrl))
                {
                    sBaseUrl = dto.baseUrl.Trim();
                }

                if (!string.IsNullOrWhiteSpace(dto.vault))
                {
                    sVault = dto.vault.Trim();
                }

                sCredentials = dto.credentials ?? string.Empty;
                sPassword = dto.password ?? string.Empty;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[FnsPlaytest] 读取凭据失败：" + ex.Message);
            }
        }

        private static string BuildStandaloneNote(string title, DateTime stamp, string body)
        {
            var sb = new StringBuilder(body.Length + 160);
            sb.AppendLine("# " + title);
            sb.AppendLine();
            sb.AppendLine("- 时间：" + stamp.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("- 版本：`" + Application.version + "`");
            sb.AppendLine("- 平台：" + Application.platform);
            sb.AppendLine();
            sb.AppendLine(body);
            sb.AppendLine();
            return sb.ToString();
        }

        private static string ExtractMessage(string body, SimpleResponse parsed)
        {
            if (parsed != null && !string.IsNullOrEmpty(parsed.message))
            {
                return parsed.message;
            }

            return string.IsNullOrEmpty(body) ? "未知错误" : body;
        }

        private static string ExtractMessage(string body, LoginResponse parsed)
        {
            if (parsed != null && !string.IsNullOrEmpty(parsed.message))
            {
                return parsed.message;
            }

            return string.IsNullOrEmpty(body) ? "未知错误" : body;
        }

        private static string MapTransportError(string raw)
        {
            if (!string.IsNullOrEmpty(raw)
                && raw.IndexOf("Insecure", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "正式包禁止明文 HTTP，笔记提交被拦截。";
            }

            return "网络错误：" + (string.IsNullOrEmpty(raw) ? "未知错误" : raw);
        }

        private static string JsonEscape(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(raw.Length + 16);
            for (var i = 0; i < raw.Length; i++)
            {
                var c = raw[i];
                switch (c)
                {
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '"':
                        sb.Append("\\\"");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    default:
                        if (c < 32)
                        {
                            sb.Append("\\u");
                            sb.Append(((int)c).ToString("x4"));
                        }
                        else
                        {
                            sb.Append(c);
                        }

                        break;
                }
            }

            return sb.ToString();
        }

        [Serializable]
        private sealed class SecretDto
        {
            public string baseUrl;
            public string vault;
            public string credentials;
            public string password;
        }

        [Serializable]
        private sealed class SimpleResponse
        {
            public int code;
            public bool status;
            public string message;
        }

        [Serializable]
        private sealed class LoginResponse
        {
            public int code;
            public bool status;
            public string message;
            public LoginData data;
        }

        [Serializable]
        private sealed class LoginData
        {
            public string token;
            public string username;
        }
    }
}
