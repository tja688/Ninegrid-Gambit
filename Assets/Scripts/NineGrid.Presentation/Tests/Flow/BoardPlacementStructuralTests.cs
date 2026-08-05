using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests.Flow
{
    /// <summary>
    /// ADR-0024 / #104+#105：落格不 parent 到 GroundAnchors；无 Fit / slotHitBoxSize 回流；
    /// 落格路径不写 localScale 绝对值。
    /// </summary>
    public sealed class BoardPlacementStructuralTests
    {
        private static readonly string[] PresenterRelPaths =
        {
            Path.Combine("Flow", "ShopBoard", "ShopBoardPresenter.cs"),
            Path.Combine("Flow", "TavernBoard", "TavernBoardPresenter.cs"),
            Path.Combine("Flow", "RewardBoard", "RewardBoardPresenter.cs"),
            Path.Combine("Flow", "AttributeBoard", "AttributeBoardPresenter.cs"),
            Path.Combine("Flow", "RoomIcons", "RoomIconBoardPresenter.cs"),
        };

        private static readonly string[] FitBannedTokens =
        {
            "RoomIconVisualFit",
            "FitRoomIcon",
            "FitSpawnedIcon",
            "FitToTarget",
            "ResolveTargetSize",
        };

        private static string PresentationRoot => Path.GetFullPath(Path.Combine(
            Application.dataPath,
            "Scripts",
            "NineGrid.Presentation"));

        private static string[] EnumerateProductionCs()
        {
            return Directory
                .EnumerateFiles(PresentationRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path =>
                    path.IndexOf("\\Tests\\", StringComparison.OrdinalIgnoreCase) < 0
                    && path.IndexOf("/Tests/", StringComparison.OrdinalIgnoreCase) < 0
                    && path.IndexOf("\\Editor\\", StringComparison.OrdinalIgnoreCase) < 0
                    && path.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) < 0)
                .ToArray();
        }

        private static string Rel(string path) =>
            path.Substring(Application.dataPath.Length).TrimStart('\\', '/');

        [Test]
        public void RoomIconVisualFit_TypeAndCallSitesRemoved()
        {
            Assert.IsFalse(
                File.Exists(Path.Combine(PresentationRoot, "Flow", "RoomIcons", "RoomIconVisualFit.cs")),
                "RoomIconVisualFit.cs 须删除");

            for (var i = 0; i < PresenterRelPaths.Length; i++)
            {
                var text = File.ReadAllText(Path.Combine(PresentationRoot, PresenterRelPaths[i]));
                for (var t = 0; t < FitBannedTokens.Length; t++)
                {
                    Assert.IsFalse(
                        text.IndexOf(FitBannedTokens[t], StringComparison.Ordinal) >= 0,
                        "不得引用 " + FitBannedTokens[t] + ": " + PresenterRelPaths[i]);
                }
            }
        }

        [Test]
        public void ProductionSources_DoNotContainFitApisOrSlotHitBoxSize()
        {
            var offenders = EnumerateProductionCs()
                .Where(path =>
                {
                    var text = File.ReadAllText(path);
                    if (text.IndexOf("slotHitBoxSize", StringComparison.Ordinal) >= 0)
                    {
                        return true;
                    }

                    for (var i = 0; i < FitBannedTokens.Length; i++)
                    {
                        if (text.IndexOf(FitBannedTokens[i], StringComparison.Ordinal) >= 0)
                        {
                            return true;
                        }
                    }

                    return false;
                })
                .Select(Rel)
                .ToArray();

            Assert.IsEmpty(
                offenders,
                "生产代码禁止 Fit API / slotHitBoxSize 回流：\n" + string.Join("\n", offenders));
        }

        [Test]
        public void Presenters_DoNotParentSpawnedObjectsToGroundAnchors()
        {
            for (var i = 0; i < PresenterRelPaths.Length; i++)
            {
                var text = File.ReadAllText(Path.Combine(PresentationRoot, PresenterRelPaths[i]));
                Assert.IsFalse(
                    text.IndexOf("Instantiate(prefab, parent)", StringComparison.Ordinal) >= 0,
                    "不得 Instantiate(prefab, parent) 到格位锚点: " + PresenterRelPaths[i]);
                Assert.IsFalse(
                    text.IndexOf("Instantiate(prefab, anchor)", StringComparison.Ordinal) >= 0,
                    "不得 Instantiate(prefab, anchor): " + PresenterRelPaths[i]);
                // SpawnPresentationOnly 不得把 GetGroundAnchor 当 parent 传入。
                Assert.IsFalse(
                    text.Contains("SpawnPresentationOnly(\n                    entry.DefId,\n                    parent,", StringComparison.Ordinal)
                    || text.Contains("SpawnPresentationOnly(\r\n                    entry.DefId,\r\n                    parent,", StringComparison.Ordinal),
                    "SpawnPresentationOnly 不得以格位锚点为 parent: " + PresenterRelPaths[i]);
                Assert.IsTrue(
                    text.IndexOf("parent: null", StringComparison.Ordinal) >= 0
                    || text.IndexOf("BoardSlotWorldPlacement.TryAlignToSlot", StringComparison.Ordinal) >= 0,
                    "落格须 parent:null 或 BoardSlotWorldPlacement: " + PresenterRelPaths[i]);
            }
        }

        [Test]
        public void ProductionSources_DoNotParentToGroundAnchors()
        {
            var parentViaAnchor = new Regex(
                @"(Instantiate\s*\([^;]{0,200}GetGroundAnchor|SetParent\s*\([^;]{0,200}GetGroundAnchor|SpawnPresentationOnly\s*\([^;]{0,200}GetGroundAnchor)",
                RegexOptions.CultureInvariant);
            // GetGroundAnchor(...) 赋给任意标识符后，紧跟 SetParent / Instantiate 第二参用该标识符。
            var assignThenParent = new Regex(
                @"(?:var|Transform)\s+(?<id>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*[^;]*GetGroundAnchor\s*\([^)]*\)\s*;[\s\S]{0,320}?(\.SetParent\s*\(\s*\k<id>\b|Instantiate\s*\([^;,]{0,120},\s*\k<id>\b)",
                RegexOptions.CultureInvariant);

            var offenders = EnumerateProductionCs()
                .Where(path =>
                {
                    var text = File.ReadAllText(path);
                    return parentViaAnchor.IsMatch(text) || assignThenParent.IsMatch(text);
                })
                .Select(Rel)
                .ToArray();

            Assert.IsEmpty(
                offenders,
                "生产代码禁止把对象 parent 到 GroundAnchors：\n" + string.Join("\n", offenders));
        }

        [Test]
        public void BoardSlotPlacementPaths_DoNotWriteLocalScale()
        {
            // 落格对齐只写世界位置；尺寸权威在预制体（ADR-0024）。
            var paths = PresenterRelPaths
                .Concat(new[] { Path.Combine("Flow", "BoardSlotWorldPlacement.cs") })
                .ToArray();

            for (var i = 0; i < paths.Length; i++)
            {
                var text = File.ReadAllText(Path.Combine(PresentationRoot, paths[i]));
                Assert.IsFalse(
                    Regex.IsMatch(text, @"\.localScale\s*="),
                    "落格路径不得写 localScale 绝对值: " + paths[i]);
            }
        }

        [Test]
        public void SlotHitBoxSize_RemovedFromGroundFieldLayoutSettings()
        {
            var path = Path.Combine(PresentationRoot, "Cards", "GroundFieldLayoutSettings.cs");
            var text = File.ReadAllText(path);
            Assert.IsFalse(
                text.IndexOf("slotHitBoxSize", StringComparison.Ordinal) >= 0,
                "GroundFieldLayoutSettings.slotHitBoxSize 须删除");

            var handPath = Path.Combine(PresentationRoot, "Cards", "CardHandLayoutSettings.cs");
            var handText = File.ReadAllText(handPath);
            Assert.IsTrue(
                handText.IndexOf("handHitBoxSize", StringComparison.Ordinal) >= 0,
                "handHitBoxSize 须保留");
        }
    }
}
