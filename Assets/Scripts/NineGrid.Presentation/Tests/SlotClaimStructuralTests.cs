using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// #102 / ADR-0023：命中恒成功与落格认领护栏——禁止规则关框与 Avatar 穿透回流。
    /// </summary>
    public sealed class SlotClaimStructuralTests
    {
        private static readonly string PresentationRoot = Path.GetFullPath(
            Path.Combine(Application.dataPath, "Scripts", "NineGrid.Presentation"));

        [Test]
        public void BoardWalkSlotHitPolicy_IsRetired()
        {
            Assert.IsFalse(
                File.Exists(Path.Combine(PresentationRoot, "Cards", "BoardWalkSlotHitPolicy.cs")),
                "BoardWalkSlotHitPolicy 须退役");
            Assert.IsFalse(
                File.Exists(Path.Combine(
                    PresentationRoot, "Tests", "Cards", "BoardWalkSlotHitPolicyTests.cs")),
                "BoardWalkSlotHitPolicyTests 须退役");
        }

        [Test]
        public void GroundCardHitProxy_HasNoAvatarMinValuePassthrough()
        {
            var text = File.ReadAllText(Path.Combine(PresentationRoot, "Cards", "GroundCardHitProxy.cs"));
            Assert.IsFalse(
                text.Contains("int.MinValue"),
                "GroundCardHitProxy 不得再用 HitSortOrder=int.MinValue 穿透");
            Assert.IsFalse(
                text.Contains("IPointerHitTarget"),
                "GroundCardHitProxy 不得再实现 IPointerHitTarget");
            Assert.IsFalse(
                Regex.IsMatch(text, @"RequireComponent\s*\(\s*typeof\s*\(\s*BoxCollider2D\s*\)"),
                "GroundCardHitProxy 不得 RequireComponent BoxCollider2D");
        }

        [Test]
        public void BoardProxies_DoNotEnsureColliderOrRegisterRouter()
        {
            var files = new[]
            {
                Path.Combine("Flow", "BoardBriefTip", "BoardBriefTipHitProxy.cs"),
                Path.Combine("Flow", "ShopBoard", "ShopBoardHitProxy.cs"),
                Path.Combine("Flow", "TavernBoard", "TavernBoardHitProxy.cs"),
                Path.Combine("Flow", "RewardBoard", "RewardBoardHitProxy.cs"),
            };

            foreach (var relative in files)
            {
                var text = File.ReadAllText(Path.Combine(PresentationRoot, relative));
                Assert.IsFalse(
                    text.Contains("EnsureCollider"),
                    relative + " 不得自建 EnsureCollider");
                Assert.IsFalse(
                    text.Contains("PointerHitRegistry"),
                    relative + " 不得注册 PointerHitRegistry");
                Assert.IsFalse(
                    text.Contains("IPointerHitTarget"),
                    relative + " 不得实现 IPointerHitTarget");
                Assert.IsFalse(
                    Regex.IsMatch(text, @"Vector2\?\s*colliderSize"),
                    relative + " 不得再接受 hitBox / colliderSize 传参");
            }
        }

        [Test]
        public void MotionExecutor_DoesNotGateSlotHitsByPolicy()
        {
            var text = File.ReadAllText(Path.Combine(
                PresentationRoot, "Cards", "Ground", "GroundMotionExecutor.cs"));
            Assert.IsFalse(
                text.Contains("BoardWalkSlotHitPolicy"),
                "GroundMotionExecutor 不得再引用 BoardWalkSlotHitPolicy");
            Assert.IsFalse(
                text.Contains("softOccupied"),
                "GroundMotionExecutor 不得再按软占关命中框");
        }
    }
}
