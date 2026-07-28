using NineGrid.Content;
using NineGrid.Content.CardPresentation;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// 阶段一门禁：默认 catalog 全量 DSL 可解析、引用完整、无 Pending。
    /// #67：生产 Bootstrap（Luban/Hardcoded + 帮助卡 JSON 投影）同样须绿。
    /// </summary>
    public sealed class ContentCatalogValidationTests
    {
        private IArchitecture mArch;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, TableNineContentCatalog.CreateDefault());
        }

        [TearDown]
        public void TearDown()
        {
            CardPresentationConfigCatalog.Invalidate();
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void DefaultCatalog_ValidateCatalog_IsValidWithNoPendingEffects()
        {
            var report = mArch.GetSystem<IContentSystem>().ValidateCatalog();

            Assert.IsTrue(report.IsValid, FormatIssues(report));
            Assert.AreEqual(0, report.PendingEffectIds.Count, "Pending effect ids: " + string.Join(", ", report.PendingEffectIds));
            Assert.Greater(report.ImplementedEffectIds.Count, 0, "Expected implemented effects in default catalog.");
        }

        [Test]
        public void BootstrapCatalog_ValidateCatalog_IsValidWithHelpCardsFromJson()
        {
            CardPresentationConfigCatalog.Invalidate();
            var catalog = ContentCatalogBootstrap.Load(ContentCatalogSourceKind.Hardcoded);
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, catalog);

            var report = mArch.GetSystem<IContentSystem>().ValidateCatalog();

            Assert.IsTrue(report.IsValid, FormatIssues(report));
            Assert.AreEqual(0, report.PendingEffectIds.Count, "Pending effect ids: " + string.Join(", ", report.PendingEffectIds));

            Assert.IsTrue(catalog.Cards.TryGetValue("help.healing_spring", out var spring));
            Assert.AreEqual(CardKind.HelpCard, spring.Kind);
            Assert.AreEqual(3, spring.EffectIds.Count, "healing_spring mounts must come from JSON projection");
            Assert.AreEqual("help.healing_spring.board_adjacent", spring.EffectIds[0]);
            Assert.AreEqual("help.healing_spring.item_battle", spring.EffectIds[1]);
            Assert.AreEqual("help.healing_spring.use", spring.EffectIds[2]);
            // JSON SSOT displayName（与 Luban「治疗泉」可不同）
            Assert.AreEqual("治疗圣光", spring.DisplayName);

            Assert.IsTrue(catalog.Cards.TryGetValue("help.doubling_tower", out var tower));
            Assert.AreEqual(2, tower.EffectIds.Count);
            Assert.AreEqual("help.doubling_tower.board_monster", tower.EffectIds[0]);
            Assert.AreEqual("help.doubling_tower.item_player", tower.EffectIds[1]);
        }

        [Test]
        public void BootstrapLubanCatalog_WhenAvailable_ValidateCatalog_IsValidWithHelpJsonProjection()
        {
            CardPresentationConfigCatalog.Invalidate();
            if (string.IsNullOrEmpty(ContentCatalogBootstrap.ResolveLubanDataDirectory()))
            {
                Assert.Ignore("Luban data directory unavailable in this environment.");
            }

            var catalog = ContentCatalogBootstrap.Load(ContentCatalogSourceKind.Luban);
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, catalog);

            var report = mArch.GetSystem<IContentSystem>().ValidateCatalog();
            Assert.IsTrue(report.IsValid, FormatIssues(report));
            Assert.AreEqual(0, report.PendingEffectIds.Count);

            Assert.IsTrue(catalog.Cards.TryGetValue("help.healing_spring", out var spring));
            Assert.AreEqual("治疗圣光", spring.DisplayName);
            Assert.AreEqual(3, spring.EffectIds.Count);
        }

        private static string FormatIssues(ContentValidationReport report)
        {
            if (report.Issues.Count == 0)
            {
                return string.Empty;
            }

            return string.Join("; ", report.Issues);
        }
    }
}
