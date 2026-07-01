using System.Collections;
using NineGrid.Core;
using NineGrid.Presentation.Orchestration;
using UnityEngine;

namespace NineGrid.Presentation.Debugging
{
    public sealed class PerformanceDebugBatchRunner
    {
        private readonly PerformanceDebugHarness harness;
        private readonly PerformanceDebugLogBuffer log;
        private readonly MonoBehaviour coroutineHost;

        private Coroutine playCoroutine;
        private BatchFixtureEntry currentFixture;
        private string lastPlanDump = string.Empty;
        private string lastError;

        public BatchFixtureEntry CurrentFixture => currentFixture;
        public string LastPlanDump => lastPlanDump;
        public string LastError => lastError;
        public bool IsPlaying => playCoroutine != null;

        public PerformanceDebugBatchRunner(
            PerformanceDebugHarness harness,
            MonoBehaviour coroutineHost)
        {
            this.harness = harness;
            this.log = harness.Log;
            this.coroutineHost = coroutineHost;
        }

        public void Play(BatchFixtureEntry fixture)
        {
            if (fixture == null || harness?.BatchPlayer == null)
            {
                lastError = "Batch player not ready.";
                log.Error(lastError);
                return;
            }

            Stop();
            currentFixture = fixture;
            lastError = null;

            harness.ApplyContextPreset(fixture.Preset);
            PresentationBatch batch = fixture.CreateBatch();
            PresentationPlan plan = harness.BatchPlayer.BuildPlan(batch);
            lastPlanDump = harness.BatchPlayer.DumpPlan(plan);

            log.Info($"Batch fixture: {fixture.DisplayName}");
            log.Info(lastPlanDump);
            playCoroutine = coroutineHost.StartCoroutine(PlayRoutine(batch, plan));
        }

        public void Stop()
        {
            if (playCoroutine != null && coroutineHost != null)
            {
                coroutineHost.StopCoroutine(playCoroutine);
                playCoroutine = null;
            }

            harness?.StopAllModules();
        }

        private IEnumerator PlayRoutine(PresentationBatch batch, PresentationPlan plan)
        {
            float startedAt = Time.unscaledTime;
            yield return harness.BatchPlayer.PlayCoroutine(batch);
            playCoroutine = null;
            float elapsed = Time.unscaledTime - startedAt;
            log.Info($"Batch finished (~{elapsed:0.00}s) Batch#{plan.BatchId}");
        }
    }
}
