namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 玩家输入转成的可缓冲请求；不直接改状态。
    /// </summary>
    public readonly struct InputIntent : System.IEquatable<InputIntent>
    {
        public InputIntent(string kind, int targetId = 0)
        {
            Kind = kind ?? string.Empty;
            TargetId = targetId;
        }

        public string Kind { get; }
        public int TargetId { get; }

        public bool Equals(InputIntent other)
        {
            return TargetId == other.TargetId
                && string.Equals(Kind, other.Kind, System.StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is InputIntent other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Kind != null ? Kind.GetHashCode() : 0) * 397) ^ TargetId;
            }
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
