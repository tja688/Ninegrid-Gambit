using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.Audio
{
    /// <summary>
    /// 运行时快照：汇总当前所有正在发声的 AudioSource，供 F1 调试面板展示。
    /// </summary>
    public static class GameAudioMonitor
    {
        public enum AudioChannel
        {
            Music,
            Loop,
            Sfx,
            AudioKit,
            Other,
        }

        public struct PlayingAudioSnapshot
        {
            public int InstanceId;
            public AudioSource Source;
            public string ClipName;
            public string HierarchyPath;
            public AudioChannel Channel;
            public AudioKey? CueKey;
            public string CueDisplayName;
            public bool IsLoop;
            public float Volume;
            public bool IsDebugMuted;
        }

        public struct MonitorReport
        {
            public List<PlayingAudioSnapshot> Playing;
            public AudioKey? CurrentBgmKey;
            public int MusicSourceCount;
            public int LoopSourceCount;
            public bool SuspectedDoubleBgm;

            public static MonitorReport Empty => new MonitorReport
            {
                Playing = new List<PlayingAudioSnapshot>(),
            };
        }

        public static MonitorReport Gather(GameAudioDebugPanel debugPanel)
        {
            var report = MonitorReport.Empty;
            report.Playing = new List<PlayingAudioSnapshot>(16);

            var service = GameAudioService.Instance;
            if (service != null)
            {
                report.CurrentBgmKey = service.CurrentBgmKey;
            }

            Transform musicRoot = MusicManager.Main != null ? MusicManager.Main.Parent : null;
            Transform sfxRoot = SFXManager.Main != null ? SFXManager.Main.Parent : null;
            Transform loopRoot = SFXLoopManager.Main != null ? SFXLoopManager.Main.Parent : null;

            var sources = Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < sources.Length; i++)
            {
                var source = sources[i];
                if (source == null || !source.isPlaying) continue;

                var channel = Classify(source.transform, musicRoot, sfxRoot, loopRoot);
                AudioKey? cueKey = null;
                string displayName = null;
                if (service != null)
                    service.TryResolvePlayingSource(source, channel, out cueKey, out displayName);

                bool debugMuted = debugPanel != null && debugPanel.IsDebugMuted(source);
                report.Playing.Add(new PlayingAudioSnapshot
                {
                    InstanceId = source.GetInstanceID(),
                    Source = source,
                    ClipName = source.clip != null ? source.clip.name : "(no clip)",
                    HierarchyPath = BuildPath(source.transform),
                    Channel = channel,
                    CueKey = cueKey,
                    CueDisplayName = displayName,
                    IsLoop = source.loop,
                    Volume = source.volume,
                    IsDebugMuted = debugMuted,
                });

                if (channel == AudioChannel.Music) report.MusicSourceCount++;
                else if (channel == AudioChannel.Loop) report.LoopSourceCount++;
            }

            report.Playing.Sort(CompareSnapshots);
            report.SuspectedDoubleBgm = report.MusicSourceCount > 1
                || (report.MusicSourceCount >= 1 && report.LoopSourceCount >= 1 && HasBgmLoop(report.Playing));
            return report;
        }

        static bool HasBgmLoop(List<PlayingAudioSnapshot> playing)
        {
            for (int i = 0; i < playing.Count; i++)
            {
                if (playing[i].Channel != AudioChannel.Loop) continue;
                if (!playing[i].CueKey.HasValue) continue;
                var key = playing[i].CueKey.Value;
                if (IsBgmKey(key)) return true;
            }
            return false;
        }

        static bool IsBgmKey(AudioKey key)
        {
            return key == AudioKey.BgmBattle
                || key == AudioKey.BgmBoss
                || key == AudioKey.BgmIsland
                || key == AudioKey.BgmRoute
                || key == AudioKey.BgmMainMenu;
        }

        static int CompareSnapshots(PlayingAudioSnapshot a, PlayingAudioSnapshot b)
        {
            int channel = a.Channel.CompareTo(b.Channel);
            if (channel != 0) return channel;
            return string.CompareOrdinal(a.ClipName, b.ClipName);
        }

        static AudioChannel Classify(Transform t, Transform musicRoot, Transform sfxRoot, Transform loopRoot)
        {
            if (IsUnderRoot(t, musicRoot)) return AudioChannel.Music;
            if (IsUnderRoot(t, loopRoot)) return AudioChannel.Loop;
            if (IsUnderRoot(t, sfxRoot)) return AudioChannel.Sfx;
            var path = BuildPath(t);
            if (path.Contains("AudioKit") || path.Contains("AudioManager"))
                return AudioChannel.AudioKit;
            return AudioChannel.Other;
        }

        static bool IsUnderRoot(Transform t, Transform root)
        {
            if (root == null || t == null) return false;
            return t == root || t.IsChildOf(root);
        }

        static string BuildPath(Transform t)
        {
            if (t == null) return "(null)";
            var parts = new List<string>(6);
            while (t != null)
            {
                parts.Add(t.name);
                t = t.parent;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
