using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NineGrid.Audio
{
    /// <summary>
    /// F1 音频调试面板：列出当前持续播放的音频，支持逐条临时静音以定位叠音问题。
    /// 挂在 <see cref="GameAudioService"/> 同一 GameObject 上，运行时自动创建。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameAudioDebugPanel : MonoBehaviour
    {
        const float PanelWidth = 560f;
        const float PanelMargin = 12f;

        bool _visible;
        Vector2 _scroll;
        GameAudioMonitor.MonitorReport _report;
        readonly Dictionary<int, float> _debugMutedVolumes = new();
        readonly Dictionary<int, bool> _debugMutedFlags = new();
        GUIStyle _boxStyle;
        GUIStyle _headerStyle;
        GUIStyle _warnStyle;
        GUIStyle _rowStyle;
        bool _stylesReady;

        public bool IsVisible => _visible;

        public bool IsDebugMuted(AudioSource source)
        {
            if (source == null) return false;
            return _debugMutedFlags.TryGetValue(source.GetInstanceID(), out bool muted) && muted;
        }

        void Update()
        {
            if (WasF1PressedThisFrame())
            {
                _visible = !_visible;
                if (_visible) RefreshReport();
            }

            if (_visible)
            {
                RefreshReport();
            }
        }

        void RefreshReport()
        {
            _report = GameAudioMonitor.Gather(this);
        }

        static bool WasF1PressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
                return true;
#endif
            return Input.GetKeyDown(KeyCode.F1);
        }

        void OnGUI()
        {
            if (!_visible) return;
            EnsureStyles();

            float panelHeight = Mathf.Min(Screen.height - PanelMargin * 2f, 640f);
            var rect = new Rect(PanelMargin, PanelMargin, PanelWidth, panelHeight);
            GUI.Box(rect, GUIContent.none, _boxStyle);

            GUILayout.BeginArea(new Rect(rect.x + 10f, rect.y + 8f, rect.width - 20f, rect.height - 16f));
            DrawHeader();
            DrawSystemSummary();
            DrawWarnings();
            DrawPlayingList();
            DrawFooter();
            GUILayout.EndArea();
        }

        void EnsureStyles()
        {
            if (_stylesReady) return;
            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTex(2, 2, new Color(0.08f, 0.08f, 0.1f, 0.92f)) },
            };
            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.95f, 0.9f, 0.75f) },
            };
            _warnStyle = new GUIStyle(GUI.skin.label)
            {
                wordWrap = true,
                normal = { textColor = new Color(1f, 0.55f, 0.45f) },
            };
            _rowStyle = new GUIStyle(GUI.skin.label)
            {
                wordWrap = true,
                fontSize = 11,
                normal = { textColor = new Color(0.88f, 0.88f, 0.88f) },
            };
            _stylesReady = true;
        }

        static Texture2D MakeTex(int width, int height, Color col)
        {
            var pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            var result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        void DrawHeader()
        {
            GUILayout.Label("音频调试面板  [F1 关闭]", _headerStyle);
            GUILayout.Space(4f);
        }

        void DrawSystemSummary()
        {
            var service = GameAudioService.Instance;
            string bgm = _report.CurrentBgmKey.HasValue
                ? $"{_report.CurrentBgmKey.Value} ({service?.GetCueDisplayName(_report.CurrentBgmKey.Value) ?? "?"})"
                : "(无)";
            GUILayout.Label($"GameAudioService BGM 键: {bgm}", _rowStyle);
            GUILayout.Label(
                $"MusicManager: {(MusicManager.Main != null && MusicManager.Main.IsPlaying() ? "播放中" : "空闲")}  |  " +
                $"活跃 Music 源: {_report.MusicSourceCount}  |  Loop 源: {_report.LoopSourceCount}  |  总计: {_report.Playing.Count}",
                _rowStyle);
            GUILayout.Space(6f);
        }

        void DrawWarnings()
        {
            if (!_report.SuspectedDoubleBgm) return;
            GUILayout.Label(
                "⚠ 疑似 BGM 叠音：MusicManager 有多路输出，或 Music + BGM Loop 同时在播。逐条点「静音」排查哪一路是多余的。",
                _warnStyle);
            GUILayout.Space(4f);
        }

        void DrawPlayingList()
        {
            GUILayout.Label("持续播放中", _headerStyle);
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));

            if (_report.Playing.Count == 0)
            {
                GUILayout.Label("（当前没有正在播放的 AudioSource）", _rowStyle);
            }
            else
            {
                for (int i = 0; i < _report.Playing.Count; i++)
                {
                    DrawRow(_report.Playing[i]);
                    GUILayout.Space(4f);
                }
            }

            GUILayout.EndScrollView();
        }

        void DrawRow(GameAudioMonitor.PlayingAudioSnapshot snap)
        {
            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.BeginVertical();
            string cue = snap.CueKey.HasValue
                ? $"Cue: {snap.CueKey.Value} · {snap.CueDisplayName}"
                : "Cue: (未映射)";
            string mutedTag = snap.IsDebugMuted ? "  [已调试静音]" : "";
            GUILayout.Label(
                $"[{snap.Channel}] {snap.ClipName}{(snap.IsLoop ? " (loop)" : "")}{mutedTag}",
                _rowStyle);
            GUILayout.Label($"vol {snap.Volume:F2}  |  {cue}", _rowStyle);
            GUILayout.Label(snap.HierarchyPath, _rowStyle);
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            if (snap.Source != null)
            {
                string btnLabel = snap.IsDebugMuted ? "恢复" : "静音";
                if (GUILayout.Button(btnLabel, GUILayout.Width(52f), GUILayout.Height(36f)))
                {
                    ToggleDebugMute(snap.Source);
                    RefreshReport();
                }
            }
            GUILayout.EndHorizontal();
        }

        void DrawFooter()
        {
            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("全部恢复调试静音", GUILayout.Height(24f)))
            {
                RestoreAllDebugMutes();
                RefreshReport();
            }
            if (GUILayout.Button("刷新", GUILayout.Width(64f), GUILayout.Height(24f)))
            {
                RefreshReport();
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        void ToggleDebugMute(AudioSource source)
        {
            int id = source.GetInstanceID();
            if (_debugMutedFlags.TryGetValue(id, out bool muted) && muted)
            {
                RestoreDebugMute(source);
                return;
            }

            _debugMutedVolumes[id] = source.volume;
            _debugMutedFlags[id] = true;
            source.volume = 0f;
            source.mute = true;
        }

        void RestoreDebugMute(AudioSource source)
        {
            int id = source.GetInstanceID();
            source.mute = false;
            if (_debugMutedVolumes.TryGetValue(id, out float vol))
                source.volume = vol;
            _debugMutedVolumes.Remove(id);
            _debugMutedFlags.Remove(id);
        }

        void RestoreAllDebugMutes()
        {
            var sources = Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < sources.Length; i++)
            {
                var source = sources[i];
                if (source == null) continue;
                int id = source.GetInstanceID();
                if (!_debugMutedFlags.ContainsKey(id)) continue;
                RestoreDebugMute(source);
            }
        }
    }
}
