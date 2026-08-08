using System;

namespace NineGrid.Cards.Presentation
{
    /// <summary>
    /// 旧详情合成（人手概括 + [code] 子弹）已退役；右键效果区改词条行（ADR-0037）。
    /// 保留空实现以免 Mapper 调用方断裂。
    /// </summary>
    public static class CardDetailDescriptionComposer
    {
        public static string Compose(string summary, CardFaceDescriptionIconCatalogSO glossary)
        {
            // ADR-0037：不再把词条子弹拼进 snapshot.DetailDescription。
            return summary ?? string.Empty;
        }
    }
}
