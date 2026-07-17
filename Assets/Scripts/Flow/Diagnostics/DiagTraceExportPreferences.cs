namespace NineGrid.Flow.Diagnostics
{
    /// <summary>
    /// 诊断日志落盘开关。自动导出（Play 退出 / 胜负 / 重开轮转）受本类约束；
    /// 编辑器窗口或 DevTest 手动导出传 <c>automatic: false</c> 时不受限。
    /// </summary>
    public enum DiagTraceTrack
    {
        Battle = 0,
        Core = 1,
        Perf = 2,
        Registry = 3,
    }

    public static class DiagTraceExportPreferences
    {
        private const string PrefAutoExportEnabled = "NineGrid.DiagTrace.AutoExportEnabled";
        private const string PrefAutoExportBattle = "NineGrid.DiagTrace.AutoExportBattle";
        private const string PrefAutoExportCore = "NineGrid.DiagTrace.AutoExportCore";
        private const string PrefAutoExportPerf = "NineGrid.DiagTrace.AutoExportPerf";
        private const string PrefAutoExportRegistry = "NineGrid.DiagTrace.AutoExportRegistry";

#if UNITY_EDITOR
        private static bool sLoaded;
        private static bool sAutoExportEnabled = true;
        private static bool sAutoExportBattle = true;
        private static bool sAutoExportCore = true;
        private static bool sAutoExportPerf = true;
        private static bool sAutoExportRegistry = true;
#endif

        /// <summary>自动落盘总开关（Play 退出、OnDestroy、胜负、重开轮转）。</summary>
        public static bool AutoExportEnabled
        {
            get
            {
#if UNITY_EDITOR
                EnsureLoaded();
                return sAutoExportEnabled;
#else
                return true;
#endif
            }
            set
            {
#if UNITY_EDITOR
                EnsureLoaded();
                sAutoExportEnabled = value;
                UnityEditor.EditorPrefs.SetBool(PrefAutoExportEnabled, value);
#endif
            }
        }

        public static bool AutoExportBattle
        {
            get => GetTrackAutoExport(DiagTraceTrack.Battle);
            set => SetTrackAutoExport(DiagTraceTrack.Battle, value);
        }

        public static bool AutoExportCore
        {
            get => GetTrackAutoExport(DiagTraceTrack.Core);
            set => SetTrackAutoExport(DiagTraceTrack.Core, value);
        }

        public static bool AutoExportPerf
        {
            get => GetTrackAutoExport(DiagTraceTrack.Perf);
            set => SetTrackAutoExport(DiagTraceTrack.Perf, value);
        }

        public static bool AutoExportRegistry
        {
            get => GetTrackAutoExport(DiagTraceTrack.Registry);
            set => SetTrackAutoExport(DiagTraceTrack.Registry, value);
        }

        public static bool GetTrackAutoExport(DiagTraceTrack track)
        {
#if UNITY_EDITOR
            EnsureLoaded();
            return track switch
            {
                DiagTraceTrack.Battle => sAutoExportBattle,
                DiagTraceTrack.Core => sAutoExportCore,
                DiagTraceTrack.Perf => sAutoExportPerf,
                DiagTraceTrack.Registry => sAutoExportRegistry,
                _ => true,
            };
#else
            return true;
#endif
        }

        public static void SetTrackAutoExport(DiagTraceTrack track, bool enabled)
        {
#if UNITY_EDITOR
            EnsureLoaded();
            switch (track)
            {
                case DiagTraceTrack.Battle:
                    sAutoExportBattle = enabled;
                    UnityEditor.EditorPrefs.SetBool(PrefAutoExportBattle, enabled);
                    break;
                case DiagTraceTrack.Core:
                    sAutoExportCore = enabled;
                    UnityEditor.EditorPrefs.SetBool(PrefAutoExportCore, enabled);
                    break;
                case DiagTraceTrack.Perf:
                    sAutoExportPerf = enabled;
                    UnityEditor.EditorPrefs.SetBool(PrefAutoExportPerf, enabled);
                    break;
                case DiagTraceTrack.Registry:
                    sAutoExportRegistry = enabled;
                    UnityEditor.EditorPrefs.SetBool(PrefAutoExportRegistry, enabled);
                    break;
            }
#endif
        }

        /// <summary>
        /// 是否允许写盘。<paramref name="automatic"/> 为 false 时（手动加记）始终允许。
        /// </summary>
        public static bool ShouldWriteFile(DiagTraceTrack track, bool automatic)
        {
            if (!automatic)
            {
                return true;
            }

            if (!AutoExportEnabled)
            {
                return false;
            }

            return GetTrackAutoExport(track);
        }

#if UNITY_EDITOR
        public static void Reload()
        {
            sLoaded = false;
            EnsureLoaded();
        }

        private static void EnsureLoaded()
        {
            if (sLoaded)
            {
                return;
            }

            sAutoExportEnabled = UnityEditor.EditorPrefs.GetBool(PrefAutoExportEnabled, true);
            sAutoExportBattle = UnityEditor.EditorPrefs.GetBool(PrefAutoExportBattle, true);
            sAutoExportCore = UnityEditor.EditorPrefs.GetBool(PrefAutoExportCore, true);
            sAutoExportPerf = UnityEditor.EditorPrefs.GetBool(PrefAutoExportPerf, true);
            sAutoExportRegistry = UnityEditor.EditorPrefs.GetBool(PrefAutoExportRegistry, true);
            sLoaded = true;
        }
#endif
    }
}
