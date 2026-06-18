using NineGrid.Core.Commands;
using NineGrid.Core.Systems;
using NUnit.Framework;

namespace NineGrid.Core.Tests
{
    public sealed class P4NodeFlowTests
    {
        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            InitialGameFactory.Create(NineGridArchitecture.Current);
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void BoardRotationAndFillUseStableSlotOrder()
        {
            var architecture = NineGridArchitecture.Current;
            var registry = architecture.GetModel<CardRegistry>();
            var board = architecture.GetModel<BoardModel>();
            var deck = architecture.GetModel<DeckModel>();
            var boardSystem = architecture.GetSystem<IBoardSystem>();
            var first = registry.Create("player.first", CardKind.PlayerCard);
            var second = registry.Create("player.second", CardKind.PlayerCard);
            deck.AddToDrawPile(first, false);
            deck.AddToDrawPile(second, false);

            boardSystem.FillEmptySlots();

            Assert.AreEqual(first.Uid, board.GetCardUid(SlotId.Board(2)));
            Assert.AreEqual(second.Uid, board.GetCardUid(SlotId.Board(4)));

            boardSystem.RotateClockwise();

            Assert.AreEqual(first.Uid, board.GetCardUid(SlotId.Board(3)));
            Assert.AreEqual(second.Uid, board.GetCardUid(SlotId.Board(1)));
        }

        [Test]
        public void NoSkillNodeCanClearAfterKillAndAwardGold()
        {
            var architecture = NineGridArchitecture.Current;
            var board = architecture.GetModel<BoardModel>();
            var player = architecture.GetModel<PlayerModel>();
            var phaseSystem = architecture.GetSystem<IPhaseSystem>();
            var deckSystem = architecture.GetSystem<IDeckSystem>();
            var options = new NodeDeckOptions { PlayerOpeningCount = 0, EnemyOpeningCount = 1 }
                .AddEnemyCard(new CardDraft("monster.coin_slime", CardKind.Monster)
                {
                    MaxHp = 1,
                    Attack = 0,
                    GoldReward = 4
                });

            var startResult = architecture.SendCommand(new StartNodeCommand(options));
            Assert.IsTrue(startResult.Accepted);
            Assert.AreEqual(GamePhase.InteractionLoop, phaseSystem.CurrentPhase);

            var targetSlot = FindFirstMonsterSlot();
            Assert.AreNotEqual(SlotId.None, targetSlot);

            var attackResult = architecture.SendCommand(new AttackCommand(targetSlot));

            Assert.IsTrue(attackResult.Accepted);
            Assert.AreEqual(4, player.Coins.Value);
            Assert.AreEqual(1, player.InteractionCount.Value);
            Assert.AreEqual(0, board.GetCardUid(targetSlot));
            Assert.IsFalse(deckSystem.HasEnemyOnBoard());
            Assert.AreEqual(GamePhase.RewardItemChoice, phaseSystem.CurrentPhase);
        }

        [Test]
        public void IllegalCommandIsRejectedAndBroadcast()
        {
            var architecture = NineGridArchitecture.Current;
            var pipeline = architecture.GetSystem<IActionPipelineSystem>();
            Evt_ActionRejected rejected = null;
            architecture.RegisterEvent<Evt_ActionRejected>(evt => rejected = evt);

            var options = new NodeDeckOptions { PlayerOpeningCount = 0, EnemyOpeningCount = 1 }
                .AddEnemyCard(new CardDraft("monster.guard", CardKind.Monster)
                {
                    MaxHp = 5,
                    Attack = 0
                });
            architecture.SendCommand(new StartNodeCommand(options));

            var result = architecture.SendCommand(new AttackCommand(SlotId.Board(4)));

            Assert.IsFalse(result.Accepted);
            Assert.IsNotNull(rejected);
            Assert.AreEqual(GameCommandKind.Attack, rejected.Command);
            Assert.IsTrue(pipeline.EventLog.Contains(CoreEventType.ActionRejected));
        }

        private static SlotId FindFirstMonsterSlot()
        {
            var registry = NineGridArchitecture.Current.GetModel<CardRegistry>();
            var board = NineGridArchitecture.Current.GetModel<BoardModel>();
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var slot = SlotId.Board(i);
                var uid = board.GetCardUid(slot);
                if (uid == 0)
                {
                    continue;
                }

                if (registry.Get(uid).Kind == CardKind.Monster)
                {
                    return slot;
                }
            }

            return SlotId.None;
        }
    }
}
