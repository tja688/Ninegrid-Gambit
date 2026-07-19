namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 音效脉冲 sink 占位：真实 clip 映射可后续挂 MMSound。
    /// 经 <see cref="DebouncingTriggerPulseSink"/> 包装后接入 <see cref="TriggerPulseHub"/>；
    /// 脉冲与 debounce 语义由 hub/外层保证，本类不占时间线控制权。
    /// </summary>
    public sealed class AudioTriggerPulseSink : ITriggerPulseSink
    {
        public void Pulse(string triggerId)
        {
            // clip 播放点：triggerId（如 sfx.hit）。当前发即完成占位，不 await。
        }
    }
}
