using NineGrid.Core.Content;

namespace NineGrid.Content
{
    /// <summary>
    /// 生产内容装配：表 JSON（效果/奖励/经济/节点规则）+ schema≥2 一卡一文件投影。
    /// 不再走 Luban / Hardcoded 全量镜像 / BusinessOverlay（ADR-0008 / #69）。
    /// </summary>
    public static class ContentCatalogBootstrap
    {
        public static GameContentCatalog Load()
        {
            var catalog = new GameContentCatalog();
            ContentCatalogTableLoader.ApplyToCatalog(catalog);
            ContentJsonCatalogProjector.ApplyToCatalog(catalog);
            return catalog;
        }
    }
}
