using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NineGrid.Presentation.Debugging.Timeline
{
    public sealed class PerformanceDebugTimelineRunner
    {
        private readonly PerformanceDebugCatalog catalog;
        private readonly PerformanceDebugHarness harness;
        private readonly PerformanceDebugLogBuffer log;
        private readonly MonoBehaviour coroutineHost;

        private Coroutine playCoroutine;
        private bool isPlaying;
        private float playheadTime;

        public float PlayheadTime => playheadTime;
        public bool IsPlaying => isPlaying;

        public PerformanceDebugTimelineRunner(
            PerformanceDebugCatalog catalog,
            PerformanceDebugHarness harness,
            MonoBehaviour coroutineHost)
        {
            this.catalog = catalog;
            this.harness = harness;
            this.log = harness.Log;
            this.coroutineHost = coroutineHost;
        }

        public void Play(PerformanceDebugTimelineArrangement arrangement)
        {
            if (arrangement == null || arrangement.Clips.Count == 0)
            {
                log.Warn("Timeline is empty.");
                return;
            }

            Stop();
            playCoroutine = coroutineHost.StartCoroutine(PlayRoutine(arrangement));
        }

        public void Stop()
        {
            if (playCoroutine != null && coroutineHost != null)
            {
                coroutineHost.StopCoroutine(playCoroutine);
                playCoroutine = null;
            }

            isPlaying = false;
            harness.StopAllModules();
        }

        private IEnumerator PlayRoutine(PerformanceDebugTimelineArrangement arrangement)
        {
            List<PerformanceDebugTimelineClip> sorted = arrangement.Clips
                .OrderBy(clip => clip.StartTime)
                .ThenBy(clip => clip.Track)
                .ToList();

            isPlaying = true;
            playheadTime = 0f;
            float previousTime = 0f;
            log.Info($"Timeline play: {sorted.Count} clips.");

            for (var i = 0; i < sorted.Count; i++)
            {
                PerformanceDebugTimelineClip clip = sorted[i];
                float wait = clip.StartTime - previousTime;
                if (wait > 0f)
                {
                    float waited = 0f;
                    while (waited < wait)
                    {
                        playheadTime = previousTime + waited;
                        yield return null;
                        waited += Time.unscaledDeltaTime;
                    }
                }

                playheadTime = clip.StartTime;
                previousTime = clip.StartTime;

                IPerformanceDebugModule module = catalog.FindById(clip.ModuleId);
                if (module == null)
                {
                    log.Warn($"Timeline clip module missing: {clip.ModuleId}");
                    continue;
                }

                PerformanceDebugPayload payload = clip.Payload?.Clone() ?? module.Schema.CreateDefaultPayload();
                ApplyContextFromPayload(payload);
                PerformanceDebugContext context = harness.CreateContext();
                PerformanceDebugPlayResult result = module.Play(context, payload);
                if (!result.Success)
                {
                    log.Error($"{module.DisplayName}: {result.Error}");
                }
                else
                {
                    log.Info($"Timeline @{clip.StartTime:0.0}s → {module.DisplayName}");
                }
            }

            playheadTime = arrangement.GetDuration();
            isPlaying = false;
            playCoroutine = null;
            log.Info("Timeline finished.");
        }

        private void ApplyContextFromPayload(PerformanceDebugPayload payload)
        {
            if (payload == null)
            {
                return;
            }

            PerformanceDebugContextPreset preset = payload.GetContextPreset(PerformanceDebugPayloadKeys.ContextPreset);
            if (preset == PerformanceDebugContextPreset.None)
            {
                preset = PerformanceDebugContextPreset.BattlePair;
            }

            harness.ApplyContextPreset(preset, payload);
        }
    }
}
