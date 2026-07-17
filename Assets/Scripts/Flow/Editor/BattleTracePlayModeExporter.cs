#if UNITY_EDITOR

using NineGrid.Flow.Diagnostics;
using UnityEditor;

namespace NineGrid.Flow.Editor
{
    /// <summary>
    /// Play 结束时自动导出 BattleTrace + FlowTrace。
    /// 与 InBattleManager.OnDestroy 双保险；DiagTraceShared 内去重。
    /// </summary>
    [InitializeOnLoad]
    public static class BattleTracePlayModeExporter
    {
        static BattleTracePlayModeExporter()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                BattleTraceRecorder.NotifyEnteredPlayMode();
                BattleTraceRecorder.Clear();
                FlowTraceRecorder.Clear();
                PerfTraceRecorder.Clear();
                RegistryTraceRecorder.Clear();
                DiagBeatClock.Reset();
                return;
            }

            if (state != PlayModeStateChange.ExitingPlayMode)
            {
                return;
            }

            if (!DiagTraceExportPreferences.AutoExportEnabled)
            {
                return;
            }

            var battlePath = BattleTraceRecorder.ExportOnPlayExit("ExitingPlayMode");
            // CoreLog / PerfLog 已在 ExportOnPlayExit 内一并尝试
            if (!string.IsNullOrEmpty(battlePath)
                || FlowTraceRecorder.CurrentSession != null
                || PerfTraceRecorder.CurrentSession != null
                || RegistryTraceRecorder.CurrentSession != null)
            {
                AssetDatabase.Refresh();
            }
        }
    }
}

#endif
