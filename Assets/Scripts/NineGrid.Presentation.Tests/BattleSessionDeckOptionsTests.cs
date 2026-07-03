using NineGrid.Content;
using NineGrid.Core;
using NineGrid.Core.Systems;
using NineGrid.Presentation.Debugging.Slices;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests
{
    public sealed class BattleSessionDeckOptionsTests
    {
        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            InitialGameFactory.Create(NineGridArchitecture.Current);
            NineGridArchitecture.Current.GetSystem<IContentSystem>().Load(TableNineContentCatalog.CreateDefault());
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void BuildNodeDeckOptions_Node1_UsesCatalogMonsterDefIds()
        {
            var reward = NineGridArchitecture.Current.GetSystem<IRewardSystem>();
            var options = reward.BuildNodeDeckOptions(1, BattleSessionDriver.DefaultMonsterDeckId);

            Assert.Greater(options.EnemyCards.Count, 0);
            Assert.IsTrue(BattleSessionDeckOptions.UsesCatalogMonsterDefIds(options));
            Assert.AreEqual(8, options.PlayerCards.Count);
        }

        [Test]
        public void BuildMinimalClearDeck_KeepsPlayerCardsAndUsesCatalogMonsters()
        {
            var architecture = NineGridArchitecture.Current;
            var reward = architecture.GetSystem<IRewardSystem>();
            var source = reward.BuildNodeDeckOptions(1, BattleSessionDriver.DefaultMonsterDeckId);
            var minimal = BattleSessionDeckOptions.BuildMinimalClearDeck(architecture, source);

            Assert.AreEqual(8, minimal.PlayerCards.Count);
            Assert.AreEqual(3, minimal.EnemyCards.Count);
            Assert.IsTrue(BattleSessionDeckOptions.UsesCatalogMonsterDefIds(minimal));
        }
    }
}
