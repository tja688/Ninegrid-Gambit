namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 冲动轻点脉冲缝：主线忙时每次点击发一次；默认 no-op，未来「多点即加速」可订阅。
    /// </summary>
    public interface IAccelerationSink
    {
        void Tap(InputIntent intent);
    }

    /// <summary>生产默认：忽略所有冲动轻点。</summary>
    public sealed class NoOpAccelerationSink : IAccelerationSink
    {
        public static readonly NoOpAccelerationSink Instance = new NoOpAccelerationSink();

        private NoOpAccelerationSink()
        {
        }

        public void Tap(InputIntent intent)
        {
        }
    }
}
