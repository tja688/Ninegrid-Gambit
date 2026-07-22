using NineGrid.Content;
using NineGrid.Core;
using NineGrid.Core.Commands;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Commands;
using NineGrid.Presentation.Queries;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests.Drain
{
    /// <summary>
    /// V4：ResolveDrainRefillBatchCommand / ShouldDrainRefillQuery / 用牌 drain 锁步接缝。
    /// </summary>
    public sealed class ResolveDrainRefillBatchCommandTests
    {
        private static readonly SlotId sAdjacentSlot = SlotId.Board(2);

        [Test]
        public void UseItemIntent_NonKillBoardRemove_DrainRefillIsSeparateBatchAfterUsePresent()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGameWithCatalog(seed: 42UL))
            {
                Assert.IsTrue(arch.Phase.StartNode(CreateSingleMonsterNode(hp: 99, attack: 0, armor: 3)).Accepted);
                arch.PlaceSoleBoardCardAt(sAdjacentSlot);
                var targetUid = arch.Board.GetCardUid(sAdjacentSlot);
                Assert.Greater(targetUid, 0);
                SeedDrawPileFiller(arch, "monster.test", count: 8);
                var kidnapUid = SpawnHelpIntoItemSlots(arch, "help.kidnapping");

                var usePresent = new RecordingPresentChannel(ticksUntilComplete: 1);
                var boardPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
                var factory = new UseItemIntentScriptFactory(
                    arch.Architecture,
                    arch.Dispatcher,
                    usePresent,
                    boardPresent);

                using (var runtime = PresentationRuntimeFixture.Install(arch, factory))
                {
                    Assert.IsTrue(arch.Architecture.SendCommand(
                        new SubmitUseItemIntentCommand(kidnapUid, new[] { targetUid }, null)));
                    Assert.IsTrue(runtime.MainlineBusy.Value);

                    // Resolve ApplyUseItem：移除盘面怪，不得 in-flight FillEmptySlots
                    var useStart = arch.Pipeline.EventLog.Entries.Count;
                    runtime.Tick();
                    Assert.AreEqual(1, arch.Sync.ActiveBatchId);
                    Assert.IsTrue(
                        ContainsTypeSince(arch, useStart, CoreEventType.CardRemoved),
                        "绑票应 RemoveCard 盘面怪");
                    Assert.IsFalse(
                        ContainsTypeSince(arch, useStart, CoreEventType.CardKilled),
                        "绑票移除不是击杀路径");
                    Assert.IsFalse(
                        ContainsTypeSince(arch, useStart, CoreEventType.SlotsFilled),
                        "drain 补牌不得在 Use 解算批内 in-flight 写入");
                    Assert.IsTrue(
                        FusionRefillPlanner.HasEmptyBoardSlot(arch.Board),
                        "移除后盘面应有空位");
                    Assert.IsTrue(
                        arch.Architecture.SendQuery(new ShouldDrainRefillQuery()),
                        "非击杀空位应允许 DrainRefill");

                    // Present use ack
                    runtime.Tick();
                    Assert.AreEqual(0, arch.Sync.ActiveBatchId);
                    Assert.AreEqual(1, usePresent.BeginCount);

                    // Kill branch（非击杀空过）
                    runtime.Tick();
                    Assert.AreEqual(0, arch.Sync.ActiveBatchId);
                    Assert.AreEqual(0, boardPresent.BeginCount);

                    // DrainRefill branch：入队补牌批（本 Tick 只入队）
                    runtime.Tick();
                    Assert.AreEqual(0, arch.Sync.ActiveBatchId);
                    Assert.AreEqual(0, boardPresent.BeginCount);

                    // Resolve DrainRefill：离散 SlotsFilled 批次
                    var refillStart = arch.Pipeline.EventLog.Entries.Count;
                    runtime.Tick();
                    Assert.AreEqual(2, arch.Sync.ActiveBatchId);
                    Assert.IsTrue(
                        ContainsTypeSince(arch, refillStart, CoreEventType.SlotsFilled),
                        "drain 补牌须在独立 Resolve 批次写入 Core");

                    // Present refill ack → idle
                    runtime.Tick();
                    Assert.AreEqual(0, arch.Sync.ActiveBatchId);
                    Assert.AreEqual(1, boardPresent.BeginCount);
                    Assert.IsFalse(runtime.MainlineBusy.Value);
                }
            }
        }

        [Test]
        public void UseItemIntent_Kill_DoesNotEnqueueDrainRefill_FillHandlesSlots()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGameWithCatalog(seed: 42UL))
            {
                Assert.IsTrue(arch.Phase.StartNode(CreateSingleMonsterNode(hp: 1, attack: 0)).Accepted);
                arch.PlaceSoleBoardCardAt(sAdjacentSlot);
                var targetUid = arch.Board.GetCardUid(sAdjacentSlot);
                SeedDrawPileFiller(arch, "monster.test", count: 8);
                var knifeUid = SpawnHelpIntoItemSlots(arch, "help.throwing_knife");

                var usePresent = new RecordingPresentChannel(ticksUntilComplete: 1);
                var boardPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
                var factory = new UseItemIntentScriptFactory(
                    arch.Architecture,
                    arch.Dispatcher,
                    usePresent,
                    boardPresent);

                using (var runtime = PresentationRuntimeFixture.Install(arch, factory))
                {
                    Assert.IsTrue(arch.Architecture.SendCommand(
                        new SubmitUseItemIntentCommand(knifeUid, new[] { targetUid }, null)));

                    // use resolve + present + kill branch（入队 Fill/Rotate）+ drain branch（空过）
                    runtime.Tick();
                    runtime.Tick();
                    runtime.Tick();
                    runtime.Tick();

                    // Fill resolve：击杀补牌走 ResolvePostKillFill，不是 DrainRefill
                    var fillStart = arch.Pipeline.EventLog.Entries.Count;
                    runtime.Tick();
                    Assert.AreEqual(2, arch.Sync.ActiveBatchId);
                    Assert.IsTrue(ContainsTypeSince(arch, fillStart, CoreEventType.SlotsFilled));

                    // fill present + rotate resolve/present + fusion branch → idle
                    for (var i = 0; i < 8; i++)
                    {
                        runtime.Tick();
                        if (!runtime.MainlineBusy.Value)
                        {
                            break;
                        }
                    }

                    Assert.IsFalse(runtime.MainlineBusy.Value);
                    Assert.AreEqual(
                        2,
                        boardPresent.BeginCount,
                        "击杀路径仅 Fill/Rotate 两次板 Present，无额外 drain refill Present");
                }
            }
        }

        [Test]
        public void Command_WhenShouldNotRefill_OpensEmptyAcceptedBatch()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGameWithCatalog(seed: 42UL))
            {
                Assert.IsTrue(arch.Phase.StartNode(CreateSingleMonsterNode(hp: 99, attack: 0)).Accepted);
                // StartNode 后盘面通常已满：无空位则 ShouldRefill=false，命令仍开空批 ack。
                Assert.IsFalse(arch.Architecture.SendQuery(new ShouldDrainRefillQuery()));

                var dispatch = arch.Architecture.SendCommand(
                    new ResolveDrainRefillBatchCommand(arch.Dispatcher, boardSlot: 2));
                Assert.IsNotNull(dispatch);
                Assert.IsTrue(dispatch.Accepted);
            }
        }

        private static int SpawnHelpIntoItemSlots(PresentationArchitectureFixture arch, string defId)
        {
            arch.Pipeline.Enqueue(
                new SpawnCardAction(defId, CardKind.HelpCard, ZoneId.ItemSlots, SlotId.None, 1, "test"));
            Assert.Greater(arch.Pipeline.RunToCompletion(), 0);
            var deck = arch.Architecture.GetModel<DeckModel>();
            Assert.Greater(deck.ItemSlotUids.Count, 0);
            return deck.ItemSlotUids[deck.ItemSlotUids.Count - 1];
        }

        private static void SeedDrawPileFiller(PresentationArchitectureFixture arch, string defId, int count)
        {
            for (var i = 0; i < count; i++)
            {
                arch.Pipeline.Enqueue(
                    new SpawnCardAction(defId, CardKind.Monster, ZoneId.DrawPile, SlotId.None, 1, "test"));
            }

            Assert.Greater(arch.Pipeline.RunToCompletion(), 0);
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

        private static NodeDeckOptions CreateSingleMonsterNode(int hp, int attack, int armor = 0)
        {
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 1
            }.AddEnemyCard(new CardDraft("monster.test", CardKind.Monster)
            {
                MaxHp = hp,
                Attack = attack,
                Armor = armor,
            });
        }
    }
}
