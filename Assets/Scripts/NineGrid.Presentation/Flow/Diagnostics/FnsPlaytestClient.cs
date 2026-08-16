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
    /// 往 MyNote 指定笔记追加，并把合并后的对局 log 写成 LOG 下的一篇笔记。
    /// </summary>
    public static class FnsPlaytestClient
    {
        public const string DefaultBaseUrl = "http://8.156.36.219:9000";
        public const string DefaultVault = "MyNote";
        public const string CollaborationFolder = "游戏开发项目/九宫格登神/游戏开发协作";
        public const string BugNotePath = CollaborationFolder + "/玩家Bug反馈.md";
        public const string SuggestionNotePath = CollaborationFolder + "/玩家意见建议.md";
        public const string LogFolder = CollaborationFolder + "/LOG";
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
            public readonly string LogNotePath;

            public SubmitResult(bool success, string message, string logNotePath = "")
            {
                Success = success;
                Message = message ?? string.Empty;
                LogNotePath = logNotePath ?? string.Empty;
            }
        }

        public static IEnumerator SubmitBug(string problemText, Action<SubmitResult> done)
        {
            var problem = (problemText ?? string.Empty).Trim();
            if (problem.Length == 0)
            {
                done?.Invoke(new SubmitResult(false, "请先填写遇到的问题再提交。"));
                yield break;
            }

            var stamp = DateTime.Now;
            var version = Application.version;
            var merged = DiagTraceManualSnapshot.SaveMerged(problem);
            var stem = BuildLogStem(stamp, problem);
            var logNotePath = LogFolder + "/" + stem + ".md";
            var logBody = BuildLogNoteMarkdown(stamp, version, problem, merged, logNotePath);
            var bugAppend = BuildBugIndexEntry(stamp, version, problem, stem);

            yield return EnsureToken(error =>
            {
                done?.Invoke(new SubmitResult(false, error));
            });
            if (string.IsNullOrEmpty(sCachedToken))
            {
                yield break;
            }

            string createError = null;
            yield return PutNote(logNotePath, logBody, err => createError = err);
            if (createError != null)
            {
                done?.Invoke(new SubmitResult(false, "日志笔记写入失败：" + createError));
                yield break;
            }

            string appendError = null;
            yield return AppendNote(BugNotePath, bugAppend, err => appendError = err);
            if (appendError != null)
            {
                done?.Invoke(new SubmitResult(false, "Bug 笔记追加失败：" + appendError));
                yield break;
            }

            done?.Invoke(new SubmitResult(true, "Bug 已提交到笔记库，日志已合并为一篇。", logNotePath));
        }

        public static IEnumerator SubmitSuggestion(string suggestionText, Action<SubmitResult> done)
        {
            var suggestion = (suggestionText ?? string.Empty).Trim();
            if (suggestion.Length == 0)
            {
                done?.Invoke(new SubmitResult(false, "请先填写意见或建议再提交。"));
                yield break;
            }

            var stamp = DateTime.Now;
            var version = Application.version;
            var block = new StringBuilder(256);
            block.AppendLine();
            block.AppendLine("## " + stamp.ToString("yyyy-MM-dd HH:mm") + " · v" + version);
            block.AppendLine();
            block.AppendLine(suggestion);
            block.AppendLine();

            yield return EnsureToken(error =>
            {
                done?.Invoke(new SubmitResult(false, error));
            });
            if (string.IsNullOrEmpty(sCachedToken))
            {
                yield break;
            }

            string appendError = null;
            yield return AppendNote(SuggestionNotePath, block.ToString(), err => appendError = err);
            if (appendError != null)
            {
                done?.Invoke(new SubmitResult(false, "意见笔记追加失败：" + appendError));
                yield break;
            }

            done?.Invoke(new SubmitResult(true, "意见已提交到笔记库。"));
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

        private static IEnumerator AppendNote(string path, string content, Action<string> onError)
        {
            var payload = "{\"vault\":\"" + JsonEscape(sVault)
                + "\",\"path\":\"" + JsonEscape(path)
                + "\",\"content\":\"" + JsonEscape(content) + "\"}";
            string body = null;
            string transportError = null;
            yield return PostJson("/api/note/append", payload, withToken: true, (text, err) =>
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
            var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json ?? "{}"));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.timeout = 120;
            req.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
            req.SetRequestHeader("X-Client", WebGuiClient);
            if (withToken && !string.IsNullOrEmpty(sCachedToken))
            {
                req.SetRequestHeader("token", sCachedToken);
            }

            yield return req.SendWebRequest();
            try
            {
                var text = req.downloadHandler != null ? req.downloadHandler.text : string.Empty;
                if (req.result != UnityWebRequest.Result.Success && string.IsNullOrEmpty(text))
                {
                    done?.Invoke(null, "网络错误：" + req.error);
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

        private static string BuildLogStem(DateTime stamp, string problem)
        {
            return stamp.ToString("yyyyMMdd-HHmmss") + "_"
                + DiagTraceManualSnapshot.SanitizeForFileName(problem, 32);
        }

        private static string BuildLogNoteMarkdown(
            DateTime stamp,
            string version,
            string problem,
            DiagTraceManualSnapshot.MergedUpload merged,
            string logNotePath)
        {
            var sb = new StringBuilder(merged.Markdown != null ? merged.Markdown.Length + 512 : 512);
            sb.AppendLine("# 试玩 Bug 日志 " + stamp.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine();
            sb.AppendLine("- 版本：`" + version + "`");
            sb.AppendLine("- 平台：" + Application.platform);
            sb.AppendLine("- sessionId：`" + (DiagTraceShared.CurrentSessionId ?? string.Empty) + "`");
            sb.AppendLine("- seed：`" + (DiagTraceShared.CurrentSeed ?? "0") + "`");
            sb.AppendLine("- 回链：[[玩家Bug反馈]]");
            sb.AppendLine("- 本篇：`[[" + StripMd(logNotePath) + "]]`");
            sb.AppendLine();
            sb.AppendLine("## USER_PROBLEM");
            sb.AppendLine();
            sb.AppendLine(problem);
            sb.AppendLine();
            if (!string.IsNullOrEmpty(merged.LocalSave.FolderPath))
            {
                sb.AppendLine("> 本地快照：" + merged.LocalSave.FolderPath);
                sb.AppendLine();
            }

            sb.AppendLine("以下各段用原文件名标识，合并自当次对局诊断轨。");
            sb.AppendLine();
            sb.Append(merged.Markdown ?? string.Empty);
            if (!merged.LocalSave.Success)
            {
                sb.AppendLine();
                sb.AppendLine("> 本地快照未写全：" + merged.LocalSave.Message);
            }

            return sb.ToString();
        }

        private static string BuildBugIndexEntry(DateTime stamp, string version, string problem, string stem)
        {
            var sb = new StringBuilder(256);
            sb.AppendLine();
            sb.AppendLine("## " + stamp.ToString("yyyy-MM-dd HH:mm") + " · v" + version);
            sb.AppendLine();
            sb.AppendLine(problem);
            sb.AppendLine();
            sb.AppendLine("合并日志：[[" + LogFolder + "/" + stem + "]]");
            sb.AppendLine();
            return sb.ToString();
        }

        private static string StripMd(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            return path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                ? path.Substring(0, path.Length - 3)
                : path;
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
