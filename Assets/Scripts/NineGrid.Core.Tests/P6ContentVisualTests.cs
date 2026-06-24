using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NineGrid.Content;
using NineGrid.Core.Content;
using NUnit.Framework;

namespace NineGrid.Core.Tests
{
    public sealed class P6ContentVisualTests
    {
        private static string DataDirectory
        {
            get
            {
                return Path.Combine(
                    Environment.CurrentDirectory,
                    TableNineLubanCatalogFactory.DefaultDataRelativePath);
            }
        }

        [Test]
        public void GeneratedLubanVisualTableLoadsFromStreamingAssets()
        {
            var catalog = TableNineVisualCatalogFactory.CreateFromDirectory(DataDirectory);
            Assert.AreEqual(186, catalog.Entries.Count);
            Assert.IsTrue(catalog.TryGet("help.bomb", out var bomb));
            Assert.AreEqual(ContentVisualKind.HelpCard, bomb.Kind);
            Assert.IsFalse(string.IsNullOrEmpty(bomb.Description));
        }

        [Test]
        public void ProductionContentIdsAreCoveredByVisualTable()
        {
            var core = TableNineLubanCatalogFactory.CreateFromDirectory(DataDirectory);
            var visual = TableNineVisualCatalogFactory.CreateFromDirectory(DataDirectory);
            var missing = new List<string>();

            foreach (var id in core.Cards.Keys.OrderBy(value => value))
            {
                if (!visual.TryGet(id, out _))
                {
                    missing.Add("card:" + id);
                }
            }

            foreach (var id in core.Relics.Keys.OrderBy(value => value))
            {
                if (!visual.TryGet(id, out _))
                {
                    missing.Add("relic:" + id);
                }
            }

            foreach (var id in core.Skills.Keys.OrderBy(value => value))
            {
                if (!visual.TryGet(id, out _))
                {
                    missing.Add("skill:" + id);
                }
            }

            foreach (var kind in core.Rewards.Rooms.Keys.OrderBy(value => value.ToString()))
            {
                if (!visual.TryGet(kind.ToString(), out _))
                {
                    missing.Add("room:" + kind);
                }
            }

            foreach (var id in core.MonsterDecks.Keys.OrderBy(value => value))
            {
                if (!visual.TryGet(id, out _))
                {
                    missing.Add("deck:" + id);
                }
            }

            if (!visual.TryGet("avatar.default", out _))
            {
                missing.Add("avatar.default");
            }

            Assert.IsEmpty(missing, string.Join(", ", missing));
        }

        [Test]
        public void ResolverUsesCoreDisplayNameAndVisualDescription()
        {
            var core = TableNineLubanCatalogFactory.CreateFromDirectory(DataDirectory);
            var visual = TableNineVisualCatalogFactory.CreateFromDirectory(DataDirectory);

            ContentVisualResolvedView view;
            Assert.IsTrue(ContentVisualResolver.TryResolve("help.bomb", core, visual, out view));
            Assert.AreEqual(core.Cards["help.bomb"].DisplayName, view.DisplayName);
            Assert.IsFalse(string.IsNullOrEmpty(view.Description));
            Assert.AreEqual(ContentVisualKind.HelpCard, view.Kind);
        }

        [Test]
        public void ResolverFallsBackToIconConventionWhenIconKeyEmpty()
        {
            var core = TableNineLubanCatalogFactory.CreateFromDirectory(DataDirectory);
            var visual = TableNineVisualCatalogFactory.CreateFromDirectory(DataDirectory);

            ContentVisualResolvedView view;
            Assert.IsTrue(ContentVisualResolver.TryResolve("relic.craving", core, visual, out view));
            Assert.AreEqual(
                ContentVisualResolver.BuildIconConventionPath(ContentVisualKind.Relic, "relic.craving"),
                view.IconKey);
            Assert.AreEqual(view.IconKey, view.IconResourcePath);
        }

        [Test]
        public void ResolverFallsBackMonsterFaceToDeckId()
        {
            var core = TableNineLubanCatalogFactory.CreateFromDirectory(DataDirectory);
            var visual = TableNineVisualCatalogFactory.CreateFromDirectory(DataDirectory);

            ContentVisualResolvedView view;
            Assert.IsTrue(ContentVisualResolver.TryResolve("monster.beggar", core, visual, out view));
            Assert.AreEqual(core.Cards["monster.beggar"].DeckId, view.FaceKey);
        }

        [Test]
        public void ResolverDerivesFrameFromRarityAndEliteFlags()
        {
            var core = TableNineLubanCatalogFactory.CreateFromDirectory(DataDirectory);
            var visual = TableNineVisualCatalogFactory.CreateFromDirectory(DataDirectory);

            ContentVisualResolvedView helpView;
            Assert.IsTrue(ContentVisualResolver.TryResolve("help.bomb", core, visual, out helpView));
            Assert.AreEqual("white_frame", helpView.FrameKey);

            ContentVisualResolvedView relicView;
            Assert.IsTrue(ContentVisualResolver.TryResolve("relic.craving", core, visual, out relicView));
            Assert.AreEqual("gold_frame", relicView.FrameKey);

            ContentVisualResolvedView bossView;
            Assert.IsTrue(ContentVisualResolver.TryResolve("monster.fire_dragon", core, visual, out bossView));
            Assert.AreEqual("boss_frame", bossView.FrameKey);
        }

        [Test]
        public void ContentVisualBootstrapLoadsFromSameDirectoryAsCoreCatalog()
        {
            var directory = ContentVisualBootstrap.ResolveLubanDataDirectory();
            ContentVisualCatalog catalog;
            Assert.IsTrue(ContentVisualBootstrap.TryLoad(directory, out catalog));
            Assert.Greater(catalog.Entries.Count, 0);
        }
    }
}
