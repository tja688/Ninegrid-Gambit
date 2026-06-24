using System;
using System.IO;
using Luban.SimpleJSON;

namespace NineGrid.Content
{
    public static class TableNineVisualAssetCatalogFactory
    {
        public static VisualAssetCatalog CreateFromDirectory(string dataDirectory)
        {
            return CreateFromTables(TableNineVisualCatalogFactory.LoadTables(dataDirectory));
        }

        public static VisualAssetCatalog CreateFromTables(cfg.Tables tables)
        {
            if (tables == null)
            {
                throw new ArgumentNullException(nameof(tables));
            }

            var catalog = new VisualAssetCatalog();
            foreach (var row in tables.TbVisualAsset.DataList)
            {
                catalog.Add(new VisualAssetDefinition(
                    row.VisualId,
                    ParseKind(row.Kind),
                    row.AssetKey,
                    row.FallbackId));
            }

            return catalog;
        }

        private static VisualAssetKind ParseKind(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return VisualAssetKind.Unknown;
            }

            switch (value.Trim().ToLowerInvariant())
            {
                case "sprite":
                    return VisualAssetKind.Sprite;
                case "frame_animation":
                    return VisualAssetKind.FrameAnimation;
                case "prefab":
                    return VisualAssetKind.Prefab;
                default:
                    return VisualAssetKind.Unknown;
            }
        }
    }

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
