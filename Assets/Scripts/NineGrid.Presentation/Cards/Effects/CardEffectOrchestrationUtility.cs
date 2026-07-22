namespace NineGrid.Cards
{
    /// <summary>
    /// 卡牌效果编排辅助：读取 SO schema，供未来战斗编排层在攻击→受击间插入等待。
    /// </summary>
    public static class CardEffectOrchestrationUtility
    {
        public static bool TryGetBasicAttackHitDelay(
            CardEffectInvokeContext invoke,
            CardEffectSO hitEffect,
            out float delaySeconds)
        {
            delaySeconds = 0f;
            if (!invoke.IsOrchestrated
                || invoke.PresentationKind != CardAttackPresentationKind.NormalAttack
                || hitEffect == null
                || !hitEffect.UsesBasicAttackComboDelay)
            {
                return false;
            }

            delaySeconds = hitEffect.OrchestrationDelay;
            return delaySeconds > 0f;
        }

        public static bool ShouldUseBasicAttackComboDelay(CardEffectSO effect)
        {
            return effect != null && effect.UsesBasicAttackComboDelay;
        }
    }
}
