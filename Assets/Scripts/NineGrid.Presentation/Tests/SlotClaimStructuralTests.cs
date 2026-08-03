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
                Path.Combine("Cards", "GroundCardHitProxy.cs"),
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
                Assert.IsFalse(
                    Regex.IsMatch(text, @"AddComponent\s*<\s*\w*Collider2D\s*>"),
                    relative + " 落格对象不得自建 Collider2D 作命中区");
                Assert.IsFalse(
                    Regex.IsMatch(text, @"AddComponent\s*\(\s*typeof\s*\(\s*\w*Collider2D\s*\)"),
                    relative + " 落格对象不得 AddComponent(typeof(*Collider2D))");
                Assert.IsFalse(
                    Regex.IsMatch(text, @"RequireComponent\s*\(\s*typeof\s*\(\s*\w*Collider2D\s*\)"),
                    relative + " 落格对象不得 RequireComponent Collider2D");
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

        [Test]
        public void MotionExecutor_ReleasesClaimBeforeHop_AndSyncsAfterLand()
        {
            // ADR-0023：飞行/跳跃中不认领；落地才登记。避免悬停/点击与肉眼卡错位。
            var text = File.ReadAllText(Path.Combine(
                PresentationRoot, "Cards", "Ground", "GroundMotionExecutor.cs"));

            Assert.IsTrue(
                text.Contains("ReleaseGroundCardClaim(card);\r\n                    hopPlans.Add")
                || text.Contains("ReleaseGroundCardClaim(card);\n                    hopPlans.Add"),
                "General.Hop 须在建 hopPlans 时 ReleaseGroundCardClaim");
            Assert.IsTrue(
                text.Contains("SyncGroundCardClaim(plan.card, plan.toSlot)"),
                "General.Hop 落地后须 SyncGroundCardClaim(plan)");

            Assert.IsTrue(
                text.Contains("ReleaseGroundCardClaim(moved)"),
                "RingShift 动画前须 ReleaseGroundCardClaim(moved)");
            Assert.IsTrue(
                text.Contains("SyncGroundCardClaim(landed, toSlot)"),
                "RingShift 落地后须 SyncGroundCardClaim(landed)");

            Assert.IsTrue(
                text.Contains("ReleaseGroundCardClaim(cardA)")
                && text.Contains("ReleaseGroundCardClaim(cardB)"),
                "Cross.Swap 置换后须 Release 双方");
            Assert.IsTrue(
                text.Contains("SyncGroundCardClaim(cardA, moveA.ToSlot)")
                && text.Contains("SyncGroundCardClaim(cardB, moveB.ToSlot)"),
                "Cross.Swap 落地后须 Sync 双方认领");
        }

        [Test]
        public void PointerHitRouter_RefreshesMultiColliderHoverWhileStaying()
        {
            var router = File.ReadAllText(Path.Combine(
                PresentationRoot, "Flow", "PointerHitRouter.cs"));
            Assert.IsTrue(
                Regex.IsMatch(
                    router,
                    @"IMultiColliderPointerHitTarget\s+multiStay[\s\S]{0,120}?RefreshPointerHover"),
                "Router 在同一多框表面停留时须 RefreshPointerHover");

            var multi = File.ReadAllText(Path.Combine(
                PresentationRoot, "Flow", "IMultiColliderPointerHitTarget.cs"));
            Assert.IsTrue(
                multi.Contains("RefreshPointerHover"),
                "IMultiColliderPointerHitTarget 须声明 RefreshPointerHover");
        }
    }
}
