using NineGrid.Content;
using NineGrid.Content.CardPresentation;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// #113 / ADR-0026：离开机关为战斗房唯一清关；清场不兑金；道具卡格保留。
    /// 缝：IsNodeCleared / CompleteNodeIfCleared；真怪清零不触发；击破离开机关触发；收场清残留不兑金。
    /// </summary>
    public sealed class LeaveTrapClearContractTests
    {
        private static readonly SlotId sSlot2 = SlotId.Board(2);
        private static readonly SlotId sSlot8 = SlotId.Board(8);

        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IActionPipelineSystem mPipeline;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            EffectTemplateCatalog.Invalidate();
            CardPresentationConfigCatalog.Invalidate();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, ContentCatalogBootstrap.Load());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 113UL });
            mPhase = mArch.GetSystem<IPhaseSystem>();
            mPipeline = mArch.GetSystem<IActionPipelineSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            EffectTemplateCatalog.Invalidate();
            CardPresentationConfigCatalog.Invalidate();
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void TrueMonsterWipe_DoesNotClearNode()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 1, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sSlot2);
            Assert.IsFalse(mArch.GetModel<BattleContextModel>().IsLeaveTrapBroken);
            Assert.IsFalse(mArch.GetSystem<IDeckSystem>().IsNodeCleared());

            Assert.IsTrue(mPhase.Attack(sSlot2).Accepted);

            Assert.IsFalse(mArch.GetModel<BattleContextModel>().IsLeaveTrapBroken);
            Assert.IsFalse(
                mArch.GetSystem<IDeckSystem>().IsNodeCleared(),
                "真怪物清零后 IsNodeCleared 应为假");
            Assert.AreEqual(GamePhase.InteractionLoop, mPhase.CurrentPhase, "战斗不得因真怪清零自动完成");
        }

        [Test]
        public void BreakLeaveTrap_ClearsNodeAndOffersRooms()
        {
            StartEmptyNode();
            PrepareAvatar(99, 99, 0);
            SpawnLeaveTrap(hp: 1);
            // 场上留一只真怪：击破离开机关仍应清关。
            SpawnMonsterAt(sSlot8, hp: 20);

            Assert.IsFalse(mArch.GetSystem<IDeckSystem>().IsNodeCleared());
            Assert.IsTrue(mPhase.Attack(sSlot2).Accepted);

            Assert.IsTrue(mArch.GetModel<BattleContextModel>().IsLeaveTrapBroken);
            Assert.IsTrue(mArch.GetSystem<IDeckSystem>().IsNodeCleared(), "击破离开机关后应清关");
            Assert.AreEqual(GamePhase.RoomChoice, mPhase.CurrentPhase);
            Assert.AreEqual(PendingChoiceKind.Room, mArch.GetModel<PendingChoiceModel>().Kind.Value);
            Assert.AreNotEqual("help.choice", mArch.GetModel<PendingChoiceModel>().PoolId.Value);
        }

        [Test]
        public void Clear_RemovesBoardResidualsWithoutGold_KeepsItemSlots()
        {
            StartEmptyNode();
            PrepareAvatar(99, 99, 0);
            SpawnLeaveTrap(hp: 1);
            SpawnMonsterAt(sSlot8, hp: 20);
            SpawnHelpOnBoard("help.throwing_knife", SlotId.Board(3));
            SpawnHelpIntoItemSlots("help.throwing_knife");
            SpawnOrdinaryTrapAt(SlotId.Board(4));

            var player = mArch.GetModel<PlayerModel>();
            var coinsBefore = player.Coins.Value;
            Assert.IsTrue(mPhase.Attack(sSlot2).Accepted);

            Assert.AreEqual(GamePhase.RoomChoice, mPhase.CurrentPhase);
            Assert.AreEqual(coinsBefore, player.Coins.Value, "清关清场残留不得兑金");
            Assert.AreEqual(0, CountBoardNonAvatarCards(), "场上残留怪/帮助/机关应同拍清掉");
            Assert.AreEqual(1, CountItemSlotHelpCards(), "道具卡格内容应保留");
        }

        [Test]
        public void BreakLeaveTrap_WithDrawPile_PostKillFillDoesNotRefillVacatedSlot()
        {
            // 回归：清关后若仍 Fill，牌堆非空会 Deal 进刚腾出的格，表现发牌可卡死主线。
            StartEmptyNode();
            PrepareAvatar(99, 99, 0);
            var leaveUid = SpawnLeaveTrap(hp: 1);
            SpawnMonsterAt(sSlot8, hp: 20);
            SeedDrawPileMonsters(3);

            var board = mArch.GetModel<BoardModel>();
            var avatarUid = board.AvatarUid.Value;
            Assert.IsTrue(mPhase.ApplyCombatHit(avatarUid, leaveUid).Accepted);
            Assert.IsTrue(mArch.GetModel<BattleContextModel>().IsLeaveTrapBroken);
            Assert.AreEqual(0, board.GetCardUid(sSlot2), "击破离开机关后该格应空");

            var fillStart = mPipeline.EventLog.Entries.Count;
            Assert.IsTrue(mPhase.ResolvePostKillFill().Accepted);
            Assert.AreEqual(0, board.GetCardUid(sSlot2), "清关后 Fill 不得往空位补牌");
            Assert.IsFalse(
                ContainsEventSince(fillStart, CoreEventType.CardDealt),
                "清关后 Fill 批不得产生 CardDealt");

            Assert.IsTrue(mPhase.ResolvePostKillRotate().Accepted);
            Assert.AreEqual(GamePhase.RoomChoice, mPhase.CurrentPhase);
            Assert.AreEqual(0, CountBoardNonAvatarCards(), "收场应清空场上残留");
        }

        [Test]
        public void MarkLeaveTrapBroken_AllowsTryCompleteClearedNode_LikeQuickTestSkip()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 5, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sSlot2);
            Assert.IsFalse(mArch.GetSystem<IDeckSystem>().IsNodeCleared());

            mArch.GetModel<BattleContextModel>().MarkLeaveTrapBroken();
            Assert.IsTrue(mArch.GetSystem<IDeckSystem>().IsNodeCleared());
            Assert.IsTrue(mPhase.TryCompleteClearedNode().Accepted);
            Assert.AreEqual(GamePhase.RoomChoice, mPhase.CurrentPhase);
            Assert.AreEqual(PendingChoiceKind.Room, mArch.GetModel<PendingChoiceModel>().Kind.Value);
        }

        [Test]
        public void QuickTestStyleClear_RemovesLeaveTrapStillOnBoard()
        {
            StartEmptyNode();
            PrepareAvatar(99, 0, 0);
            SpawnLeaveTrap(hp: 6);
            Assert.AreEqual(1, CountBoardNonAvatarCards());

            mArch.GetModel<BattleContextModel>().MarkLeaveTrapBroken();
            Assert.IsTrue(mPhase.TryCompleteClearedNode().Accepted);
            Assert.AreEqual(GamePhase.RoomChoice, mPhase.CurrentPhase);
            Assert.AreEqual(0, CountBoardNonAvatarCards(), "跳关清场须卸掉仍在场的离开机关（门不得挡收场移除）");
        }

        [Test]
        public void ClearResidual_WithDeathSummonAndLordOfDeathCarriers_ShouldNotLeaveReviveStone()
        {
            // 回归 ADR-0026 addendum / #143：清关清场扫描（clearResidualBoard）不是战斗击杀，
            // 不派发 OnRemove / OnCumulative Post 触发器。死亡召唤（OnSelfRemoved）与死亡之主
            // （OnAnyCardRemoved）在清场拍不得复活石 / 召唤物，否则会占据场地且不可交互。
            StartEmptyNode();
            PrepareAvatar(99, 99, 0);
            SpawnLeaveTrap(hp: 1);
            // wandering_child：携带 skill.death_summon（OnSelfRemoved → 生成 trap.revive_stone）。
            SpawnSpecificMonsterAt("monster.wandering_child", SlotId.Board(3), hp: 5);
            // rogue：携带 skill.lord_of_death（其他怪 OnRemove → 生成 trap.revive_stone）。
            SpawnSpecificMonsterAt("monster.rogue", SlotId.Board(5), hp: 5);
            // skull_head：无死亡召唤技能；用于触发 rogue 的 OnAnyCardRemoved 路径。
            SpawnSpecificMonsterAt("monster.skull_head", SlotId.Board(8), hp: 5);

            Assert.IsTrue(mPhase.Attack(sSlot2).Accepted);

            Assert.AreEqual(GamePhase.RoomChoice, mPhase.CurrentPhase);
            Assert.AreEqual(
                0,
                CountBoardNonAvatarCards(),
                "清关清场不得留下任何卡（含复活石 / 被复活石召唤的 special_omni）");

            // 兜底断言：场上任何残留卡都不得是 trap.revive_stone。
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            foreach (var uid in board.BoardCardUids())
            {
                if (uid == 0 || uid == board.AvatarUid.Value)
                {
                    continue;
                }

                var card = registry.Get(uid);
                Assert.AreNotEqual(
                    "trap.revive_stone",
                    card.DefId,
                    "清关清场扫描不得触发死亡召唤 / 死亡之主留下复活石");
            }
        }

        private void StartEmptyNode()
        {
            Assert.IsTrue(mPhase.StartNode(new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 0
            }).Accepted);
        }

        private void PrepareAvatar(int hp, int attack, int armor)
        {
            var board = mArch.GetModel<BoardModel>();
            var avatar = mArch.GetModel<CardRegistry>().Get(board.AvatarUid.Value);
            avatar.Stats.SetBase(StatId.MaxHp, hp);
            avatar.Stats.SetBase(StatId.Hp, hp);
            avatar.Stats.SetBase(StatId.Attack, attack);
            avatar.Stats.SetBase(StatId.Armor, armor);
            avatar.Stats.SetBase(StatId.CurrentArmor, armor);
        }

        private int SpawnLeaveTrap(int hp)
        {
            mPipeline.Enqueue(new SpawnCardAction("trap.leave", CardKind.Trap, ZoneId.Board, sSlot2, 1, "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            var uid = mArch.GetModel<BoardModel>().GetCardUid(sSlot2);
            var card = mArch.GetModel<CardRegistry>().Get(uid);
            card.Stats.SetBase(StatId.MaxHp, hp);
            card.Stats.SetBase(StatId.Hp, hp);
            return uid;
        }

        private void SpawnMonsterAt(SlotId slot, int hp)
        {
            SpawnSpecificMonsterAt("monster.skull_head", slot, hp);
        }

        private void SpawnSpecificMonsterAt(string defId, SlotId slot, int hp)
        {
            mPipeline.Enqueue(new SpawnCardAction(
                defId, CardKind.Monster, ZoneId.Board, slot, 1, "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            var card = mArch.GetModel<CardRegistry>().Get(mArch.GetModel<BoardModel>().GetCardUid(slot));
            card.Stats.SetBase(StatId.MaxHp, hp);
            card.Stats.SetBase(StatId.Hp, hp);
            card.Stats.SetBase(StatId.Attack, 0);
        }

        private void SpawnOrdinaryTrapAt(SlotId slot)
        {
            mPipeline.Enqueue(new SpawnCardAction(
                "trap.revive_stone", CardKind.Trap, ZoneId.Board, slot, 1, "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
        }

        private void SeedDrawPileMonsters(int count)
        {
            var deck = mArch.GetModel<DeckModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var board = mArch.GetModel<BoardModel>();
            for (var i = 0; i < count; i++)
            {
                mPipeline.Enqueue(new SpawnCardAction(
                    "monster.skull_head", CardKind.Monster, ZoneId.DrawPile, SlotId.None, 1, "test"));
                Assert.Greater(mPipeline.RunToCompletion(), 0);

                // Spawn 的 OnDeal/OnEnter 可能把实例补上场；强制收回抽牌堆。
                var uid = FindNewestUid(registry, "monster.skull_head");
                Assert.Greater(uid, 0);
                var card = registry.Get(uid);
                if (card.Zone.Value == ZoneId.Board)
                {
                    board.ClearSlot(card.Slot.Value);
                }

                if (card.Zone.Value != ZoneId.DrawPile)
                {
                    deck.AddToDrawPile(card, false);
                }
            }

            Assert.GreaterOrEqual(deck.DrawPileUids.Count, count);
        }

        private static int FindNewestUid(CardRegistry registry, string defId)
        {
            var newest = 0;
            foreach (var pair in registry.Cards)
            {
                if (pair.Value.DefId == defId && pair.Key > newest)
                {
                    newest = pair.Key;
                }
            }

            return newest;
        }

        private bool ContainsEventSince(int startIndex, CoreEventType type)
        {
            var entries = mPipeline.EventLog.Entries;
            for (var i = startIndex; i < entries.Count; i++)
            {
                if (entries[i].Type == type)
                {
                    return true;
                }
            }

            return false;
        }

        private void SpawnHelpOnBoard(string defId, SlotId slot)
        {
            mPipeline.Enqueue(new SpawnCardAction(defId, CardKind.HelpCard, ZoneId.Board, slot, 1, "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
        }

        private void SpawnHelpIntoItemSlots(string defId)
        {
            mPipeline.Enqueue(new SpawnCardAction(defId, CardKind.HelpCard, ZoneId.ItemSlots, SlotId.None, 1, "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
        }

        private int CountBoardNonAvatarCards()
        {
            var board = mArch.GetModel<BoardModel>();
            var count = 0;
            foreach (var uid in board.BoardCardUids())
            {
                if (uid > 0 && uid != board.AvatarUid.Value)
                {
                    count++;
                }
            }

            return count;
        }

        private int CountItemSlotHelpCards()
        {
            var registry = mArch.GetModel<CardRegistry>();
            var deck = mArch.GetModel<DeckModel>();
            var count = 0;
            for (var i = 0; i < deck.ItemSlotUids.Count; i++)
            {
                CardInstance card;
                if (registry.TryGet(deck.ItemSlotUids[i], out card) && card.Kind == CardKind.HelpCard)
                {
                    count++;
                }
            }

            return count;
        }

        private static NodeDeckOptions CreateSingleMonsterNode(int hp, int attack)
        {
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 1
            }.AddEnemyCard(new CardDraft("monster.skull_head", CardKind.Monster) { MaxHp = hp, Attack = attack });
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
    }
}
