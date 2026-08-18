using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NineGrid.Flow;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// Issue #221 验收测试：
    /// 道具卡「换位」（help.position_swap）：白卡、正面真怪、消耗、不进战士来源池。
    /// </summary>
    public class PositionSwapItemCardRegressionTests
    {
        private const string PositionSwapDefId = "help.position_swap";
        private const string SwapCardDefId = "help.swap_card";

        private IArchitecture mArch;
        private GameContentCatalog mCatalog;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Interface;
            mArch.GetModel<RunModel>().SetPhase(GamePhase.InteractionLoop);
            LoadRealCatalog();
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void PositionSwap_CatalogDefinition_IsWhiteHelpCard_InSecondPlayerDeck()
        {
            Assert.IsTrue(mCatalog.TryGetCard(PositionSwapDefId, out var cardDef) && cardDef != null);
            Assert.AreEqual(CardKind.HelpCard, cardDef.Kind);
            Assert.AreEqual(ContentRarity.White, cardDef.Rarity);
            Assert.AreEqual("deck.player_assassin", cardDef.DeckId);
            Assert.IsFalse(FormalContentWiring.IsUnofficialDeck(cardDef.DeckId), "不应是非正式卡组");
        }

        [Test]
        public void PositionSwap_NotInWarriorSourcePool_NorGenericHelpDeck()
        {
            var player = mArch.GetModel<PlayerModel>();
            ProfessionCatalog.SeedItemGenerationRules(player, mCatalog, ProfessionCatalog.Jester);
            var source = player.ItemSourcePoolDefIds;
            CollectionAssert.DoesNotContain(source, PositionSwapDefId, "换位卡不得进入战士道具来源池");

            // 也不在通用道具卡组中
            Assert.AreNotEqual(ProfessionCatalog.GenericItemDeckId, mCatalog.Cards[PositionSwapDefId].DeckId);
        }

        [Test]
        public void PositionSwap_BoardSelectResolver_RecognizesSingleDragTarget_FaceUpTrueMonster()
        {
            var resolved = HelpCardBoardSelectResolver.TryGetPlayKind(
                PositionSwapDefId,
                out var playKind,
                out var spec);

            Assert.IsTrue(resolved, "应能解析换位卡目标需求");
            Assert.AreEqual(HelpCardPlayKind.SingleDragTarget, playKind);
            Assert.AreEqual(1, spec.Count);
            Assert.IsTrue(spec.RequiresMonster);
            Assert.IsTrue(spec.RequiresTrueMonster, "应要求真怪");
            Assert.IsTrue(spec.RequiresFaceUp, "应要求正面");
        }

        [Test]
        public void PositionSwap_RealUseItem_WithFaceUpTrueMonster_SwapsAvatarAndMonsterAtomically_AndConsumesItem()
        {
            var avatar = CreateAvatarOnBoard(SlotId.Center);
            var monster = CreateRealCardOnBoard("monster.salamander", SlotId.Board(2));
            var swapCard = CreateItemCard(PositionSwapDefId);

            var board = mArch.GetModel<BoardModel>();
            var deck = mArch.GetModel<DeckModel>();
            var phase = mArch.GetSystem<IPhaseSystem>();

            Assert.AreEqual(SlotId.Center, board.AvatarSlot.Value);
            Assert.AreEqual(monster.Uid, board.GetCardUid(SlotId.Board(2)));
            CollectionAssert.Contains((System.Collections.ICollection)deck.ItemSlotUids, swapCard.Uid);

            var result = phase.ApplyUseItem(swapCard.Uid, new[] { monster.Uid }, null);
            Assert.IsTrue(result.Accepted, "使用换位卡应被接受");

            // 验收：Avatar 与怪物原子换位
            Assert.AreEqual(SlotId.Board(2), board.AvatarSlot.Value, "Avatar 应来到格 2");
            Assert.AreEqual(monster.Uid, board.GetCardUid(SlotId.Center), "怪物应来到中心格 5");

            // 验收：换位卡从道具栏中消耗并移除
            CollectionAssert.DoesNotContain((System.Collections.ICollection)deck.ItemSlotUids, swapCard.Uid, "换位卡用后应从道具格消耗移除");
        }

        [Test]
        public void PositionSwap_RealUseItem_WithFaceDownMonster_Blocked_NoSwap_NotConsumed()
        {
            var avatar = CreateAvatarOnBoard(SlotId.Center);
            var monster = CreateRealCardOnBoard("monster.salamander", SlotId.Board(2));
            monster.FaceUp = false;
            var swapCard = CreateItemCard(PositionSwapDefId);

            var board = mArch.GetModel<BoardModel>();
            var deck = mArch.GetModel<DeckModel>();
            var phase = mArch.GetSystem<IPhaseSystem>();

            var result = phase.ApplyUseItem(swapCard.Uid, new[] { monster.Uid }, null);

            // 验收：背面怪物无法触发换位
            Assert.AreEqual(SlotId.Center, board.AvatarSlot.Value, "Avatar 仍在中心");
            Assert.AreEqual(monster.Uid, board.GetCardUid(SlotId.Board(2)), "怪物仍在格 2");
        }

        [Test]
        public void PositionSwap_RealUseItem_WithTrap_Blocked()
        {
            var avatar = CreateAvatarOnBoard(SlotId.Center);
            var trap = CreateRealCardOnBoard("trap.spike", SlotId.Board(3));
            var swapCard = CreateItemCard(PositionSwapDefId);

            var board = mArch.GetModel<BoardModel>();
            var phase = mArch.GetSystem<IPhaseSystem>();

            phase.ApplyUseItem(swapCard.Uid, new[] { trap.Uid }, null);

            Assert.AreEqual(SlotId.Center, board.AvatarSlot.Value, "Avatar 仍在中心");
            Assert.AreEqual(trap.Uid, board.GetCardUid(SlotId.Board(3)), "机关仍在格 3");
        }

        [Test]
        public void PositionSwap_RealUseItem_WithLeaveTrap_Blocked()
        {
            var avatar = CreateAvatarOnBoard(SlotId.Center);
            var leaveTrap = CreateRealCardOnBoard("trap.leave", SlotId.Board(3));
            var swapCard = CreateItemCard(PositionSwapDefId);

            var board = mArch.GetModel<BoardModel>();
            var phase = mArch.GetSystem<IPhaseSystem>();

            phase.ApplyUseItem(swapCard.Uid, new[] { leaveTrap.Uid }, null);

            Assert.AreEqual(SlotId.Center, board.AvatarSlot.Value, "Avatar 仍在中心");
            Assert.AreEqual(leaveTrap.Uid, board.GetCardUid(SlotId.Board(3)), "离开机关仍在格 3");
        }

        [Test]
        public void PositionSwap_RealUseItem_WithGroundHelpCard_Blocked()
        {
            var avatar = CreateAvatarOnBoard(SlotId.Center);
            var groundHelp = CreateRealCardOnBoard("help.food_card", SlotId.Board(1));
            var swapCard = CreateItemCard(PositionSwapDefId);

            var board = mArch.GetModel<BoardModel>();
            var phase = mArch.GetSystem<IPhaseSystem>();

            phase.ApplyUseItem(swapCard.Uid, new[] { groundHelp.Uid }, null);

            Assert.AreEqual(SlotId.Center, board.AvatarSlot.Value, "Avatar 仍在中心");
            Assert.AreEqual(groundHelp.Uid, board.GetCardUid(SlotId.Board(1)), "地面道具仍在格 1");
        }

        [Test]
        public void PositionSwap_RealUseItem_WithAvatarSelf_Blocked()
        {
            var avatar = CreateAvatarOnBoard(SlotId.Center);
            var swapCard = CreateItemCard(PositionSwapDefId);

            var board = mArch.GetModel<BoardModel>();
            var phase = mArch.GetSystem<IPhaseSystem>();

            phase.ApplyUseItem(swapCard.Uid, new[] { avatar.Uid }, null);

            Assert.AreEqual(SlotId.Center, board.AvatarSlot.Value, "Avatar 仍在中心");
        }

        [Test]
        public void PositionSwap_UseItem_DoesNotAdvanceNineGridInteractions()
        {
            var avatar = CreateAvatarOnBoard(SlotId.Center);
            var monster = CreateRealCardOnBoard("monster.salamander", SlotId.Board(2));
            var swapCard = CreateItemCard(PositionSwapDefId);

            var phase = mArch.GetSystem<IPhaseSystem>();
            var run = mArch.GetModel<RunModel>();

            var phaseBefore = run.Phase.Value;
            var result = phase.ApplyUseItem(swapCard.Uid, new[] { monster.Uid }, null);
            Assert.IsTrue(result.Accepted);

            // 验收：不算九宫格互动，不进入敌方行动结算阶段，保持在交互循环
            Assert.AreEqual(phaseBefore, run.Phase.Value, "道具使用不应打破 InteractionLoop 阶段进入敌方行动");
        }

        [Test]
        public void ExistingSwapCard_SemanticsUnchanged()
        {
            var avatar = CreateAvatarOnBoard(SlotId.Center);
            var monster1 = CreateRealCardOnBoard("monster.salamander", SlotId.Board(1));
            var monster2 = CreateRealCardOnBoard("monster.hoodlum", SlotId.Board(3));
            var swapCard = CreateItemCard(SwapCardDefId);

            var board = mArch.GetModel<BoardModel>();
            var phase = mArch.GetSystem<IPhaseSystem>();

            var result = phase.ApplyUseItem(swapCard.Uid, new[] { monster1.Uid, monster2.Uid }, null);
            Assert.IsTrue(result.Accepted);

            // 验收：原「交换」卡两卡换位，Avatar 保持中心不变
            Assert.AreEqual(monster2.Uid, board.GetCardUid(SlotId.Board(1)));
            Assert.AreEqual(monster1.Uid, board.GetCardUid(SlotId.Board(3)));
            Assert.AreEqual(SlotId.Center, board.AvatarSlot.Value);
        }

        private void LoadRealCatalog()
        {
            var content = mArch.GetSystem<IContentSystem>();
            content.Load(NineGrid.Content.ContentCatalogBootstrap.Load());
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, content.Catalog);
            Assert.IsTrue(content.HasCatalog, "需要真实内容目录");
            mCatalog = content.Catalog;
        }

        private CardInstance CreateAvatarOnBoard(SlotId slot)
        {
            var avatar = mArch.GetModel<CardRegistry>().Create("avatar.default", CardKind.Avatar);
            avatar.Stats.SetBase(StatId.MaxHp, 30);
            avatar.Stats.SetBase(StatId.Hp, 30);
            avatar.Stats.SetBase(StatId.Attack, 2);
            mArch.GetModel<BoardModel>().SetAvatar(avatar, slot);
            return avatar;
        }

        private CardInstance CreateRealCardOnBoard(string defId, SlotId slot)
        {
            var content = mArch.GetSystem<IContentSystem>();
            var registry = mArch.GetModel<CardRegistry>();
            var draft = content.CreateDraft(defId);
            Assert.AreNotEqual(CardKind.Unknown, draft.Kind, defId + " 应在内容目录中");
            var card = draft.Create(registry);
            content.ApplyContentToCard(card);
            mArch.GetModel<BoardModel>().PlaceCard(card, slot);
            return card;
        }

        private CardInstance CreateItemCard(string defId)
        {
            var content = mArch.GetSystem<IContentSystem>();
            var registry = mArch.GetModel<CardRegistry>();
            var draft = content.CreateDraft(defId);
            Assert.AreNotEqual(CardKind.Unknown, draft.Kind, defId + " 应在内容目录中");
            var card = draft.Create(registry);
            content.ApplyContentToCard(card);
            card.Zone.Value = ZoneId.ItemSlots;
            mArch.GetModel<DeckModel>().AddToItemSlots(card);
            return card;
        }
    }
}
