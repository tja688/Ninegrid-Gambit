#if UNITY_EDITOR
using UnityEditor;

namespace NineGrid.Content.Editor
{
    /// <summary>
    /// #187/#195 Audio 工作台 loopback 入口：委托通用 <see cref="EditorWorkbenchTransport"/>，域逻辑仍在
    /// <see cref="AudioWorkbenchEditorState"/>。
    /// </summary>
    [InitializeOnLoad]
    public static class AudioWorkbenchServer
    {
        public const string TokenSessionKey = "NineGrid.AudioWorkbench.Token.v1";
        public const string PortSessionKey = "NineGrid.AudioWorkbench.Port.v1";
        public const int ProtocolVersion = 1;
        public const int MaxPayloadBytes = EditorWorkbenchTransport.DefaultMaxPayloadBytes;
        public const int MinPort = 7910;
        public const int MaxPort = 7939;
        public const int MaxJobsPerTick = EditorWorkbenchTransport.DefaultMaxJobsPerTick;

        private static readonly EditorWorkbenchTransport Transport;

        static AudioWorkbenchServer()
        {
            var options = new EditorWorkbenchTransportOptions
            {
                TokenSessionKey = TokenSessionKey,
                PortSessionKey = PortSessionKey,
                MinPort = MinPort,
                MaxPort = MaxPort,
                ProtocolVersion = ProtocolVersion,
                WebRootRelativePath =
                    "Assets/Scripts/NineGrid.Foundation/NineGrid.Content.Editor/AudioWorkbenchWeb",
                DisplayName = "AudioWorkbenchServer",
            };
            Transport = new EditorWorkbenchTransport(
                options,
                new AudioWorkbenchHost(AudioWorkbenchEditorState.Instance));

            EditorApplication.quitting += Shutdown;
            AssemblyReloadEvents.beforeAssemblyReload += () =>
            {
                Transport.Host.OnBeforeAssemblyReload();
                Shutdown();
            };
        }

        public static string LaunchUrl => Transport.LaunchUrl;

        public static bool IsRunning => Transport.IsRunning;

        public static int Port => Transport.Port;

        public static string Token => Transport.Token;

        public static bool WebsocketSupported => Transport.WebsocketSupported;

        public static void EnsureStarted() => Transport.EnsureStarted();

        public static void Shutdown() => Transport.Shutdown();

        public static void ResetForTests()
        {
            Transport.ResetForTests(AudioWorkbenchEditorState.ResetForTests);
        }

        internal static string CreateTokenForTests() => Transport.CreateTokenForTests();

        public static void PumpForTests() => Transport.PumpForTests();
    }
}
#endif
