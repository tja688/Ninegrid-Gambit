#if UNITY_EDITOR
using UnityEditor;

namespace NineGrid.Content.Editor
{
    /// <summary>
    /// 表现层配置网页工作台 loopback 入口：复用通用 <see cref="EditorWorkbenchTransport"/>；
    /// 域逻辑在 <see cref="CardPresentationWorkbenchEditorState"/>。
    /// 域重载后按会话标记自动重启，页面无需重开即可重连。
    /// </summary>
    [InitializeOnLoad]
    public static class CardPresentationWorkbenchServer
    {
        public const string TokenSessionKey = "NineGrid.CardPresentationWorkbench.Token.v1";
        public const string PortSessionKey = "NineGrid.CardPresentationWorkbench.Port.v1";
        public const string AutoRestartSessionKey = "NineGrid.CardPresentationWorkbench.AutoRestart.v1";
        public const int ProtocolVersion = 1;
        public const int MinPort = 7860;
        public const int MaxPort = 7889;

        private static readonly EditorWorkbenchTransport Transport;

        static CardPresentationWorkbenchServer()
        {
            var options = new EditorWorkbenchTransportOptions
            {
                TokenSessionKey = TokenSessionKey,
                PortSessionKey = PortSessionKey,
                MinPort = MinPort,
                MaxPort = MaxPort,
                ProtocolVersion = ProtocolVersion,
                WebRootRelativePath =
                    "Assets/Scripts/NineGrid.Foundation/NineGrid.Content.Editor/CardPresentationWorkbenchWeb",
                DisplayName = "CardPresentationWorkbenchServer",
            };
            Transport = new EditorWorkbenchTransport(
                options,
                new CardPresentationWorkbenchHost(CardPresentationWorkbenchEditorState.Instance));

            EditorApplication.quitting += Shutdown;
            AssemblyReloadEvents.beforeAssemblyReload += () =>
            {
                Transport.Host.OnBeforeAssemblyReload();
                Shutdown();
            };

            if (SessionState.GetBool(AutoRestartSessionKey, false))
            {
                EditorApplication.delayCall += () =>
                {
                    try
                    {
                        Transport.EnsureStarted();
                    }
                    catch
                    {
                        // 端口暂不可用时等下次手动打开。
                    }
                };
            }
        }

        public static string LaunchUrl
        {
            get
            {
                SessionState.SetBool(AutoRestartSessionKey, true);
                return Transport.LaunchUrl;
            }
        }

        public static bool IsRunning => Transport.IsRunning;

        public static int Port => Transport.Port;

        public static string Token => Transport.Token;

        public static void EnsureStarted()
        {
            SessionState.SetBool(AutoRestartSessionKey, true);
            Transport.EnsureStarted();
        }

        public static void Shutdown() => Transport.Shutdown();

        public static void ResetForTests()
        {
            Transport.ResetForTests(CardPresentationWorkbenchEditorState.ResetForTests);
            SessionState.EraseBool(AutoRestartSessionKey);
        }

        internal static string CreateTokenForTests() => Transport.CreateTokenForTests();

        public static void PumpForTests() => Transport.PumpForTests();
    }
}
#endif
