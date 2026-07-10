#if UNITY_EDITOR

using NineGrid.Flow.Diagnostics;
using UnityEditor;

namespace NineGrid.Flow.Editor
{
    /// <summary>
    /// Play 结束时自动把本局 BattleTrace 导出到 Assets/Notes/BattleLog。
    /// 与 InBattleManager.OnDestroy 双保险；Recorder 内去重。
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
                return;
            }

            if (state != PlayModeStateChange.ExitingPlayMode)
            {
                return;
            }

            var path = BattleTraceRecorder.ExportOnPlayExit("ExitingPlayMode");
            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.Refresh();
            }
        }
    }
}

#endif
