using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using NineGrid.Cards;

namespace NineGrid.Presentation.Tests.Cards
{
    /// <summary>
    /// 占格生命周期契约：Core-owned 错位不可当幽灵清；旧异步句柄不可凭 uid 释放新实体；
    /// Drain 尾部禁止 relocate/repair 自愈。
    /// </summary>
    public sealed class OccupancyLifecycleContractTests
    {
        private GameObject _prefab;
        private CardManagerSingleton _cards;

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("Cards_LifecycleContract");
            _cards = go.AddComponent<CardManagerSingleton>();
            _prefab = new GameObject("StandardCardPrefab");
            _prefab.AddComponent<StandardCardView>();
            _cards.RegisterPrefab(CardManagerSingleton.StandardDefId, _prefab);
        }

        [TearDown]
        public void TearDown()
        {
            if (_cards != null)
            {
                UnityEngine.Object.DestroyImmediate(_cards.gameObject);
                _cards = null;
            }

            if (_prefab != null)
            {
                UnityEngine.Object.DestroyImmediate(_prefab);
                _prefab = null;
            }
        }

        [Test]
        public void ReleaseManagedCard_StaleHandle_DoesNotDestroyReusedUidEntity()
        {
            const int uid = 51001;
            var stale = _cards.SpawnView(uid, CardManagerSingleton.StandardDefId);
            Assert.IsNotNull(stale);
            var staleViewId = stale.View.GetInstanceID();

            _cards.Release(stale, "Test.RetireOld");
            Assert.IsFalse(_cards.TryGet(uid, out _));

            var fresh = _cards.SpawnView(uid, CardManagerSingleton.StandardDefId);
            Assert.IsNotNull(fresh);
            Assert.AreNotSame(stale, fresh);
            Assert.AreNotEqual(staleViewId, fresh.View.GetInstanceID());

            // 旧异步回调仅持有过期 ManagedCard：必须拒绝，保留新实体。
            _cards.Release(stale, "Test.StaleAsyncCallback");
            Assert.IsTrue(_cards.TryGet(uid, out var stillRegistered));
            Assert.AreSame(fresh, stillRegistered);
            Assert.IsNotNull(fresh.View);
        }

        [Test]
        public void AsyncCleanupSites_ReleaseByManagedCardIdentity()
        {
            AssertSourceMatches(
                "Cards/Battle/FieldBattlePresentationExecutor.cs",
                @"Release\(\s*victim\s*,\s*""Combat\.CompleteRemoveVictim""");
            AssertSourceMatches(
                "Cards/Battle/FieldBattlePresentationExecutor.cs",
                @"Release\(\s*card\s*,\s*""Combat\.FinalizeLethal""");
            AssertSourceDoesNotMatch(
                "Cards/Battle/FieldBattlePresentationExecutor.cs",
                @"Release\(\s*(?:victim|card)\.Uid\s*,");

            AssertSourceMatches(
                "Cards/Ground/DealFlightCoordinator.cs",
                @"Release\(\s*card\s*,\s*""DealFlight\.Rollback""");
            AssertSourceDoesNotMatch(
                "Cards/Ground/DealFlightCoordinator.cs",
                @"Release\(\s*card\.Uid\s*,\s*""DealFlight\.Rollback""");

            AssertSourceMatches(
                "Cards/Ground/GroundMotionExecutor.cs",
                @"Release\(\s*card\s*,\s*""Ground\.RemoveAnimatedComplete""");
            AssertSourceDoesNotMatch(
                "Cards/Ground/GroundMotionExecutor.cs",
                @"Release\(\s*card\.Uid\s*,\s*""Ground\.RemoveAnimated");
        }

        [Test]
        public void DrainTail_DoesNotHealWithRelocateOrRepair()
        {
            var text = ReadPresentationSource("Flow/BattleSession/BoardPresentationPlayer.cs");
            Assert.IsFalse(
                text.Contains("RelocateMisplacedPresentationToCore", StringComparison.Ordinal),
                "Drain 尾部不得 relocate 自愈");
            Assert.IsFalse(
                text.Contains("RepairMissingPresentationAgainstCore", StringComparison.Ordinal),
                "Drain 尾部不得 repair 自愈");
            Assert.IsFalse(
                text.Contains("TryPlaceMissingCoreCard", StringComparison.Ordinal),
                "Drain 尾部不得定向补位自愈");
            Assert.IsTrue(
                text.Contains("AssertOccupancySyncForbidden", StringComparison.Ordinal),
                "分叉须保留断言探针");
        }

        [Test]
        public void DealGhostClear_RefusesCoreOwnedMisplacedCards()
        {
            var text = ReadPresentationSource("Flow/BattleSession/BoardPresentationPlayer.cs");
            var method = Regex.Match(
                text,
                @"TryClearGhostOccupantForDeal[\s\S]*?private static int ResolveCoreBoardUid",
                RegexOptions.CultureInvariant);
            Assert.IsTrue(method.Success, "找不到 TryClearGhostOccupantForDeal");
            Assert.IsTrue(
                method.Value.Contains("FindCoreBoardSlotOfUid(blocker.Uid)", StringComparison.Ordinal),
                "Deal 幽灵分类须识别 Core 仍拥有的错位牌");
            Assert.IsTrue(
                Regex.IsMatch(
                    method.Value,
                    @"FindCoreBoardSlotOfUid\(blocker\.Uid\)\s*>\s*0[\s\S]{0,120}return false"),
                "Core 仍拥有时不得 RequestRemoveFromField");
        }

        [Test]
        public void Pickup_DoesNotForceEndOrContinueWithoutLease()
        {
            var text = ReadPresentationSource("Cards/CardHandManagerSingleton.cs");
            Assert.IsFalse(
                text.Contains("Pickup-preempt", StringComparison.Ordinal),
                "Pickup 不得 ForceEnd preempt 撕主线租约");
            Assert.IsFalse(
                text.Contains("LockFailContinue", StringComparison.Ordinal),
                "Pickup 不得无租约继续表现");
            Assert.IsTrue(
                text.Contains("LockFailAbort", StringComparison.Ordinal),
                "租约失败须中止表现");
        }

        private static string ReadPresentationSource(string relativeUnderPresentation)
        {
            var path = Path.GetFullPath(
                Path.Combine(
                    Application.dataPath,
                    "Scripts",
                    "NineGrid.Presentation",
                    relativeUnderPresentation.Replace('/', Path.DirectorySeparatorChar)));
            Assert.IsTrue(File.Exists(path), "missing " + relativeUnderPresentation);
            return File.ReadAllText(path);
        }

        private static void AssertSourceMatches(string relative, string pattern)
        {
            Assert.IsTrue(
                Regex.IsMatch(ReadPresentationSource(relative), pattern),
                relative + " 应符合 " + pattern);
        }

        private static void AssertSourceDoesNotMatch(string relative, string pattern)
        {
            Assert.IsFalse(
                Regex.IsMatch(ReadPresentationSource(relative), pattern),
                relative + " 不应匹配 " + pattern);
        }
    }
}
