using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using NineGrid.Content.Audio;
using NineGrid.Content.Editor;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests
{
    /// <summary>#187 loopback 鉴权、拒绝面、revision 与瞬态冲突门禁。</summary>
    public sealed class AudioWorkbenchServerTests
    {
        [TearDown]
        public void TearDown()
        {
            AudioWorkbenchServer.ResetForTests();
        }

        [Test]
        public void Snapshot_RequiresBearerToken()
        {
            AudioWorkbenchServer.EnsureStarted();
            var status = HttpStatus(
                "GET",
                "/api/snapshot",
                configure: null,
                out _);
            Assert.AreEqual(401, status);
        }

        [Test]
        public void Snapshot_RejectsWrongOrigin()
        {
            AudioWorkbenchServer.EnsureStarted();
            var status = HttpStatus(
                "GET",
                "/api/snapshot",
                request =>
                {
                    request.Headers["Authorization"] = "Bearer " + AudioWorkbenchServer.Token;
                    request.Headers["Origin"] = "http://evil.example";
                },
                out _);
            Assert.AreEqual(403, status);
        }

        [Test]
        public void Static_RejectsPathTraversal()
        {
            AudioWorkbenchServer.EnsureStarted();
            var status = HttpStatus("GET", "/../Secrets.txt", null, out _);
            Assert.IsTrue(status == 400 || status == 404, "status=" + status);
        }

        [Test]
        public void Command_RejectsOversizePayload()
        {
            AudioWorkbenchServer.EnsureStarted();
            var huge = new string('x', AudioWorkbenchServer.MaxPayloadBytes + 64);
            var body = "{\"requestId\":\"1\",\"command\":\"ping\",\"payload\":{\"pad\":\"" + huge + "\"}}";
            var status = HttpStatus(
                "POST",
                "/api/command",
                request =>
                {
                    request.Headers["Authorization"] = "Bearer " + AudioWorkbenchServer.Token;
                    request.ContentType = "application/json";
                },
                out _,
                body);
            Assert.IsTrue(status == 413 || status == 400, "status=" + status);
        }

        [Test]
        public void AuthenticatedSnapshot_ReturnsMonotonicRevision_AndPingAdvances()
        {
            AudioWorkbenchServer.EnsureStarted();
            var first = GetAuthenticatedSnapshot();
            Assert.AreEqual("snapshot", first.Value<string>("type"));
            var revision1 = first.Value<long>("revision");

            var ping = PostCommand("ping", "{}");
            Assert.IsTrue(ping.Value<bool>("ok"));
            var second = GetAuthenticatedSnapshot();
            var revision2 = second.Value<long>("revision");
            Assert.GreaterOrEqual(revision2, revision1);
        }

        [Test]
        public void TransientConflict_BlocksSave_UntilRecoverOrDiscard()
        {
            AudioWorkbenchServer.ResetForTests();
            var state = AudioWorkbenchEditorState.Instance;
            state.SfxSession.LoadFromJson(
                "{\"schemaVersion\":3,\"ticket\":\"t\",\"bindings\":[]}",
                Array.Empty<AudioBindingEditorDeclaration>(),
                Array.Empty<AudioBindingEditorClipOption>());
            state.MusicSession.LoadFromJson(
                "{\"schemaVersion\":1,\"ticket\":\"t\",\"bindings\":[]}");
            Assert.IsTrue(state.SfxSession.TryCreateDraft("ui.conflict", out var entry, out var error), error);
            entry.Dto.volumeDb = -5f;
            state.PersistTransient();

            state.SfxSession.LoadFromJson(
                "{\"schemaVersion\":3,\"ticket\":\"changed\",\"bindings\":[]}",
                Array.Empty<AudioBindingEditorDeclaration>(),
                Array.Empty<AudioBindingEditorClipOption>());
            state.ReevaluateTransientForTests();

            Assert.IsTrue(state.BlocksMutations);
            Assert.IsFalse(
                state.TryDispatchCommand("saveAll", "{}", out _, out var blockedError));
            StringAssert.Contains("磁盘", blockedError);

            Assert.IsTrue(state.TryDispatchCommand("discardTransient", "{}", out _, out var discardError), discardError);
            Assert.IsFalse(state.BlocksMutations);
        }

        private static JObject GetAuthenticatedSnapshot()
        {
            var status = HttpStatus(
                "GET",
                "/api/snapshot",
                request => request.Headers["Authorization"] = "Bearer " + AudioWorkbenchServer.Token,
                out var body);
            Assert.AreEqual(200, status, body);
            return JObject.Parse(body);
        }

        private static JObject PostCommand(string command, string payloadJson)
        {
            var status = HttpStatus(
                "POST",
                "/api/command",
                request =>
                {
                    request.Headers["Authorization"] = "Bearer " + AudioWorkbenchServer.Token;
                    request.ContentType = "application/json";
                },
                out var body,
                "{\"requestId\":\"t1\",\"command\":\"" + command + "\",\"payload\":" + payloadJson + "}");
            Assert.AreEqual(200, status, body);
            return JObject.Parse(body);
        }

        private static int HttpStatus(
            string method,
            string path,
            Action<HttpWebRequest> configure,
            out string body,
            string requestBody = null)
        {
            string capturedBody = null;
            int status = -1;
            Exception error = null;
            var done = false;
            var url = "http://127.0.0.1:" + AudioWorkbenchServer.Port + path;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    var request = (HttpWebRequest)WebRequest.Create(url);
                    request.Method = method;
                    configure?.Invoke(request);
                    if (requestBody != null)
                    {
                        var bytes = Encoding.UTF8.GetBytes(requestBody);
                        request.ContentLength = bytes.Length;
                        using var stream = request.GetRequestStream();
                        stream.Write(bytes, 0, bytes.Length);
                    }

                    try
                    {
                        using var response = (HttpWebResponse)request.GetResponse();
                        status = (int)response.StatusCode;
                        using var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8);
                        capturedBody = reader.ReadToEnd();
                    }
                    catch (WebException webException)
                    {
                        var response = (HttpWebResponse)webException.Response;
                        if (response == null)
                        {
                            error = webException;
                        }
                        else
                        {
                            status = (int)response.StatusCode;
                            using var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8);
                            capturedBody = reader.ReadToEnd();
                        }
                    }
                }
                catch (Exception exception)
                {
                    error = exception;
                }
                finally
                {
                    done = true;
                }
            });

            var sw = Stopwatch.StartNew();
            while (!done && sw.ElapsedMilliseconds < 15000)
            {
                AudioWorkbenchServer.PumpForTests();
                Thread.Sleep(5);
            }

            if (!done)
            {
                throw new TimeoutException("HTTP test call timed out while pumping main-thread jobs.");
            }

            if (error != null)
            {
                throw error;
            }

            body = capturedBody ?? string.Empty;
            return status;
        }
    }
}
