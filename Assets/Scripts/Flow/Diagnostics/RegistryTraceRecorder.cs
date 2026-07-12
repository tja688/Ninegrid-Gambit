using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Systems;
using QFramework;
using UnityEngine;

namespace NineGrid.Flow.Diagnostics
{
    /// <summary>
    /// CardManager 注册表专项旁路记录器。自然落盘至 Logs/OtherLog/RegistryLog，无需小键盘。
    /// </summary>
    public static class RegistryTraceRecorder
    {
        private static readonly float[] IdleWatchDelaysSeconds = { 2f, 5f, 10f };

        private static RegistryTraceSession sSession;
        private static float sSessionStartRealtime;
        private static int sIdleWatchGeneration;
        private static bool sEnabled =
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            true;
#else
            false;
#endif

        public static bool Enabled
        {
            get => sEnabled;
            set => sEnabled = value;
        }

        public static RegistryTraceSession CurrentSession => sSession;

        public static bool HasEvents =>
            sSession != null && sSession.events != null && sSession.events.Count > 0;

        public static void Clear()
        {
            try
            {
                CancelIdleWatch();
                sSession = null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[RegistryTrace] Clear failed: " + ex.Message);
            }
        }

        public static void BeginSessionIfNeeded(ulong seed = 0UL)
        {
            if (!sEnabled)
            {
                return;
            }

            try
            {
                DiagTraceShared.EnsureSessionIdentity(seed);
                if (sSession != null)
                {
                    sSession.sessionId = DiagTraceShared.CurrentSessionId;
                    sSession.seed = DiagTraceShared.CurrentSeed;
                    return;
                }

                sSessionStartRealtime = Time.realtimeSinceStartup;
                sSession = new RegistryTraceSession
                {
                    schemaVersion = 1,
                    seed = DiagTraceShared.CurrentSeed,
                    sessionId = DiagTraceShared.CurrentSessionId,
                    events = new List<RegistryTraceEvent>(),
                };
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[RegistryTrace] BeginSession failed: " + ex.Message);
            }
        }

        /// <summary>
        /// 从 PerfTrace 镜像注册表相关事件（Release 唯一出口、Audit、Miss 等）。
        /// </summary>
        public static void MirrorFromPerf(PerfTraceEvent ev)
        {
            if (!sEnabled || ev == null)
            {
                return;
            }

            try
            {
                if (!ShouldMirrorPerfKind(ev.kind, ev.payload))
                {
                    return;
                }

                Record(
                    ev.kind,
                    ev.uid,
                    ev.site,
                    ev.payload != null
                        ? new Dictionary<string, string>(ev.payload)
                        : null);
            }
            catch
            {
                // swallow
            }
        }

