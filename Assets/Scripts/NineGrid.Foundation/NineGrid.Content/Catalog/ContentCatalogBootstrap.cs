using NineGrid.Core.Content;

namespace NineGrid.Content
{
    /// <summary>
    /// 生产内容装配：表 JSON（效果模板/奖励/经济/节点规则）+ schema≥2 一卡一文件投影
    /// （装配引用解析进 Catalog.Effects）。ADR-0008 / ADR-0009 / #70。
    /// </summary>
    public static class ContentCatalogBootstrap
    {
        public static GameContentCatalog Load()
        {
            EffectTemplateCatalog.Invalidate();
            var catalog = new GameContentCatalog();
            ContentCatalogTableLoader.ApplyToCatalog(catalog);
            ContentJsonCatalogProjector.ApplyToCatalog(catalog);
            return catalog;
        }
    }
}
