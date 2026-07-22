#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NineGrid.Flow.Diagnostics;
using UnityEditor;
using UnityEngine;
using NineGrid.Flow;

namespace NineGrid.Presentation.Editor
{
    /// <summary>
    /// 诊断日志控制台：统一管理四轨 Trace 的开关、会话状态、手动加记与历史文件。
    /// </summary>
    public sealed class DiagTraceEditorWindow : EditorWindow
    {
        private const string MenuPath = "NineGrid/Diagnostics/诊断日志控制台";

        private static readonly (DiagTraceTrack Track, string Label, string Dir, string Prefix)[] TrackDefs =
        {
            (DiagTraceTrack.Battle, "BattleLog（战斗）", "Logs/OtherLog/BattleLog", "battlelog"),
            (DiagTraceTrack.Core, "CoreLog（流程）", "Logs/CoreLog", "corelog"),
            (DiagTraceTrack.Perf, "PerfLog（表现）", "Logs/PerfLog", "perflog"),
            (DiagTraceTrack.Registry, "RegistryLog（注册表）", "Logs/OtherLog/RegistryLog", "registrylog"),
        };

        private Vector2 _scroll;
        private string _status = string.Empty;
        private double _nextRepaintTime;
        private bool _showRecentFiles = true;
        private int _recentFileCount = 8;
        private readonly List<RecentLogEntry> _recentFiles = new();

        private struct RecentLogEntry
        {
            public string TrackLabel;
            public string FileName;
            public string FullPath;
            public long SizeBytes;
            public DateTime ModifiedUtc;
        }

        [MenuItem(MenuPath)]
        public static void Open()
        {
            var window = GetWindow<DiagTraceEditorWindow>();
            window.titleContent = new GUIContent("诊断日志");
            window.minSize = new Vector2(420f, 560f);
            window.Show();
        }

        private void OnEnable()
        {
            DiagTraceExportPreferences.Reload();
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            RefreshRecentFiles();
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                RefreshRecentFiles();
            }

            Repaint();
        }

        private void OnEditorUpdate()
        {
            if (EditorApplication.timeSinceStartup < _nextRepaintTime)
            {
                return;
            }

            _nextRepaintTime = EditorApplication.timeSinceStartup + (Application.isPlaying ? 0.5d : 2d);
            Repaint();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawHeader();
            DrawSessionSection();
            EditorGUILayout.Space(6f);
            DrawAutoExportSection();
            EditorGUILayout.Space(6f);
            DrawRecordingSection();
            EditorGUILayout.Space(6f);
            DrawManualExportSection();
            EditorGUILayout.Space(6f);
            DrawRecentFilesSection();

            if (!string.IsNullOrEmpty(_status))
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.HelpBox(_status, MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("TableNine 诊断日志控制台", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "四轨 Trace：Battle / Core / Perf / Registry。"
                + "「自动落盘」与「内存记录」均写入 EditorPrefs，域重载 / 重启 Unity 后保持。"
                + "Play 中可手动「加记」落盘且不影响继续游戏。",
                MessageType.None);
        }

        private void DrawSessionSection()
        {
            EditorGUILayout.LabelField("当前会话", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                var sessionId = DiagTraceShared.CurrentSessionId;
                var seed = DiagTraceShared.CurrentSeed;
                var runTag = DiagTraceShared.RunTag;
                EditorGUILayout.LabelField("Session ID", string.IsNullOrEmpty(sessionId) ? "（未开始）" : sessionId);
                EditorGUILayout.LabelField("Seed", seed);
                EditorGUILayout.LabelField("Run Tag", string.IsNullOrEmpty(runTag) ? "—" : runTag);
                if (!string.IsNullOrEmpty(DiagTraceShared.RunTagNote))
                {
                    EditorGUILayout.LabelField("Run Tag Note", DiagTraceShared.RunTagNote);
                }

                EditorGUILayout.LabelField("Play 模式", Application.isPlaying ? "运行中" : "未运行");
                EditorGUILayout.LabelField("本局已自动导出", DiagTraceShared.AlreadyExportedThisPlayExit ? "是" : "否");

                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("内存缓冲", EditorStyles.miniBoldLabel);
                DrawTrackBufferRow(DiagTraceTrack.Battle, CountBattle());
                DrawTrackBufferRow(DiagTraceTrack.Core, CountCore());
                DrawTrackBufferRow(DiagTraceTrack.Perf, CountPerf());
                DrawTrackBufferRow(DiagTraceTrack.Registry, CountRegistry());
            }
        }

        private static void DrawTrackBufferRow(DiagTraceTrack track, int count)
        {
            var label = TrackDefs.First(t => t.Track == track).Label;
            var recording = IsRecordingEnabled(track);
            var suffix = recording ? "条" : "（记录已关）";
            EditorGUILayout.LabelField(label, count > 0 ? count + suffix : "0" + suffix);
        }

        private static bool IsRecordingEnabled(DiagTraceTrack track)
        {
            return DiagTraceExportPreferences.GetTrackRecording(track);
        }

        private static void SetRecordingEnabled(DiagTraceTrack track, bool enabled)
        {
            DiagTraceExportPreferences.SetTrackRecording(track, enabled);
        }

        private static int CountBattle()
        {
            var session = BattleTraceRecorder.CurrentSession;
            return session?.ops?.Count ?? 0;
        }

        private static int CountCore()
        {
            var session = FlowTraceRecorder.CurrentSession;
            return session?.events?.Count ?? 0;
        }

        private static int CountPerf()
        {
            var session = PerfTraceRecorder.CurrentSession;
            return session?.events?.Count ?? 0;
        }

        private static int CountRegistry()
        {
            var session = RegistryTraceRecorder.CurrentSession;
            return session?.events?.Count ?? 0;
        }

        private void DrawAutoExportSection()
        {
            EditorGUILayout.LabelField("自动落盘", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "关闭后 Play 退出、胜负、重开轮转均不写 JSON 文件；内存记录仍继续（可另关）。"
                + "开关会持久化。手动加记不受此限制。",
                MessageType.None);

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUI.BeginChangeCheck();
                var master = EditorGUILayout.Toggle(
                    new GUIContent("启用自动落盘", "Play 结束 / OnDestroy / 胜负 / 重开轮转时写盘"),
                    DiagTraceExportPreferences.AutoExportEnabled);
                if (EditorGUI.EndChangeCheck())
                {
                    DiagTraceExportPreferences.AutoExportEnabled = master;
                }

                using (new EditorGUI.DisabledScope(!DiagTraceExportPreferences.AutoExportEnabled))
                {
                    DrawTrackAutoExportToggle(DiagTraceTrack.Battle, "BattleLog");
                    DrawTrackAutoExportToggle(DiagTraceTrack.Core, "CoreLog");
                    DrawTrackAutoExportToggle(DiagTraceTrack.Perf, "PerfLog");
                    DrawTrackAutoExportToggle(DiagTraceTrack.Registry, "RegistryLog");
                }
            }
        }