        /// <summary>
        /// Audit 完成后的生命周期检查点：全量 BoardSnap + Checkpoint（等同原 Keypad7 自动版）。
        /// </summary>
        public static void OnAuditCompleted(
            string trigger,
            int registryCount,
            int fieldCount,
            string ghosts,
            string orphans)
        {
            if (!sEnabled)
            {
                return;
            }

            try
            {
                var payload = new Dictionary<string, string>
                {
                    { "trigger", trigger ?? string.Empty },
                    { "registryCount", registryCount.ToString() },
                    { "fieldCount", fieldCount.ToString() },
                    { "ghosts", ghosts ?? string.Empty },
                    { "orphans", orphans ?? string.Empty },
                };

                Record(RegistryTraceKinds.RegistryAudit, -1, PerfTraceSites.CardRegistryAudit, payload);

                var isLifecycle = IsLifecycleTrigger(trigger);
                var hasMismatch = !string.IsNullOrEmpty(ghosts) || !string.IsNullOrEmpty(orphans);
                if (isLifecycle || hasMismatch)
                {
                    var checkpointTrigger = hasMismatch
                        ? RegistryTraceTriggers.AuditMismatchPrefix + (trigger ?? "unknown")
                        : trigger;
                    CaptureCheckpoint(checkpointTrigger, registryCount, fieldCount, ghosts, orphans);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[RegistryTrace] OnAuditCompleted failed: " + ex.Message);
            }
        }

        /// <summary>
        /// 发牌后就位且进入 InteractionLoop 后，按固定延迟自动打检查点（抓 idle 段缺卡）。
        /// </summary>
        public static void BeginIdleWatch(CancellationToken cancellationToken)
        {
            if (!sEnabled)
            {
                return;
            }

            CancelIdleWatch();
            RunIdleWatchAsync(++sIdleWatchGeneration, cancellationToken).Forget();
        }

        public static void CancelIdleWatch()
        {
            sIdleWatchGeneration++;
        }

        public static void CaptureCheckpoint(
            string trigger,
            int registryCount = -1,
            int fieldCount = -1,
            string ghosts = null,
            string orphans = null)
        {
            if (!sEnabled)
            {
                return;
            }

            try
            {
                if (registryCount < 0)
                {
                    var cardManager = CardManagerSingleton.TryGetInstance();
                    registryCount = cardManager != null ? cardManager.CardsByUid.Count : 0;
                }

                if (fieldCount < 0)
                {
                    fieldCount = CountFieldOccupants();
                }

                Record(
                    RegistryTraceKinds.Checkpoint,
                    -1,
                    "Registry.Checkpoint",
                    new Dictionary<string, string>
                    {
                        { "trigger", trigger ?? string.Empty },
                        { "registryCount", registryCount.ToString() },
                        { "fieldCount", fieldCount.ToString() },
                        { "ghosts", ghosts ?? string.Empty },
                        { "orphans", orphans ?? string.Empty },
                        { "phase", BattleTraceRecorder.CurrentPhaseName() },
                    });

                RecordFullBoardSnap("checkpoint:" + (trigger ?? string.Empty));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[RegistryTrace] CaptureCheckpoint failed: " + ex.Message);
            }
        }

        public static string ExportJson(bool silentIfEmpty = false)
        {
            try
            {
                if (sSession == null || sSession.events == null || sSession.events.Count == 0)
                {
                    if (!silentIfEmpty)
                    {
                        Debug.Log("[RegistryTrace] 无事件，跳过导出。");
                    }

                    return null;
                }

                sSession.sessionId = DiagTraceShared.CurrentSessionId;
                sSession.seed = DiagTraceShared.CurrentSeed;
                var json = RegistryTraceJson.Serialize(sSession);
                var fileName = DiagTraceShared.BuildFileName(
                    "registrylog",
                    sSession.sessionId,
                    sSession.seed);
                var path = DiagTraceShared.WriteUtf8File(ResolveExportDirectory(), fileName, json);
                if (!string.IsNullOrEmpty(path))
                {
                    Debug.Log("[RegistryTrace] 已导出注册表日志：" + path + " events=" + sSession.events.Count);
                }

                return path;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[RegistryTrace] ExportJson failed: " + ex.Message);
                return null;
            }
        }

        public static string ResolveExportDirectory()
        {
            return DiagTraceShared.ResolveNotesDir("Logs/OtherLog/RegistryLog");
        }

        private static async UniTaskVoid RunIdleWatchAsync(int generation, CancellationToken cancellationToken)
        {
            try
            {
                for (var i = 0; i < IdleWatchDelaysSeconds.Length; i++)
                {
                    var delay = IdleWatchDelaysSeconds[i];
                    await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: cancellationToken);
                    if (generation != sIdleWatchGeneration || cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    if (!IsInteractionLoopPhase())
                    {
                        return;
                    }

                    var trigger = RegistryTraceTriggers.IdleWatchPrefix + delay.ToString("0") + "s";
                    CardManagerSingleton.TryGetInstance()?.AuditRegistryIntegrity(trigger);
                }
            }
            catch (OperationCanceledException)
            {
                // expected on battle end / new node
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[RegistryTrace] IdleWatch failed: " + ex.Message);
            }
        }

        private static bool IsInteractionLoopPhase()
        {
            try
            {
                return NineGridArchitecture.Current.GetSystem<IPhaseSystem>().CurrentPhase
                    == GamePhase.InteractionLoop;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsLifecycleTrigger(string trigger)
        {
            if (string.IsNullOrEmpty(trigger))
            {
                return false;
            }

            return trigger == RegistryTraceTriggers.OpeningSettled
                || trigger == RegistryTraceTriggers.InteractionLoopIdle
                || trigger.StartsWith(RegistryTraceTriggers.IdleWatchPrefix, StringComparison.Ordinal)
                || trigger.StartsWith(RegistryTraceTriggers.UserMarkPrefix, StringComparison.Ordinal);
        }

        private static bool ShouldMirrorPerfKind(string kind, Dictionary<string, string> payload)
        {
            if (string.IsNullOrEmpty(kind))
            {
                return false;
            }

            if (kind == RegistryTraceKinds.RegistryDelta
                || kind == RegistryTraceKinds.RegistryMiss
                || kind == RegistryTraceKinds.Despawn
                || kind == RegistryTraceKinds.Vacate)
            {
                return true;
            }

            if (kind != RegistryTraceKinds.Anomaly || payload == null)
            {
                return false;
            }

            if (!payload.TryGetValue("code", out var code) || string.IsNullOrEmpty(code))
            {
                return false;
            }

            return code == PerfTraceAnomalyCodes.FieldOccupancyWithoutView
                || code == PerfTraceAnomalyCodes.ViewWithoutFieldOccupancy
                || code == PerfTraceAnomalyCodes.OrphanAtWrongAnchor;
        }

        private static void RecordFullBoardSnap(string phase)
        {
            var cards = DiagBoardSnapCapture.CaptureLiveBoard();
            Record(
                RegistryTraceKinds.BoardSnap,
                -1,
                PerfTraceSites.BoardSnapCapture,
                new Dictionary<string, string>
                {
                    { "phase", phase ?? string.Empty },
                    { "full", "1" },
                    { "cards", DiagBoardSnapCapture.BuildFullCardsString(cards) },
                });
        }

        private static void Record(
            string kind,
            int uid = -1,
            string site = null,
            Dictionary<string, string> payload = null)
        {
            if (!sEnabled)
            {
                return;
            }

            try
            {
                BeginSessionIfNeeded();
                if (sSession == null)
                {
                    return;
                }

                var ev = new RegistryTraceEvent
                {
                    index = sSession.events.Count,
                    beatId = DiagBeatClock.ResolveBeatIdForEvent(),
                    tMs = ElapsedMs(),
                    kind = kind ?? string.Empty,
                    uid = uid,
                    site = site ?? string.Empty,
                    payload = payload != null
                        ? new Dictionary<string, string>(payload)
                        : new Dictionary<string, string>(),
                };
                sSession.events.Add(ev);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[RegistryTrace] Record failed: " + ex.Message);
            }
        }

        private static int CountFieldOccupants()
        {
            try
            {
                var field = GroundFieldManagerSingleton.TryGetInstance();
                if (field == null)
                {
                    return 0;
                }

                var snap = field.GetSnapshot();
                var count = 0;
                for (var i = 0; i < snap.Slots.Length; i++)
                {
                    var occ = snap.Slots[i];
                    if (!occ.IsEmpty && !occ.IsAvatarReserved)
                    {
                        count++;
                    }
                }

                return count;
            }
            catch
            {
                return 0;
            }
        }

        private static int ElapsedMs()
        {
            return Mathf.Max(0, Mathf.RoundToInt((Time.realtimeSinceStartup - sSessionStartRealtime) * 1000f));
        }
    }
}
