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
    /// ADR-0017：Trap 双桶——可交战/可伤、清关与赏金排除、静默 CounterAttackBanned。
    /// </summary>
    public sealed class TrapKindDualBucketRegressionTests
    {
        private static readonly SlotId sAdjacentSlot = SlotId.Board(2);
        private static readonly SlotId sFarSlot = SlotId.Board(9);

        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IActionPipelineSystem mPipeline;
        private IContentSystem mContent;
        private IStatSystem mStats;
        private IDeckSystem mDeck;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            EffectTemplateCatalog.Invalidate();
            CardPresentationConfigCatalog.Invalidate();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, ContentCatalogBootstrap.Load());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 41UL });
            mPhase = mArch.GetSystem<IPhaseSystem>();
            mPipeline = mArch.GetSystem<IActionPipelineSystem>();
            mContent = mArch.GetSystem<IContentSystem>();
            mStats = mArch.GetSystem<IStatSystem>();
            mDeck = mArch.GetSystem<IDeckSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            EffectTemplateCatalog.Invalidate();
            CardPresentationConfigCatalog.Invalidate();
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void Catalog_ReviveStone_IsTrapOnDeckTrap()
        {
            Assert.IsTrue(mContent.Catalog.TryGetCard("trap.revive_stone", out var stone));
            Assert.AreEqual(CardKind.Trap, stone.Kind);
            Assert.AreEqual("deck.trap", stone.DeckId);
            Assert.IsTrue(mContent.Catalog.TryGetEffect("trap.revive_stone.interact", out var fx));
            Assert.AreEqual(EffectContainerType.Trap, fx.ContainerType);
        }

        [Test]
        public void ActivateTrap_GrantsSilentCounterAttackBanned()
        {
            StartEmptyNode();
            var stoneUid = SpawnTrap(sAdjacentSlot);
            var stone = mArch.GetModel<CardRegistry>().Get(stoneUid);
            Assert.Greater(
                mStats.EvaluateRule(RuleId.CounterAttackBanned, 0f, mStats.CreateContext(stone)),
                0f,
                "Trap 装配后应静默挂 CounterAttackBanned");
        }

        [Test]
        public void Attack_AcceptsFaceUpTrapAsCombatTarget()
        {
            StartEmptyNode();
            var stoneUid = SpawnTrap(sAdjacentSlot);
            PrepareAvatar(99, 5, 0);
            var board = mArch.GetModel<BoardModel>();
            var hit = mPhase.ApplyCombatHit(board.AvatarUid.Value, stoneUid);
            Assert.IsTrue(hit.Accepted, hit.Reason);
            Assert.AreEqual(
                1,
                (int)mArch.GetModel<CardRegistry>().Get(stoneUid).Stats.GetBase(StatId.Hp),
                "ATK5 打 HP6 机关应剩 1");
        }

        [Test]
        public void Bomb_AllMonsters_DamagesFaceUpTrap()
        {
            StartEmptyNode();
            var stoneUid = SpawnTrap(sAdjacentSlot);
            var monsterUid = SpawnHighHpMonster(sFarSlot, hp: 8);
            var stoneHp = (int)mArch.GetModel<CardRegistry>().Get(stoneUid).Stats.GetBase(StatId.Hp);
            var monsterHp = (int)mArch.GetModel<CardRegistry>().Get(monsterUid).Stats.GetBase(StatId.Hp);

            mPipeline.Enqueue(new SpawnCardAction(
                "help.bomb",
                CardKind.HelpCard,
                ZoneId.ItemSlots,
                SlotId.None,
                1,
                "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            var bombUid = mArch.GetModel<DeckModel>().ItemSlotUids[
                mArch.GetModel<DeckModel>().ItemSlotUids.Count - 1];

            Assert.IsTrue(mPhase.ApplyUseItem(bombUid, null, null).Accepted, "爆弹 UseItem 应接受");
            Assert.AreEqual(
                stoneHp - 4,
                (int)mArch.GetModel<CardRegistry>().Get(stoneUid).Stats.GetBase(StatId.Hp),
                "爆弹应伤正面机关");
            Assert.AreEqual(
                monsterHp - 4,
                (int)mArch.GetModel<CardRegistry>().Get(monsterUid).Stats.GetBase(StatId.Hp),
                "爆弹仍伤正面怪");
        }

        /// <summary>
        /// 飞刀等 SelectedCards(kind=Monster) 单目标伤：应走可交战桶（含 Trap）。
        /// </summary>
        [Test]
        public void ThrowingKnife_SelectedCards_DamagesFaceUpTrap()
        {
            StartEmptyNode();
            var stoneUid = SpawnTrap(sAdjacentSlot);
            var stoneHp = (int)mArch.GetModel<CardRegistry>().Get(stoneUid).Stats.GetBase(StatId.Hp);

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

            Assert.IsTrue(
                mPhase.ApplyUseItem(knifeUid, new System.Collections.Generic.List<int> { stoneUid }, null)
                    .Accepted,
                "飞刀 UseItem 应接受");
            Assert.AreEqual(
                stoneHp - 6,
                (int)mArch.GetModel<CardRegistry>().Get(stoneUid).Stats.GetBase(StatId.Hp),
                "飞刀应对正面机关造成 6 伤（可交战桶）");
        }

        /// <summary>
        /// 绑架 trueMonsterOnly：Trap 不得被 SelectedCards 选中移除。
        /// </summary>
        [Test]
        public void Kidnapping_TrueMonsterOnly_DoesNotRemoveTrap()
        {
            StartEmptyNode();
            var stoneUid = SpawnTrap(sAdjacentSlot);
            Assert.AreEqual(ZoneId.Board, mArch.GetModel<CardRegistry>().Get(stoneUid).Zone.Value);

            mPipeline.Enqueue(new SpawnCardAction(
                "help.kidnapping",
                CardKind.HelpCard,
                ZoneId.ItemSlots,
                SlotId.None,
                1,
                "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            var itemUid = mArch.GetModel<DeckModel>().ItemSlotUids[
                mArch.GetModel<DeckModel>().ItemSlotUids.Count - 1];

            Assert.IsTrue(
                mPhase.ApplyUseItem(itemUid, new System.Collections.Generic.List<int> { stoneUid }, null)
                    .Accepted,
                "绑架 UseItem 命令可接受，但目标应被滤空");
            Assert.AreEqual(
                ZoneId.Board,
                mArch.GetModel<CardRegistry>().Get(stoneUid).Zone.Value,
                "真怪桶：机关不得被绑架移除");
        }

        [Test]
        public void SelectedKindFilter_MonsterDefaultsToCombatBucket()
        {
            Assert.IsTrue(CardCombatRules.MatchesSelectedKindFilter(
                CardKind.Trap, CardKind.Monster, trueMonsterOnly: false));
            Assert.IsFalse(CardCombatRules.MatchesSelectedKindFilter(
                CardKind.Trap, CardKind.Monster, trueMonsterOnly: true));
            Assert.IsTrue(CardCombatRules.MatchesSelectedKindFilter(
                CardKind.Monster, CardKind.Monster, trueMonsterOnly: true));
        }

        [Test]
        public void NodeCleared_WhenOnlyTrapRemainsOnBoardAndDrawPile()
        {
            StartEmptyNode();
            SpawnTrap(sAdjacentSlot);
            Assert.IsFalse(mDeck.HasEnemyOnBoard(), "场上仅 Trap 不算有敌");

            var registry = mArch.GetModel<CardRegistry>();
            var deck = mArch.GetModel<DeckModel>();
            var pileTrap = registry.Create("trap.revive_stone", CardKind.Trap);
            mContent.ApplyContentToCard(pileTrap);
            deck.AddToDrawPile(pileTrap, true);

            Assert.IsFalse(mDeck.HasEnemyInDrawPile(), "抽牌堆仅 Trap 不算有敌");
            Assert.IsTrue(mDeck.IsNodeCleared(), "仅剩 Trap 时应可清关");
        }

        [Test]
        public void KillTrap_DoesNotAwardGold()
        {
            StartEmptyNode();
            var stoneUid = SpawnTrap(sAdjacentSlot);
            PrepareAvatar(99, 99, 0);
            var player = mArch.GetModel<PlayerModel>();
            var coinsBefore = player.Coins.Value;
            var board = mArch.GetModel<BoardModel>();

            mPipeline.Enqueue(new KillAction(board.AvatarUid.Value, stoneUid));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            Assert.AreEqual(coinsBefore, player.Coins.Value, "打死机关无赏金");
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
        }

        private int SpawnTrap(SlotId slot)
        {
            mPipeline.Enqueue(new SpawnCardAction("trap.revive_stone", CardKind.Trap, ZoneId.Board, slot, 1, "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            return mArch.GetModel<BoardModel>().GetCardUid(slot);
        }

        private int SpawnHighHpMonster(SlotId slot, int hp)
        {
            var registry = mArch.GetModel<CardRegistry>();
            var board = mArch.GetModel<BoardModel>();
            var card = registry.Create("monster.test.trap_bucket", CardKind.Monster);
            card.Stats.SetBase(StatId.MaxHp, hp);
            card.Stats.SetBase(StatId.Hp, hp);
            card.Stats.SetBase(StatId.Attack, 0);
            card.Stats.SetBase(StatId.Armor, 0);
            card.FaceUp = true;
            board.PlaceCard(card, slot);
            return card.Uid;
        }
    }
}
