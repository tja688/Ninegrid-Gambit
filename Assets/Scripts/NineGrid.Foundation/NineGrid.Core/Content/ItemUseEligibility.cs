using NineGrid.Core.Systems;
using QFramework;

namespace NineGrid.Core.Content
{
    /// <summary>
    /// ADR-0032：道具卡「非战斗可用」裁决。卡级声明 <see cref="CardContentDefinition.UsableOutsideBattle"/>
    /// 只管辖非战斗相位（RoomChoice / RewardItemChoice）；InteractionLoop 永放行，RoomEvent 永不放行。
    /// Core 门禁（PhaseSystem.UseItem）与表现层合法性（BoardIntentLegality / 拖放校验）共用本裁决。
    /// </summary>
    public static class ItemUseEligibility
    {
        public static bool IsUsableInPhase(GamePhase phase, CardContentDefinition def)
        {
            if (def == null)
            {
                return false;
            }

            if (phase == GamePhase.InteractionLoop)
            {
                return true;
            }

            if (phase != GamePhase.RoomChoice && phase != GamePhase.RewardItemChoice)
            {
                return false;
            }

            return def.UsableOutsideBattle;
        }

        /// <summary>按当前相位与卡 defId 裁决；InteractionLoop 恒放行且不依赖 Catalog，卡片不在 Catalog 时非战斗相位一律不成立。</summary>
        public static bool IsUsableInCurrentPhase(IArchitecture arch, string cardDefId)
        {
            if (arch == null || string.IsNullOrEmpty(cardDefId))
            {
                return false;
            }

            var phase = arch.GetSystem<IPhaseSystem>();
            if (phase == null)
            {
                return false;
            }

            if (phase.CurrentPhase == GamePhase.InteractionLoop)
            {
                return true;
            }

            if (phase.CurrentPhase != GamePhase.RoomChoice && phase.CurrentPhase != GamePhase.RewardItemChoice)
            {
                return false;
            }

            var content = arch.GetSystem<IContentSystem>();
            if (content == null || !content.HasCatalog || content.Catalog == null || content.Catalog.Cards == null)
            {
                return false;
            }

            CardContentDefinition def;
            if (!content.Catalog.Cards.TryGetValue(cardDefId, out def))
            {
                return false;
            }

            return def.UsableOutsideBattle;
        }
    }
}
