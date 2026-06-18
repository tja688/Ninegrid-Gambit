using System.IO;
using NineGrid.Content;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    public sealed class P6R3LubanIntegrationTests
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
        public void BootstrapPrefersHardcodedCatalogForFullGameContent()
        {
            var hardcoded = ContentCatalogBootstrap.Load(ContentCatalogSourceKind.Hardcoded);
            P5CatalogTestSupport.RegisterCatalog(NineGridArchitecture.Current.GetUtility<IConfigUtility>(), hardcoded);
            NineGridArchitecture.Current.GetSystem<IContentSystem>().Load(hardcoded);

            var content = NineGridArchitecture.Current.GetSystem<IContentSystem>();
            Assert.GreaterOrEqual(hardcoded.Cards.Count, 70);
            Assert.GreaterOrEqual(hardcoded.Effects.Count, 35);
            Assert.IsTrue(content.HasCatalog);
        }

        [Test]
        public void BootstrapCanResolveLubanDataDirectoryWhenStreamingAssetsExist()
        {
            var dataDirectory = ContentCatalogBootstrap.ResolveLubanDataDirectory();
            if (string.IsNullOrEmpty(dataDirectory))
            {
                Assert.Ignore("Luban StreamingAssets are not available in this test runner context.");
            }

            Assert.IsTrue(File.Exists(Path.Combine(dataDirectory, "tablenine_tbeffect.json")));
            Assert.IsTrue(File.Exists(Path.Combine(dataDirectory, "tablenine_tbcard.json")));
        }

        [Test]
        public void LubanCatalogIsMinimalSampleAndFallsBackToHardcodedForProduction()
        {
            var lubanDirectory = ContentCatalogBootstrap.ResolveLubanDataDirectory();
            if (string.IsNullOrEmpty(lubanDirectory))
            {
                Assert.Ignore("Luban StreamingAssets are not available in this test runner context.");
            }

            GameContentCatalog lubanCatalog;
            Assert.IsTrue(ContentCatalogBootstrap.TryLoadLubanCatalog(lubanDirectory, out lubanCatalog));
            var hardcoded = TableNineContentCatalog.CreateDefault();

            Assert.Less(lubanCatalog.Cards.Count, hardcoded.Cards.Count);
            Assert.Less(lubanCatalog.Effects.Count, hardcoded.Effects.Count);
            Assert.AreNotEqual(hardcoded.Cards.Count, lubanCatalog.Cards.Count);
        }

        [Test]
        public void AutoSourceUsesLubanOnlyWhenExplicitlyAvailableAndCallerAcceptsSampleScope()
        {
            var lubanDirectory = ContentCatalogBootstrap.ResolveLubanDataDirectory();
            if (string.IsNullOrEmpty(lubanDirectory))
            {
                var fallback = ContentCatalogBootstrap.Load(ContentCatalogSourceKind.Auto);
                Assert.GreaterOrEqual(fallback.Cards.Count, 70);
                return;
            }

            var autoCatalog = ContentCatalogBootstrap.Load(ContentCatalogSourceKind.Auto);
            Assert.IsNotNull(autoCatalog);
            Assert.Greater(autoCatalog.Effects.Count, 0);
        }
    }
}
