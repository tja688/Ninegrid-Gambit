using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    public sealed class AudioPlaybackStructuralTests
    {
        private static readonly Regex DirectPlaybackCall = new Regex(
            @"(?<![A-Za-z0-9_])(?:AudioKit|MMSoundManager)\s*\.",
            RegexOptions.CultureInvariant);

        private static readonly Regex MusicTrackSelection = new Regex(
            @"MMSoundManagerTracks\s*\.\s*Music",
            RegexOptions.CultureInvariant);

        [Test]
        public void ProjectBusinessSources_DoNotBypassAudioPlaybackAdapter()
        {
            var adapterPath = Normalize(Path.Combine(
                Application.dataPath,
                "Scripts",
                "NineGrid.Presentation",
                "Systems",
                "MMSoundManagerAudioPlaybackAdapter.cs"));
            var offenders = new List<string>();

            foreach (var sourcePath in EnumerateProjectSources())
            {
                if (string.Equals(sourcePath, adapterPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var text = File.ReadAllText(sourcePath);
                if (DirectPlaybackCall.IsMatch(text))
                {
                    offenders.Add(sourcePath);
                }
            }

            Assert.IsEmpty(
                offenders,
                "业务源码不得直接调用 AudioKit 或 MMSoundManager；唯一允许位置是 MMSoundManagerAudioPlaybackAdapter：\n"
                + string.Join("\n", offenders.ToArray()));
        }

        [Test]
        public void SfxAndMusicTrackSelection_IsOwnedByAudioAdapterOnly()
        {
            var adapterPath = Normalize(Path.Combine(
                Application.dataPath,
                "Scripts",
                "NineGrid.Presentation",
                "Systems",
                "MMSoundManagerAudioPlaybackAdapter.cs"));
            var offenders = new List<string>();

            foreach (var sourcePath in EnumerateProjectSources())
            {
                var text = File.ReadAllText(sourcePath);
                if (MusicTrackSelection.IsMatch(text)
                    && !string.Equals(sourcePath, adapterPath, StringComparison.OrdinalIgnoreCase))
                {
                    offenders.Add(sourcePath);
                }
            }

            Assert.IsEmpty(offenders, "MMSoundManager Music/Sfx 轨选择必须封装在音频 Adapter 内。");
            Assert.IsTrue(File.Exists(adapterPath), "缺少唯一 MMSoundManager 音频 Adapter。");
        }

        private static IEnumerable<string> EnumerateProjectSources()
        {
            var scriptsRoot = Normalize(Path.Combine(Application.dataPath, "Scripts"));
            var paths = Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories);
            for (var i = 0; i < paths.Length; i++)
            {
                var normalized = Normalize(paths[i]);
                if (normalized.IndexOf("/Tests/", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                yield return normalized;
            }
        }

        private static string Normalize(string path)
        {
            return path.Replace('\\', '/');
        }
    }
}
