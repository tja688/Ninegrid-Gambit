#if UNITY_EDITOR
using UnityEditor;

namespace NineGrid.Content.Editor
{
    [InitializeOnLoad]
    public static class VfxWorkbenchServer
    {
        public const string TokenSessionKey = "NineGrid.VfxWorkbench.Token.v1";
        public const string PortSessionKey = "NineGrid.VfxWorkbench.Port.v1";
        public const int ProtocolVersion = 1;
        public const int MaxPayloadBytes = EditorWorkbenchTransport.DefaultMaxPayloadBytes;
        public const int MinPort = 7950;
        public const int MaxPort = 7979;
        public const int MaxJobsPerTick = EditorWorkbenchTransport.DefaultMaxJobsPerTick;

        private static readonly EditorWorkbenchTransport Transport;

        static VfxWorkbenchServer()
        {
            var options = new EditorWorkbenchTransportOptions
            {
                TokenSessionKey = TokenSessionKey,
                PortSessionKey = PortSessionKey,
                MinPort = MinPort,
                MaxPort = MaxPort,
                ProtocolVersion = ProtocolVersion,
                WebRootRelativePath =
                    "Assets/Scripts/NineGrid.Foundation/NineGrid.Content.Editor/VfxWorkbenchWeb",
                DisplayName = "VfxWorkbenchServer",
            };
            Transport = new EditorWorkbenchTransport(
                options,
                new VfxWorkbenchHost(VfxWorkbenchEditorState.Instance));

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
            Transport.ResetForTests(VfxWorkbenchEditorState.ResetForTests);
        }

        internal static string CreateTokenForTests() => Transport.CreateTokenForTests();

        public static void PumpForTests() => Transport.PumpForTests();
    }
}
#endif
