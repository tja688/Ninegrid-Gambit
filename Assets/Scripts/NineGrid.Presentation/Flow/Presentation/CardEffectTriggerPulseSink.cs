using System.Globalization;
using NineGrid.Cards;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// FX 脉冲 sink：triggerId = fx.card.{uid} → 场上卡 PlayEffectTriggerPulse。
    /// 查找失败则静默（可降级）。
    /// </summary>
    public sealed class CardEffectTriggerPulseSink : ITriggerPulseSink
    {
        public const string IdPrefix = "fx.card.";

        public void Pulse(string triggerId)
        {
            if (string.IsNullOrEmpty(triggerId) || !triggerId.StartsWith(IdPrefix))
            {
                return;
            }

            int uid;
            if (!int.TryParse(
                    triggerId.Substring(IdPrefix.Length),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out uid)
                || uid <= 0)
            {
                return;
            }

            var cardManager = CardManagerSingleton.Instance;
            if (cardManager == null
                || !cardManager.TryGet(uid, out var card)
                || card == null
                || card.IsFieldDead
                || card.DisplayMode == CardDisplayMode.RemovedMode
                || card.DisplayMode == CardDisplayMode.CardDeckMode
                || card.DisplayMode == CardDisplayMode.HandCardMode)
            {
                return;
            }

            if (card.TryGetEffectManager(out var effectManager))
            {
                effectManager.PlayEffectTriggerPulse();
            }
        }

        public static string IdForCard(int uid)
        {
            return IdPrefix + uid.ToString(CultureInfo.InvariantCulture);
        }
    }
}
