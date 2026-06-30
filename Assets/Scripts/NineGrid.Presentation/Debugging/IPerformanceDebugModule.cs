using System;

namespace NineGrid.Presentation.Debugging
{
    public sealed class PerformanceDebugPlayResult
    {
        public bool Success { get; private set; }
        public string Error { get; private set; }
        public float ExpectedDuration { get; private set; }

        public static PerformanceDebugPlayResult Ok(float expectedDuration = 0f)
        {
            return new PerformanceDebugPlayResult
            {
                Success = true,
                ExpectedDuration = expectedDuration,
            };
        }

        public static PerformanceDebugPlayResult Fail(string error)
        {
            return new PerformanceDebugPlayResult
            {
                Success = false,
                Error = error ?? "Unknown error",
            };
        }
    }

    public interface IPerformanceDebugModule
    {
        string Id { get; }
        string DisplayName { get; }
        PerformanceDebugCategory Category { get; }
        Type RequiredComponentType { get; }
        PerformanceDebugSchema Schema { get; }
        PerformanceDebugPlayResult Play(PerformanceDebugContext context, PerformanceDebugPayload payload);
        void Stop(PerformanceDebugContext context);
        void Reset(PerformanceDebugContext context);
        bool TryGetIsPlaying(PerformanceDebugContext context, out bool isPlaying);
        float TryGetExpectedDuration(PerformanceDebugContext context);
    }
}
