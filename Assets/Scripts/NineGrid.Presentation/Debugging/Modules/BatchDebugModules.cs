using System;
using NineGrid.Presentation.Contracts;
using UnityEngine;

namespace NineGrid.Presentation.Debugging.Modules
{
    public sealed class BatchFixtureDebugModule : IPerformanceDebugModule
    {
        private readonly BatchFixtureEntry fixture;

        public BatchFixtureDebugModule(BatchFixtureEntry fixture)
        {
            this.fixture = fixture;
        }

        public string Id => fixture.Id;
        public string DisplayName => fixture.DisplayName;
        public PerformanceDebugCategory Category => PerformanceDebugCategory.Batch;
        public Type RequiredComponentType => null;
        public PerformanceDebugSchema Schema { get; } = new PerformanceDebugSchema()
            .Add(
                "contextPreset",
                "Context",
                PerformanceDebugParamKind.ContextPreset,
                PerformanceDebugContextPreset.BattlePair.ToString(),
                System.Enum.GetNames(typeof(PerformanceDebugContextPreset)));

        public PerformanceDebugPlayResult Play(PerformanceDebugContext context, PerformanceDebugPayload payload)
        {
            if (context?.Harness?.BatchRunner == null)
            {
                return PerformanceDebugPlayResult.Fail("Batch runner not ready.");
            }

            context.Harness.BatchRunner.Play(fixture);
            return PerformanceDebugPlayResult.Ok(0f);
        }

        public void Stop(PerformanceDebugContext context)
        {
            context?.Harness?.BatchRunner?.Stop();
        }

        public void Reset(PerformanceDebugContext context)
        {
            Stop(context);
        }

        public bool TryGetIsPlaying(PerformanceDebugContext context, out bool isPlaying)
        {
            isPlaying = context?.Harness?.BatchRunner?.IsPlaying ?? false;
            return context?.Harness?.BatchRunner != null;
        }

        public float TryGetExpectedDuration(PerformanceDebugContext context)
        {
            return 0f;
        }
    }
}
