using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Presentation.Controllers;
using NineGrid.Presentation.Queries;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests.Zone
{
    /// <summary>
    /// V3：区域归属经 Query；CardZoneOwnershipHook 接到 Query，生产不再用 Sink。
    /// </summary>
    public sealed class CardZoneQueryTests
    {
        [Test]
        public void IsCardInItemSlotsQuery_MatchesCoreZone()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGame(seed: 42UL))
            {
                Assert.IsTrue(arch.Phase.StartNode(CreateEmptyNode()).Accepted);
                var uid = SpawnHelp(arch, "help.throwing_knife", ZoneId.ItemSlots, SlotId.None);

                Assert.IsTrue(arch.Architecture.SendQuery(new IsCardInItemSlotsQuery(uid)));
                Assert.IsFalse(arch.Architecture.SendQuery(new IsCardInDrawPileQuery(uid)));
            }
        }

        [Test]
        public void IsCardInDrawPileQuery_MatchesCoreZone()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGame(seed: 42UL))
            {
                Assert.IsTrue(arch.Phase.StartNode(CreateEmptyNode()).Accepted);
                var uid = SpawnHelp(arch, "help.throwing_knife", ZoneId.DrawPile, SlotId.None);

                Assert.IsTrue(arch.Architecture.SendQuery(new IsCardInDrawPileQuery(uid)));
                Assert.IsFalse(arch.Architecture.SendQuery(new IsCardInItemSlotsQuery(uid)));
            }
        }

        [Test]
        public void ZoneOwnershipQueryController_WiresHookToQueries()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGame(seed: 42UL))
            {
                Assert.IsTrue(arch.Phase.StartNode(CreateEmptyNode()).Accepted);
                var uid = SpawnHelp(arch, "help.throwing_knife", ZoneId.ItemSlots, SlotId.None);

                CardZoneOwnershipHook.Reset();
                ZoneOwnershipQueryController.EnsureWired();

                Assert.IsNotNull(CardZoneOwnershipHook.IsCoreItemSlots);
                Assert.IsTrue(CardZoneOwnershipHook.CoreSaysItemSlots(uid));
                Assert.IsFalse(CardZoneOwnershipHook.CoreSaysDrawPile(uid));
            }
        }

        [Test]
        public void CardZoneOwnershipSink_TypeRemoved()
        {
            var cardsAsm = typeof(CombatHitBridgeHook).Assembly;
            Assert.IsNull(
                cardsAsm.GetType("NineGrid.Cards.CardZoneOwnershipSink"),
                "CardZoneOwnershipSink 应已删除，改由 Hook + Query");
        }

        private static int SpawnHelp(
            PresentationArchitectureFixture arch,
            string defId,
            ZoneId zone,
            SlotId slot)
        {
            arch.Pipeline.Enqueue(new SpawnCardAction(defId, CardKind.HelpCard, zone, slot, 1, "test"));
            Assert.Greater(arch.Pipeline.RunToCompletion(), 0);
            var uid = FindLatestUid(arch, zone);
            Assert.Greater(uid, 0);
            return uid;
        }

        private static int FindLatestUid(PresentationArchitectureFixture arch, ZoneId zone)
        {
            var deck = arch.Architecture.GetModel<DeckModel>();
            if (zone == ZoneId.ItemSlots)
            {
                return deck.ItemSlotUids.Count > 0
                    ? deck.ItemSlotUids[deck.ItemSlotUids.Count - 1]
                    : 0;
            }

            if (zone == ZoneId.DrawPile)
            {
                return deck.DrawPileUids.Count > 0
                    ? deck.DrawPileUids[deck.DrawPileUids.Count - 1]
                    : 0;
            }

            return 0;
        }

        private static NodeDeckOptions CreateEmptyNode()
        {
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 0
            };
        }
    }
}
