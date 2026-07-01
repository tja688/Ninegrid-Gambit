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
        private PerformanceDebugContextPreset lastContextPreset = PerformanceDebugContextPreset.BattlePair;

        public IPerformanceDebugModule CurrentModule => currentModule;
        public PerformanceDebugPayload CurrentPayload => currentPayload;
        public string LastError => lastError;
        public float LastPlayElapsed { get; private set; }
        public PerformanceDebugContextPreset LastContextPreset => lastContextPreset;

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

            PrepareActorsForPlay(currentPayload);
            PerformanceDebugContext context = harness.CreateContext();
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

        /// <summary>
        /// 清场：停止模块并移除所有测试演员，不自动重建预设。
        /// </summary>
        public void ClearStage()
        {
            StopCurrent();
            harness.ClearPerformanceStage();
            log.Info("Stage cleared.");
        }

        public void ResetScene()
        {
            StopCurrent();
            lastContextPreset = PerformanceDebugContextPreset.BattlePair;
            harness.ApplyContextPreset(lastContextPreset);
            log.Info("Scene actors reset to BattlePair.");
        }

        public void RebuildContext(PerformanceDebugContextPreset preset)
        {
            StopCurrent();
            if (preset != PerformanceDebugContextPreset.None)
            {
                lastContextPreset = preset;
            }

            harness.ApplyContextPreset(lastContextPreset);
            log.Info($"Context rebuilt: {lastContextPreset}");
        }

        private void PrepareActorsForPlay(PerformanceDebugPayload payload)
        {
            PerformanceDebugContextPreset preset = payload.GetContextPreset(PerformanceDebugPayloadKeys.ContextPreset);
            if (preset != PerformanceDebugContextPreset.None)
            {
                lastContextPreset = preset;
            }

            harness.ApplyContextPreset(lastContextPreset, payload);
        }

        private IEnumerator TrackPlayback(IPerformanceDebugModule module, float expectedDuration)
        {
            float wait = Mathf.Max(0.1f, expectedDuration > 0f ? expectedDuration : 0.5f);
            yield return new WaitForSecondsRealtime(wait);
            LastPlayElapsed = Time.unscaledTime - playStartedAt;
            playCoroutine = null;
            log.Info($"{module.DisplayName} finished (~{LastPlayElapsed:0.00}s)");
        }
    }
}
