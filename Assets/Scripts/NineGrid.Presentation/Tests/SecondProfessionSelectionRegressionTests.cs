using System;
using System.Collections.Generic;
using System.Linq;
using NineGrid.Cards;
using NineGrid.Content;
using NineGrid.Content.CardPresentation;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Effects;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NineGrid.Flow;
using NineGrid.Presentation.Ui;
using NUnit.Framework;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// Issue #223 验收测试：
    /// 第二职业与选人第三席位（刺客 / Icey / 空间振荡器 / 换位）。
    /// </summary>
    [TestFixture]
    public class SecondProfessionSelectionRegressionTests
    {
        private IArchitecture mArch;
        private GameContentCatalog mCatalog;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Interface;
            mArch.GetModel<RunModel>().SetPhase(GamePhase.InteractionLoop);
            LoadRealCatalog();
            RunSetupSelection.ResetToDefault();
        }

        [TearDown]
        public void TearDown()
        {
            RunSetupSelection.ResetToDefault();
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void ProfessionCatalog_Assassin_PropertiesAndRelic_AreCorrect()
        {
            var assassin = ProfessionCatalog.Get(ProfessionCatalog.Assassin);
            Assert.IsNotNull(assassin);
            Assert.AreEqual(ProfessionCatalog.Assassin, assassin.DefId);
            Assert.AreEqual(10, assassin.MaxHp);
            Assert.AreEqual(3, assassin.Attack);
            Assert.AreEqual(0, assassin.Armor);
            Assert.AreEqual(1, assassin.Recovery);
            Assert.AreEqual("relic.space_oscillator", assassin.InitialRelicDefId);
            Assert.AreEqual("avatar.layla", assassin.AvatarDefId);
            Assert.IsTrue(assassin.ItemSourceDeckIds.Contains(ProfessionCatalog.GenericItemDeckId));
            Assert.IsTrue(assassin.ItemSourceDeckIds.Contains(ProfessionCatalog.AssassinItemDeckId));
            Assert.IsFalse(assassin.ItemSourceDeckIds.Contains(ProfessionCatalog.WarriorItemDeckId));
        }

        [Test]
        public void ProfessionCatalog_Warrior_PropertiesAndRelic_RemainUnchanged()
        {
            var warrior = ProfessionCatalog.Get(ProfessionCatalog.Jester);
            Assert.IsNotNull(warrior);
            Assert.AreEqual(ProfessionCatalog.Jester, warrior.DefId);
            Assert.AreEqual(10, warrior.MaxHp);
            Assert.AreEqual(3, warrior.Attack);
            Assert.AreEqual(0, warrior.Armor);
            Assert.AreEqual(1, warrior.Recovery);
            Assert.AreEqual("relic.rotten_cleave_axe", warrior.InitialRelicDefId);
            Assert.AreEqual("avatar.default", warrior.AvatarDefId);
            Assert.IsTrue(warrior.ItemSourceDeckIds.Contains(ProfessionCatalog.GenericItemDeckId));
            Assert.IsTrue(warrior.ItemSourceDeckIds.Contains(ProfessionCatalog.WarriorItemDeckId));
            Assert.IsFalse(warrior.ItemSourceDeckIds.Contains(ProfessionCatalog.AssassinItemDeckId));
        }

        [Test]
        public void ProfessionCatalog_ItemSourcePool_AssassinContainsPositionSwap_AndExcludesWarriorSpecificCards()
        {
            var player = mArch.GetModel<PlayerModel>();

            ProfessionCatalog.SeedItemGenerationRules(player, mCatalog, ProfessionCatalog.Assassin);

            var pool = player.ItemSourcePoolDefIds;
            Assert.IsNotNull(pool);
            Assert.IsTrue(pool.Contains("help.position_swap"), "刺客道具来源池必须包含专属白卡「换位」");
            Assert.IsFalse(pool.Contains("help.impact_tutorial"), "刺客道具来源池不得包含战士专属道具卡");
            Assert.IsFalse(pool.Contains("help.doubling_tower"), "刺客道具来源池不得包含战士专属道具卡");
        }

        [Test]
        public void ProfessionCatalog_ItemSourcePool_WarriorDoesNotContainPositionSwap()
        {
            var player = mArch.GetModel<PlayerModel>();

            ProfessionCatalog.SeedItemGenerationRules(player, mCatalog, ProfessionCatalog.Jester);

            var pool = player.ItemSourcePoolDefIds;
            Assert.IsNotNull(pool);
            Assert.IsFalse(pool.Contains("help.position_swap"), "战士道具来源池绝不得包含刺客专属白卡「换位」");
        }

        [Test]
        public void InitialGameFactory_CreateAssassin_InitializesLaylaAvatarAndSpaceOscillatorRelic()
        {
            var options = InitialGameOptions.CreateForNewRun();
            options.ProfessionId = ProfessionCatalog.Assassin;

            var snapshot = InitialGameFactory.Create(mArch, options);
            Assert.IsNotNull(snapshot);

            var player = mArch.GetModel<PlayerModel>();
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();

            Assert.AreEqual(ProfessionCatalog.Assassin, player.ProfessionId.Value);
            Assert.IsTrue(player.RelicDefIds.Contains("relic.space_oscillator"));
            Assert.IsFalse(player.RelicDefIds.Contains("relic.rotten_cleave_axe"));

            var avatar = registry.Get(board.AvatarUid.Value);
            Assert.IsNotNull(avatar);
            Assert.AreEqual("avatar.layla", avatar.DefId);
            Assert.AreEqual(CardKind.Avatar, avatar.Kind);
            Assert.AreEqual(10, avatar.Stats.GetBase(StatId.Hp));
            Assert.AreEqual(3, avatar.Stats.GetBase(StatId.Attack));

            Assert.IsTrue(player.ItemSourcePoolDefIds.Contains("help.position_swap"));
            Assert.IsFalse(player.ItemSourcePoolDefIds.Contains("help.impact_tutorial"));
        }

        [Test]
        public void RunSetupSelection_TracksProfessionId_AndResetsToDefault()
        {
            Assert.AreEqual(ProfessionCatalog.Jester, RunSetupSelection.ProfessionId);

            RunSetupSelection.SetProfession(ProfessionCatalog.Assassin);
            Assert.AreEqual(ProfessionCatalog.Assassin, RunSetupSelection.ProfessionId);

            RunSetupSelection.ResetToDefault();
            Assert.AreEqual(ProfessionCatalog.Jester, RunSetupSelection.ProfessionId);
        }

        [Test]
        public void EndToEnd_AssassinRun_SwapWithMonster_TriggersSpaceOscillator_AndHomesAfterThreeMoves()
        {
            // 1. 初始化刺客新对局
            var options = InitialGameOptions.CreateForNewRun();
            options.ProfessionId = ProfessionCatalog.Assassin;
            InitialGameFactory.Create(mArch, options);

            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var player = mArch.GetModel<PlayerModel>();
            var deck = mArch.GetModel<DeckModel>();
            var phase = mArch.GetSystem<IPhaseSystem>();
            var pipeline = mArch.GetSystem<IActionPipelineSystem>();

            // 2. 验证初始状态：中心格 Avatar 为 Layla，遗物为空间振荡器
            Assert.AreEqual(SlotId.Center, board.AvatarSlot.Value);
            Assert.IsTrue(player.RelicDefIds.Contains("relic.space_oscillator"));

            // 3. 在格 2 放怪物 A (Hp 10)，在格 3 放怪物 B (Hp 10, 与格 2 邻接)
            var monsterA = CreateRealCardOnBoard("monster.salamander", SlotId.Board(2));
            var monsterB = CreateRealCardOnBoard("monster.salamander", SlotId.Board(3));
            var hpBeforeB = monsterB.Stats.GetBase(StatId.Hp);

            // 在道具栏放入一张「换位」道具卡
            var swapCard = CreateItemCard("help.position_swap");
            Assert.IsTrue(deck.ItemSlotUids.Contains(swapCard.Uid));

            // 4. 对格 2 的怪物 A 打出「换位」道具卡
            var useResult = phase.ApplyUseItem(swapCard.Uid, new[] { monsterA.Uid }, null);
            Assert.IsTrue(useResult.Accepted, "使用换位卡应成功");

            // 验收换位：
            // Avatar 到格 2，怪物 A 到中心格 5
            Assert.AreEqual(SlotId.Board(2), board.AvatarSlot.Value);
            Assert.AreEqual(monsterA.Uid, board.GetCardUid(SlotId.Center));
            Assert.IsTrue(board.IsAvatarOffHome.Value, "换位后进入离巢状态");
            Assert.AreEqual(0, board.AvatarHomingSteps.Value);

            // 验收空间振荡器遗物光环触发：
            // 格 2 邻接格（格 3 与中心格 5）受到 2 点振荡器直伤，怪物 B (在格 3) HP 减 2
            Assert.AreEqual(hpBeforeB - 2, monsterB.Stats.GetBase(StatId.Hp), "空间振荡器应在 Avatar 落地格 2 邻接的格 3 造成 2 点伤害");

            // 5. 3 次位移（旋转外圈 3 次）
            pipeline.Execute(new RotateBoardClockwiseAction(true)); // 2 -> 3 (count = 1)
            Assert.AreEqual(SlotId.Board(3), board.AvatarSlot.Value);
            Assert.AreEqual(1, board.AvatarHomingSteps.Value);

            pipeline.Execute(new RotateBoardClockwiseAction(true)); // 3 -> 6 (count = 2)
            Assert.AreEqual(SlotId.Board(6), board.AvatarSlot.Value);
            Assert.AreEqual(2, board.AvatarHomingSteps.Value);

            pipeline.Execute(new RotateBoardClockwiseAction(true)); // 6 -> 9 (count = 3 -> 触发归位回中心)

            // 验收满 3 次后归位回中心格：
            Assert.AreEqual(SlotId.Center, board.AvatarSlot.Value, "满 3 次位移后 Avatar 归位至中心格 5");
            Assert.IsFalse(board.IsAvatarOffHome.Value, "归位后退出离巢状态");
            Assert.AreEqual(0, board.AvatarHomingSteps.Value, "归位后倒计时清零");
        }

        private CardInstance CreateRealCardOnBoard(string defId, SlotId slot)
        {
            var content = mArch.GetSystem<IContentSystem>();
            var registry = mArch.GetModel<CardRegistry>();
            var draft = content.CreateDraft(defId);
            var card = draft.Create(registry);
            content.ApplyContentToCard(card);
            card.FaceUp = true;
            mArch.GetModel<BoardModel>().PlaceCard(card, slot);
            return card;
        }

        private CardInstance CreateItemCard(string defId)
        {
            var content = mArch.GetSystem<IContentSystem>();
            var registry = mArch.GetModel<CardRegistry>();
            var draft = content.CreateDraft(defId);
            var card = draft.Create(registry);
            content.ApplyContentToCard(card);
            card.Zone.Value = ZoneId.ItemSlots;
            mArch.GetModel<DeckModel>().AddToItemSlots(card);
            return card;
        }

        private void LoadRealCatalog()
        {
            var content = mArch.GetSystem<IContentSystem>();
            content.Load(ContentCatalogBootstrap.Load());
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, content.Catalog);
            Assert.IsTrue(content.HasCatalog, "需要真实内容目录");
            mCatalog = content.Catalog;
        }
    }
}
