using NineGrid.Content.CardPresentation;
using NineGrid.Core.Content;

namespace NineGrid.Content
{
    /// <summary>
    /// 生产内容装配：表 JSON（效果模板/奖励查询规则/经济/节点规则）+ schema≥2 一卡一文件投影
    /// （装配引用解析进 Catalog.Effects；奖池查询展开）。ADR-0008 / ADR-0009 / #71。
    /// </summary>
    public static class ContentCatalogBootstrap
    {
        public static GameContentCatalog Load()
        {
            // Disable Domain Reload 下静态缓存会跨 Play 残留；每次 Load 必须重读盘。
            CardPresentationConfigCatalog.Invalidate();
            EffectTemplateCatalog.Invalidate();
            var catalog = new GameContentCatalog();
            ContentCatalogTableLoader.ApplyToCatalog(catalog);
            ContentJsonCatalogProjector.ApplyToCatalog(catalog);
            RewardPoolQueryExpander.ExpandAll(catalog);
            return catalog;
        }
    }
}
