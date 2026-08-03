using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// #103 / ADR-0023：退役野生拾取路径——禁 z=0 TryPickCollider、手牌 OverlapPointAll、
    /// HUD 私有 ScreenToWorld、legacy OnMouse*。
    /// </summary>
    public sealed class WildPickPathStructuralTests
    {
        private static readonly string PresentationRoot = Path.GetFullPath(
            Path.Combine(Application.dataPath, "Scripts", "NineGrid.Presentation"));

        private static readonly string TemporaryTestRoot = Path.GetFullPath(
            Path.Combine(Application.dataPath, "Scripts", "Temporary Test"));

        [Test]
        public void WorldPointerUtility_TryPickCollider_IsRetired()
        {
            var text = File.ReadAllText(Path.Combine(PresentationRoot, "Flow", "WorldPointerUtility.cs"));
            Assert.IsFalse(
                Regex.IsMatch(text, @"\bbool\s+TryPickCollider\s*\("),
                "WorldPointerUtility.TryPickCollider 须退役（z=0 野生拾取）");
        }

        [Test]
        public void CardHandManager_DoesNotResolveDropViaOverlapPointAll()
        {
            var text = File.ReadAllText(Path.Combine(PresentationRoot, "Cards", "CardHandManagerSingleton.cs"));
            Assert.IsFalse(
                text.Contains("OverlapPointAll"),
                "手牌拖拽落点须交棒场地面，不得自算 OverlapPointAll");
            Assert.IsTrue(
                text.Contains("TryResolveSlotAtWorld"),
                "手牌落点须查询场地面 TryResolveSlotAtWorld");
        }

        [Test]
        public void PlayerInfoHud_DoesNotUseForcedZ0ScreenToWorld()
        {
            var text = File.ReadAllText(Path.Combine(PresentationRoot, "Flow", "PlayerInfoHudPresenter.cs"));
            Assert.IsFalse(
                Regex.IsMatch(
                    text,
                    @"ScreenToWorldPoint\s*\(\s*new\s+Vector3\s*\(\s*mouse\.x\s*,\s*mouse\.y\s*,\s*0f\s*\)"),
                "PlayerInfoHudPresenter 不得私有 z=0 ScreenToWorldPoint");
            Assert.IsTrue(
                text.Contains("TryOverlapColliderOnPlane")
                || text.Contains("TryGetPointerWorldOnPlane"),
                "HUD 血槽悬停须走 WorldPointerUtility 平面换算（禁 z=0 私有路径）");
        }

        [Test]
        public void GameFlowController_DoesNotCallTryPickCollider()
        {
            var text = File.ReadAllText(Path.Combine(PresentationRoot, "Flow", "GameFlowController.cs"));
            Assert.IsFalse(
                text.Contains("TryPickCollider"),
                "主菜单 StartRun/Quit 不得再走 TryPickCollider");
        }

        [Test]
        public void StartRunHoverScale_DoesNotDeclareOnMouseCallbacks()
        {
            var path = Path.Combine(TemporaryTestRoot, "StartRunHoverScale.cs");
            Assert.IsTrue(File.Exists(path), "missing StartRunHoverScale.cs");
            var ban = new Regex(
                @"\bvoid\s+OnMouse(Enter|Exit|Down|Up|Over|Drag)\s*\(",
                RegexOptions.CultureInvariant);
            Assert.IsFalse(
                ban.IsMatch(File.ReadAllText(path)),
                "StartRunHoverScale 禁止 OnMouse*（NOLEGACY 下无效）");
        }
    }
}
