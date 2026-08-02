using System;
using System.IO;
using System.Linq;
using NineGrid.Core;
using NineGrid.Flow;
using NineGrid.Presentation.Systems;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests.FlowShell
{
    /// <summary>
    /// #91：退役跳格沙盒；\0 改为流程测试通道。
    /// </summary>
    public sealed class WalkSandboxRetirementStructuralTests
    {
        private static readonly string PresentationRoot = Path.GetFullPath(
            Path.Combine(Application.dataPath, "Scripts", "NineGrid.Presentation"));

        private static readonly string CoreRoot = Path.GetFullPath(
            Path.Combine(Application.dataPath, "Scripts", "NineGrid.Foundation", "NineGrid.Core"));

        [Test]
        public void Channel0_IsFlowTest_NotWalkSandbox()
        {
            Assert.IsTrue(QuickTestDeckCatalog.TryResolvePickerCode(0, out var preset));
            Assert.AreEqual("流程测试", preset.DisplayName);
            Assert.AreEqual(0, preset.SkillIds.Count);
            Assert.AreEqual(0, preset.TrapContentIds.Count);
            Assert.AreEqual(QuickTestNodeOrderMode.Sequential, preset.NodeOrder);
            Assert.IsTrue(string.IsNullOrEmpty(preset.PinnedFirstBattleDeckId));
        }

        [Test]
        public void GameCommandKind_HasNoStartWalkSandboxNode()
        {
            Assert.IsFalse(
                Enum.IsDefined(typeof(GameCommandKind), "StartWalkSandboxNode"),
                "GameCommandKind.StartWalkSandboxNode 应已删除");
        }

        [Test]
        public void IBattleSessionSystem_HasNoStartWalkSandboxNodeAsync()
        {
            Assert.IsNull(
                typeof(IBattleSessionSystem).GetMethod("StartWalkSandboxNodeAsync"),
                "IBattleSessionSystem 不应再暴露 StartWalkSandboxNodeAsync");
        }

        [Test]
        public void QuickTestRunOptions_HasNoWalkSandboxField()
        {
            Assert.IsNull(
                typeof(QuickTestRunOptions).GetField("WalkSandbox"),
                "QuickTestRunOptions.WalkSandbox 应已删除");
        }

        [Test]
        public void ProductionSources_DoNotReferenceWalkSandbox()
        {
            var tokens = new[]
            {
                "WalkSandbox",
                "StartWalkSandboxNode",
                "CreateWalkSandbox",
                "EnterWalkSandbox",
            };
            var offenders = EnumerateProductionCs(PresentationRoot)
                .Concat(EnumerateProductionCs(CoreRoot))
                .Where(path =>
                {
                    var text = File.ReadAllText(path);
                    for (var i = 0; i < tokens.Length; i++)
                    {
                        if (text.IndexOf(tokens[i], StringComparison.Ordinal) >= 0)
                        {
                            return true;
                        }
                    }

                    return false;
                })
                .Select(Relativize)
                .ToArray();

            Assert.IsEmpty(
                offenders,
                "生产脚本仍引用跳格沙盒：\n" + string.Join("\n", offenders));
        }

        private static string[] EnumerateProductionCs(string root)
        {
            return Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(path => path.IndexOf($"{Path.DirectorySeparatorChar}Tests{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) < 0)
                .ToArray();
        }

        private static string Relativize(string path)
        {
            var data = Application.dataPath.Replace('\\', '/');
            var normalized = path.Replace('\\', '/');
            return normalized.StartsWith(data, StringComparison.OrdinalIgnoreCase)
                ? "Assets" + normalized.Substring(data.Length)
                : normalized;
        }
    }
}
