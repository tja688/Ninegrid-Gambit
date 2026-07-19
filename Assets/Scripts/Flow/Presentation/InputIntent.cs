namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 玩家输入转成的可缓冲请求；不直接改状态。
    /// UseItem：TargetId = itemUid；SelectedCardUids / SelectedOption 承载选目标与选项。
    /// </summary>
    public readonly struct InputIntent : System.IEquatable<InputIntent>
    {
        public InputIntent(string kind, int targetId = 0)
            : this(kind, targetId, null, null)
        {
        }

        public InputIntent(string kind, int targetId, int[] selectedCardUids, string selectedOption)
        {
            Kind = kind ?? string.Empty;
            TargetId = targetId;
            SelectedCardUids = selectedCardUids;
            SelectedOption = selectedOption ?? string.Empty;
        }

        public string Kind { get; }
        public int TargetId { get; }
        public int[] SelectedCardUids { get; }
        public string SelectedOption { get; }

        public bool Equals(InputIntent other)
        {
            return TargetId == other.TargetId
                && string.Equals(Kind, other.Kind, System.StringComparison.Ordinal)
                && string.Equals(SelectedOption, other.SelectedOption, System.StringComparison.Ordinal)
                && SelectedUidsEqual(SelectedCardUids, other.SelectedCardUids);
        }

        public override bool Equals(object obj)
        {
            return obj is InputIntent other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = ((Kind != null ? Kind.GetHashCode() : 0) * 397) ^ TargetId;
                hash = (hash * 397) ^ (SelectedOption != null ? SelectedOption.GetHashCode() : 0);
                if (SelectedCardUids != null)
                {
                    for (var i = 0; i < SelectedCardUids.Length; i++)
                    {
                        hash = (hash * 397) ^ SelectedCardUids[i];
                    }
                }

                return hash;
            }
        }

        private static bool SelectedUidsEqual(int[] left, int[] right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null || left.Length != right.Length)
            {
                return left == null && right == null;
            }

            for (var i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                {
                    return false;
                }
            }

            return true;
        }
    }

    public enum IntentClearReason
    {
        PhaseChange = 0,
        Defeat = 1,
        LayerChange = 2,
    }

    /// <summary>
    /// 忙时缓冲意图时给出的 uiPick 预告观察点（EditMode 可断言）。
    /// </summary>
    public interface IUiPickPreviewSink
    {
        void Preview(InputIntent intent);
    }

    /// <summary>
    /// 将已接纳的输入意图编成主线剧本（Resolve→Present…）。阶段0用假工厂。
    /// </summary>
    public interface IIntentScriptFactory
    {
        void BuildScript(InputIntent intent, BattleTimeline timeline);
    }
}
