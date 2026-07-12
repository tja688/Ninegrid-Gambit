using System;

namespace NineGrid.Cards
{
    /// <summary>
    /// Timeline / DOTween / Animation Event / UnityEvent 统一回调动作。
    /// Play 段数值与 <see cref="CardEffectKind"/> 一致；Stop 段从 100 起。
    /// </summary>
    public enum CardEffectCallbackAction
    {
        PlayAttack = 0,
        PlayHit = 1,
        PlayDeath = 2,
        PlayUse = 3,
        PlayHitFlash = 4,
        PlayEffectTrigger = 5,

        StopCurrent = 100,
        StopHitFlash = 101,
    }

    public static class CardEffectCallbackActionUtility
    {
        public static bool TryParse(int actionId, out CardEffectCallbackAction action)
        {
            if (Enum.IsDefined(typeof(CardEffectCallbackAction), actionId))
            {
                action = (CardEffectCallbackAction)actionId;
                return true;
            }

            action = default;
            return false;
        }

        public static bool TryParse(string actionName, out CardEffectCallbackAction action)
        {
            action = default;
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            var trimmed = actionName.Trim();
            if (Enum.TryParse(trimmed, ignoreCase: true, out CardEffectCallbackAction parsed)
                && Enum.IsDefined(typeof(CardEffectCallbackAction), parsed))
            {
                action = parsed;
                return true;
            }

            switch (trimmed.ToLowerInvariant())
            {
                case "attack":
                case "攻击":
                    action = CardEffectCallbackAction.PlayAttack;
                    return true;
                case "hit":
                case "受击":
                    action = CardEffectCallbackAction.PlayHit;
                    return true;
                case "death":
                case "死亡":
                    action = CardEffectCallbackAction.PlayDeath;
                    return true;
                case "use":
                case "使用":
                    action = CardEffectCallbackAction.PlayUse;
                    return true;
                case "flash":
                case "hitflash":
                case "闪白":
                    action = CardEffectCallbackAction.PlayHitFlash;
                    return true;
                case "effecttrigger":
                case "trigger":
                case "效果触发":
                case "基础卡牌效果触发":
                    action = CardEffectCallbackAction.PlayEffectTrigger;
                    return true;
                case "stop":
                case "stopcurrent":
                case "停止":
                    action = CardEffectCallbackAction.StopCurrent;
                    return true;
                case "stopflash":
                case "stophitflash":
                case "停止闪白":
                    action = CardEffectCallbackAction.StopHitFlash;
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsPlayAction(CardEffectCallbackAction action)
        {
            return (int)action >= (int)CardEffectCallbackAction.PlayAttack
                && (int)action <= (int)CardEffectCallbackAction.PlayEffectTrigger;
        }

        public static CardEffectKind ToPlayKind(CardEffectCallbackAction action)
        {
            return (CardEffectKind)(int)action;
        }
    }
}
