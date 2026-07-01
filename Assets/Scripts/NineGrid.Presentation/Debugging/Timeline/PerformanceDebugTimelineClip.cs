using System;

namespace NineGrid.Presentation.Debugging.Timeline
{
    public sealed class PerformanceDebugTimelineClip
    {
        public string ClipId { get; set; }
        public string ModuleId { get; set; }
        public float StartTime { get; set; }
        public int Track { get; set; }
        public PerformanceDebugPayload Payload { get; set; }

        public PerformanceDebugTimelineClip Clone()
        {
            return new PerformanceDebugTimelineClip
            {
                ClipId = Guid.NewGuid().ToString("N"),
                ModuleId = ModuleId,
                StartTime = StartTime,
                Track = Track,
                Payload = Payload?.Clone(),
            };
        }

        public static PerformanceDebugTimelineClip Create(string moduleId, float startTime, int track, PerformanceDebugPayload payload)
        {
            return new PerformanceDebugTimelineClip
            {
                ClipId = Guid.NewGuid().ToString("N"),
                ModuleId = moduleId,
                StartTime = startTime,
                Track = track,
                Payload = payload?.Clone(),
            };
        }
    }
}
