using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// #126 清理护栏：构建启用场景与预制体不得引用无法解析的 m_Script（Missing Script）。
    /// 防止已删除脚本（如 TableNineSortingKey）的组件残留回流 Release。
    /// </summary>
    public sealed class BuildSceneMissingScriptStructuralTests
    {
        [Test]
        public void EnabledBuildScenesAndPrefabs_DoNotReference_UnresolvableScripts()
        {
            var offenders = new List<string>();
            var scanned = 0;

            foreach (var assetPath in GetGatedAssetPaths())
            {
                scanned++;
                var yaml = ReadProjectText(assetPath);

                // 1) 带 GUID 但解析不到脚本（.cs.meta / 包脚本）的引用
                var guids = Regex.Matches(
                        yaml,
                        @"m_Script: \{fileID: 11500000, guid: ([0-9a-f]{32}), type: 3\}")
                    .Cast<Match>()
                    .Select(m => m.Groups[1].Value)
                    .Distinct();
                foreach (var guid in guids)
                {
                    if (!ScriptGuidExists(guid))
                    {
                        offenders.Add(assetPath + " :: guid " + guid);
                    }
                }

                // 2) fileID-only 的 m_Script（无 guid/type，破坏引用 → 加载即 Missing Script）
                if (Regex.IsMatch(yaml, @"m_Script: \{fileID: \d+\}\s*$", RegexOptions.Multiline))
                {
                    offenders.Add(assetPath + " :: fileID-only m_Script（破坏引用）");
                }
            }

            Assert.Greater(scanned, 0, "门禁未扫描到任何场景/预制体");
            Assert.IsEmpty(
                offenders,
                "构建场景与预制体不得含 Missing Script 引用：\n" + string.Join("\n", offenders));
        }

        private static IEnumerable<string> GetGatedAssetPaths()
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var paths = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToList();
            paths.AddRange(Directory
                .EnumerateFiles(Path.Combine(root, "Assets"), "*.prefab", SearchOption.AllDirectories)
                .Where(full => !full.Replace('\\', '/').Contains("/Plugins/"))
                .Select(full => full.Substring(root.Length).TrimStart('\\', '/')));
            return paths;
        }

        private static bool ScriptGuidExists(string guid)
        {
            var meta = "Assets/**/" + guid + ".meta";
            // 直接按 GUID 文件名检索成本高；改为查 Assets 与包内全部 .cs.meta 一次并缓存。
            return AllScriptGuids.Contains(guid);
        }

        private static readonly HashSet<string> AllScriptGuids = BuildScriptGuids();

        private static HashSet<string> BuildScriptGuids()
        {
            var set = new HashSet<string>();
            var roots = new[] { "Assets", "Packages", "Library/PackageCache" };
            foreach (var root in roots)
            {
                var full = Path.GetFullPath(Path.Combine(Application.dataPath, "..", root));
                if (!Directory.Exists(full))
                {
                    continue;
                }

                foreach (var meta in Directory.EnumerateFiles(full, "*.cs.meta", SearchOption.AllDirectories))
                {
                    var text = File.ReadAllText(meta);
                    var m = Regex.Match(text, @"^guid:\s*([0-9a-f]{32})\s*$", RegexOptions.Multiline);
                    if (m.Success)
                    {
                        set.Add(m.Groups[1].Value);
                    }
                }
            }

            return set;
        }

        private static string ReadProjectText(string relativePath)
        {
            var full = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
            Assert.IsTrue(File.Exists(full), "missing " + relativePath);
            return File.ReadAllText(full);
        }
    }
}
