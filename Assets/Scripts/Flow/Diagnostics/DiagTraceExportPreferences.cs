namespace NineGrid.Flow.Diagnostics
{
    /// <summary>
    /// 诊断日志偏好：自动落盘 + 内存记录。编辑器下经 EditorPrefs 持久化，
    /// 域重载 / 重启 Unity 后由 <see cref="Reload"/> / <see cref="ApplyRecordingToRecorders"/> 恢复。
    /// 自动导出（Play 退出 / 胜负 / 重开轮转）受本类约束；
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

        private const string PrefRecordingBattle = "NineGrid.DiagTrace.RecordingBattle";
        private const string PrefRecordingCore = "NineGrid.DiagTrace.RecordingCore";
        private const string PrefRecordingPerf = "NineGrid.DiagTrace.RecordingPerf";
        private const string PrefRecordingRegistry = "NineGrid.DiagTrace.RecordingRegistry";

#if UNITY_EDITOR
        private static bool sLoaded;
        private static bool sAutoExportEnabled = true;
        private static bool sAutoExportBattle = true;
        private static bool sAutoExportCore = true;
        private static bool sAutoExportPerf = true;
        private static bool sAutoExportRegistry = true;
        private static bool sRecordingBattle = true;
        private static bool sRecordingCore = true;
        private static bool sRecordingPerf = true;
        private static bool sRecordingRegistry = true;
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
                if (sAutoExportEnabled == value)
                {
                    return;
                }

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
                    if (sAutoExportBattle == enabled)
                    {
                        return;
                    }

                    sAutoExportBattle = enabled;
                    UnityEditor.EditorPrefs.SetBool(PrefAutoExportBattle, enabled);
                    break;
                case DiagTraceTrack.Core:
                    if (sAutoExportCore == enabled)
                    {
                        return;
                    }

                    sAutoExportCore = enabled;
                    UnityEditor.EditorPrefs.SetBool(PrefAutoExportCore, enabled);
                    break;
                case DiagTraceTrack.Perf:
                    if (sAutoExportPerf == enabled)
                    {
                        return;
                    }

                    sAutoExportPerf = enabled;
                    UnityEditor.EditorPrefs.SetBool(PrefAutoExportPerf, enabled);
                    break;
                case DiagTraceTrack.Registry:
                    if (sAutoExportRegistry == enabled)
                    {
                        return;
                    }

                    sAutoExportRegistry = enabled;
                    UnityEditor.EditorPrefs.SetBool(PrefAutoExportRegistry, enabled);
                    break;
                default:
                    return;
            }
#endif
        }

        /// <summary>该轨是否写入内存缓冲（编辑器下持久化）。</summary>
        public static bool GetTrackRecording(DiagTraceTrack track)
        {
#if UNITY_EDITOR
            EnsureLoaded();
            return track switch
            {
                DiagTraceTrack.Battle => sRecordingBattle,
                DiagTraceTrack.Core => sRecordingCore,
                DiagTraceTrack.Perf => sRecordingPerf,
                DiagTraceTrack.Registry => sRecordingRegistry,
                _ => true,
            };
#else
            return true;
#endif
        }

        /// <summary>设置该轨内存记录，并同步到对应 Recorder.Enabled。</summary>
        public static void SetTrackRecording(DiagTraceTrack track, bool enabled)
        {
#if UNITY_EDITOR
            EnsureLoaded();
            switch (track)
            {
                case DiagTraceTrack.Battle:
                    if (sRecordingBattle != enabled)
                    {
                        sRecordingBattle = enabled;
                        UnityEditor.EditorPrefs.SetBool(PrefRecordingBattle, enabled);
                    }

                    break;
                case DiagTraceTrack.Core:
                    if (sRecordingCore != enabled)
                    {
                        sRecordingCore = enabled;
                        UnityEditor.EditorPrefs.SetBool(PrefRecordingCore, enabled);
                    }

                    break;
                case DiagTraceTrack.Perf:
                    if (sRecordingPerf != enabled)
                    {
                        sRecordingPerf = enabled;
                        UnityEditor.EditorPrefs.SetBool(PrefRecordingPerf, enabled);
                    }

                    break;
                case DiagTraceTrack.Registry:
                    if (sRecordingRegistry != enabled)
                    {
                        sRecordingRegistry = enabled;
                        UnityEditor.EditorPrefs.SetBool(PrefRecordingRegistry, enabled);
                    }

                    break;
                default:
                    return;
            }

            ApplyRecordingToRecorder(track, enabled);
#else
            ApplyRecordingToRecorder(track, enabled);
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
        /// <summary>从 EditorPrefs 重载缓存，并把内存记录开关套到各 Recorder。</summary>
        public static void Reload()
        {
            sLoaded = false;
            EnsureLoaded();
            ApplyRecordingToRecorders();
        }

        /// <summary>把已加载的内存记录偏好写回四个 Recorder（域重载后必调）。</summary>
        public static void ApplyRecordingToRecorders()
        {
            EnsureLoaded();
            ApplyRecordingToRecorder(DiagTraceTrack.Battle, sRecordingBattle);
            ApplyRecordingToRecorder(DiagTraceTrack.Core, sRecordingCore);
            ApplyRecordingToRecorder(DiagTraceTrack.Perf, sRecordingPerf);
            ApplyRecordingToRecorder(DiagTraceTrack.Registry, sRecordingRegistry);
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
            sRecordingBattle = UnityEditor.EditorPrefs.GetBool(PrefRecordingBattle, true);
            sRecordingCore = UnityEditor.EditorPrefs.GetBool(PrefRecordingCore, true);
            sRecordingPerf = UnityEditor.EditorPrefs.GetBool(PrefRecordingPerf, true);
            sRecordingRegistry = UnityEditor.EditorPrefs.GetBool(PrefRecordingRegistry, true);
            sLoaded = true;
        }
#endif

        private static void ApplyRecordingToRecorder(DiagTraceTrack track, bool enabled)
        {
            switch (track)
            {
                case DiagTraceTrack.Battle:
                    BattleTraceRecorder.SetEnabledFromPreferences(enabled);
                    break;
                case DiagTraceTrack.Core:
                    FlowTraceRecorder.SetEnabledFromPreferences(enabled);
                    break;
                case DiagTraceTrack.Perf:
                    PerfTraceRecorder.SetEnabledFromPreferences(enabled);
                    break;
                case DiagTraceTrack.Registry:
                    RegistryTraceRecorder.SetEnabledFromPreferences(enabled);
                    break;
            }
        }
    }
}
