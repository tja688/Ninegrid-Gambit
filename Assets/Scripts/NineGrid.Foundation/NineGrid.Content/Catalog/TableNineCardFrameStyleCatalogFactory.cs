using System;
using System.IO;
using Luban.SimpleJSON;

namespace NineGrid.Content
{
    public static class TableNineCardFrameStyleCatalogFactory
    {
        public static CardFrameStyleCatalog CreateFromDirectory(string dataDirectory)
        {
            return CreateFromTables(TableNineVisualCatalogFactory.LoadTables(dataDirectory));
        }

        public static CardFrameStyleCatalog CreateFromTables(cfg.Tables tables)
        {
            if (tables == null)
            {
                throw new ArgumentNullException(nameof(tables));
            }

            var catalog = new CardFrameStyleCatalog();
            foreach (var row in tables.TbCardFrameStyle.DataList)
            {
                catalog.Add(new CardFrameStyleDefinition(
                    row.StyleId,
                    new ContentColor(row.ColorR, row.ColorG, row.ColorB, row.ColorA)));
            }

            return catalog;
        }
    }
}