        private static void DrawTrackAutoExportToggle(DiagTraceTrack track, string shortName)
        {
            EditorGUI.BeginChangeCheck();
            var value = EditorGUILayout.Toggle(
                new GUIContent(shortName + " 自动写盘", "仅影响自动落盘；手动加记仍可写此轨"),
                DiagTraceExportPreferences.GetTrackAutoExport(track));
            if (EditorGUI.EndChangeCheck())
            {
                DiagTraceExportPreferences.SetTrackAutoExport(track, value);
            }
        }

        private void DrawRecordingSection()
        {
            EditorGUILayout.LabelField("内存记录", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "关闭后该轨不再写入内存缓冲（与自动落盘独立，且会持久化）。"
                + "若只需禁止写文件、仍要内存缓冲，请只关「自动落盘」。"
                + "DevTest 小键盘切换记录也会写入同一偏好。",
                MessageType.None);

            using (new EditorGUI.IndentLevelScope())
            {
                foreach (var def in TrackDefs)
                {
                    EditorGUI.BeginChangeCheck();
                    var enabled = EditorGUILayout.Toggle(def.Label, IsRecordingEnabled(def.Track));
                    if (EditorGUI.EndChangeCheck())
                    {
                        SetRecordingEnabled(def.Track, enabled);
                    }
                }

                EditorGUILayout.Space(4f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("全部开启记录"))
                    {
                        foreach (var def in TrackDefs)
                        {
                            SetRecordingEnabled(def.Track, true);
                        }
                    }

                    if (GUILayout.Button("全部关闭记录"))
                    {
                        foreach (var def in TrackDefs)
                        {
                            SetRecordingEnabled(def.Track, false);
                        }
                    }
                }
            }
        }

        private void DrawManualExportSection()
        {
            EditorGUILayout.LabelField("手动加记（Play 中可用）", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "将当前内存缓冲落盘为 JSON，游戏继续运行；不受自动落盘开关限制。"
                + "同名 session 会覆盖已有文件。",
                MessageType.None);

            using (new EditorGUI.IndentLevelScope())
            {
                using (new EditorGUI.DisabledScope(!Application.isPlaying))
                {
                    if (GUILayout.Button("加记四轨（全部）"))
                    {
                        ExportManualAll();
                    }

                    EditorGUILayout.Space(2f);
                    foreach (var def in TrackDefs)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (GUILayout.Button("加记 " + def.Label.Split('（')[0], GUILayout.Height(22f)))
                            {
                                ExportManualTrack(def.Track);
                            }
                        }
                    }
                }

                if (!Application.isPlaying)
                {
                    EditorGUILayout.HelpBox("进入 Play 模式后可手动加记。", MessageType.Warning);
                }

                EditorGUILayout.Space(4f);
                if (GUILayout.Button("刷新历史文件列表"))
                {
                    RefreshRecentFiles();
                    _status = "已刷新历史文件。";
                }

                if (GUILayout.Button("在资源管理器中打开 Logs 根目录"))
                {
                    RevealInOs(DiagTraceShared.ResolveNotesDir("Logs"));
                }
            }
        }

        private void DrawRecentFilesSection()
        {
            _showRecentFiles = EditorGUILayout.Foldout(_showRecentFiles, "最近落盘文件", true);
            if (!_showRecentFiles)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                _recentFileCount = EditorGUILayout.IntSlider(
                    new GUIContent("每轨显示条数", "按修改时间倒序"),
                    _recentFileCount,
                    3,
                    20);

                if (_recentFiles.Count == 0)
                {
                    EditorGUILayout.LabelField("（暂无 JSON 文件）");
                    return;
                }

                var grouped = _recentFiles
                    .GroupBy(f => f.TrackLabel)
                    .OrderBy(g => g.Key);

                foreach (var group in grouped)
                {
                    EditorGUILayout.LabelField(group.Key, EditorStyles.miniBoldLabel);
                    foreach (var entry in group.Take(_recentFileCount))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField(
                                entry.FileName,
                                GUILayout.MinWidth(120f),
                                GUILayout.ExpandWidth(true));
                            if (GUILayout.Button("打开", GUILayout.Width(44f)))
                            {
                                OpenJsonFile(entry.FullPath);
                            }

                            if (GUILayout.Button("定位", GUILayout.Width(44f)))
                            {
                                RevealInOs(entry.FullPath);
                            }
                        }

                        EditorGUILayout.LabelField(
                            FormatFileMeta(entry),
                            EditorStyles.miniLabel);
                    }

                    EditorGUILayout.Space(2f);
                }
            }
        }

        private static string FormatFileMeta(RecentLogEntry entry)
        {
            var kb = entry.SizeBytes / 1024f;
            var local = entry.ModifiedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            return kb.ToString("F1") + " KB · " + local;
        }

        private void ExportManualAll()
        {
            BattleTraceRecorder.ExportBothNow(silentIfEmpty: false, automatic: false);
            AssetDatabase.Refresh();
            RefreshRecentFiles();
            _status = BuildManualExportStatus(null);
        }

        private void ExportManualTrack(DiagTraceTrack track)
        {
            var path = BattleTraceRecorder.ExportTrackNow(track, silentIfEmpty: false);
            AssetDatabase.Refresh();
            RefreshRecentFiles();
            _status = BuildManualExportStatus(path, track);
        }

        private static string BuildManualExportStatus(string path, DiagTraceTrack? track = null)
        {
            if (!string.IsNullOrEmpty(path))
            {
                return "已加记：" + path;
            }

            var label = track.HasValue
                ? TrackDefs.First(t => t.Track == track.Value).Label
                : "四轨";
            return label + " 加记完成（部分轨可能无数据，已跳过）。";
        }

        private void RefreshRecentFiles()
        {
            _recentFiles.Clear();
            foreach (var def in TrackDefs)
            {
                var dir = DiagTraceShared.ResolveNotesDir(def.Dir);
                if (!Directory.Exists(dir))
                {
                    continue;
                }

                IEnumerable<string> files;
                try
                {
                    files = Directory.GetFiles(dir, def.Prefix + "-*.json");
                }
                catch
                {
                    continue;
                }

                foreach (var fullPath in files)
                {
                    try
                    {
                        var info = new FileInfo(fullPath);
                        _recentFiles.Add(new RecentLogEntry
                        {
                            TrackLabel = def.Label.Split('（')[0],
                            FileName = info.Name,
                            FullPath = fullPath,
                            SizeBytes = info.Length,
                            ModifiedUtc = info.LastWriteTimeUtc,
                        });
                    }
                    catch
                    {
                        // ignore unreadable entries
                    }
                }
            }

            _recentFiles.Sort((a, b) => b.ModifiedUtc.CompareTo(a.ModifiedUtc));
        }

        private static void OpenJsonFile(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
            {
                return;
            }

            var assetPath = FullPathToAssetPath(fullPath);
            if (!string.IsNullOrEmpty(assetPath))
            {
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
                if (asset != null)
                {
                    AssetDatabase.OpenAsset(asset);
                    return;
                }
            }

            EditorUtility.OpenWithDefaultApp(fullPath);
        }

        private static void RevealInOs(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath))
            {
                return;
            }

            if (File.Exists(fullPath))
            {
                EditorUtility.RevealInFinder(fullPath);
                return;
            }

            if (Directory.Exists(fullPath))
            {
                EditorUtility.RevealInFinder(fullPath);
            }
        }

        private static string FullPathToAssetPath(string fullPath)
        {
            fullPath = fullPath.Replace('\\', '/');
            var dataPath = Application.dataPath.Replace('\\', '/');
            if (!fullPath.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return "Assets" + fullPath.Substring(dataPath.Length);
        }
    }
}

#endif
