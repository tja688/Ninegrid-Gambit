using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Presentation.Orchestration;
using NineGrid.Presentation.Tests.Support;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    public sealed class BoardDealResolverTests
    {
        private GameObject mRoot;
        private TableNineViewRegistry mRegistry;
        private TableNineActorFactory mActorFactory;
        private FlowPlaybackScope mScope;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            InitialGameFactory.Create(NineGridArchitecture.Current);

            mRoot = new GameObject("BoardDealResolverTests");
            mRegistry = new TableNineViewRegistry();

            for (var i = 1; i <= 9; i++)
            {
                if (i == 5)
                {
                    continue;
                }

                var slotGo = new GameObject($"slot{i}");
                slotGo.transform.SetParent(mRoot.transform);
                mRegistry.RegisterAnchor(SlotId.Board(i), slotGo.transform);
            }

            for (var i = 1; i <= 8; i++)
            {
                var deckSlotGo = new GameObject($"deckSlot{i}");
                deckSlotGo.transform.SetParent(mRoot.transform);
                mRegistry.RegisterNamedAnchor($"deck.slot{i}", deckSlotGo.transform);
            }

            var actorsRoot = new GameObject("actors");
            actorsRoot.transform.SetParent(mRoot.transform);
            mActorFactory = new TableNineActorFactory(
                actorsRoot.transform,
                null,
                mRegistry,
                NineGridArchitecture.Current);

            var cards = new Dictionary<int, CardView>
            {
                {
                    101,
                    new CardView(101, "monster.a", CardKind.Monster, ZoneId.Board, SlotId.Board(2), CardStatView.Empty, new string[0])
                },
                {
                    102,
                    new CardView(102, "monster.b", CardKind.Monster, ZoneId.Board, SlotId.Board(4), CardStatView.Empty, new string[0])
                },
            };

            var snapshot = new CoreViewSnapshot(
                1,
                new RunView(GamePhase.InteractionLoop, 0, 1, RoomKind.None, 0UL),
                new PlayerView(0, 3, new string[0], new string[0], CardStatView.Empty),
                new BoardView(new BoardSlotView[0], 1, SlotId.Avatar),
                new DeckView(new int[0], new int[0], new int[0], new int[0]),
                new ChoiceView(PendingChoiceKind.None, string.Empty, new RewardEntry[0], new RoomKind[0], RoomKind.None),
                cards);

            mScope = FlowPlaybackScope.Push(snapshot, mActorFactory);
            mScope.Activate();
        }

        [TearDown]
        public void TearDown()
        {
            mScope?.Dispose();
            if (mRoot != null)
            {
                Object.DestroyImmediate(mRoot);
            }
        }

        [Test]
        public void TryBuildBoardDeal_ResolvesBoardAnchorsAndSpawnsCards()
        {
            var deals = new List<FlowPayload>
            {
                new FlowPayload { CardUid = 101, ToSlot = SlotId.Board(2) },
                new FlowPayload { CardUid = 102, ToSlot = SlotId.Board(4) },
            };

            bool resolved = DeckFlowActorResolver.TryBuildBoardDeal(
                mRegistry,
                deals,
                out List<Transform> cards,
                out List<Transform> slots);

            Assert.IsTrue(resolved);
            Assert.AreEqual(2, cards.Count);
            Assert.AreEqual(2, slots.Count);
            Assert.AreEqual(mRegistry.ResolveAnchor(SlotId.Board(2)), slots[0]);
            Assert.AreEqual(mRegistry.ResolveAnchor(SlotId.Board(4)), slots[1]);
            Assert.AreEqual(mRegistry.ResolveNamedAnchor("deck.slot1").position, cards[0].position);
            Assert.AreEqual(mRegistry.ResolveNamedAnchor("deck.slot2").position, cards[1].position);
        }
    }
}
