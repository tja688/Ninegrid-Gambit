using NineGrid.Content;
using NineGrid.Content.CardPresentation;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Effects;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// 机关卡落地批次2：滚石 / 捕熊 / 烈焰语义 + Help 三卡迁徙回归。
    /// </summary>
    public sealed class TrapBatch2RegressionTests
    {
        private static readonly SlotId sSlot3 = SlotId.Board(3);
        private static readonly SlotId sSlot6 = SlotId.Board(6);
        private static readonly SlotId sSlot2 = SlotId.Board(2);
        private static readonly SlotId sSlot4 = SlotId.Board(4);
        private static readonly SlotId sSlot8 = SlotId.Board(8);
        private static readonly SlotId sSlot9 = SlotId.Board(9);

        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IActionPipelineSystem mPipeline;
        private IContentSystem mContent;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            EffectTemplateCatalog.Invalidate();
            CardPresentationConfigCatalog.Invalidate();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, ContentCatalogBootstrap.Load());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 42UL });
            mPhase = mArch.GetSystem<IPhaseSystem>();
            mPipeline = mArch.GetSystem<IActionPipelineSystem>();
            mContent = mArch.GetSystem<IContentSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            EffectTemplateCatalog.Invalidate();
            CardPresentationConfigCatalog.Invalidate();
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void Catalog_Batch2Traps_AreTrapOnDeckTrap_AndHelpIdsGone()
        {
            AssertTrapMounted("trap.rolling_stone", "trap.rolling_stone.slot3");
            AssertTrapMounted("trap.bear_trap", "trap.bear_trap.fill");
            AssertTrapMounted("trap.flame", "trap.flame.move");
            AssertTrapMounted("trap.flame", "trap.flame.remove");

            Assert.IsFalse(mContent.Catalog.TryGetCard("help.rolling_stone", out _), "help.rolling_stone 应已删除");
            Assert.IsFalse(mContent.Catalog.TryGetCard("help.bear_trap", out _), "help.bear_trap 应已删除");
            Assert.IsFalse(mContent.Catalog.TryGetCard("help.flame", out _), "help.flame 应已删除");
        }

        [Test]
        public void RollingStone_MoveToSlot3_ClearsNormalMonster_DoesNotSelfRemove()
        {
            StartEmptyNode();
            var stoneUid = SpawnTrap("trap.rolling_stone", sSlot2);
            var monsterUid = SpawnKind("monster.skull_head", CardKind.Monster, sSlot6, hp: 5);

            mPipeline.Enqueue(new MoveCardAction(stoneUid, sSlot3, "test", "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            var registry = mArch.GetModel<CardRegistry>();
            Assert.AreEqual(ZoneId.Removed, registry.Get(monsterUid).Zone.Value, "应清掉格6真怪");
            Assert.AreEqual(sSlot3, registry.Get(stoneUid).Slot.Value, "滚石应留在格3");
            Assert.AreEqual(ZoneId.Board, registry.Get(stoneUid).Zone.Value, "滚石不自移除");
        }

        [Test]
        public void RollingStone_MoveToSlot3_ClearsHelpCardOnSlot6()
        {
            StartEmptyNode();
            var stoneUid = SpawnTrap("trap.rolling_stone", sSlot2);
            var helpUid = SpawnKind("help.bomb", CardKind.HelpCard, sSlot6, hp: 0);

            mPipeline.Enqueue(new MoveCardAction(stoneUid, sSlot3, "test", "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            var registry = mArch.GetModel<CardRegistry>();
            Assert.AreEqual(ZoneId.Removed, registry.Get(helpUid).Zone.Value, "应清掉格6道具");
            Assert.AreEqual(ZoneId.Board, registry.Get(stoneUid).Zone.Value);
        }

        [Test]
        public void RollingStone_MoveToSlot3_ClearsTrapOnSlot6()
        {
            StartEmptyNode();
            var stoneUid = SpawnTrap("trap.rolling_stone", sSlot2);
            var otherTrapUid = SpawnTrap("trap.revive_stone", sSlot6);

            mPipeline.Enqueue(new MoveCardAction(stoneUid, sSlot3, "test", "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            var registry = mArch.GetModel<CardRegistry>();
            Assert.AreEqual(ZoneId.Removed, registry.Get(otherTrapUid).Zone.Value, "应清掉格6机关");
            Assert.AreEqual(ZoneId.Board, registry.Get(stoneUid).Zone.Value);
        }

        [Test]
        public void RollingStone_DoesNotClearEliteOrBossMonsterOnSlot6()
        {
            StartEmptyNode();
            var stoneUid = SpawnTrap("trap.rolling_stone", sSlot2);
            var eliteUid = SpawnKind("monster.skull_head", CardKind.Monster, sSlot6, hp: 8);
            var elite = mArch.GetModel<CardRegistry>().Get(eliteUid);
            elite.Counters.Set(CoreCounterKeys.Elite, 1);

            mPipeline.Enqueue(new MoveCardAction(stoneUid, sSlot3, "test", "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            Assert.AreEqual(ZoneId.Board, elite.Zone.Value, "精英怪不受滚石清除");
            Assert.AreEqual(ZoneId.Board, mArch.GetModel<CardRegistry>().Get(stoneUid).Zone.Value);
        }

        [Test]
        public void BearTrap_AdjacentFill_MonsterTakes10Damage_ThenSelfRemoves()
        {
            StartEmptyNode();
            var trapUid = SpawnTrap("trap.bear_trap", sSlot2);
            PrepareAvatar(99, 0, 0);
            EnqueueDrawAndFill("monster.skull_head", CardKind.Monster, hp: 12, armor: 0);
            var registry = mArch.GetModel<CardRegistry>();
            var filledUid = FindBoardUidExcept(trapUid);
            Assert.AreNotEqual(0, filledUid, "应补出一张怪");
            Assert.AreEqual(
                2,
                (int)registry.Get(filledUid).Stats.GetBase(StatId.Hp),
                "邻格补怪应受 10 伤（12→2）");
            Assert.AreEqual(ZoneId.Removed, registry.Get(trapUid).Zone.Value, "捕熊应自移除");
        }

        [Test]
        public void BearTrap_AdjacentFill_HelpCardRemoved_ThenSelfRemoves()
        {
            StartEmptyNode();
            var trapUid = SpawnTrap("trap.bear_trap", sSlot2);
            var registry = mArch.GetModel<CardRegistry>();
            var helpPendingUid = PrepareDrawPileCard("help.bomb", CardKind.HelpCard, hp: 0);
            mPipeline.Enqueue(new FillEmptySlotsAction());
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            Assert.AreEqual(ZoneId.Removed, registry.Get(trapUid).Zone.Value, "捕熊应自移除");
            Assert.AreEqual(
                ZoneId.Removed,
                registry.Get(helpPendingUid).Zone.Value,
                "邻格补道具应被猛夹移除");
            Assert.AreEqual(0, FindBoardUidExcept(trapUid), "场上不应残留该道具");
        }

        [Test]
        public void BearTrap_AdjacentFill_TrapUnaffected_ThenSelfRemoves()
        {
            StartEmptyNode();
            var trapUid = SpawnTrap("trap.bear_trap", sSlot2);
            var registry = mArch.GetModel<CardRegistry>();
            EnqueueDrawAndFill("trap.revive_stone", CardKind.Trap, hp: 6);
            var filledUid = FindBoardUidExcept(trapUid);
            Assert.AreNotEqual(0, filledUid);
            Assert.AreEqual("trap.revive_stone", registry.Get(filledUid).DefId);
            Assert.AreEqual(ZoneId.Board, registry.Get(filledUid).Zone.Value, "邻格补机关不受影响");
            Assert.AreEqual(6, (int)registry.Get(filledUid).Stats.GetBase(StatId.Hp), "机关 HP 不变");
            Assert.AreEqual(ZoneId.Removed, registry.Get(trapUid).Zone.Value, "捕熊仍自移除");
        }

        [Test]
        public void Flame_MoveAdjacentDamagesPlayer_ThirdMoveSelfRemoves()
        {
            StartEmptyNode();
            PrepareAvatar(99, 0, 0);
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var avatar = registry.Get(board.AvatarUid.Value);
            Assert.AreEqual(SlotId.Board(5), board.AvatarSlot.Value, "Avatar 应在中心格5");

            // 格2 正交邻中心；先激活再移动
            var flameUid = SpawnTrap("trap.flame", sSlot2);
            var flame = registry.Get(flameUid);
            Assert.AreEqual(ZoneId.Board, flame.Zone.Value);
            Assert.IsTrue(flame.FaceUp);
            Assert.Greater(
                mArch.GetSystem<IEffectSystem>().GetInstanceIdsByOwner(flameUid).Count,
                0,
                "烈焰应已激活效果实例");

            var hp0 = (int)avatar.Stats.GetBase(StatId.Hp);
            Assert.AreEqual(0, (int)StatArmorUtility.GetCurrentArmor(avatar), "Avatar 甲应清零");

            var eventStart = mPipeline.EventLog.Entries.Count;

            mPipeline.Enqueue(new MoveCardAction(flameUid, sSlot4, "test", "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            Assert.AreEqual(sSlot4, registry.Get(flameUid).Slot.Value);
            Assert.IsTrue(
                ContainsEffectTriggeredSince(eventStart, flameUid),
                "邻移应触发烈焰效果");
            Assert.IsTrue(
                ContainsHpChangedSince(eventStart, board.AvatarUid.Value),
                "邻移应产生 Avatar HpChanged");
            Assert.AreEqual(hp0 - 1, (int)avatar.Stats.GetBase(StatId.Hp), "第1次邻移应伤玩家1");

            mPipeline.Enqueue(new MoveCardAction(flameUid, sSlot2, "test", "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            Assert.AreEqual(hp0 - 2, (int)avatar.Stats.GetBase(StatId.Hp), "第2次邻移应再伤1");

            mPipeline.Enqueue(new MoveCardAction(flameUid, sSlot4, "test", "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            Assert.AreEqual(hp0 - 3, (int)avatar.Stats.GetBase(StatId.Hp), "第3次邻移仍伤1");
            Assert.AreEqual(ZoneId.Removed, registry.Get(flameUid).Zone.Value, "满3次应自移除");
        }

        [Test]
        public void Flame_DealFromDrawPile_DoesNotCountAsSelfMove()
        {
            StartEmptyNode();
            PrepareAvatar(99, 0, 0);
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var avatar = registry.Get(board.AvatarUid.Value);
            var hp0 = (int)avatar.Stats.GetBase(StatId.Hp);

            var flameUid = PrepareDrawPileCard("trap.flame", CardKind.Trap, hp: 6);
            var eventStart = mPipeline.EventLog.Entries.Count;
            mPipeline.Enqueue(new FillEmptySlotsAction());
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            Assert.AreEqual(ZoneId.Board, registry.Get(flameUid).Zone.Value, "烈焰应从牌库入场");
            Assert.AreEqual(hp0, (int)avatar.Stats.GetBase(StatId.Hp), "入场落地不计 OnSelfMove，不伤玩家");
            Assert.IsFalse(
                ContainsEffectTriggeredSince(eventStart, flameUid),
                "入场落地不应 EffectTriggered");

            // 入场后强制落到邻中心格，再盘面邻移一次：应只计 1 次，伤 1，仍在场。
            mPipeline.Enqueue(new MoveCardAction(flameUid, sSlot2, "test", "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            var hpAfterFirstBoardMove = (int)avatar.Stats.GetBase(StatId.Hp);
            Assert.AreEqual(hp0 - 1, hpAfterFirstBoardMove, "入场后第1次盘面邻移才伤1");
            Assert.AreEqual(ZoneId.Board, registry.Get(flameUid).Zone.Value, "仅1次盘面移动不应自移除");

            mPipeline.Enqueue(new MoveCardAction(flameUid, sSlot4, "test", "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            mPipeline.Enqueue(new MoveCardAction(flameUid, sSlot2, "test", "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            Assert.AreEqual(hp0 - 3, (int)avatar.Stats.GetBase(StatId.Hp), "满3次盘面移动应共伤3");
            Assert.AreEqual(ZoneId.Removed, registry.Get(flameUid).Zone.Value, "满3次盘面移动应自移除");
        }

        [Test]
        public void RollingStone_NonBoardToSlot3_DoesNotClearSlot6()
        {
            StartEmptyNode();
            var otherUid = SpawnKind("monster.skull_head", CardKind.Monster, sSlot6, hp: 5);
            var stoneUid = PrepareDrawPileCard("trap.rolling_stone", CardKind.Trap, hp: 6);

            // 直接把牌库中的滚石 Move 到格3（FromSlot 非盘面）：不应视为 OnMoveToSlot。
            mPipeline.Enqueue(new MoveCardAction(stoneUid, sSlot3, "test", "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            var registry = mArch.GetModel<CardRegistry>();
            Assert.AreEqual(sSlot3, registry.Get(stoneUid).Slot.Value);
            Assert.AreEqual(ZoneId.Board, registry.Get(otherUid).Zone.Value, "入场到格3不应清格6");
        }

        private bool ContainsEffectTriggeredSince(int startIndex, int ownerUid)
        {
            var entries = mPipeline.EventLog.Entries;
            for (var i = startIndex; i < entries.Count; i++)
            {
                if (entries[i].Type == CoreEventType.EffectTriggered && entries[i].CardUid == ownerUid)
                {
                    return true;
                }
            }

            return false;
        }

        private bool ContainsHpChangedSince(int startIndex, int targetUid)
        {
            var entries = mPipeline.EventLog.Entries;
            for (var i = startIndex; i < entries.Count; i++)
            {
                if (entries[i].Type == CoreEventType.HpChanged && entries[i].TargetUid == targetUid)
                {
                    return true;
                }
            }

            return false;
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

        private int SpawnTrap(string defId, SlotId slot)
        {
            return SpawnKind(defId, CardKind.Trap, slot, hp: 6);
        }

        private int SpawnKind(string defId, CardKind kind, SlotId slot, int hp)
        {
            mPipeline.Enqueue(new SpawnCardAction(defId, kind, ZoneId.Board, slot, 1, "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            var uid = mArch.GetModel<BoardModel>().GetCardUid(slot);
            if (hp > 0 && kind != CardKind.HelpCard)
            {
                var card = mArch.GetModel<CardRegistry>().Get(uid);
                card.Stats.SetBase(StatId.MaxHp, hp);
                card.Stats.SetBase(StatId.Hp, hp);
            }

            return uid;
        }

        private void EnqueueDrawAndFill(string defId, CardKind kind, int hp, int armor = 0)
        {
            PrepareDrawPileCard(defId, kind, hp, armor);
            mPipeline.Enqueue(new FillEmptySlotsAction());
            Assert.Greater(mPipeline.RunToCompletion(), 0);
        }

        private int PrepareDrawPileCard(string defId, CardKind kind, int hp, int armor = 0)
        {
            var registry = mArch.GetModel<CardRegistry>();
            var deck = mArch.GetModel<DeckModel>();
            var draft = mContent.CreateDraft(defId);
            if (draft.Kind == CardKind.Unknown)
            {
                draft = new CardDraft(defId, kind);
            }

            if (hp > 0)
            {
                draft.MaxHp = hp;
                draft.Hp = hp;
            }

            draft.Armor = armor;

            var pending = draft.Create(registry);
            mContent.ApplyContentToCard(pending);
            if (hp > 0)
            {
                pending.Stats.SetBase(StatId.MaxHp, hp);
                pending.Stats.SetBase(StatId.Hp, hp);
            }

            pending.Stats.SetBase(StatId.Armor, armor);
            pending.Stats.SetBase(StatId.CurrentArmor, armor);

            deck.AddToDrawPile(pending, true);
            return pending.Uid;
        }

        private int FindBoardUidExcept(int excludeUid)
        {
            var board = mArch.GetModel<BoardModel>();
            var avatarUid = board.AvatarUid.Value;
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var uid = board.GetCardUid(SlotId.Board(i));
                if (uid != 0 && uid != excludeUid && uid != avatarUid)
                {
                    return uid;
                }
            }

            return 0;
        }

        private void AssertTrapMounted(string trapId, string effectId)
        {
            Assert.IsTrue(mContent.Catalog.TryGetCard(trapId, out var trap), "missing " + trapId);
            Assert.AreEqual(CardKind.Trap, trap.Kind, trapId + " catalog kind");
            Assert.AreEqual("deck.trap", trap.DeckId, trapId + " deckId");
            CollectionAssert.Contains(trap.EffectIds, effectId, trapId + " should mount " + effectId);
            Assert.IsTrue(mContent.Catalog.TryGetEffect(effectId, out var fx), "missing effect " + effectId);
            Assert.AreEqual(ContentImplementationState.Implemented, fx.State, effectId);
            Assert.AreEqual(EffectContainerType.Trap, fx.ContainerType, effectId + " container");
        }
    }
}
