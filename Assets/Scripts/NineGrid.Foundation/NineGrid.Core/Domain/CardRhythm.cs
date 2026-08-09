namespace NineGrid.Core
{
    /// <summary>
    /// 卡级节奏源（ADR-0038）：行动计数 ← 九宫格互动；移动计数 ← 本卡盘面→盘面换格。
    /// <see cref="Unspecified"/> 仅表示缺省；有节奏需求时加载期须报错。
    /// </summary>
    public enum CardRhythmSource
    {
        Unspecified = 0,
        Action = 1,
        Move = 2,
    }

    /// <summary>卡级节奏解析与裁决辅助（ADR-0038 / #184）。</summary>
    public static class CardRhythmRules
    {
        public const string TokenAction = "行动计数";
        public const string TokenMove = "移动计数";

        public static bool TryParse(string raw, out CardRhythmSource source)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                source = CardRhythmSource.Unspecified;
                return false;
            }

            switch (raw.Trim())
            {
                case TokenAction:
                case "行动":
                case "Action":
                    source = CardRhythmSource.Action;
                    return true;
                case TokenMove:
                case "移动":
                case "Move":
                    source = CardRhythmSource.Move;
                    return true;
                default:
                    source = CardRhythmSource.Unspecified;
                    return false;
            }
        }

        public static string ToToken(CardRhythmSource source)
        {
            switch (source)
            {
                case CardRhythmSource.Action:
                    return TokenAction;
                case CardRhythmSource.Move:
                    return TokenMove;
                default:
                    return string.Empty;
            }
        }

        /// <summary>是否绑定了有效节奏通道。</summary>
        public static bool HasBoundSource(CardRhythmSource source)
        {
            return source == CardRhythmSource.Action || source == CardRhythmSource.Move;
        }

        /// <summary>
        /// 卡是否有节奏需求：攻击模式参与开火，或挂有技能同步触发。
        /// </summary>
        public static bool NeedsRhythm(AttackPattern pattern, bool hasSyncRhythmSkills)
        {
            return AttackPatternRules.ParticipatesInEnemyAction(pattern) || hasSyncRhythmSkills;
        }

        public static bool NeedsRhythm(CardInstance card)
        {
            return card != null && NeedsRhythm(card.AttackPattern, card.HasSyncRhythmSkills);
        }

        /// <summary>共享倒计时是否应初始化 / 推进 / 上屏。</summary>
        public static bool HasActiveRhythm(CardInstance card)
        {
            return card != null
                && NeedsRhythm(card)
                && HasBoundSource(card.RhythmSource)
                && card.RhythmPeriod > 0;
        }

        public static bool ShouldTickOnInteract(CardInstance card)
        {
            return HasActiveRhythm(card) && card.RhythmSource == CardRhythmSource.Action;
        }

        public static bool ShouldTickOnBoardMove(CardInstance card)
        {
            return HasActiveRhythm(card) && card.RhythmSource == CardRhythmSource.Move;
        }

        public static int GetPeriod(CardInstance card)
        {
            return card != null && card.RhythmPeriod > 0 ? card.RhythmPeriod : 0;
        }
    }
}
