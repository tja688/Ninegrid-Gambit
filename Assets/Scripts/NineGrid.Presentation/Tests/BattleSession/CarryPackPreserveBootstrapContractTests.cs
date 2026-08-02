using NineGrid.Core;
using NineGrid.Presentation.Systems;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests.BattleSession
{
    /// <summary>
    /// #96：携带卡包跨 <see cref="IBattleSessionSystem.BootstrapRun"/> preserve 存活。
    /// Seam：BootstrapRun(preserveRunInventory: true) ↔ PlayerModel.CarryPackDefIds。
    /// </summary>
    public sealed class CarryPackPreserveBootstrapContractTests
    {
        [Test]
        public void BootstrapRun_Preserve_RestoresCarryPack()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGameWithCatalog(seed: 7UL))
            {
                var player = arch.Architecture.GetModel<PlayerModel>();
                player.ReplaceCarryPack(new[]
                {
                    "help.healing_potion",
                    "help.food_card",
                    "help.hp_card"
                });
                Assert.AreEqual(3, player.CarryPackDefIds.Count);

                var session = BattleSessionSystem.EnsureRegistered(arch.Architecture);
                session.BootstrapRun(
                    new InitialGameOptions { Seed = 7UL },
                    preserveRunInventory: true);

                player = arch.Architecture.GetModel<PlayerModel>();
                Assert.AreEqual(3, player.CarryPackDefIds.Count);
                Assert.AreEqual("help.healing_potion", player.CarryPackDefIds[0]);
                Assert.AreEqual("help.food_card", player.CarryPackDefIds[1]);
                Assert.AreEqual("help.hp_card", player.CarryPackDefIds[2]);
            }
        }

        [Test]
        public void BootstrapRun_WithoutPreserve_ClearsCarryPack()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGameWithCatalog(seed: 7UL))
            {
                var player = arch.Architecture.GetModel<PlayerModel>();
                player.ReplaceCarryPack(new[] { "help.healing_potion" });

                var session = BattleSessionSystem.EnsureRegistered(arch.Architecture);
                session.BootstrapRun(new InitialGameOptions { Seed = 7UL }, preserveRunInventory: false);

                player = arch.Architecture.GetModel<PlayerModel>();
                Assert.AreEqual(0, player.CarryPackDefIds.Count);
            }
        }
    }
}
