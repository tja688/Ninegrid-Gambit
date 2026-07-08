using System;
using System.IO;
using Luban.SimpleJSON;

namespace NineGrid.Content
{
    public static class TableNineVisualCatalogFactory
    {
        public static ContentVisualCatalog CreateFromDirectory(string dataDirectory)
        {
            if (string.IsNullOrEmpty(dataDirectory))
            {
                throw new ArgumentException("Luban data directory is required.", "dataDirectory");
            }

            var tables = new cfg.Tables(file => LoadJson(dataDirectory, file));
            return CreateFromTables(tables);
        }

        public static ContentVisualCatalog CreateFromTables(cfg.Tables tables)
        {
            if (tables == null)
            {
                throw new ArgumentNullException("tables");
            }

            var catalog = new ContentVisualCatalog();
            foreach (var row in tables.TbContentVisual.DataList)
            {
                catalog.Add(new ContentVisualDefinition(
                    row.ContentId,
                    ParseKind(row.ContentKind),
                    row.Description));
            }

            return catalog;
        }

        public static cfg.Tables LoadTables(string dataDirectory)
        {
            return new cfg.Tables(file => LoadJson(dataDirectory, file));
        }

        private static JSONNode LoadJson(string dataDirectory, string file)
        {
            var path = Path.Combine(dataDirectory, file + ".json");
            return JSON.Parse(File.ReadAllText(path));
        }

        private static ContentVisualKind ParseKind(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return ContentVisualKind.Unknown;
            }

            try
            {
                return (ContentVisualKind)Enum.Parse(typeof(ContentVisualKind), value, true);
            }
            catch (ArgumentException)
            {
                return ContentVisualKind.Unknown;
            }
        }
    }
}
