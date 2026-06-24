using System;
using System.IO;

namespace NineGrid.Content
{
    public static class ContentVisualBootstrap
    {
        public static string ResolveLubanDataDirectory()
        {
            return ContentCatalogBootstrap.ResolveLubanDataDirectory();
        }

        public static bool TryLoad(string lubanDataDirectory, out ContentVisualCatalog catalog)
        {
            VisualAssetCatalog assetCatalog;
            CardFrameStyleCatalog frameStyleCatalog;
            if (!TryLoadAll(lubanDataDirectory, out catalog, out assetCatalog, out frameStyleCatalog))
            {
                return false;
            }

            return catalog != null;
        }

        public static bool TryLoadAll(
            string lubanDataDirectory,
            out ContentVisualCatalog visualCatalog,
            out VisualAssetCatalog visualAssetCatalog,
            out CardFrameStyleCatalog frameStyleCatalog)
        {
            visualCatalog = null;
            visualAssetCatalog = null;
            frameStyleCatalog = null;
            if (string.IsNullOrEmpty(lubanDataDirectory) || !Directory.Exists(lubanDataDirectory))
            {
                return false;
            }

            var visualJson = Path.Combine(lubanDataDirectory, "tablenine_tbcontentvisual.json");
            var assetJson = Path.Combine(lubanDataDirectory, "tablenine_tbvisualasset.json");
            var frameJson = Path.Combine(lubanDataDirectory, "tablenine_tbcardframestyle.json");
            if (!File.Exists(visualJson) || !File.Exists(assetJson) || !File.Exists(frameJson))
            {
                return false;
            }

            try
            {
                var tables = TableNineVisualCatalogFactory.LoadTables(lubanDataDirectory);
                visualCatalog = TableNineVisualCatalogFactory.CreateFromTables(tables);
                visualAssetCatalog = TableNineVisualAssetCatalogFactory.CreateFromTables(tables);
                frameStyleCatalog = TableNineCardFrameStyleCatalogFactory.CreateFromTables(tables);
                return visualCatalog != null && visualAssetCatalog != null && frameStyleCatalog != null;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        public static ContentVisualCatalog Load(string lubanDataDirectory = null)
        {
            ContentVisualCatalog catalog;
            if (TryLoad(lubanDataDirectory ?? ResolveLubanDataDirectory(), out catalog))
            {
                return catalog;
            }

            throw new InvalidOperationException("Luban content visual data is unavailable.");
        }
    }
}
