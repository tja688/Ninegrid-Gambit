using NUnit.Framework;
using NineGrid.Content;
using NineGrid.Content.CardPresentation;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NineGrid.Flow;
using NineGrid.Presentation.Tests.Fixtures;

namespace NineGrid.Presentation.Tests.Cards
{
    /// <summary>
    /// Disable Domain Reload 下：编辑 JSON 后若 <see cref="CoreCardPresentationMapper.EnsureContentCatalogLoaded"/>
    /// 因 HasCatalog 短路不重读盘，会出现「磁盘已清空挂载，战斗仍触发旧技能」。
    /// </summary>
    public sealed class ContentCatalogReloadRegressionTests
    {
        [TearDown]
        public void TearDown()
        {
            CardPresentationConfigCatalog.Invalidate();
            EffectTemplateCatalog.Invalidate();
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void EnsureContentCatalogLoaded_RebindsFromDisk_WhenStaleCatalogStillPresent()
        {
            using (PresentationArchitectureFixture.CreateBare())
            {
                var arch = NineGridArchitecture.Current;
                var content = arch.GetSystem<IContentSystem>();
                var config = arch.GetUtility<IConfigUtility>();

                var stale = new GameContentCatalog();
                stale.AddCard(
                    new CardContentDefinition("monster.stone_shrimp", "石虾-陈旧", CardKind.Monster)
                        .AddSkill("skill.stone_lover")
                        .AddEffect("skill.stone_lover.armor_lost"));
                content.Load(stale);
                config.Set(ContentConfigKeys.DefaultCatalog, stale);

                Assert.IsTrue(content.HasCatalog);
                Assert.AreEqual(1, content.Catalog.Cards["monster.stone_shrimp"].SkillIds.Count);

                CardPresentationConfigCatalog.Invalidate();
                CoreCardPresentationMapper.EnsureContentCatalogLoaded();

                Assert.IsTrue(
                    content.Catalog.TryGetCard("monster.stone_shrimp", out var card),
                    "生产 Bootstrap 应投影 monster.stone_shrimp");
                Assert.AreEqual(
                    0,
                    card.SkillIds.Count,
                    "磁盘石虾已无 skillIds 时，Ensure 不得保留陈旧 Catalog 的 skill.stone_lover");
                Assert.AreEqual(
                    0,
                    card.EffectIds.Count,
                    "磁盘石虾已无 effectAssemblies 时，Ensure 不得保留陈旧 EffectIds");
                Assert.AreEqual(
                    "deck.transition",
                    card.DeckId,
                    "重绑后卡应取磁盘归属（稳定 ID），而非陈旧内存卡");
            }
        }

        [Test]
        public void BootstrapLoad_InvalidatesPresentationCache_ThenReadsCurrentDiskMounts()
        {
            CardPresentationConfigCatalog.UpsertForTests(new CardPresentationConfigDto
            {
                schemaVersion = 2,
                contentId = "monster.stone_shrimp",
                kind = "Monster",
                displayName = "缓存里的假石虾",
                skillIds = new[] { "skill.stone_lover" },
            });

            var catalog = ContentCatalogBootstrap.Load();
            Assert.IsTrue(catalog.TryGetCard("monster.stone_shrimp", out var card));
            Assert.AreEqual("deck.transition", card.DeckId);
            Assert.AreEqual(0, card.SkillIds.Count);
            Assert.AreEqual(0, card.EffectIds.Count);
        }
    }
}
