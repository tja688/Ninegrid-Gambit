using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    /// <summary>#200 结构护栏：扫描项目自有源码，禁止绕过类型化 VFX Cue/State 入口、动态 UID Binding Key，以及程序化播放器越界改共享视觉所有权。</summary>
    public sealed class VfxStructureGuardTests
    {
        private static readonly string[] ScanRoots =
        {
            "Assets/Scripts/NineGrid.Presentation",
            "Assets/Scripts/NineGrid.Foundation/NineGrid.Content",
            "Assets/Scripts/NineGrid.Foundation/NineGrid.Core",
            "Assets/Scripts/NineGrid.Core",
        };

        private static readonly HashSet<string> RequestCueAllowlist = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            "Assets/Scripts/NineGrid.Presentation/Systems/VfxSystem.cs",
            "Assets/Scripts/NineGrid.Presentation/Flow/Presentation/VfxTriggerPulseSink.cs",
        };

        private static readonly HashSet<string> SetSlotAllowlist = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            "Assets/Scripts/NineGrid.Presentation/Systems/VfxSystem.cs",
        };

        private static readonly HashSet<string> StartPulseAllowlistPrefixes = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            "Assets/Scripts/NineGrid.Presentation/Systems/Vfx/",
            "Assets/Scripts/NineGrid.Presentation/Systems/VfxSystem.cs",
        };

        private static readonly HashSet<string> GoldGainFxCommentAllowlist = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            "Assets/Scripts/NineGrid.Presentation/Systems/Vfx/VfxGoldFlightPlayer.cs",
        };

        [Test]
        public void ProjectSources_ForbidBypassingTypedVfxEntry()
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
                    if (ShouldSkip(relative))
                    {
                        continue;
                    }

                    var text = File.ReadAllText(file);
                    ScanFile(relative, text, violations);
                }
            }

            Assert.IsEmpty(
                violations,
                "VFX 结构护栏违规：\n" + string.Join("\n", violations.Take(40)));
        }

        [Test]
        public void ProgrammaticPlayers_DoNotBypassDomainHostsForSharedVisualOwnership()
        {
            var projectRoot = FindProjectRoot();
            var playersRoot = Path.Combine(
                projectRoot,
                "Assets",
                "Scripts",
                "NineGrid.Presentation",
                "Systems",
                "Vfx");
            Assert.IsTrue(Directory.Exists(playersRoot), "缺少 Systems/Vfx 播放器目录");

            var violations = new List<string>();
            foreach (var file in Directory.EnumerateFiles(playersRoot, "*.cs", SearchOption.TopDirectoryOnly))
            {
                var relative = ToAssetPath(projectRoot, file);
                if (relative.EndsWith("Tests.cs", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var code = StripComments(File.ReadAllText(file));
                if (Regex.IsMatch(code, @"\bCamera\.main\b"))
                {
                    violations.Add(relative + ": 程序化/素材播放器禁止直取 Camera.main（须经视觉域宿主）。");
                }

                if (Regex.IsMatch(code, @"\bGameObject\.Find\b")
                    || Regex.IsMatch(code, @"\bFindObjectOfType\b")
                    || Regex.IsMatch(code, @"\bFindObjectsOfType\b")
                    || Regex.IsMatch(code, @"\bFindFirstObjectByType\b")
                    || Regex.IsMatch(code, @"\bFindAnyObjectByType\b"))
                {
                    violations.Add(relative + ": 程序化/素材播放器禁止按名/类型搜索场景对象绕过域宿主。");
                }

                if (Regex.IsMatch(code, @"\bSortingGroup\b"))
                {
                    violations.Add(relative + ": 播放器禁止触碰卡级 SortingGroup；排序边界经域宿主协商。");
                }

                if (Regex.IsMatch(code, @"\bworldCamera\b")
                    || Regex.IsMatch(code, @"GetComponent(?:InParent|InChildren)?\s*<\s*Canvas\b"))
                {
                    violations.Add(relative + ": 播放器禁止修改共享 Canvas / worldCamera。");
                }
            }

            Assert.IsEmpty(
                violations,
                "域宿主越界违规：\n" + string.Join("\n", violations.Take(40)));
        }

        [Test]
        public void ProductionSources_DoNotReviveGoldGainFxManagerSingleton()
        {
            var projectRoot = FindProjectRoot();
            var bannedPath = Path.Combine(
                projectRoot,
                "Assets",
                "Scripts",
                "NineGrid.Presentation",
                "Flow",
                "GoldGainFxManagerSingleton.cs");
            Assert.IsFalse(
                File.Exists(bannedPath),
                "禁止复活 GoldGainFxManagerSingleton.cs（#204 已删，金币走 economy.gold_flight）。");

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
                    if (ShouldSkip(relative) || GoldGainFxCommentAllowlist.Contains(relative))
                    {
                        continue;
                    }

                    var code = StripComments(File.ReadAllText(file));
                    if (code.IndexOf("GoldGainFxManagerSingleton", StringComparison.Ordinal) >= 0)
                    {
                        violations.Add(relative + ": 禁止生产引用已删的 GoldGainFxManagerSingleton。");
                    }
                }
            }

            Assert.IsEmpty(
                violations,
                "旧金币壳复活违规：\n" + string.Join("\n", violations.Take(40)));
        }

        private static void ScanFile(string relativePath, string text, List<string> violations)
        {
            var code = StripComments(text);

            if (Regex.IsMatch(code, @"\bstatic\s+class\s+\w*Pulse\w*Hub\b")
                && relativePath.IndexOf("TriggerPulseHub.cs", StringComparison.OrdinalIgnoreCase) < 0)
            {
                violations.Add(relativePath + ": 禁止新增第二个业务静态 Pulse Hub（须复用 TriggerPulseHub）。");
            }

            if (Regex.IsMatch(code, @"\.RequestCue\s*\(")
                && IsVfxRequestCueSite(code)
                && !RequestCueAllowlist.Contains(relativePath))
            {
                violations.Add(relativePath + ": 业务不得直调 IVfxSystem.RequestCue；须经 TriggerPulseHub.PulseVfx。");
            }

            if (Regex.IsMatch(code, @"\.SetSlot\s*\(")
                && !SetSlotAllowlist.Contains(relativePath))
            {
                violations.Add(relativePath + ": 业务不得直调 IVfxSystem.SetSlot；持续状态经类型化期望槽 API / 工作台。");
            }

            if (Regex.IsMatch(code, @"\.StartPulse\s*\(")
                && !IsUnderAllowlistedPrefix(relativePath, StartPulseAllowlistPrefixes))
            {
                violations.Add(relativePath + ": 业务不得直调 IVfxPulsePlayer.StartPulse；须经 VfxSystem 解析后创建。");
            }

            if (Regex.IsMatch(
                    code,
                    @"VfxCueRequest\.(?:Simple\s*\([^;]*\+|new\s+VfxCueRequest\s*\([^;]*\+)"))
            {
                violations.Add(relativePath + ": 禁止用字符串拼接构造 VfxCueRequest（须稳定声明常量）。");
            }

            if (Regex.IsMatch(
                    code,
                    @"(""fx\.card\.""\s*\+|\$""fx\.card\.\{)")
                && relativePath.IndexOf("CardEffectTriggerPulseSink.cs", StringComparison.OrdinalIgnoreCase) < 0
                && relativePath.IndexOf("EffectTriggerPulseBeatHandler.cs", StringComparison.OrdinalIgnoreCase) < 0)
            {
                violations.Add(relativePath + ": 禁止在新路径拼动态 fx.card.<uid>；旧 FX 通道仅限既有 sink/handler。");
            }

            if (Regex.IsMatch(
                    code,
                    @"VfxBindingKey\.Compose\s*\([^)]*(CardUid|DiagnosticCardUid|\.Uid\b|\$"")"))
            {
                violations.Add(relativePath + ": 禁止把运行时 UID 写入 VfxBindingKey。");
            }

            if (Regex.IsMatch(
                    code,
                    @"(""vfx\.[^""]*""\s*\+|\$""vfx\.[^""]*\{)"))
            {
                violations.Add(relativePath + ": 禁止动态拼接 VFX Cue/State ID。");
            }
        }

        private static bool IsVfxRequestCueSite(string code)
        {
            // 避免把 AudioSystem.RequestCue 误判为 VFX 绕过。
            return code.IndexOf("IVfxSystem", StringComparison.Ordinal) >= 0
                || code.IndexOf("VfxSystem", StringComparison.Ordinal) >= 0
                || code.IndexOf("VfxCueRequest", StringComparison.Ordinal) >= 0
                || code.IndexOf("VfxSpatialContext", StringComparison.Ordinal) >= 0;
        }

        private static bool IsUnderAllowlistedPrefix(string relativePath, HashSet<string> prefixes)
        {
            foreach (var prefix in prefixes)
            {
                if (relativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(relativePath, prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ShouldSkip(string relative)
        {
            return relative.IndexOf("/Tests/", StringComparison.OrdinalIgnoreCase) >= 0
                || relative.EndsWith("Tests.cs", StringComparison.OrdinalIgnoreCase)
                || relative.IndexOf(".Editor/", StringComparison.OrdinalIgnoreCase) >= 0
                || relative.IndexOf("/NineGrid.Content.Editor/", StringComparison.OrdinalIgnoreCase) >= 0
                || relative.IndexOf("/NineGrid.DevTest/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string StripComments(string text)
        {
            var withoutBlock = Regex.Replace(text, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            var withoutLine = Regex.Replace(withoutBlock, @"//.*?$", " ", RegexOptions.Multiline);
            return Regex.Replace(withoutLine, @"///.*?$", " ", RegexOptions.Multiline);
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
