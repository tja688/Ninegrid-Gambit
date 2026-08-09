#if UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace NineGrid.Content.Editor
{
    /// <summary>
    /// #187 loopback 鉴权工作台服务：静态 CSP 页 + Bearer /api + WS/long-poll 同协议 envelope。
    /// </summary>
    [InitializeOnLoad]
    public static class AudioWorkbenchServer
    {
        public const string TokenSessionKey = "NineGrid.AudioWorkbench.Token.v1";
        public const string PortSessionKey = "NineGrid.AudioWorkbench.Port.v1";
        public const int ProtocolVersion = 1;
        public const int MaxPayloadBytes = 1 * 1024 * 1024;
        public const int MinPort = 7910;
        public const int MaxPort = 7939;
        public const int MaxJobsPerTick = 32;

        private static readonly ConcurrentQueue<Action> MainThreadJobs = new ConcurrentQueue<Action>();
        private static readonly object Gate = new object();
        private static readonly List<ClientPushTarget> PushTargets = new List<ClientPushTarget>();

        private static HttpListener listener;
        private static CancellationTokenSource cts;
        private static string token;
        private static int port;
        private static int mainThreadId;
        private static bool websocketSupported = true;
        private static long lastBroadcastRevision = -1;
        private static bool updateHooked;
        private static string webRoot;
        private static string cachedIndex;
        private static string cachedCss;
        private static string cachedJs;

        static AudioWorkbenchServer()
        {
            EditorApplication.quitting += Shutdown;
            AssemblyReloadEvents.beforeAssemblyReload += () =>
            {
                AudioWorkbenchEditorState.Instance.OnBeforeAssemblyReload();
                Shutdown();
            };
        }

        public static string LaunchUrl
        {
            get
            {
                EnsureStarted();
                return "http://127.0.0.1:" + port + "/#token=" + Uri.EscapeDataString(token);
            }
        }

        public static bool IsRunning
        {
            get
            {
                lock (Gate)
                {
                    return listener != null && listener.IsListening;
                }
            }
        }

        public static int Port
        {
            get
            {
                EnsureStarted();
                return port;
            }
        }

        public static string Token
        {
            get
            {
                EnsureStarted();
                return token;
            }
        }

        public static bool WebsocketSupported
        {
            get
            {
                EnsureStarted();
                return websocketSupported;
            }
        }

        public static void EnsureStarted()
        {
            lock (Gate)
            {
                if (listener != null && listener.IsListening)
                {
                    return;
                }

                EnsureTokenAndPort();
                webRoot = ResolveWebRoot();
                LoadStaticCache();
                AudioWorkbenchEditorState.Instance.InitializeFromDisk();

                listener = new HttpListener();
                listener.Prefixes.Add("http://127.0.0.1:" + port + "/");
                listener.Prefixes.Add("http://localhost:" + port + "/");
                try
                {
                    listener.Start();
                }
                catch (Exception first)
                {
                    listener.Close();
                    listener = null;
                    port = FindFreePort(exclude: port);
                    SessionState.SetInt(PortSessionKey, port);
                    listener = new HttpListener();
                    listener.Prefixes.Add("http://127.0.0.1:" + port + "/");
                    listener.Prefixes.Add("http://localhost:" + port + "/");
                    try
                    {
                        listener.Start();
                    }
                    catch (Exception second)
                    {
                        listener = null;
                        throw new InvalidOperationException(
                            "AudioWorkbenchServer 无法绑定 loopback 端口：" + first.Message + " / " + second.Message);
                    }
                }

                cts = new CancellationTokenSource();
                mainThreadId = Thread.CurrentThread.ManagedThreadId;
                websocketSupported = ProbeWebsocketSupport();
                if (!updateHooked)
                {
                    EditorApplication.update += PumpMainThread;
                    updateHooked = true;
                }

                var tokenLocal = cts.Token;
                Task.Run(() => AcceptLoop(tokenLocal), tokenLocal);
            }
        }

        public static void Shutdown()
        {
            lock (Gate)
            {
                try
                {
                    cts?.Cancel();
                }
                catch
                {
                    // ignore
                }

                cts = null;
                try
                {
                    listener?.Stop();
                    listener?.Close();
                }
                catch
                {
                    // ignore
                }

                listener = null;
                lock (PushTargets)
                {
                    PushTargets.Clear();
                }
            }
        }

        public static void ResetForTests()
        {
            Shutdown();
            SessionState.EraseString(TokenSessionKey);
            SessionState.EraseInt(PortSessionKey);
            token = null;
            port = 0;
            lastBroadcastRevision = -1;
            AudioWorkbenchEditorState.ResetForTests();
        }

        internal static string CreateTokenForTests()
        {
            EnsureTokenAndPort();
            return token;
        }

        private static void EnsureTokenAndPort()
        {
            token = SessionState.GetString(TokenSessionKey, string.Empty);
            if (string.IsNullOrEmpty(token))
            {
                token = CreateToken();
                SessionState.SetString(TokenSessionKey, token);
            }

            port = SessionState.GetInt(PortSessionKey, 0);
            if (port < MinPort || port > MaxPort)
            {
                port = FindFreePort();
                SessionState.SetInt(PortSessionKey, port);
            }
        }

        private static string CreateToken()
        {
            var bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }

            return Base64Url(bytes);
        }

        private static string Base64Url(byte[] bytes)
        {
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private static int FindFreePort(int exclude = -1)
        {
            for (var candidate = MinPort; candidate <= MaxPort; candidate++)
            {
                if (candidate == exclude)
                {
                    continue;
                }

                var probe = new HttpListener();
                probe.Prefixes.Add("http://127.0.0.1:" + candidate + "/");
                try
                {
                    probe.Start();
                    probe.Stop();
                    probe.Close();
                    return candidate;
                }
                catch
                {
                    try
                    {
                        probe.Close();
                    }
                    catch
                    {
                        // ignore
                    }
                }
            }

            return MinPort;
        }

        private static string ResolveWebRoot()
        {
            return Path.GetFullPath(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/Scripts/NineGrid.Foundation/NineGrid.Content.Editor/AudioWorkbenchWeb"));
        }

        private static void LoadStaticCache()
        {
            cachedIndex = File.ReadAllText(Path.Combine(webRoot, "index.html"), Encoding.UTF8);
            cachedCss = File.ReadAllText(Path.Combine(webRoot, "styles.css"), Encoding.UTF8);
            cachedJs = File.ReadAllText(Path.Combine(webRoot, "app.js"), Encoding.UTF8);
        }

        private static bool ProbeWebsocketSupport()
        {
            try
            {
                // AcceptWebSocketAsync exists on modern Unity Mono; keep long-poll fallback if disabled.
                return typeof(HttpListenerContext).GetMethod("AcceptWebSocketAsync") != null;
            }
            catch
            {
                return false;
            }
        }

        private static async Task AcceptLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                HttpListener listenerLocal;
                lock (Gate)
                {
                    listenerLocal = listener;
                }

                if (listenerLocal == null || !listenerLocal.IsListening)
                {
                    break;
                }

                HttpListenerContext context;
                try
                {
                    context = await listenerLocal.GetContextAsync().ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (Exception)
                {
                    continue;
                }

                _ = Task.Run(() => HandleContext(context), cancellationToken);
            }
        }

        private static void HandleContext(HttpListenerContext context)
        {
            try
            {
                if (!IsLoopback(context.Request.RemoteEndPoint))
                {
                    WriteText(context, 403, "text/plain", "forbidden: non-loopback");
                    return;
                }

                var path = context.Request.Url?.AbsolutePath ?? "/";
                if (path.IndexOf("..", StringComparison.Ordinal) >= 0
                    || path.IndexOf('\\', StringComparison.Ordinal) >= 0)
                {
                    WriteText(context, 400, "text/plain", "forbidden: path traversal");
                    return;
                }

                if (!IsAllowedHost(context.Request))
                {
                    WriteText(context, 403, "text/plain", "forbidden: host/origin");
                    return;
                }

                if (path == "/" || path.Equals("/index.html", StringComparison.OrdinalIgnoreCase))
                {
                    WriteStatic(context, "text/html; charset=utf-8", cachedIndex);
                    return;
                }

                if (path.Equals("/styles.css", StringComparison.OrdinalIgnoreCase))
                {
                    WriteStatic(context, "text/css; charset=utf-8", cachedCss);
                    return;
                }

                if (path.Equals("/app.js", StringComparison.OrdinalIgnoreCase))
                {
                    WriteStatic(context, "application/javascript; charset=utf-8", cachedJs);
                    return;
                }

                if (path.Equals("/stream", StringComparison.OrdinalIgnoreCase))
                {
                    if (!websocketSupported || !context.Request.IsWebSocketRequest)
                    {
                        WriteText(context, 501, "text/plain", "websocket unavailable; use /api/events");
                        return;
                    }

                    HandleWebSocket(context).GetAwaiter().GetResult();
                    return;
                }

                if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
                {
                    if (!IsAuthorized(context.Request))
                    {
                        WriteText(context, 401, "text/plain", "unauthorized");
                        return;
                    }

                    HandleApi(context);
                    return;
                }

                WriteText(context, 404, "text/plain", "not found");
            }
            catch (Exception exception)
            {
                try
                {
                    WriteText(context, 500, "text/plain", exception.Message);
                }
                catch
                {
                    // ignore
                }
            }
        }

        private static void HandleApi(HttpListenerContext context)
        {
            var path = context.Request.Url.AbsolutePath;
            var method = context.Request.HttpMethod ?? "GET";

            if (path.Equals("/api/snapshot", StringComparison.OrdinalIgnoreCase) && method == "GET")
            {
                var envelope = RunOnMainThread(() => BuildSnapshotEnvelope());
                WriteJson(context, 200, envelope);
                return;
            }

            if (path.Equals("/api/events", StringComparison.OrdinalIgnoreCase) && method == "GET")
            {
                HandleLongPoll(context);
                return;
            }

            if (path.Equals("/api/command", StringComparison.OrdinalIgnoreCase) && method == "POST")
            {
                if (!TryReadBody(context, out var body, out var bodyError))
                {
                    WriteJson(context, 413, new
                    {
                        type = "commandResult",
                        requestId = "",
                        ok = false,
                        payload = (object)null,
                        error = bodyError,
                    });
                    return;
                }

                JObject request;
                try
                {
                    request = JObject.Parse(body);
                }
                catch (Exception exception)
                {
                    WriteJson(context, 400, new
                    {
                        type = "commandResult",
                        requestId = "",
                        ok = false,
                        payload = (object)null,
                        error = "invalid json: " + exception.Message,
                    });
                    return;
                }

                var requestId = request.Value<string>("requestId") ?? string.Empty;
                var command = request.Value<string>("command") ?? string.Empty;
                var payloadToken = request["payload"];
                var payloadJson = payloadToken == null || payloadToken.Type == JTokenType.Null
                    ? "{}"
                    : payloadToken.ToString(Formatting.None);

                var result = RunOnMainThread(() =>
                {
                    var ok = AudioWorkbenchEditorState.Instance.TryDispatchCommand(
                        command,
                        payloadJson,
                        out var payload,
                        out var error);
                    return new
                    {
                        type = "commandResult",
                        requestId,
                        ok,
                        payload,
                        error = error ?? string.Empty,
                        protocolVersion = ProtocolVersion,
                        revision = AudioWorkbenchEditorState.Instance.LocalRevision,
                    };
                });

                WriteJson(context, result.ok ? 200 : 400, result);
                QueueBroadcast();
                return;
            }

            WriteText(context, 405, "text/plain", "method not allowed");
        }

        private static void HandleLongPoll(HttpListenerContext context)
        {
            var afterText = context.Request.QueryString["afterRevision"];
            var timeoutText = context.Request.QueryString["timeoutMs"];
            long.TryParse(afterText, out var afterRevision);
            if (!int.TryParse(timeoutText, out var timeoutMs))
            {
                timeoutMs = 25000;
            }

            timeoutMs = Math.Clamp(timeoutMs, 0, 25000);
            var started = DateTime.UtcNow;
            while ((DateTime.UtcNow - started).TotalMilliseconds < timeoutMs)
            {
                var current = RunOnMainThread(() => AudioWorkbenchEditorState.Instance.LocalRevision);
                if (current > afterRevision)
                {
                    var envelope = RunOnMainThread(() => BuildSnapshotEnvelope());
                    WriteJson(context, 200, envelope);
                    return;
                }

                Thread.Sleep(50);
            }

            var timeoutEnvelope = RunOnMainThread(() => BuildSnapshotEnvelope());
            WriteJson(context, 200, timeoutEnvelope);
        }

        private static async Task HandleWebSocket(HttpListenerContext context)
        {
            var wsContext = await context.AcceptWebSocketAsync(null).ConfigureAwait(false);
            var socket = wsContext.WebSocket;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var hello = await ReceiveJsonAsync(socket, timeout.Token).ConfigureAwait(false);
            if (hello == null
                || !string.Equals(hello.Value<string>("type"), "hello", StringComparison.Ordinal)
                || !string.Equals(hello.Value<string>("token"), token, StringComparison.Ordinal))
            {
                await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "hello failed", CancellationToken.None)
                    .ConfigureAwait(false);
                return;
            }

            var target = new ClientPushTarget(socket);
            lock (PushTargets)
            {
                PushTargets.Add(target);
            }

            var snapshot = RunOnMainThread(() => BuildSnapshotEnvelope());
            await SendJsonAsync(socket, snapshot).ConfigureAwait(false);

            try
            {
                while (socket.State == WebSocketState.Open)
                {
                    var message = await ReceiveJsonAsync(socket, CancellationToken.None).ConfigureAwait(false);
                    if (message == null)
                    {
                        break;
                    }

                    if (string.Equals(message.Value<string>("type"), "command", StringComparison.Ordinal))
                    {
                        var requestId = message.Value<string>("requestId") ?? string.Empty;
                        var command = message.Value<string>("command") ?? string.Empty;
                        var payloadToken = message["payload"];
                        var payloadJson = payloadToken == null || payloadToken.Type == JTokenType.Null
                            ? "{}"
                            : payloadToken.ToString(Formatting.None);
                        var result = RunOnMainThread(() =>
                        {
                            var ok = AudioWorkbenchEditorState.Instance.TryDispatchCommand(
                                command,
                                payloadJson,
                                out var payload,
                                out var error);
                            return new
                            {
                                type = "commandResult",
                                requestId,
                                ok,
                                payload,
                                error = error ?? string.Empty,
                                protocolVersion = ProtocolVersion,
                                revision = AudioWorkbenchEditorState.Instance.LocalRevision,
                            };
                        });
                        await SendJsonAsync(socket, result).ConfigureAwait(false);
                        QueueBroadcast();
                    }
                }
            }
            finally
            {
                lock (PushTargets)
                {
                    PushTargets.Remove(target);
                }
            }
        }

        private static object BuildSnapshotEnvelope()
        {
            var state = AudioWorkbenchEditorState.Instance;
            return new
            {
                type = "snapshot",
                protocolVersion = ProtocolVersion,
                revision = state.LocalRevision,
                payload = state.BuildSnapshotPayload(),
            };
        }

        private static object BuildDeltaEnvelope()
        {
            var state = AudioWorkbenchEditorState.Instance;
            return new
            {
                type = "delta",
                protocolVersion = ProtocolVersion,
                revision = state.LocalRevision,
                payload = new
                {
                    playMode = state.IsPlayMode,
                    conflict = state.ConflictKind.ToString(),
                    blocksMutations = state.BlocksMutations,
                    focusedBindingKey = state.FocusedBindingKey,
                    statusMessage = state.StatusMessage,
                    errorMessage = state.ErrorMessage,
                    sfxDirtyCount = state.SfxSession.DirtyCount,
                    musicDirtyCount = state.MusicSession.DirtyCount,
                    sfxWorkingJson = state.SfxSession.BuildWorkingJson(),
                    musicWorkingJson = state.MusicSession.BuildWorkingJson(),
                },
            };
        }

        private static void QueueBroadcast()
        {
            MainThreadJobs.Enqueue(() =>
            {
                var revision = AudioWorkbenchEditorState.Instance.LocalRevision;
                if (revision == lastBroadcastRevision)
                {
                    return;
                }

                object envelope;
                if (lastBroadcastRevision >= 0 && revision == lastBroadcastRevision + 1)
                {
                    envelope = BuildDeltaEnvelope();
                }
                else
                {
                    envelope = BuildSnapshotEnvelope();
                }

                lastBroadcastRevision = revision;
                var json = JsonConvert.SerializeObject(envelope);
                List<ClientPushTarget> copy;
                lock (PushTargets)
                {
                    copy = new List<ClientPushTarget>(PushTargets);
                }

                foreach (var target in copy)
                {
                    _ = target.SendAsync(json);
                }
            });
        }

        public static void PumpForTests()
        {
            PumpMainThread();
        }

        private static void PumpMainThread()
        {
            var processed = 0;
            while (processed < MaxJobsPerTick && MainThreadJobs.TryDequeue(out var job))
            {
                try
                {
                    job?.Invoke();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }

                processed++;
            }

            if (!IsRunning)
            {
                return;
            }

            var revision = AudioWorkbenchEditorState.Instance.LocalRevision;
            if (revision != lastBroadcastRevision)
            {
                QueueBroadcast();
            }
        }

        private static T RunOnMainThread<T>(Func<T> func)
        {
            if (func == null)
            {
                return default;
            }

            if (!EditorApplication.isCompiling && IsMainThread())
            {
                return func();
            }

            T result = default;
            Exception error = null;
            using var done = new ManualResetEventSlim(false);
            MainThreadJobs.Enqueue(() =>
            {
                try
                {
                    result = func();
                }
                catch (Exception exception)
                {
                    error = exception;
                }
                finally
                {
                    done.Set();
                }
            });

            if (!done.Wait(TimeSpan.FromSeconds(30)))
            {
                throw new TimeoutException("AudioWorkbenchServer main-thread job timed out.");
            }

            if (error != null)
            {
                throw error;
            }

            return result;
        }

        private static bool IsMainThread()
        {
            return mainThreadId != 0 && Thread.CurrentThread.ManagedThreadId == mainThreadId;
        }

        private static bool IsLoopback(IPEndPoint endPoint)
        {
            if (endPoint == null)
            {
                return false;
            }

            return IPAddress.IsLoopback(endPoint.Address);
        }

        private static bool IsAllowedHost(HttpListenerRequest request)
        {
            var host = request.UserHostName;
            if (string.IsNullOrEmpty(host))
            {
                host = request.Headers["Host"];
            }

            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "127.0.0.1:" + port,
                "localhost:" + port,
            };
            if (string.IsNullOrEmpty(host) || !allowed.Contains(host))
            {
                return false;
            }

            var origin = request.Headers["Origin"];
            if (!string.IsNullOrEmpty(origin))
            {
                var allowedOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "http://127.0.0.1:" + port,
                    "http://localhost:" + port,
                };
                if (!allowedOrigins.Contains(origin))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsAuthorized(HttpListenerRequest request)
        {
            var header = request.Headers["Authorization"] ?? string.Empty;
            const string prefix = "Bearer ";
            if (!header.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            var presented = header.Substring(prefix.Length).Trim();
            return string.Equals(presented, token, StringComparison.Ordinal);
        }

        private static bool TryReadBody(HttpListenerContext context, out string body, out string error)
        {
            body = null;
            error = null;
            if (context.Request.ContentLength64 > MaxPayloadBytes)
            {
                error = "payload too large";
                return false;
            }

            using var stream = context.Request.InputStream;
            using var reader = new StreamReader(stream, context.Request.ContentEncoding ?? Encoding.UTF8);
            var buffer = new char[MaxPayloadBytes + 1];
            var read = reader.Read(buffer, 0, buffer.Length);
            if (read > MaxPayloadBytes)
            {
                error = "payload too large";
                return false;
            }

            body = new string(buffer, 0, read);
            return true;
        }

        private static void WriteStatic(HttpListenerContext context, string contentType, string body)
        {
            var bytes = Encoding.UTF8.GetBytes(body ?? string.Empty);
            context.Response.StatusCode = 200;
            context.Response.ContentType = contentType;
            context.Response.Headers["Content-Security-Policy"] =
                "default-src 'self'; connect-src 'self'; script-src 'self'; style-src 'self'; object-src 'none'; base-uri 'none'";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["Cache-Control"] = "no-store";
            context.Response.ContentLength64 = bytes.Length;
            context.Response.OutputStream.Write(bytes, 0, bytes.Length);
            context.Response.OutputStream.Close();
        }

        private static void WriteText(HttpListenerContext context, int status, string contentType, string body)
        {
            var bytes = Encoding.UTF8.GetBytes(body ?? string.Empty);
            context.Response.StatusCode = status;
            context.Response.ContentType = contentType;
            context.Response.ContentLength64 = bytes.Length;
            context.Response.OutputStream.Write(bytes, 0, bytes.Length);
            context.Response.OutputStream.Close();
        }

        private static void WriteJson(HttpListenerContext context, int status, object payload)
        {
            var json = JsonConvert.SerializeObject(payload);
            WriteText(context, status, "application/json; charset=utf-8", json);
        }

        private static async Task<JObject> ReceiveJsonAsync(WebSocket socket, CancellationToken cancellationToken)
        {
            var buffer = new byte[MaxPayloadBytes];
            using var ms = new MemoryStream();
            while (true)
            {
                var segment = new ArraySegment<byte>(buffer);
                var result = await socket.ReceiveAsync(segment, cancellationToken).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return null;
                }

                ms.Write(buffer, 0, result.Count);
                if (ms.Length > MaxPayloadBytes)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.MessageTooBig, "too large", CancellationToken.None)
                        .ConfigureAwait(false);
                    return null;
                }

                if (result.EndOfMessage)
                {
                    break;
                }
            }

            var text = Encoding.UTF8.GetString(ms.ToArray());
            return JObject.Parse(text);
        }

        private static async Task SendJsonAsync(WebSocket socket, object payload)
        {
            var json = JsonConvert.SerializeObject(payload);
            var bytes = Encoding.UTF8.GetBytes(json);
            await socket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None)
                .ConfigureAwait(false);
        }

        private sealed class ClientPushTarget
        {
            private readonly WebSocket socket;
            private readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);

            public ClientPushTarget(WebSocket socket)
            {
                this.socket = socket;
            }

            public async Task SendAsync(string json)
            {
                if (socket.State != WebSocketState.Open)
                {
                    return;
                }

                await sendLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    var bytes = Encoding.UTF8.GetBytes(json);
                    await socket.SendAsync(
                            new ArraySegment<byte>(bytes),
                            WebSocketMessageType.Text,
                            true,
                            CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch
                {
                    // drop
                }
                finally
                {
                    sendLock.Release();
                }
            }
        }
    }
}
#endif
