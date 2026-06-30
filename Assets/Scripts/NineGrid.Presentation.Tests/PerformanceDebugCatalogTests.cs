using System.Collections.Generic;
using NUnit.Framework;
using NineGrid.Presentation.Debugging;

namespace NineGrid.Presentation.Tests
{
    public sealed class PerformanceDebugCatalogTests
    {
        [Test]
        public void Discover_FindsAllModules_WithUniqueIdsAndNonEmptySchema()
        {
            PerformanceDebugCatalog catalog = PerformanceDebugCatalog.Discover();
            Assert.GreaterOrEqual(catalog.Modules.Count, 20, "Expected first-wave performance debug modules.");

            var ids = new HashSet<string>();
            for (var i = 0; i < catalog.Modules.Count; i++)
            {
                IPerformanceDebugModule module = catalog.Modules[i];
                Assert.IsFalse(string.IsNullOrWhiteSpace(module.Id));
                Assert.IsFalse(string.IsNullOrWhiteSpace(module.DisplayName));
                Assert.IsNotNull(module.Schema);
                Assert.IsNotNull(module.Schema.Fields);
                Assert.Greater(module.Schema.Fields.Count, 0, module.Id);

                PerformanceDebugPayload payload = module.Schema.CreateDefaultPayload();
                Assert.IsNotNull(payload);
                Assert.IsTrue(ids.Add(module.Id), $"Duplicate module id: {module.Id}");
            }
        }
    }
}
