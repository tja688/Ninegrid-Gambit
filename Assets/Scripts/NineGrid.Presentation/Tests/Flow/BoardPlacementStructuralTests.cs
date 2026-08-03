using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests.Flow
{
    /// <summary>
    /// ADR-0024 / #104：落格不 parent 到 GroundAnchors；无 RoomIconVisualFit 回流。
    /// </summary>
    public sealed class BoardPlacementStructuralTests
    {
        private static readonly string[] PresenterRelPaths =
        {
            Path.Combine("Flow", "ShopBoard", "ShopBoardPresenter.cs"),
            Path.Combine("Flow", "TavernBoard", "TavernBoardPresenter.cs"),
            Path.Combine("Flow", "RewardBoard", "RewardBoardPresenter.cs"),
            Path.Combine("Flow", "RoomIcons", "RoomIconBoardPresenter.cs"),
        };

        [Test]
        public void RoomIconVisualFit_TypeAndCallSitesRemoved()
        {
            var presentationRoot = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "Scripts",
                "NineGrid.Presentation"));
            Assert.IsFalse(
                File.Exists(Path.Combine(presentationRoot, "Flow", "RoomIcons", "RoomIconVisualFit.cs")),
                "RoomIconVisualFit.cs 须删除");

            for (var i = 0; i < PresenterRelPaths.Length; i++)
            {
                var text = File.ReadAllText(Path.Combine(presentationRoot, PresenterRelPaths[i]));
                Assert.IsFalse(
                    text.IndexOf("RoomIconVisualFit", StringComparison.Ordinal) >= 0,
                    "不得引用 RoomIconVisualFit: " + PresenterRelPaths[i]);
                Assert.IsFalse(
                    text.IndexOf("FitRoomIcon", StringComparison.Ordinal) >= 0,
                    "不得调用 FitRoomIcon: " + PresenterRelPaths[i]);
                Assert.IsFalse(
                    text.IndexOf("FitSpawnedIcon", StringComparison.Ordinal) >= 0,
                    "不得调用 FitSpawnedIcon: " + PresenterRelPaths[i]);
                Assert.IsFalse(
                    text.IndexOf("FitToTarget", StringComparison.Ordinal) >= 0,
                    "不得调用 FitToTarget: " + PresenterRelPaths[i]);
            }
        }

        [Test]
        public void Presenters_DoNotParentSpawnedObjectsToGroundAnchors()
        {
            var presentationRoot = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "Scripts",
                "NineGrid.Presentation"));

            for (var i = 0; i < PresenterRelPaths.Length; i++)
            {
                var text = File.ReadAllText(Path.Combine(presentationRoot, PresenterRelPaths[i]));
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
            }
        }

        [Test]
        public void SlotHitBoxSize_RemovedFromGroundFieldLayoutSettings()
        {
            var path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "Scripts",
                "NineGrid.Presentation",
                "Cards",
                "GroundFieldLayoutSettings.cs"));
            var text = File.ReadAllText(path);
            Assert.IsFalse(
                text.IndexOf("slotHitBoxSize", StringComparison.Ordinal) >= 0,
                "GroundFieldLayoutSettings.slotHitBoxSize 须删除");

            var handPath = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "Scripts",
                "NineGrid.Presentation",
                "Cards",
                "CardHandLayoutSettings.cs"));
            var handText = File.ReadAllText(handPath);
            Assert.IsTrue(
                handText.IndexOf("handHitBoxSize", StringComparison.Ordinal) >= 0,
                "handHitBoxSize 须保留");
        }
    }
}
