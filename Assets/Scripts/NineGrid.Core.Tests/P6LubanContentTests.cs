using System;
using System.IO;
using NineGrid.Content;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    public sealed class P6LubanContentTests
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
        public void GeneratedLubanTablesCanFeedContentSystem()
        {
            var dataDirectory = Path.Combine(
                Environment.CurrentDirectory,
                TableNineLubanCatalogFactory.DefaultDataRelativePath);

            var catalog = TableNineLubanCatalogFactory.CreateFromDirectory(dataDirectory);
            Assert.AreEqual(3, catalog.Cards.Count);
            Assert.AreEqual(5, catalog.Effects.Count);
            Assert.AreEqual(1, catalog.Relics.Count);
            Assert.IsTrue(catalog.Rewards.Pools.ContainsKey("reward.help.basic"));
            Assert.IsTrue(catalog.Rewards.Rooms.ContainsKey(RoomKind.Fountain));

            var architecture = NineGridArchitecture.Current;
            architecture.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, catalog);

            var content = architecture.GetSystem<IContentSystem>();
            Assert.IsTrue(content.TryReloadFromConfig());

            var report = content.ValidateCatalog();
            Assert.IsTrue(report.IsValid, FirstIssue(report));
            Assert.AreEqual(4, report.ImplementedEffectIds.Count);
            Assert.AreEqual(1, report.PendingEffectIds.Count);

            var slime = content.CreateDraft("monster.slime");
            Assert.AreEqual(CardKind.Monster, slime.Kind);
            Assert.AreEqual(8, slime.MaxHp);
            Assert.AreEqual(2, slime.Attack);
            Assert.IsTrue(Contains(slime.EffectIds, "monster.slime.pending"));
        }

        private static string FirstIssue(ContentValidationReport report)
        {
            return report.Issues.Count == 0 ? string.Empty : report.Issues[0];
        }

        private static bool Contains<T>(System.Collections.Generic.IReadOnlyList<T> list, T value)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (Equals(list[i], value))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
