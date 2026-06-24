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
            catalog = null;
            if (string.IsNullOrEmpty(lubanDataDirectory) || !Directory.Exists(lubanDataDirectory))
            {
                return false;
            }

            var visualJson = Path.Combine(lubanDataDirectory, "tablenine_tbcontentvisual.json");
            if (!File.Exists(visualJson))
            {
                return false;
            }

            try
            {
                catalog = TableNineVisualCatalogFactory.CreateFromDirectory(lubanDataDirectory);
                return catalog != null;
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
