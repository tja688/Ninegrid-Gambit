using System.Collections.Generic;
using NineGrid.Content;
using NineGrid.Core;
using NineGrid.Core.Commands;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NineGrid.Flow;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Commands;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests.Fusion
{
    /// <summary>
    /// V4：ResolveFusionRefillBatchCommand / 探索融合锁步接缝。
    /// </summary>
    public sealed class ResolveFusionRefillBatchCommandTests
    {
        private static readonly SlotId sAdjacentSlot = SlotId.Board(2);
        private static readonly SlotId sFarCornerSlot = SlotId.Board(1);
        private static readonly SlotId sHeadlessSlot = SlotId.Board(4);
        private static readonly SlotId sSkullSlot = SlotId.Board(7);

        [Test]
        public void ExploreIntent_RotateFusion_FusionRefillIsSeparateBatchAfterRotatePresent()
        {
            using (var arch = CreateGameWithCatalog(seed: 19UL))
            {
                Assert.IsTrue(arch.Phase.StartNode(CreateSingleMonsterNode(hp: 99, attack: 0)).Accepted);
                arch.PlaceSoleBoardCardAt(sFarCornerSlot);
                SpawnOnBoard(arch, "monster.headless_skeleton", sHeadlessSlot);
                SpawnOnBoard(arch, "monster.skull_head", sSkullSlot);
                SeedDrawPileFiller(arch, "monster.headless_skeleton", count: 16);
                Assert.IsTrue(arch.Board.IsEmpty(sAdjacentSlot));

                var boardPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
                var factory = new ExploreIntentScriptFactory(
                    arch.Architecture,
                    arch.Dispatcher,
                    boardPresent);

                using (var runtime = PresentationRuntimeFixture.Install(arch, factory))
                {
                    Assert.IsTrue(arch.Architecture.SendCommand(
                        new SubmitExploreIntentCommand(sAdjacentSlot.Index)));
                    Assert.IsTrue(runtime.MainlineBusy.Value);

                    // ClickEmpty resolve + present
                    runtime.Tick();
                    Assert.AreEqual(1, arch.Sync.ActiveBatchId);
                    runtime.Tick();
                    Assert.AreEqual(0, arch.Sync.ActiveBatchId);
                    Assert.AreEqual(1, boardPresent.BeginCount);

                    // Fill resolve + present
                    runtime.Tick();
                    Assert.AreEqual(2, arch.Sync.ActiveBatchId);
                    runtime.Tick();
                    Assert.AreEqual(0, arch.Sync.ActiveBatchId);
                    Assert.AreEqual(2, boardPresent.BeginCount);

                    // Rotate resolve：应含融合，但不得 FillEmptySlots
                    var rotateStart = arch.Pipeline.EventLog.Entries.Count;
                    runtime.Tick();
                    Assert.AreEqual(3, arch.Sync.ActiveBatchId);
                    Assert.IsTrue(HasFusionSince(arch, rotateStart), "Rotate 批应触发骷髅融合");
                    Assert.IsFalse(
                        ContainsTypeSince(arch, rotateStart, CoreEventType.SlotsFilled),
                        "融合伴随补牌不得在 Rotate 解算批内 in-flight 写入");

                    // Rotate present ack
                    runtime.Tick();
                    Assert.AreEqual(0, arch.Sync.ActiveBatchId);
                    Assert.AreEqual(3, boardPresent.BeginCount);

                    // Branch 入队融合补牌（本 Tick 只入队）
                    runtime.Tick();
                    Assert.AreEqual(0, arch.Sync.ActiveBatchId);
                    Assert.AreEqual(3, boardPresent.BeginCount);

                    var deck = arch.Architecture.GetModel<DeckModel>();
                    Assert.IsTrue(FusionRefillPlanner.HasEmptyBoardSlot(arch.Board), "融合后盘面应有空位");
                    Assert.IsTrue(
                        FusionRefillPlanner.HasRefillCandidateExcluding(
                            deck,
                            CollectFusionResultUidsSince(arch, rotateStart)),
                        "牌堆应有非合体结果的补牌候选");

                    // Fusion refill resolve：离散 SlotsFilled 批次
                    var refillStart = arch.Pipeline.EventLog.Entries.Count;
                    runtime.Tick();
                    Assert.AreEqual(4, arch.Sync.ActiveBatchId);
                    Assert.IsTrue(
                        ContainsTypeSince(arch, refillStart, CoreEventType.SlotsFilled),
                        "融合补牌须在独立 Resolve 批次写入 Core");

                    // Fusion refill present ack → idle
                    runtime.Tick();
                    Assert.AreEqual(0, arch.Sync.ActiveBatchId);
                    Assert.AreEqual(4, boardPresent.BeginCount);
                    Assert.IsFalse(runtime.MainlineBusy.Value);
                }
            }
        }

        [Test]
        public void Command_SkipWhenNoCandidate_OpensEmptyAcceptedBatch()
        {
            using (var arch = CreateGameWithCatalog(seed: 19UL))
            {
                Assert.IsTrue(arch.Phase.StartNode(CreateSingleMonsterNode(hp: 99, attack: 0)).Accepted);

                var dispatch = arch.Architecture.SendCommand(
                    new ResolveFusionRefillBatchCommand(
                        arch.Dispatcher,
                        boardSlot: 2,
                        excludeResultUids: new List<int> { 1, 2 }));

                Assert.IsNotNull(dispatch);
                Assert.IsTrue(dispatch.Accepted);
            }
        }

        [Test]
        public void ExploreIntent_NoFusion_DoesNotEnqueueFusionRefill()
        {
            using (var arch = CreateGameWithCatalog(seed: 19UL))
            {
                Assert.IsTrue(arch.Phase.StartNode(CreateSingleMonsterNode(hp: 99, attack: 0)).Accepted);
                arch.PlaceSoleBoardCardAt(sFarCornerSlot);
                Assert.IsTrue(arch.Board.IsEmpty(sAdjacentSlot));

                var boardPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
                var factory = new ExploreIntentScriptFactory(
                    arch.Architecture,
                    arch.Dispatcher,
                    boardPresent);

                using (var runtime = PresentationRuntimeFixture.Install(arch, factory))
                {
                    Assert.IsTrue(arch.Architecture.SendCommand(
                        new SubmitExploreIntentCommand(sAdjacentSlot.Index)));
                    runtime.TickUntilIdle();
                    Assert.IsFalse(runtime.MainlineBusy.Value);
                    Assert.AreEqual(3, boardPresent.BeginCount, "无融合时仅 Click/Fill/Rotate 三次 Present");
                }
            }
        }

        private static PresentationArchitectureFixture CreateGameWithCatalog(ulong seed)
        {
            return PresentationArchitectureFixture.CreateStartedGameWithCatalog(seed);
        }

        private static bool HasFusionSince(PresentationArchitectureFixture arch, int startIndex)
        {
            var fusions = SkeletonFusionPresentationScanner.Collect(
                arch.Pipeline.EventLog.Entries,
                startIndex);
            return fusions.Count > 0;
        }

        private static List<int> CollectFusionResultUidsSince(
            PresentationArchitectureFixture arch,
            int startIndex)
        {
            var uids = new List<int>(2);
            FusionRefillPlanner.TryCollectResultUids(arch.Pipeline.EventLog.Entries, startIndex, uids);
            return uids;
        }

        private static bool ContainsTypeSince(
            PresentationArchitectureFixture arch,
            int startIndex,
            CoreEventType type)
        {
            var entries = arch.Pipeline.EventLog.Entries;
            for (var i = startIndex; i < entries.Count; i++)
            {
                if (entries[i].Type == type)
                {
                    return true;
                }
            }

            return false;
        }

        private static NodeDeckOptions CreateSingleMonsterNode(int hp, int attack)
        {
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 1
            }.AddEnemyCard(new CardDraft("monster.test", CardKind.Monster) { MaxHp = hp, Attack = attack });
        }

        private static void SpawnOnBoard(PresentationArchitectureFixture arch, string defId, SlotId slot)
        {
            Assert.IsTrue(arch.Board.IsEmpty(slot), "Spawn 目标格应为空: " + slot);
            arch.Pipeline.Enqueue(new SpawnCardAction(defId, CardKind.Monster, ZoneId.Board, slot, 1, "test"));
            Assert.Greater(arch.Pipeline.RunToCompletion(), 0, "应成功生成 " + defId);
        }

        private static void SeedDrawPileFiller(PresentationArchitectureFixture arch, string defId, int count)
        {
            for (var i = 0; i < count; i++)
            {
                arch.Pipeline.Enqueue(
                    new SpawnCardAction(defId, CardKind.Monster, ZoneId.DrawPile, SlotId.None, 1, "test"));
                Assert.Greater(arch.Pipeline.RunToCompletion(), 0);
            }
        }
    }
}
