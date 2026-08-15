using NineGrid.Content.CardPresentation;
using NineGrid.Core;
using QFramework;

namespace NineGrid.Cards.Presentation
{
    /// <summary>
    /// 道具卡印刷数值直读 <see cref="PlayerModel.ItemStatBonus"/>（卡店强化 +3 等）。
    /// 与结算 <c>HelpCardStatBonusUtility</c> 同口径：仅装配实参 <c>amount</c> 叠加，派生 <c>value</c> 表达式不加。
    /// </summary>
    internal static class HelpCardMagnitudeOverlay
    {
        public static int ResolveLiveBonus()
        {
            var arch = NineGridArchitecture.Interface;
            if (arch == null)
            {
                return 0;
            }

            var player = arch.GetModel<PlayerModel>();
            return player == null ? 0 : player.ItemStatBonus;
        }

        public static string ProjectDescription(
            string checkDescription,
            EffectAssemblyDto[] assemblies,
            int itemStatBonus)
        {
            if (string.IsNullOrEmpty(checkDescription))
            {
                return checkDescription ?? string.Empty;
            }

            return CardFaceDescriptionParamFiller.FillFromAssemblies(
                checkDescription,
                assemblies,
                remainingOverrides: null,
                amountBonus: itemStatBonus);
        }

        public static string ProjectHelpCardDescription(
            string checkDescription,
            EffectAssemblyDto[] assemblies)
        {
            return ProjectDescription(checkDescription, assemblies, ResolveLiveBonus());
        }
    }
}
