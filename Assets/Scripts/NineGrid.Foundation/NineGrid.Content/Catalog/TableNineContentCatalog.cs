using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Effects;

namespace NineGrid.Content
{
    /// <summary>
    /// 小型 EditMode 测试夹具（ADR-0008 / #69）。生产全量内容走
    /// <see cref="ContentCatalogBootstrap.Load"/>（表 JSON + 一卡一文件投影）。
    /// </summary>
    public static class TableNineContentCatalog
    {
        public static GameContentCatalog CreateDefault()
        {
            var catalog = new GameContentCatalog();
            catalog.Economy.MonsterRemovedGold = 5;
            catalog.Economy.UnusedHelpCardGold = 10;
            catalog.Economy.SkipHelpChoiceGold = 10;
            catalog.Economy.SkipRelicChoiceGold = 20;
            catalog.Economy.DiscardRelicGold = 20;
            catalog.Economy.ShopDeleteHelpCardGold = 10;

            catalog.AddEffect(new ContentEffectDefinition(
                "fixture.heal.use",
                EffectContainerType.HelpCard,
                "{\"id\":\"fixture.heal.use\",\"typeTag\":\"【类型帮助卡】\",\"containerType\":\"HelpCard\",\"kind\":\"Triggered\","
                + "\"trigger\":{\"atom\":\"OnUseHelpCard\"},"
                + "\"target\":{\"atom\":\"Player\"},"
                + "\"action\":{\"atom\":\"Heal\",\"amount\":5,\"actor\":\"Player\"}}",
                ContentImplementationState.Implemented,
                "[夹具] 恢复5点血量"));

            catalog.AddCard(
                new CardContentDefinition("fixture.heal", "夹具治疗", CardKind.HelpCard)
                    .WithPrice(10)
                    .WithRarity(ContentRarity.White)
                    .AddEffect("fixture.heal.use"));

            catalog.Rewards.AddPool(new RewardPoolDefinition("fixture.pool", 1)
                .Add("fixture.heal", CardKind.HelpCard, 1, 1));

            catalog.Rewards.AddRoom(new RoomDefinition(RoomKind.Fountain, "夹具喷泉")
            {
                Weight = 1,
                HealToFull = true
            });

            catalog.Rewards.AddNodeRule(new NodeDeckRule
            {
                NodeIndex = 1,
                TotalMonsterCount = 1,
                Level1Min = 1,
                Level1Max = 1,
                DeckKind = MonsterDeckKind.WeakElite
            });

            return catalog;
        }
    }
}
