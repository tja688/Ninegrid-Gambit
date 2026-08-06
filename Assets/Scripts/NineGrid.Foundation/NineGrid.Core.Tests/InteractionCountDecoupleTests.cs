using System.Collections.Generic;
using NineGrid.Content;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// #75：互动计数与补牌/旋转解耦；未击杀交战漏计修复；道具使用不计互动。
    /// Seam：IPhaseSystem 互动命令面（见 #74 Testing Decisions）。
    /// </summary>
    public sealed class InteractionCountDecoupleTests
    {
        private static readonly SlotId sAdjacentSlot = SlotId.Board(2);
        private static readonly SlotId sFarCornerSlot = SlotId.Board(1);

        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IActionPipelineSystem mPipeline;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(
                ContentConfigKeys.DefaultCatalog,
                ContentCatalogBootstrap.Load());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 42UL });
            mPhase = mArch.GetSystem<IPhaseSystem>();
            mPipeline = mArch.GetSystem<IActionPipelineSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void Attack_NonKill_AdvancesInteractionCount_WithoutRotate()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 99, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);
            var player = mArch.GetModel<PlayerModel>();
            var before = player.InteractionCount.Value;
            var startIndex = mPipeline.EventLog.Entries.Count;

            Assert.IsTrue(mPhase.Attack(sAdjacentSlot).Accepted);

            Assert.AreEqual(before + 1, player.InteractionCount.Value);
            var events = SliceEvents(startIndex);
            Assert.IsTrue(ContainsType(events, CoreEventType.InteractionChanged));
            Assert.IsFalse(ContainsType(events, CoreEventType.CardKilled));
            Assert.IsFalse(ContainsType(events, CoreEventType.BoardRotated));
        }

        [Test]
        public void Attack_Kill_AdvancesInteractionCountOnce_WithFillAndRotate()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 1, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);
            var player = mArch.GetModel<PlayerModel>();
            var before = player.InteractionCount.Value;
            var startIndex = mPipeline.EventLog.Entries.Count;

            Assert.IsTrue(mPhase.Attack(sAdjacentSlot).Accepted);

            Assert.AreEqual(before + 1, player.InteractionCount.Value);
            var events = SliceEvents(startIndex);
            Assert.AreEqual(1, CountType(events, CoreEventType.InteractionChanged));
            Assert.IsTrue(ContainsType(events, CoreEventType.CardKilled));
            AssertFillBeforeRotate(events);
        }

        [Test]
        public void Pickup_AdvancesInteractionCount()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 1, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sFarCornerSlot);
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var monsterUid = board.GetCardUid(sFarCornerSlot);
            board.RemoveCard(registry.Get(monsterUid));
            SpawnHelpOnBoard("help.throwing_knife", sAdjacentSlot);
            Assert.AreEqual(GamePhase.InteractionLoop, mPhase.CurrentPhase);

            var player = mArch.GetModel<PlayerModel>();
            var before = player.InteractionCount.Value;

            var result = mPhase.PickupItem(sAdjacentSlot);
            Assert.IsTrue(result.Accepted, result.Reason);

            Assert.AreEqual(before + 1, player.InteractionCount.Value);
        }

        [Test]
        public void ClickEmpty_ThenResolvePostKillBoard_AdvancesInteractionCount()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 1, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sFarCornerSlot);
            Assert.IsTrue(mArch.GetModel<BoardModel>().IsEmpty(sAdjacentSlot));
            var player = mArch.GetModel<PlayerModel>();
            var before = player.InteractionCount.Value;

            Assert.IsTrue(mPhase.ClickEmpty(sAdjacentSlot).Accepted);
            Assert.AreEqual(before, player.InteractionCount.Value, "ClickEmpty 本身不计；留给盘面分拍");

            Assert.IsTrue(mPhase.ResolvePostKillBoard().Accepted);
            Assert.AreEqual(before + 1, player.InteractionCount.Value);
        }

        [Test]
        public void UseItem_DoesNotAdvanceInteractionCount_EvenAfterKillFillRotate()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 1, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);
            var board = mArch.GetModel<BoardModel>();
            var targetUid = board.GetCardUid(sAdjacentSlot);
            Assert.Greater(targetUid, 0);

            mPipeline.Enqueue(new SpawnCardAction(
                "help.throwing_knife",
                CardKind.HelpCard,
                ZoneId.ItemSlots,
                SlotId.None,
                1,
                "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            var knifeUid = mArch.GetModel<DeckModel>().ItemSlotUids[
                mArch.GetModel<DeckModel>().ItemSlotUids.Count - 1];

            var player = mArch.GetModel<PlayerModel>();
            var before = player.InteractionCount.Value;

            Assert.IsTrue(
                mPhase.ApplyUseItem(knifeUid, new List<int> { targetUid }, null).Accepted);
            Assert.IsTrue(mPhase.ResolveBoardStabilization().Accepted);
            Assert.IsTrue(mPhase.ResolvePostKillRotate().Accepted);

            Assert.AreEqual(
                before,
                player.InteractionCount.Value,
                "道具使用（含击杀后补牌旋转）不得推进 interactionCount");
        }

        [Test]
        public void AdvanceFillRotate_AreIndependentlyCallable()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 1, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);
            var board = mArch.GetModel<BoardModel>();
            var targetUid = board.GetCardUid(sAdjacentSlot);
            Assert.IsTrue(mPhase.ApplyCombatHit(board.AvatarUid.Value, targetUid).Accepted);

            var player = mArch.GetModel<PlayerModel>();
            var before = player.InteractionCount.Value;

            var countStart = mPipeline.EventLog.Entries.Count;
            Assert.IsTrue(mPhase.AdvanceInteractionCount().Accepted);
            Assert.AreEqual(before + 1, player.InteractionCount.Value);
            Assert.IsTrue(ContainsType(SliceEvents(countStart), CoreEventType.InteractionChanged));
            Assert.IsFalse(ContainsType(SliceEvents(countStart), CoreEventType.SlotsFilled));
            Assert.IsFalse(ContainsType(SliceEvents(countStart), CoreEventType.BoardRotated));

            var fillStart = mPipeline.EventLog.Entries.Count;
            Assert.IsTrue(mPhase.ResolveBoardStabilization().Accepted);
            var fillEvents = SliceEvents(fillStart);
            Assert.IsTrue(ContainsType(fillEvents, CoreEventType.SlotsFilled));
            Assert.IsFalse(ContainsType(fillEvents, CoreEventType.InteractionChanged));
            Assert.IsFalse(ContainsType(fillEvents, CoreEventType.BoardRotated));

            var rotateStart = mPipeline.EventLog.Entries.Count;
            Assert.IsTrue(mPhase.ResolvePostKillRotate().Accepted);
            var rotateEvents = SliceEvents(rotateStart);
            Assert.IsTrue(ContainsType(rotateEvents, CoreEventType.BoardRotated));
            Assert.IsFalse(ContainsType(rotateEvents, CoreEventType.SlotsFilled));
            Assert.IsFalse(ContainsType(rotateEvents, CoreEventType.InteractionChanged));
        }

        private static NodeDeckOptions CreateSingleMonsterNode(int hp, int attack)
        {
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 1
            }.AddEnemyCard(new CardDraft("monster.test", CardKind.Monster) { MaxHp = hp, Attack = attack });
        }

        private void SpawnHelpOnBoard(string defId, SlotId slot)
        {
            mPipeline.Enqueue(
                new SpawnCardAction(defId, CardKind.HelpCard, ZoneId.Board, slot, 1, "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            Assert.Greater(mArch.GetModel<BoardModel>().GetCardUid(slot), 0);
        }

        private void PlaceSoleBoardCardAt(SlotId targetSlot)
        {
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            CardInstance sole = null;
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var slot = SlotId.Board(i);
                if (slot == board.AvatarSlot.Value)
                {
                    continue;
                }

                var uid = board.GetCardUid(slot);
                if (uid == 0)
                {
                    continue;
                }

                Assert.IsNull(sole, "Expected at most one non-avatar board card for relocate helper.");
                sole = registry.Get(uid);
            }

            Assert.IsNotNull(sole, "No board card to relocate.");
            if (sole.Slot.Value == targetSlot)
            {
                return;
            }

            board.ClearSlot(sole.Slot.Value);
            board.PlaceCard(sole, targetSlot);
        }

        private IReadOnlyList<CoreGameEvent> SliceEvents(int startIndex)
        {
            var entries = mPipeline.EventLog.Entries;
            var list = new List<CoreGameEvent>(entries.Count - startIndex);
            for (var i = startIndex; i < entries.Count; i++)
            {
                list.Add(entries[i]);
            }

            return list;
        }

        private static void AssertFillBeforeRotate(IReadOnlyList<CoreGameEvent> events)
        {
            var fillIndex = IndexOfType(events, CoreEventType.SlotsFilled);
            var rotateIndex = IndexOfType(events, CoreEventType.BoardRotated);
            Assert.GreaterOrEqual(fillIndex, 0, "Expected SlotsFilled");
            Assert.GreaterOrEqual(rotateIndex, 0, "Expected BoardRotated");
            Assert.Less(fillIndex, rotateIndex, "R1: Fill must precede Rotate");
        }

        private static bool ContainsType(IReadOnlyList<CoreGameEvent> events, CoreEventType type)
        {
            return IndexOfType(events, type) >= 0;
        }

        private static int CountType(IReadOnlyList<CoreGameEvent> events, CoreEventType type)
        {
            var count = 0;
            for (var i = 0; i < events.Count; i++)
            {
                if (events[i].Type == type)
                {
                    count++;
                }
            }

            return count;
        }

        private static int IndexOfType(IReadOnlyList<CoreGameEvent> events, CoreEventType type)
        {
            for (var i = 0; i < events.Count; i++)
            {
                if (events[i].Type == type)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
