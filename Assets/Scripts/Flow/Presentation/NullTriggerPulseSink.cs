namespace NineGrid.Flow.Presentation
{
    /// <summary>关闭 FX/音效时的可降级空脉冲：发即丢弃，不占控制权。</summary>
    public sealed class NullTriggerPulseSink : ITriggerPulseSink
    {
        public static readonly NullTriggerPulseSink Instance = new NullTriggerPulseSink();

        private NullTriggerPulseSink()
        {
        }

        public void Pulse(string triggerId)
        {
        }
    }
}
