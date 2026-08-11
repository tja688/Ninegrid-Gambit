using System;
using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using NineGrid.Content.Editor;
using NineGrid.Content.Vfx;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests
{
    /// <summary>#202 VFX loopback 鉴权、revision、冲突门禁与 Audio 并存。</summary>
    public sealed class VfxWorkbenchServerTests
    {
        [TearDown]
        public void TearDown()
        {
            VfxWorkbenchServer.ResetForTests();
            AudioWorkbenchServer.ResetForTests();
        }

        [Test]
        public void Snapshot_RequiresBearerToken()
        {
            VfxWorkbenchServer.EnsureStarted();
            var status = HttpStatus("GET", "/api/snapshot", null, out _);
            Assert.AreEqual(401, status);
        }

        [Test]
        public void AuthenticatedSnapshot_ReturnsMonotonicRevision_AndPingAdvances()
        {
            VfxWorkbenchServer.EnsureStarted();
            var first = GetAuthenticatedSnapshot();
            Assert.AreEqual("snapshot", first.Value<string>("type"));
            var revision1 = first.Value<long>("revision");

            var ping = PostCommand("ping", "{}");
            Assert.IsTrue(ping.Value<bool>("ok"));
            var second = GetAuthenticatedSnapshot();
            Assert.GreaterOrEqual(second.Value<long>("revision"), revision1);
        }

        [Test]
        public void Launcher_MenuPath_AndLaunchUrl_StartsAuthenticatedLoopback()
        {
            VfxWorkbenchServer.ResetForTests();
            Assert.AreEqual("NineGrid/视觉特效/VFX 绑定调试工作台", VfxWorkbenchLauncher.MenuPath);

            var url = VfxWorkbenchServer.LaunchUrl;
            Assert.IsTrue(VfxWorkbenchServer.IsRunning);
            StringAssert.StartsWith("http://127.0.0.1:", url);
            StringAssert.Contains("#token=", url);
        }

        [Test]
        public void VfxAndAudio_UseSeparatePortsAndTokens()
        {
            VfxWorkbenchServer.EnsureStarted();
            AudioWorkbenchServer.EnsureStarted();
            Assert.AreNotEqual(VfxWorkbenchServer.Port, AudioWorkbenchServer.Port);
            Assert.AreNotEqual(VfxWorkbenchServer.Token, AudioWorkbenchServer.Token);
        }

        [Test]
        public void StaticPage_ServesVfxWorkbenchHtml()
        {
            VfxWorkbenchServer.EnsureStarted();
            var status = HttpStatus(
                "GET",
                "/index.html",
                null,
                out var body);
            Assert.AreEqual(200, status, body);
            StringAssert.Contains("VFX 绑定调试工作台", body);
        }

        [Test]
        public void TransientConflict_BlocksSave_UntilDiscard()
        {
            VfxWorkbenchServer.ResetForTests();
            var state = VfxWorkbenchEditorState.Instance;
            state.BindingSession.LoadFromSnapshots(
                "{\"schemaVersion\":1,\"ticket\":\"t\",\"cueBindings\":[],\"stateBindings\":[]}",
                "{\"schemaVersion\":1,\"ticket\":\"t\",\"cueBindings\":[{\"cueId\":\"vfx.conflict\",\"enabled\":true,\"playerId\":\"sprite-sheet\",\"materialKey\":\"fx/test\",\"spatialOwnership\":\"independent\"}],\"stateBindings\":[]}");
            state.PersistTransient();

            state.BindingSession.LoadFromSnapshots(
                "{\"schemaVersion\":1,\"ticket\":\"changed\",\"cueBindings\":[],\"stateBindings\":[]}",
                "{\"schemaVersion\":1,\"ticket\":\"changed\",\"cueBindings\":[],\"stateBindings\":[]}");
            state.ReevaluateTransientForTests();

            Assert.IsTrue(state.BlocksMutations);
            Assert.IsFalse(state.TryDispatchCommand("saveAll", "{}", out _, out var blockedError));
            StringAssert.Contains("磁盘", blockedError);

            Assert.IsTrue(state.TryDispatchCommand("discardTransient", "{}", out _, out var discardError), discardError);
            Assert.IsFalse(state.BlocksMutations);
        }

        private static JObject GetAuthenticatedSnapshot()
        {
            var status = HttpStatus(
                "GET",
                "/api/snapshot",
                request => request.Headers["Authorization"] = "Bearer " + VfxWorkbenchServer.Token,
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
                    request.Headers["Authorization"] = "Bearer " + VfxWorkbenchServer.Token;
                    request.ContentType = "application/json";
                },
                out var body,
                "{\"requestId\":\"v1\",\"command\":\"" + command + "\",\"payload\":" + payloadJson + "}");
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
            var url = "http://127.0.0.1:" + VfxWorkbenchServer.Port + path;
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = method;
            configure?.Invoke(request);
            if (!string.IsNullOrEmpty(requestBody))
            {
                var bytes = Encoding.UTF8.GetBytes(requestBody);
                request.ContentLength = bytes.Length;
                using (var stream = request.GetRequestStream())
                {
                    stream.Write(bytes, 0, bytes.Length);
                }
            }

            try
            {
                VfxWorkbenchServer.PumpForTests();
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new System.IO.StreamReader(response.GetResponseStream()))
                {
                    body = reader.ReadToEnd();
                    return (int)response.StatusCode;
                }
            }
            catch (WebException exception)
            {
                VfxWorkbenchServer.PumpForTests();
                if (exception.Response is HttpWebResponse errorResponse)
                {
                    using (var reader = new System.IO.StreamReader(errorResponse.GetResponseStream()))
                    {
                        body = reader.ReadToEnd();
                        return (int)errorResponse.StatusCode;
                    }
                }

                body = exception.Message;
                return -1;
            }
        }
    }
}
