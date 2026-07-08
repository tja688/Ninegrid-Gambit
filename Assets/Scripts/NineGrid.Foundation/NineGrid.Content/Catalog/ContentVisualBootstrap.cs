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

        public static bool TryLoad(
            string lubanDataDirectory,
            out ContentVisualCatalog visualCatalog,
            out CardFrameStyleCatalog frameStyleCatalog)
        {
            visualCatalog = null;
            frameStyleCatalog = null;
            if (string.IsNullOrEmpty(lubanDataDirectory) || !Directory.Exists(lubanDataDirectory))
            {
                return false;
            }

            var visualJson = Path.Combine(lubanDataDirectory, "tablenine_tbcontentvisual.json");
            var frameJson = Path.Combine(lubanDataDirectory, "tablenine_tbcardframestyle.json");
            if (!File.Exists(visualJson) || !File.Exists(frameJson))
            {
                return false;
            }

            try
            {
                var tables = TableNineVisualCatalogFactory.LoadTables(lubanDataDirectory);
                visualCatalog = TableNineVisualCatalogFactory.CreateFromTables(tables);
                frameStyleCatalog = TableNineCardFrameStyleCatalogFactory.CreateFromTables(tables);
                return visualCatalog != null && frameStyleCatalog != null;
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
            ContentVisualCatalog visualCatalog;
            CardFrameStyleCatalog frameStyleCatalog;
            if (TryLoad(lubanDataDirectory ?? ResolveLubanDataDirectory(), out visualCatalog, out frameStyleCatalog))
            {
                return visualCatalog;
            }

            throw new InvalidOperationException("Luban content visual data is unavailable.");
        }
    }
}
