using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    /// <summary>#179 结构护栏：扫描项目自有源码，禁止绕过声音提示/音乐状态入口。</summary>
    public sealed class AudioStructureGuardTests
    {
        private static readonly string[] ScanRoots =
        {
            "Assets/Scripts/NineGrid.Presentation",
            "Assets/Scripts/NineGrid.Foundation/NineGrid.Content",
            "Assets/Scripts/NineGrid.Foundation/NineGrid.Core",
            "Assets/Scripts/NineGrid.Core",
        };

        private static readonly HashSet<string> MmSoundManagerAllowlist = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            "Assets/Scripts/NineGrid.Presentation/Systems/MMSoundManagerAudioPlaybackAdapter.cs",
            "Assets/Scripts/NineGrid.Presentation/Systems/MMSoundManagerBootstrap.cs",
            "Assets/Scripts/NineGrid.Presentation/Systems/PlayerAudioSettingsSystem.cs",
        };

        private static readonly HashSet<string> MusicTrackAllowlist = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            "Assets/Scripts/NineGrid.Presentation/Systems/MMSoundManagerAudioPlaybackAdapter.cs",
        };

        private static readonly HashSet<string> BarePulseAllowlist = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            "Assets/Scripts/NineGrid.Presentation/Flow/Presentation/TriggerPulseHub.cs",
            "Assets/Scripts/NineGrid.Presentation/Flow/Presentation/AudioTriggerPulseSink.cs",
        };

        [Test]
        public void ProjectSources_ForbidBypassingAudioDeepModule()
        {
            var projectRoot = FindProjectRoot();
            var violations = new List<string>();

            foreach (var relativeRoot in ScanRoots)
            {
                var absoluteRoot = Path.Combine(projectRoot, relativeRoot.Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(absoluteRoot))
                {
                    continue;
                }

                foreach (var file in Directory.EnumerateFiles(absoluteRoot, "*.cs", SearchOption.AllDirectories))
                {
                    var relative = ToAssetPath(projectRoot, file);
                    if (relative.IndexOf("/Tests/", StringComparison.OrdinalIgnoreCase) >= 0
                        || relative.EndsWith("Tests.cs", StringComparison.OrdinalIgnoreCase)
                        || relative.IndexOf(".Editor/", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        continue;
                    }

                    var text = File.ReadAllText(file);
                    ScanFile(relative, text, violations);
                }
            }

            Assert.IsEmpty(
                violations,
                "结构护栏违规：\n" + string.Join("\n", violations.Take(40)));
        }

        private static void ScanFile(string relativePath, string text, List<string> violations)
        {
            var code = StripComments(text);

            if (Regex.IsMatch(code, @"\bAudioKit\b"))
            {
                violations.Add(relativePath + ": 禁止业务直接引用 AudioKit。");
            }

            if (ContainsBareMmSoundManagerToken(code)
                && !MmSoundManagerAllowlist.Contains(relativePath))
            {
                violations.Add(relativePath + ": 禁止业务直接调用 MMSoundManager（仅 Adapter/总线允许）。");
            }

            if (code.IndexOf("MMSoundManagerTracks.Music", StringComparison.Ordinal) >= 0
                && !MusicTrackAllowlist.Contains(relativePath))
            {
                violations.Add(relativePath + ": 禁止非音乐 Adapter 选择 Music 轨。");
            }

            if (Regex.IsMatch(code, @"PulseAudio\s*\(\s*""")
                && !BarePulseAllowlist.Contains(relativePath))
            {
                violations.Add(relativePath + ": 禁止裸 cue 字符串调用 PulseAudio(\"...\")。");
            }

            if (Regex.IsMatch(
                    code,
                    @"(""sfx\.effect\.""\s*\+|\$""sfx\.effect\.\{)"))
            {
                violations.Add(relativePath + ": 禁止动态 UID 声音提示键（sfx.effect.<uid>）。");
            }
        }

        private static string StripComments(string text)
        {
            var withoutBlock = Regex.Replace(text, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            var withoutLine = Regex.Replace(withoutBlock, @"//.*?$", " ", RegexOptions.Multiline);
            return Regex.Replace(withoutLine, @"///.*?$", " ", RegexOptions.Multiline);
        }

        private static bool ContainsBareMmSoundManagerToken(string text)
        {
            // 允许类型名 MMSoundManagerAudio* / SettingsSO；抓类型实例与静态成员。
            return Regex.IsMatch(
                text,
                @"(?<![\w])MMSoundManager(?!Audio|Settings|PlayOptions|Tracks)(\.|\s|/)");
        }

        private static string FindProjectRoot()
        {
            var dir = new DirectoryInfo(Application.dataPath).Parent;
            Assert.IsNotNull(dir);
            return dir.FullName;
        }

        private static string ToAssetPath(string projectRoot, string absolutePath)
        {
            var normalizedRoot = projectRoot.Replace('\\', '/').TrimEnd('/');
            var normalizedPath = absolutePath.Replace('\\', '/');
            if (normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                return normalizedPath.Substring(normalizedRoot.Length).TrimStart('/');
            }

            return normalizedPath;
        }
    }
}
