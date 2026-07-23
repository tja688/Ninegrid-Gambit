using System;
using System.Collections.Generic;
using System.Globalization;
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
        private static readonly float[] IdleWatchDelaysSeconds = { 1f, 2f, 3f, 5f, 7f, 10f, 15f };
        private static readonly float IdlePollIntervalSeconds = 0.5f;

        private static RegistryTraceSession sSession;
        private static float sSessionStartRealtime;
        private static int sIdleWatchGeneration;
        private static int sVisualBaseline = -1;
        private static int sLastReportedVisualGap = -1;
        private static int sUserInteractionCount;
        private static int sLastReleaseUid = -1;
        private static string sLastReleaseReason = string.Empty;
        private static string sLastReleaseCaller = string.Empty;
        private static int sLastReleaseTMs;
        private static string sLastVacateDetail = string.Empty;
        private static int sLastVacateTMs;
        private static bool sEnabled =
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            true;
#else
            false;
#endif

#if UNITY_EDITOR
        static RegistryTraceRecorder()
        {
            sEnabled = DiagTraceExportPreferences.GetTrackRecording(DiagTraceTrack.Registry);
        }
#endif

        public static bool Enabled
        {
            get => sEnabled;
            set
            {
#if UNITY_EDITOR
                DiagTraceExportPreferences.SetTrackRecording(DiagTraceTrack.Registry, value);
#else
                sEnabled = value;
#endif
            }
        }

        /// <summary>由偏好系统写入，避免与 <see cref="Enabled"/> setter 互相递归。</summary>
        internal static void SetEnabledFromPreferences(bool enabled)
        {
            sEnabled = enabled;
        }

        public static RegistryTraceSession CurrentSession => sSession;

        public static bool HasEvents =>
            sSession != null && sSession.events != null && sSession.events.Count > 0;

        public static void RegisterSinkHandlers()
        {
            RegistryTraceSink.NotifyUserInteraction = NotifyUserInteraction;
            RegistryTraceSink.RecordSuspectGroundRelease = RecordSuspectGroundRelease;
            RegistryTraceSink.RecordPickupEligibility = (
                uid,
                defId,
                coreKind,
                displayMode,
                worldX,
                worldY,
                registeredSlot,
                nearestSlot,
                slotDist,
                canRespond,
                fieldBusy,
                handCanAccept,
                isOrtho,
                isOrphan,
                isGhost) => RecordPickupEligibility(
                uid,
                defId,
                coreKind,
                displayMode,
                worldX,
                worldY,
                registeredSlot,
                nearestSlot,
                slotDist,
                canRespond,
                fieldBusy,
                handCanAccept,
                isOrtho,
                isOrphan,
                isGhost);
        }

        public static void UnregisterSinkHandlers()
        {
            RegistryTraceSink.ClearHandlers();
        }

        public static void Clear()
        {
            try
            {
                CancelIdleWatch();
                sSession = null;
                ResetVisualSessionState();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[RegistryTrace] Clear failed: " + ex.Message);
            }
        }

        public static void NotifyUserInteraction(string site)
        {
            if (!sEnabled)
            {
                return;
            }

            sUserInteractionCount++;
            Record(
                RegistryTraceKinds.Checkpoint,
                -1,
                "Registry.UserInteraction",
                new Dictionary<string, string>
                {
                    { "trigger", "UserInteraction." + (site ?? string.Empty) },
                    { "count", sUserInteractionCount.ToString() },
                    { "phase", BattleTraceRecorder.CurrentPhaseName() },
                });
        }

        public static void ResetVisualBaselineForOpening()
        {
            sVisualBaseline = -1;
            sLastReportedVisualGap = -1;
            sUserInteractionCount = 0;
        }

        /// <summary>BeatClose 时补采样，抓检查点之间的瞬时空槽。</summary>
        public static void OnPresentationBeatClosed(string beatKind)
        {
            if (!sEnabled || string.IsNullOrEmpty(beatKind))
            {
                return;
            }

            if (beatKind == DiagBeatKinds.BoardChoreo)
            {
                if (ChoreoTraceContext.OpenChoreoCount > 0 || PerfTraceRecorder.OpenMotionCount > 0)
                {
                    Record(
                        RegistryTraceKinds.Anomaly,
                        -1,
                        "Registry.ChoreoIncomplete",
                        new Dictionary<string, string>
                        {
                            { "code", RegistryTraceAnomalyCodes.ChoreoIncompleteAtBeatClose },
                            { "trigger", RegistryTraceTriggers.BeatClosePrefix + beatKind },
                            { "openChoreo", ChoreoTraceContext.GetOpenChoreoSummary() },
                            { "openMotionCount", PerfTraceRecorder.OpenMotionCount.ToString() },
                        });
                }
            }

            if (!IsInteractionLoopPhase())
            {
                return;
            }

            CaptureFieldVisualState(RegistryTraceTriggers.BeatClosePrefix + beatKind, includeCheckpoint: false);
        }

        /// <summary>Help/道具卡点击前拾取门禁审计。</summary>
        public static void RecordPickupEligibility(
            int uid,
            string defId,
            string coreKind,
            string displayMode,
            float worldX,
            float worldY,
            int registeredSlot,
            int nearestSlot,
            float slotDist,
            bool canRespond,
            bool fieldBusy,
            bool handCanAccept,
            bool isOrtho,
            bool isOrphan,
            bool isGhost)
        {
            if (!sEnabled)
            {
                return;
            }

            try
            {
                BeginSessionIfNeeded();
                ChoreoTraceContext.NotePickupEligibility(canRespond);
                Record(
                    RegistryTraceKinds.PickupEligibility,
                    uid,
                    "Pickup.Click",
                    new Dictionary<string, string>
                    {
                        { "trigger", "Pickup.Click" },
                        { "defId", defId ?? string.Empty },
                        { "coreKind", coreKind ?? string.Empty },
                        { "displayMode", displayMode ?? string.Empty },
                        { "worldX", CardPresentationProbe.FormatXy(worldX) },
                        { "worldY", CardPresentationProbe.FormatXy(worldY) },
                        { "registeredSlot", registeredSlot.ToString() },
                        { "nearestSlot", nearestSlot.ToString() },
                        { "slotDist", slotDist.ToString("0.###", CultureInfo.InvariantCulture) },
                        { "canRespond", canRespond ? "true" : "false" },
                        { "fieldBusy", fieldBusy ? "true" : "false" },
                        { "handCanAccept", handCanAccept ? "true" : "false" },
                        { "isOrtho", isOrtho ? "true" : "false" },
                        { "isOrphan", isOrphan ? "true" : "false" },
                        { "isGhost", isGhost ? "true" : "false" },
                        { "choreoSeqId", ChoreoTraceContext.CurrentSeqId.ToString() },
                        { "phase", BattleTraceRecorder.CurrentPhaseName() },
                    });
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[RegistryTrace] RecordPickupEligibility failed: " + ex.Message);
            }
        }

        public static void RecordSessionChoreoSummary(Dictionary<string, string> payload)
        {
            if (!sEnabled)
            {
                return;
            }

            Record(
                RegistryTraceKinds.SessionChoreoSummary,
                -1,
                "Registry.SessionChoreoSummary",
                payload != null
                    ? new Dictionary<string, string>(payload)
                    : new Dictionary<string, string>());
        }

        public static void RecordSuspectGroundRelease(
            int uid,
            string reason,
            string caller,
            int groundSlot)
        {
            if (!sEnabled)
            {
                return;
            }

            Record(
                RegistryTraceKinds.SuspectRelease,
                uid,
                "Card.SuspectGroundRelease",
                new Dictionary<string, string>
                {
                    { "reason", reason ?? string.Empty },
                    { "caller", caller ?? string.Empty },
                    { "groundSlot", groundSlot.ToString() },
                    { "userInteractionCount", sUserInteractionCount.ToString() },
                    { "beatKind", DiagBeatClock.CurrentBeatKind ?? string.Empty },
                });
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
                    DiagTraceShared.StampSessionRunMetadata(out sSession.runTag, out sSession.runTagNote);
                    return;
                }

                sSessionStartRealtime = Time.realtimeSinceStartup;
                sSession = new RegistryTraceSession
                {
                    schemaVersion = 2,
                    seed = DiagTraceShared.CurrentSeed,
                    sessionId = DiagTraceShared.CurrentSessionId,
                    events = new List<RegistryTraceEvent>(),
                };
                DiagTraceShared.StampSessionRunMetadata(out sSession.runTag, out sSession.runTagNote);
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
                BeginSessionIfNeeded();
                if (!ShouldMirrorPerfKind(ev.kind, ev.payload))
                {
                    return;
                }

                if (ev.kind == RegistryTraceKinds.RegistryDelta
                    && ev.payload != null
                    && ev.payload.TryGetValue("op", out var op)
                    && op == "remove")
                {
                    TrackReleaseFromMirror(ev.uid, ev.payload);
                }
                else if (ev.kind == RegistryTraceKinds.Despawn && ev.payload != null)
                {
                    TrackReleaseFromMirror(ev.uid, ev.payload);
                }
                else if (ev.kind == RegistryTraceKinds.Vacate)
                {
                    TrackVacateFromMirror(ev.uid, ev.payload);
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

                CaptureFieldVisualState(trigger ?? string.Empty, includeCheckpoint: false);
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
                    var cardManager = CardEntityLifecycleHook.CardsOrNull();
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
                CaptureFieldVisualState(trigger ?? string.Empty, includeCheckpoint: true);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[RegistryTrace] CaptureCheckpoint failed: " + ex.Message);
            }
        }

        public static string ExportJson(bool silentIfEmpty = false, bool automatic = true)
        {
            try
            {
                if (!DiagTraceExportPreferences.ShouldWriteFile(DiagTraceTrack.Registry, automatic))
                {
                    return null;
                }

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
                var elapsed = 0f;
                var nextWatchIndex = 0;
                while (elapsed < IdleWatchDelaysSeconds[IdleWatchDelaysSeconds.Length - 1] + 0.01f)
                {
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(IdlePollIntervalSeconds),
                        cancellationToken: cancellationToken);
                    if (generation != sIdleWatchGeneration || cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    if (!IsInteractionLoopPhase())
                    {
                        return;
                    }

                    elapsed += IdlePollIntervalSeconds;

                    while (nextWatchIndex < IdleWatchDelaysSeconds.Length
                           && elapsed + 0.001f >= IdleWatchDelaysSeconds[nextWatchIndex])
                    {
                        var delay = IdleWatchDelaysSeconds[nextWatchIndex];
                        var trigger = RegistryTraceTriggers.IdleWatchPrefix + delay.ToString("0") + "s";
                        CardEntityLifecycleHook.CardsOrNull()?.AuditRegistryIntegrity(trigger);
                        nextWatchIndex++;
                    }

                    CaptureFieldVisualState(
                        RegistryTraceTriggers.IdlePollPrefix + Mathf.RoundToInt(elapsed) + "s",
                        includeCheckpoint: false);
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
                || trigger.StartsWith(RegistryTraceTriggers.IdlePollPrefix, StringComparison.Ordinal)
                || trigger.StartsWith(RegistryTraceTriggers.BeatClosePrefix, StringComparison.Ordinal)
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
                var field = GroundFieldGeometryHook.FieldOrNull();
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

        private static void ResetVisualSessionState()
        {
            sVisualBaseline = -1;
            sLastReportedVisualGap = -1;
            sUserInteractionCount = 0;
            sLastReleaseUid = -1;
            sLastReleaseReason = string.Empty;
            sLastReleaseCaller = string.Empty;
            sLastReleaseTMs = 0;
            sLastVacateDetail = string.Empty;
            sLastVacateTMs = 0;
        }

        private static void CaptureFieldVisualState(string trigger, bool includeCheckpoint)
        {
            if (!sEnabled)
            {
                return;
            }

            try
            {
                var isOpeningBaseline = trigger == RegistryTraceTriggers.OpeningSettled
                    || trigger == RegistryTraceTriggers.InteractionLoopIdle;
                if (isOpeningBaseline && sVisualBaseline < 0)
                {
                    var baselineReport = DiagFieldVisualCapture.Capture(-1);
                    sVisualBaseline = baselineReport.VisibleCount;
                }

                var report = DiagFieldVisualCapture.Capture(sVisualBaseline);
                var payload = BuildVisualPayload(trigger, report);
                Record(RegistryTraceKinds.FieldVisualAudit, -1, "Registry.FieldVisualAudit", payload);

                if (includeCheckpoint && IsLifecycleTrigger(trigger))
                {
                    Record(
                        RegistryTraceKinds.Checkpoint,
                        -1,
                        "Registry.VisualCheckpoint",
                        new Dictionary<string, string>(payload));
                }

                MaybeEmitVisualGap(trigger, report);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[RegistryTrace] CaptureFieldVisualState failed: " + ex.Message);
            }
        }

        private static Dictionary<string, string> BuildVisualPayload(
            string trigger,
            DiagFieldVisualCapture.FieldVisualReport report)
        {
            return new Dictionary<string, string>
            {
                { "trigger", trigger ?? string.Empty },
                { "baselineVisible", report.BaselineVisible.ToString() },
                { "visibleCount", report.VisibleCount.ToString() },
                { "occupiedCount", report.OccupiedCount.ToString() },
                { "coreCount", report.CoreCount.ToString() },
                { "visualMissing", report.MissingVisible.ToString() },
                { "emptyVisualSlots", report.EmptyVisualSlots ?? string.Empty },
                { "emptyVisualSlotCount", report.EmptyVisualSlotCount.ToString() },
                { "hiddenOccupied", report.HiddenOccupied ?? string.Empty },
                { "offModeOccupied", report.OffModeOccupied ?? string.Empty },
                { "slotDetail", report.SlotDetail ?? string.Empty },
                { "userInteractionCount", sUserInteractionCount.ToString() },
                { "phase", BattleTraceRecorder.CurrentPhaseName() },
                { "beatKind", DiagBeatClock.CurrentBeatKind ?? string.Empty },
                { "chainId", DirectorTrace.CurrentChainId.ToString() },
                { "choreoSeqId", ChoreoTraceContext.CurrentSeqId.ToString() },
            };
        }

        private static void MaybeEmitVisualGap(
            string trigger,
            DiagFieldVisualCapture.FieldVisualReport report)
        {
            if (sVisualBaseline <= 0 || report.MissingVisible <= 0)
            {
                if (report.MissingVisible <= 0)
                {
                    sLastReportedVisualGap = 0;
                }

                return;
            }

            if (report.MissingVisible == sLastReportedVisualGap)
            {
                return;
            }

            sLastReportedVisualGap = report.MissingVisible;
            var gapPayload = BuildVisualPayload(trigger, report);
            gapPayload["code"] = RegistryTraceAnomalyCodes.FieldVisualGap;
            gapPayload["lastReleaseUid"] = sLastReleaseUid.ToString();
            gapPayload["lastReleaseReason"] = sLastReleaseReason ?? string.Empty;
            gapPayload["lastReleaseCaller"] = sLastReleaseCaller ?? string.Empty;
            gapPayload["lastReleaseTMs"] = sLastReleaseTMs.ToString();
            gapPayload["lastVacate"] = sLastVacateDetail ?? string.Empty;
            gapPayload["lastVacateTMs"] = sLastVacateTMs.ToString();

            Record(RegistryTraceKinds.FieldVisualGap, -1, "Registry.FieldVisualGap", gapPayload);
            Record(
                RegistryTraceKinds.Anomaly,
                -1,
                "Anomaly.Detect",
                new Dictionary<string, string>
                {
                    { "code", RegistryTraceAnomalyCodes.FieldVisualGap },
                    { "detail", "missing=" + report.MissingVisible + " empty=" + report.EmptyVisualSlots },
                    { "trigger", trigger ?? string.Empty },
                });
        }

        private static void TrackReleaseFromMirror(int uid, Dictionary<string, string> payload)
        {
            sLastReleaseUid = uid;
            sLastReleaseTMs = ElapsedMs();
            sLastReleaseReason = payload != null && payload.TryGetValue("reason", out var reason)
                ? reason
                : string.Empty;
            sLastReleaseCaller = payload != null && payload.TryGetValue("caller", out var caller)
                ? caller
                : string.Empty;
        }

        private static void TrackVacateFromMirror(int uid, Dictionary<string, string> payload)
        {
            sLastVacateTMs = ElapsedMs();
            var slot = payload != null && payload.TryGetValue("slot", out var slotText) ? slotText : "?";
            sLastVacateDetail = uid + "@" + slot;
        }
    }
}
