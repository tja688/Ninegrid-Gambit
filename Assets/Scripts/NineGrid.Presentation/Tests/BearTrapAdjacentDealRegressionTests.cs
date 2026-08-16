using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// 捕熊陷阱（trap.bear_trap）邻格补牌触发契约：
    /// 真怪受伤后自毁；道具移除后自毁；机关卡不触发（陷阱保留在场上）。
    /// </summary>
    public class BearTrapAdjacentDealRegressionTests
    {
        private IArchitecture mArch;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Interface;
            mArch.GetModel<RunModel>().SetPhase(GamePhase.InteractionLoop);
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void AdjacentTrapDeal_DoesNotTriggerBearTrap_TrapStaysOnBoard()
        {
            LoadRealCatalog();
            CreateAvatarOnBoard(SlotId.Board(5));
            var bearTrap = CreateRealCardOnBoard("trap.bear_trap", SlotId.Board(4));
            FillBoardExcept(SlotId.Board(1));
            Run(new ShuffleIntoDrawPileAction("trap.spike", CardKind.Trap, 1, true, "test.refill"));

            Run(new FillEmptySlotsAction());

            var board = mArch.GetModel<BoardModel>();
            Assert.AreEqual(
                bearTrap.Uid,
                board.GetCardUid(SlotId.Board(4)),
                "邻格补入机关卡时捕熊陷阱不得自毁");
            Assert.AreNotEqual(
                0,
                board.GetCardUid(SlotId.Board(1)),
                "邻格机关应正常补入盘面");
        }

        [Test]
        public void AdjacentMonsterDeal_TriggersBearTrap_MonsterDamagedTrapRemoved()
        {
            LoadRealCatalog();
            CreateAvatarOnBoard(SlotId.Board(5));
            var bearTrap = CreateRealCardOnBoard("trap.bear_trap", SlotId.Board(4));
            FillBoardExcept(SlotId.Board(1));
            QueueAdjacentRefillCard("monster.melee_3", CardKind.Monster, hp: 20);

            Run(new FillEmptySlotsAction());

            var board = mArch.GetModel<BoardModel>();
            Assert.AreEqual(
                0,
                board.GetCardUid(SlotId.Board(4)),
                "邻格补入真怪后捕熊陷阱应自毁");
            var monsterUid = board.GetCardUid(SlotId.Board(1));
            Assert.AreNotEqual(0, monsterUid, "邻格怪物应正常补入盘面");
            var monster = mArch.GetModel<CardRegistry>().Get(monsterUid);
            Assert.Less(
                monster.Stats.GetBase(StatId.Hp),
                20f,
                "捕熊陷阱应对邻格补入的真怪造成伤害");
            Assert.AreNotEqual(bearTrap.Uid, board.GetCardUid(SlotId.Board(4)));
        }

        [Test]
        public void AdjacentHelpCardDeal_TriggersBearTrap_HelpRemovedTrapRemoved()
        {
            LoadRealCatalog();
            CreateAvatarOnBoard(SlotId.Board(5));
            var bearTrap = CreateRealCardOnBoard("trap.bear_trap", SlotId.Board(4));
            FillBoardExcept(SlotId.Board(1));
            Run(new ShuffleIntoDrawPileAction("help.healing_potion", CardKind.HelpCard, 1, true, "test.refill"));

            Run(new FillEmptySlotsAction());

            var board = mArch.GetModel<BoardModel>();
            Assert.AreEqual(
                0,
                board.GetCardUid(SlotId.Board(4)),
                "邻格补入道具后捕熊陷阱应自毁");
            Assert.AreEqual(
                0,
                board.GetCardUid(SlotId.Board(1)),
                "邻格道具应被捕熊陷阱移除");
            Assert.AreNotEqual(bearTrap.Uid, board.GetCardUid(SlotId.Board(4)));
        }

        private void LoadRealCatalog()
        {
            var content = mArch.GetSystem<IContentSystem>();
            content.Load(NineGrid.Content.ContentCatalogBootstrap.Load());
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, content.Catalog);
            Assert.IsTrue(content.HasCatalog, "需要真实内容目录");
        }

        private void QueueAdjacentRefillCard(string defId, CardKind fallbackKind, int hp = 0)
        {
            var content = mArch.GetSystem<IContentSystem>();
            var registry = mArch.GetModel<CardRegistry>();
            var draft = content.CreateDraft(defId);
            Assert.AreNotEqual(CardKind.Unknown, draft.Kind, defId + " 应在内容目录中");
            var card = draft.Kind == CardKind.Unknown
                ? registry.Create(defId, fallbackKind)
                : draft.Create(registry);
            content.ApplyContentToCard(card);
            if (hp > 0)
            {
                card.Stats.SetBase(StatId.MaxHp, hp);
                card.Stats.SetBase(StatId.Hp, hp);
            }

            Run(new ShuffleCardIntoDrawPileAction(card.Uid, true, "test.refill"));
        }

        private void FillBoardExcept(SlotId emptySlot)
        {
            var fillerSlots = new[]
            {
                SlotId.Board(1),
                SlotId.Board(2),
                SlotId.Board(3),
                SlotId.Board(6),
                SlotId.Board(7),
                SlotId.Board(8),
                SlotId.Board(9)
            };

            for (var i = 0; i < fillerSlots.Length; i++)
            {
                var slot = fillerSlots[i];
                if (slot == emptySlot)
                {
                    continue;
                }

                CreateRealCardOnBoard("monster.melee_3", slot);
            }
        }

        private CardInstance CreateAvatarOnBoard(SlotId slot)
        {
            var avatar = mArch.GetModel<CardRegistry>().Create("avatar.default", CardKind.Avatar);
            avatar.Stats.SetBase(StatId.MaxHp, 20);
            avatar.Stats.SetBase(StatId.Hp, 20);
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

        private void Run(GameAction action)
        {
            mArch.GetSystem<IActionPipelineSystem>().Execute(action);
        }
    }
}
