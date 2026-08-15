using NineGrid.Core.Content;

namespace NineGrid.Core.Systems
{
    /// <summary>
    /// 「开宝箱选遗物」类卡的稳定识别规则（ADR-0027 addendum / #143）：
    /// `.use` 效果装配 OfferRewardChoice 且 poolId 以 relic. 开头。
    /// 权威门禁（<see cref="PhaseSystem.ExecuteUseItem"/>）与表现层合法性镜像
    /// （Flow 侧 drag-apply 校验）共用同一识别源，避免两处 JSON 嗅探规则漂移。
    /// </summary>
    public static class ChestUseRelicPoolRule
    {
        /// <summary>
        /// 按 defId 判定该卡使用后是否开出遗物三选一（普通/蓝/金宝箱卡均命中）。
        /// </summary>
        public static bool IsChestUseOfferingRelicPool(IContentSystem content, string defId)
        {
            if (content == null || string.IsNullOrEmpty(defId))
            {
                return false;
            }

            content.TryReloadFromConfig();
            if (!content.HasCatalog)
            {
                return false;
            }

            var catalog = content.Catalog;
            if (catalog == null || catalog.Cards == null || catalog.Effects == null)
            {
                return false;
            }

            CardContentDefinition cardDef;
            if (!catalog.Cards.TryGetValue(defId, out cardDef) || cardDef == null || cardDef.EffectIds == null)
            {
                return false;
            }

            for (var i = 0; i < cardDef.EffectIds.Count; i++)
            {
                var effectId = cardDef.EffectIds[i];
                if (string.IsNullOrEmpty(effectId)
                    || !effectId.EndsWith(".use", System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                ContentEffectDefinition effect;
                if (!catalog.Effects.TryGetValue(effectId, out effect)
                    || effect == null
                    || string.IsNullOrEmpty(effect.Json))
                {
                    continue;
                }

                var poolId = ExtractRelicPoolIdFromEffectJson(effect.Json);
                if (!string.IsNullOrEmpty(poolId) && PendingChoiceModel.IsRelicRewardPool(poolId))
                {
                    return true;
                }
            }

            return false;
        }

        private static string ExtractRelicPoolIdFromEffectJson(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            // 模板实参已 Substitute 进 effect.Json：action 含 OfferRewardChoice 且 poolId:"relic.*"。
            // 与键顺序无关，独立查找两段；只要本 JSON 同时含 OfferRewardChoice 原子与 poolId 即可。
            const string OfferAtom = "\"atom\":\"OfferRewardChoice\"";
            if (json.IndexOf(OfferAtom, System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                return null;
            }

            const string poolKey = "\"poolId\":\"";
            var poolIdx = json.IndexOf(poolKey, System.StringComparison.OrdinalIgnoreCase);
            if (poolIdx < 0)
            {
                return null;
            }

            var start = poolIdx + poolKey.Length;
            var end = json.IndexOf('"', start);
            if (end < 0)
            {
                return null;
            }

            return json.Substring(start, end - start);
        }
    }
}
