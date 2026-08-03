using NineGrid.Core;
using NineGrid.Core.Systems;
using NineGrid.Presentation.Systems;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests.BattleSession
{
    /// <summary>
    /// #107 / #108 / ADR-0025：道具卡格跨 <see cref="IBattleSessionSystem.BootstrapRun"/> preserve 存活。
    /// Seam：BootstrapRun(preserveRunInventory: true) ↔ DeckModel.ItemSlotUids / ItemSlotsCapacity。
    /// </summary>
    public sealed class CarryPackPreserveBootstrapContractTests
    {
        [Test]
        public void BootstrapRun_Preserve_RestoresItemSlots()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGameWithCatalog(seed: 7UL))
            {
                SpawnHelpIntoItemSlots(arch.Architecture, "help.healing_potion");
                SpawnHelpIntoItemSlots(arch.Architecture, "help.food_card");
                Assert.AreEqual(2, CountItemSlots(arch.Architecture));

                var session = BattleSessionSystem.EnsureRegistered(arch.Architecture);
                session.BootstrapRun(
                    new InitialGameOptions { Seed = 7UL },
                    preserveRunInventory: true);

                var deck = arch.Architecture.GetModel<DeckModel>();
                var registry = arch.Architecture.GetModel<CardRegistry>();
                Assert.AreEqual(2, deck.ItemSlotUids.Count);
                Assert.AreEqual("help.healing_potion", registry.Get(deck.ItemSlotUids[0]).DefId);
                Assert.AreEqual("help.food_card", registry.Get(deck.ItemSlotUids[1]).DefId);
            }
        }

        [Test]
        public void BootstrapRun_WithoutPreserve_ClearsItemSlots()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGameWithCatalog(seed: 7UL))
            {
                SpawnHelpIntoItemSlots(arch.Architecture, "help.healing_potion");
                Assert.AreEqual(1, CountItemSlots(arch.Architecture));

                var session = BattleSessionSystem.EnsureRegistered(arch.Architecture);
                session.BootstrapRun(new InitialGameOptions { Seed = 7UL }, preserveRunInventory: false);

                Assert.AreEqual(0, arch.Architecture.GetModel<DeckModel>().ItemSlotUids.Count);
            }
        }

        [Test]
        public void BootstrapRun_Preserve_RestoresItemSlotsCapacity()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGameWithCatalog(seed: 7UL))
            {
                var player = arch.Architecture.GetModel<PlayerModel>();
                player.SetItemSlotsCapacity(4);

                var session = BattleSessionSystem.EnsureRegistered(arch.Architecture);
                session.BootstrapRun(
                    new InitialGameOptions { Seed = 7UL },
                    preserveRunInventory: true);

                player = arch.Architecture.GetModel<PlayerModel>();
                Assert.AreEqual(4, player.ItemSlotsCapacity);
            }
        }

        private static void SpawnHelpIntoItemSlots(IArchitecture architecture, string defId)
        {
            var content = architecture.GetSystem<IContentSystem>();
            var registry = architecture.GetModel<CardRegistry>();
            var deck = architecture.GetModel<DeckModel>();
            var card = content.CreateDraft(defId).Create(registry);
            deck.AddToItemSlots(card);
        }

        private static int CountItemSlots(IArchitecture architecture)
        {
            return architecture.GetModel<DeckModel>().ItemSlotUids.Count;
        }
    }
}
