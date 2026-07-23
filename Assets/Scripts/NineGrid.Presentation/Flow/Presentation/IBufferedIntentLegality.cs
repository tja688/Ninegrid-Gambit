namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// Flush 前对缓冲意图复用提交时同一套 Core 合法性判定（#48 latest-wins）。
    /// </summary>
    public interface IBufferedIntentLegality
    {
        bool IsStillLegal(InputIntent intent);
    }
}