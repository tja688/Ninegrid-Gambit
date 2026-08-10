using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using NineGrid.Content.Editor;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests
{
    /// <summary>#195 通用 transport 与 Audio 并存：假 host 独立端口/协议/静态页。</summary>
    public sealed class EditorWorkbenchTransportTests
    {
        private string tempWebRoot;
        private EditorWorkbenchTransport stubTransport;

        [SetUp]
        public void SetUp()
        {
            tempWebRoot = Path.Combine(
                Path.GetTempPath(),
                "NineGrid.StubWorkbench." + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempWebRoot);
            File.WriteAllText(Path.Combine(tempWebRoot, "index.html"), "<html><body>stub-workbench</body></html>");
            File.WriteAllText(Path.Combine(tempWebRoot, "styles.css"), "body{}");
            File.WriteAllText(Path.Combine(tempWebRoot, "app.js"), "// stub");
        }

        [TearDown]
        public void TearDown()
        {
            if (stubTransport != null)
            {
                stubTransport.ResetForTests();
                stubTransport = null;
            }

            AudioWorkbenchServer.ResetForTests();
            if (tempWebRoot != null && Directory.Exists(tempWebRoot))
            {
                try
                {
                    Directory.Delete(tempWebRoot, true);
                }
                catch
                {
                    // ignore temp cleanup races
                }
            }
        }

        [Test]
        public void StubHost_StartsIndependentProtocol_OnSeparatePort()
        {
            var host = new StubWorkbenchHost();
            stubTransport = CreateStubTransport(host);
            stubTransport.EnsureStarted();
            AudioWorkbenchServer.EnsureStarted();

            Assert.AreNotEqual(AudioWorkbenchServer.Port, stubTransport.Port);
            Assert.AreNotEqual(AudioWorkbenchServer.Token, stubTransport.Token);

            var staticBody = HttpGet(
                stubTransport.Port,
                "/index.html",
                configure: null,
                pump: stubTransport.PumpForTests);
            StringAssert.Contains("stub-workbench", staticBody);

            var snapshot = GetSnapshot(stubTransport);
            Assert.AreEqual("snapshot", snapshot.Value<string>("type"));
            Assert.AreEqual(2, snapshot.Value<int>("protocolVersion"));
            Assert.IsTrue(snapshot["payload"].Value<bool>("stubSnapshot"));

            var ping = PostCommand(stubTransport, "stubPing", "{}");
            Assert.IsTrue(ping.Value<bool>("ok"));
            Assert.AreEqual(2, ping.Value<int>("protocolVersion"));
            Assert.IsTrue(ping["payload"].Value<bool>("stub"));

            var audioSnapshot = GetAuthenticatedAudioSnapshot();
            Assert.AreEqual(1, audioSnapshot.Value<int>("protocolVersion"));
        }

        private EditorWorkbenchTransport CreateStubTransport(IEditorWorkbenchHost host)
        {
            var options = new EditorWorkbenchTransportOptions
            {
                TokenSessionKey = "NineGrid.StubWorkbench.Token.v1",
                PortSessionKey = "NineGrid.StubWorkbench.Port.v1",
                MinPort = 7940,
                MaxPort = 7949,
                ProtocolVersion = 2,
                WebRootRelativePath = tempWebRoot,
                DisplayName = "StubWorkbenchTransport",
            };
            return new EditorWorkbenchTransport(options, host);
        }

        private static JObject GetSnapshot(EditorWorkbenchTransport transport)
        {
            var status = HttpStatus(
                transport.Port,
                "GET",
                "/api/snapshot",
                request => request.Headers["Authorization"] = "Bearer " + transport.Token,
                out var body,
                pump: transport.PumpForTests);
            Assert.AreEqual(200, status, body);
            return JObject.Parse(body);
        }

        private static JObject PostCommand(EditorWorkbenchTransport transport, string command, string payloadJson)
        {
            var status = HttpStatus(
                transport.Port,
                "POST",
                "/api/command",
                request =>
                {
                    request.Headers["Authorization"] = "Bearer " + transport.Token;
                    request.ContentType = "application/json";
                },
                out var body,
                pump: transport.PumpForTests,
                requestBody:
                "{\"requestId\":\"stub1\",\"command\":\"" + command + "\",\"payload\":" + payloadJson + "}");
            Assert.AreEqual(200, status, body);
            return JObject.Parse(body);
        }

        private static JObject GetAuthenticatedAudioSnapshot()
        {
            var status = HttpStatus(
                AudioWorkbenchServer.Port,
                "GET",
                "/api/snapshot",
                request => request.Headers["Authorization"] = "Bearer " + AudioWorkbenchServer.Token,
                out var body,
                pump: AudioWorkbenchServer.PumpForTests);
            Assert.AreEqual(200, status, body);
            return JObject.Parse(body);
        }

        private static string HttpGet(int port, string path, Action<HttpWebRequest> configure, Action pump)
        {
            var status = HttpStatus(port, "GET", path, configure, out var body, pump);
            Assert.AreEqual(200, status, body);
            return body;
        }

        private static int HttpStatus(
            int port,
            string method,
            string path,
            Action<HttpWebRequest> configure,
            out string body,
            Action pump,
            string requestBody = null)
        {
            string capturedBody = null;
            int status = -1;
            Exception error = null;
            var done = false;
            var url = "http://127.0.0.1:" + port + path;
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
                pump?.Invoke();
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

        private sealed class StubWorkbenchHost : IEditorWorkbenchHost
        {
            private long revision = 1;

            public int ProtocolVersion => 2;

            public long LocalRevision => revision;

            public void Initialize()
            {
            }

            public void OnBeforeAssemblyReload()
            {
            }

            public void TickObservation()
            {
            }

            public bool TryDispatchCommand(string command, string payloadJson, out object payload, out string error)
            {
                if (command == "stubPing")
                {
                    revision++;
                    payload = new { stub = true };
                    error = null;
                    return true;
                }

                payload = null;
                error = "unknown stub command";
                return false;
            }

            public object BuildSnapshotPayload()
            {
                return new { stubSnapshot = true, revision };
            }
        }
    }
}
