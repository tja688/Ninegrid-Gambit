using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// #127 / ADR-0029 内容护栏：测试不得断言可变的生产内容 displayName。
    /// 命中模式：同一行出现 Assert 与 DisplayName 的取值比较（排除 IsNullOrEmpty 非空检查、
    /// DisplayNameZh 等非内容字段）；白名单文件仅限自建夹具 / 代码常量，不依赖生产 JSON 文案。
    /// 局限：跨行断言的 DisplayName 不在本护栏扫描范围，靠 code review 兜底。
    /// </summary>
    public sealed class ContentDisplayNameAssertGuardrailTests
    {
        private static readonly string[] ExemptFiles =
        {
            // 自建夹具投影测试：displayName 由测试自身构造并验证字段映射，生产改名不影响。
            "ContentJsonCatalogProjectionTests.cs",
            "HelpCardJsonCatalogProjectionTests.cs",
            "CardPresentationAuthorityTests.cs",
            // QuickTest 预设通道名是代码常量，非内容数据。
            "WalkSandboxRetirementStructuralTests.cs",
        };

        [Test]
        public void TestSources_DoNotAssertContentDisplayName()
        {
            var roots = new[]
            {
                Path.GetFullPath(Path.Combine(Application.dataPath, "Scripts", "NineGrid.Foundation", "NineGrid.Core.Tests")),
                Path.GetFullPath(Path.Combine(Application.dataPath, "Scripts", "NineGrid.Presentation", "Tests")),
            };

            var violations = new List<string>();
            for (var r = 0; r < roots.Length; r++)
            {
                if (!Directory.Exists(roots[r]))
                {
                    continue;
                }

                var files = Directory.GetFiles(roots[r], "*.cs", SearchOption.AllDirectories);
                for (var f = 0; f < files.Length; f++)
                {
                    var fileName = Path.GetFileName(files[f]);
                    if (IsExempt(fileName))
                    {
                        continue;
                    }

                    var lines = File.ReadAllLines(files[f]);
                    for (var i = 0; i < lines.Length; i++)
                    {
                        var line = lines[i];
                        if (!line.Contains("Assert")
                            || line.Contains("string.IsNullOrEmpty")
                            || !ContainsDisplayNameValueUse(line))
                        {
                            continue;
                        }

                        violations.Add(fileName + ":" + (i + 1) + ": " + line.Trim());
                    }
                }
            }

            Assert.AreEqual(
                0,
                violations.Count,
                "测试断言了可变 displayName（应改断言稳定 ID / 槽位 / sequence）：\n"
                + string.Join("\n", violations));
        }

        private static bool IsExempt(string fileName)
        {
            for (var i = 0; i < ExemptFiles.Length; i++)
            {
                if (string.Equals(ExemptFiles[i], fileName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsDisplayNameValueUse(string line)
        {
            var needle = "DisplayName";
            var idx = line.IndexOf(needle, System.StringComparison.Ordinal);
            while (idx >= 0)
            {
                var next = idx + needle.Length;
                if (next < line.Length)
                {
                    var c = line[next];
                    if (c == '"' || c == ')' || c == ';')
                    {
                        return true;
                    }
                }

                idx = line.IndexOf(needle, next, System.StringComparison.Ordinal);
            }

            return false;
        }
    }
}
