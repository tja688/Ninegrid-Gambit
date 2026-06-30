using System;
using System.Collections;
using UnityEngine;

namespace NineGrid.Presentation.Debugging
{
    public sealed class PerformanceDebugSequenceRunner
    {
        private readonly PerformanceDebugCatalog catalog;
        private readonly PerformanceDebugHarness harness;
        private readonly PerformanceDebugLogBuffer log;
        private readonly MonoBehaviour coroutineHost;

        private IPerformanceDebugModule currentModule;
        private PerformanceDebugPayload currentPayload;
        private Coroutine playCoroutine;
        private float playStartedAt;
        private string lastError;

        public IPerformanceDebugModule CurrentModule => currentModule;
        public PerformanceDebugPayload CurrentPayload => currentPayload;
        public string LastError => lastError;
        public float LastPlayElapsed { get; private set; }

        public PerformanceDebugSequenceRunner(
            PerformanceDebugCatalog catalog,
            PerformanceDebugHarness harness,
            MonoBehaviour coroutineHost)
        {
            this.catalog = catalog;
            this.harness = harness;
            this.log = harness.Log;
            this.coroutineHost = coroutineHost;
        }

        public void Play(IPerformanceDebugModule module, PerformanceDebugPayload payload)
        {
            if (module == null)
            {
                return;
            }

            StopCurrent();
            currentModule = module;
            currentPayload = payload?.Clone() ?? module.Schema.CreateDefaultPayload();
            playStartedAt = Time.unscaledTime;
            lastError = null;

            PerformanceDebugContext context = harness.CreateContext();
            MaybeApplyContextPreset(context, currentPayload);
            PerformanceDebugPlayResult result = module.Play(context, currentPayload);
            if (!result.Success)
            {
                lastError = result.Error;
                log.Error($"{module.DisplayName}: {result.Error}");
                return;
            }

            log.Info($"Playing {module.DisplayName}");
            playCoroutine = coroutineHost.StartCoroutine(TrackPlayback(module, result.ExpectedDuration));
        }

        public void Replay()
        {
            if (currentModule == null)
            {
                return;
            }

            Play(currentModule, currentPayload);
        }

        public void StopCurrent()
        {
            if (playCoroutine != null && coroutineHost != null)
            {
                coroutineHost.StopCoroutine(playCoroutine);
                playCoroutine = null;
            }

            if (currentModule != null)
            {
                currentModule.Stop(harness.CreateContext());
            }
        }

        public void StopAll()
        {
            StopCurrent();
            harness.StopAllModules();
            log.Info("Stopped all modules.");
        }

        public void ResetScene()
        {
            StopAll();
            harness.ClearDebugActors();
            harness.ApplyContextPreset(PerformanceDebugContextPreset.BattlePair);
            log.Info("Scene actors reset.");
        }

        public void RebuildContext(PerformanceDebugContextPreset preset)
        {
            StopAll();
            harness.ApplyContextPreset(preset);
        }

        private IEnumerator TrackPlayback(IPerformanceDebugModule module, float expectedDuration)
        {
            float wait = Mathf.Max(0.1f, expectedDuration > 0f ? expectedDuration : 0.5f);
            yield return new WaitForSecondsRealtime(wait);
            LastPlayElapsed = Time.unscaledTime - playStartedAt;
            playCoroutine = null;
            log.Info($"{module.DisplayName} finished (~{LastPlayElapsed:0.00}s)");
        }

        private static void MaybeApplyContextPreset(PerformanceDebugContext context, PerformanceDebugPayload payload)
        {
            PerformanceDebugContextPreset preset = payload.GetContextPreset("contextPreset");
            if (preset != PerformanceDebugContextPreset.None)
            {
                context.Harness.ApplyContextPreset(preset);
            }
        }
    }
}
