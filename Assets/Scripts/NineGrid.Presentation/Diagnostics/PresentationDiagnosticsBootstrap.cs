using NineGrid.Presentation.Diagnostics;
using UnityEngine;

namespace NineGrid.Presentation.Diagnostics
{
    /// <summary>
    /// Play Mode 启动时确保 trace 基础设施已初始化（无 Watchdog 时兜底）。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-250)]
    public sealed class PresentationDiagnosticsBootstrap : MonoBehaviour
    {
        [SerializeField] private PresentationTraceConfig config;

        private void Awake()
        {
            PresentationTrace.Initialize(config);
        }
    }
}
