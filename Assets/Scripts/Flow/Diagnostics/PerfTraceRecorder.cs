using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NineGrid.Cards;
using NineGrid.Cards.Convergence;
using UnityEngine;

namespace NineGrid.Flow.Diagnostics
{
    /// <summary>
    /// 表现层 PerfTrace 记录器。打点失败一律吞掉。
    /// </summary>
    public static class PerfTraceRecorder
    {
        private const float SlotMismatchThreshold = 0.35f;
        private const float CombatBoardMargin = 6f;

        private static PerfTraceSession sSession;
        private static float sSessionStartRealtime;
        private static bool sEnabled =
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            true;
#else
            false;
#endif

#if UNITY_EDITOR
        static PerfTraceRecorder()
        {
            sEnabled = DiagTraceExportPreferences.GetTrackRecording(DiagTraceTrack.Perf);
        }
#endif

        private static readonly Dictionary<int, OpenMotion> sOpenMotions = new Dictionary<int, OpenMotion>();
        private static readonly Dictionary<int, PlannedMotion> sPlans = new Dictionary<int, PlannedMotion>();
        private static readonly HashSet<int> sCombatantUids = new HashSet<int>();
        private static Dictionary<int, BoardCardSnap> sLastBoardFull = new Dictionary<int, BoardCardSnap>();
        private static int sAnomalyCountThisBeat;

        private struct OpenMotion
        {
            public int MotionId;
            public int EventIndex;
            public string Site;
            public int ExpectMs;
            public float BeginRealtime;
        }

        private struct PlannedMotion
        {
            public int EventIndex;
            public int ToSlot;
            public bool Began;
        }

        private struct BoardCardSnap
        {
            public int Uid;
            public int Slot;
            public int XCm;
            public int YCm;
            public bool Active;
            public bool FieldDead;
            public string Mode;
            public bool Tween;
        }

        public static bool Enabled
        {
            get => sEnabled;
            set
            {
#if UNITY_EDITOR
                DiagTraceExportPreferences.SetTrackRecording(DiagTraceTrack.Perf, value);
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

        public static PerfTraceSession CurrentSession => sSession;

        public static bool HasEvents =>
            sSession != null && sSession.events != null && sSession.events.Count > 0;

        public static int OpenMotionCount => sOpenMotions.Count;

        public static void Clear()
        {
            try
            {
                sSession = null;
                sOpenMotions.Clear();
                sPlans.Clear();
                sCombatantUids.Clear();
                sLastBoardFull.Clear();
                sAnomalyCountThisBeat = 0;
                CardPresentationProbe.ResetMotionIds();
                ChoreoTraceContext.Reset();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[PerfTrace] Clear failed: " + ex.Message);
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
                    DiagTraceShared.StampSessionRunMetadata(out sSession.runTag, out sSession.runTagNote);
                    return;
                }

                sSessionStartRealtime = Time.realtimeSinceStartup;
                sSession = new PerfTraceSession
                {
                    schemaVersion = 2,
                    seed = DiagTraceShared.CurrentSeed,
                    sessionId = DiagTraceShared.CurrentSessionId,
                    events = new List<PerfTraceEvent>(),
                };
                DiagTraceShared.StampSessionRunMetadata(out sSession.runTag, out sSession.runTagNote);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[PerfTrace] BeginSession failed: " + ex.Message);
            }
        }

        public static void RegisterSinkHandlers()
        {
            PerfTraceSink.Record = OnSinkRecord;
            PerfTraceSink.RequestBoardSnap = (phase, full) => RecordBoardSnap(phase, full);
            PerfTraceSink.OpenBeat = (kind, node) => OpenBeat(kind, node);
            PerfTraceSink.CloseBeat = () => CloseBeat();
            PerfTraceSink.SetCombatants = SetCombatants;
        }

        public static void UnregisterSinkHandlers()
        {
            PerfTraceSink.ClearHandlers();
        }

        public static int OpenBeat(string beatKind, int nodeIndex = 0, bool captureBoard = true)
        {
            if (!sEnabled)
            {
                return DiagBeatClock.Open(beatKind, nodeIndex);
            }

            try
            {
                if (DiagBeatClock.IsOpen)
                {
                    CloseBeat(captureBoard: true);
                }

                sAnomalyCountThisBeat = 0;
                sPlans.Clear();
                var beatId = DiagBeatClock.Open(beatKind, nodeIndex);
                Record(
                    PerfTraceKinds.BeatOpen,
                    uid: -1,
                    site: PerfTraceSites.BeatClock,
                    payload: new Dictionary<string, string>
                    {
                        { "beatKind", beatKind ?? string.Empty },
                        { "nodeIndex", nodeIndex.ToString(CultureInfo.InvariantCulture) },
                    });

                if (captureBoard)
                {
                    RecordBoardSnap("beatOpen", full: true);
                }

                return beatId;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[PerfTrace] OpenBeat failed: " + ex.Message);
                return 0;
            }
        }

        public static void CloseBeat(bool captureBoard = true, IReadOnlyList<int> combatantUids = null)
        {
            if (!sEnabled && !DiagBeatClock.IsOpen)
            {
                DiagBeatClock.Close();
                return;
            }

            try
            {
                if (!DiagBeatClock.IsOpen)
                {
                    return;
                }

                if (combatantUids != null)
                {
                    sCombatantUids.Clear();
                    for (var i = 0; i < combatantUids.Count; i++)
                    {
                        sCombatantUids.Add(combatantUids[i]);
                    }
                }

                if (captureBoard)
                {
                    RecordBoardSnap("beatClose", full: false);
                }

                var beatKind = DiagBeatClock.CurrentBeatKind;
                if (beatKind == DiagBeatKinds.BoardChoreo)
                {
                    ChoreoTraceContext.RecordBusySnapshot("BeatClose.BoardChoreo");
                    if (ChoreoTraceContext.OpenChoreoCount > 0 || sOpenMotions.Count > 0)
                    {
                        EmitAnomaly(
                            PerfTraceAnomalyCodes.ChoreoIncompleteAtBeatClose,
                            -1,
                            "openChoreo=" + ChoreoTraceContext.GetOpenChoreoSummary()
                            + " openMotion=" + sOpenMotions.Count,
                            -1);
                    }
                }

                DetectBeatCloseAnomalies(beatKind);

                RegistryTraceRecorder.OnPresentationBeatClosed(beatKind);

                var nodeIndex = DiagBeatClock.CurrentNodeIndex;
                Record(
                    PerfTraceKinds.BeatClose,
                    uid: -1,
                    site: PerfTraceSites.BeatClock,
                    payload: new Dictionary<string, string>
                    {
                        { "beatKind", beatKind },
                        { "nodeIndex", nodeIndex.ToString(CultureInfo.InvariantCulture) },
                        { "anomalyCount", sAnomalyCountThisBeat.ToString(CultureInfo.InvariantCulture) },
                    });

                DiagBeatClock.Close();
                sCombatantUids.Clear();
                sAnomalyCountThisBeat = 0;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[PerfTrace] CloseBeat failed: " + ex.Message);
                DiagBeatClock.Close();
            }
        }

        public static void SetCombatants(int attackerUid, int targetUid)
        {
            sCombatantUids.Clear();
            if (attackerUid > 0)
            {
                sCombatantUids.Add(attackerUid);
            }

            if (targetUid > 0)
            {
                sCombatantUids.Add(targetUid);
            }
        }

        public static void Record(
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

                var ev = new PerfTraceEvent
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
                TrackAfterRecord(ev);
                RegistryTraceRecorder.MirrorFromPerf(ev);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[PerfTrace] Record failed: " + ex.Message);
            }
        }

        public static void RecordBoardSnap(string phase, bool full)
        {
            if (!sEnabled)
            {
                return;
            }

            try
            {
                var cards = CaptureLiveBoard();
                var sb = new StringBuilder(cards.Count * 24);
                if (full || sLastBoardFull.Count == 0)
                {
                    foreach (var kv in cards)
                    {
                        var c = kv.Value;
                        if (sb.Length > 0)
                        {
                            sb.Append(';');
                        }

                        AppendCardLine(sb, c);
                    }

                    sLastBoardFull = cards;
                    Record(
                        PerfTraceKinds.BoardSnap,
                        uid: -1,
                        site: PerfTraceSites.BoardSnapCapture,
                        payload: new Dictionary<string, string>
                        {
                            { "phase", phase ?? string.Empty },
                            { "full", "1" },
                            { "cards", sb.ToString() },
                        });
                    return;
                }

                // diff vs last
                foreach (var kv in cards)
                {
                    var c = kv.Value;
                    if (!sLastBoardFull.TryGetValue(c.Uid, out var prev))
                    {
                        if (sb.Length > 0)
                        {
                            sb.Append(';');
                        }

                        sb.Append('+');
                        AppendCardLine(sb, c);
                        continue;
                    }

                    if (prev.Slot != c.Slot || prev.XCm != c.XCm || prev.YCm != c.YCm
                        || prev.Active != c.Active || prev.FieldDead != c.FieldDead
                        || prev.Mode != c.Mode || prev.Tween != c.Tween)
                    {
                        if (sb.Length > 0)
                        {
                            sb.Append(';');
                        }

                        sb.Append('~');
                        sb.Append(c.Uid).Append(" slot=").Append(prev.Slot).Append("→").Append(c.Slot);
                        sb.Append(" xy=").Append(CmToStr(prev.XCm)).Append(',').Append(CmToStr(prev.YCm));
                        sb.Append("→").Append(CmToStr(c.XCm)).Append(',').Append(CmToStr(c.YCm));
                        sb.Append(" active=").Append(c.Active ? '1' : '0');
                        sb.Append(" fieldDead=").Append(c.FieldDead ? '1' : '0');
                        sb.Append(" tween=").Append(c.Tween ? '1' : '0');
                        if (!string.IsNullOrEmpty(c.Mode))
                        {
                            sb.Append(" mode=").Append(c.Mode);
                        }
                    }
                }

                foreach (var kv in sLastBoardFull)
                {
                    if (!cards.ContainsKey(kv.Key))
                    {
                        if (sb.Length > 0)
                        {
                            sb.Append(';');
                        }

                        sb.Append('-').Append(kv.Key);
                    }
                }

                sLastBoardFull = cards;
                Record(
                    PerfTraceKinds.BoardSnap,
                    uid: -1,
                    site: PerfTraceSites.BoardSnapCapture,
                    payload: new Dictionary<string, string>
                    {
                        { "phase", phase ?? string.Empty },
                        { "full", "0" },
                        { "cards", sb.ToString() },
                    });

                DetectBoardAnomalies(cards);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[PerfTrace] RecordBoardSnap failed: " + ex.Message);
            }
        }

        public static string ExportJson(bool silentIfEmpty = false, bool automatic = true)
        {
            try
            {
                if (!DiagTraceExportPreferences.ShouldWriteFile(DiagTraceTrack.Perf, automatic))
                {
                    return null;
                }

                if (sSession == null || sSession.events == null || sSession.events.Count == 0)
                {
                    if (!silentIfEmpty)
                    {
                        Debug.LogWarning("[PerfTrace] ExportJson: 无会话或无 events。");
                    }

                    return null;
                }

                var json = PerfTraceJson.Serialize(sSession);
                var dir = DiagTraceShared.ResolveNotesDir("Logs/PerfLog");
                var fileName = DiagTraceShared.BuildFileName(
                    "perflog",
                    sSession.sessionId,
                    sSession.seed);
                var path = DiagTraceShared.WriteUtf8File(dir, fileName, json);
                if (!string.IsNullOrEmpty(path))
                {
                    Debug.Log("[PerfTrace] Exported: " + path + " events=" + sSession.events.Count);
                }

                return path;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[PerfTrace] ExportJson failed: " + ex.Message);
                return null;
            }
        }

        public static string ExportOnPlayExit(string source)
        {
            try
            {
                if (!sEnabled || !HasEvents)
                {
                    return null;
                }

                var path = ExportJson(automatic: true);
                if (!string.IsNullOrEmpty(path))
                {
                    Debug.Log("[PerfTrace] Play 结束已导出表现日志（" + source + "）：" + path);
                }

                return path;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[PerfTrace] ExportOnPlayExit failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 用户/DevTest 现场戳点：RegistryAudit + 全量 BoardSnap + UserMark。
        /// 复现「卡从 CardManager 消失」时在交互修复前按下。
        /// </summary>
        public static void StampUserObservation(string label = "BugScene")
        {
            try
            {
                var safeLabel = string.IsNullOrWhiteSpace(label) ? "BugScene" : label.Trim();
                var cardManager = CardManagerSingleton.TryGetInstance();
                var registryCount = cardManager != null ? cardManager.CardsByUid.Count : 0;
                cardManager?.AuditRegistryIntegrity("UserMark." + safeLabel);
                RecordBoardSnap("userMark:" + safeLabel, full: true);
                CardPresentationProbe.UserMark(safeLabel, registryCount);
                Debug.Log(
                    "[PerfTrace] UserMark label="
                    + safeLabel
                    + " registryCount="
                    + registryCount
                    + " beatId="
                    + DiagBeatClock.ResolveBeatIdForEvent());
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[PerfTrace] StampUserObservation failed: " + ex.Message);
            }
        }

        /// <summary>编排/拾取专项 Anomaly（BeatClose 外也可实时写入）。</summary>
        public static void EmitChoreoAnomaly(string code, int uid, string detail)
        {
            if (!sEnabled)
            {
                return;
            }

            try
            {
                EmitAnomaly(code, uid, detail, -1);
            }
            catch
            {
                // swallow
            }
        }

        public static void RecordSessionChoreoSummary(Dictionary<string, string> payload)
        {
            Record(
                PerfTraceKinds.SessionChoreoSummary,
                uid: -1,
                site: "Choreo.SessionSummary",
                payload: payload != null
                    ? new Dictionary<string, string>(payload)
                    : new Dictionary<string, string>());
        }

        public static string ResolveExportDirectory()
        {
            return DiagTraceShared.ResolveNotesDir("Logs/PerfLog");
        }

        private static void OnSinkRecord(string kind, int uid, string site, string[] pairs)
        {
            var payload = PairsToDict(pairs);
            Record(kind, uid, site, payload);
        }

        private static Dictionary<string, string> PairsToDict(string[] pairs)
        {
            var dict = new Dictionary<string, string>();
            if (pairs == null)
            {
                return dict;
            }

            for (var i = 0; i + 1 < pairs.Length; i += 2)
            {
                var key = pairs[i] ?? string.Empty;
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                dict[key] = pairs[i + 1] ?? string.Empty;
            }

            return dict;
        }

        private static void TrackAfterRecord(PerfTraceEvent ev)
        {
            if (ev == null)
            {
                return;
            }

            if (ev.kind == PerfTraceKinds.MotionPlan && ev.uid > 0)
            {
                var toSlot = ParseInt(ev.payload, "toSlot", -1);
                sPlans[ev.uid] = new PlannedMotion
                {
                    EventIndex = ev.index,
                    ToSlot = toSlot,
                    Began = false,
                };
            }
            else if (ev.kind == PerfTraceKinds.MotionBegin && ev.uid > 0)
            {
                var motionId = ParseInt(ev.payload, "motionId", 0);
                if (sOpenMotions.ContainsKey(ev.uid))
                {
                    var prev = sOpenMotions[ev.uid];
                    EmitAnomaly(
                        PerfTraceAnomalyCodes.MotionOverlap,
                        ev.uid,
                        "new motionId=" + motionId + " while open motionId=" + prev.MotionId,
                        ev.index);
                }

                sOpenMotions[ev.uid] = new OpenMotion
                {
                    MotionId = motionId,
                    EventIndex = ev.index,
                    Site = ev.site,
                    ExpectMs = ParseInt(ev.payload, "expectMs", 0),
                    BeginRealtime = Time.realtimeSinceStartup,
                };
                if (sPlans.TryGetValue(ev.uid, out var plan))
                {
                    plan.Began = true;
                    sPlans[ev.uid] = plan;
                }
            }
            else if (ev.kind == PerfTraceKinds.MotionEnd && ev.uid > 0)
            {
                sOpenMotions.Remove(ev.uid);
                sPlans.Remove(ev.uid);
            }
            else if (ev.kind == PerfTraceKinds.SnapSet && ev.uid > 0)
            {
                if (sOpenMotions.TryGetValue(ev.uid, out var open))
                {
                    EmitAnomaly(
                        PerfTraceAnomalyCodes.SnapDuringMotion,
                        ev.uid,
                        "snap while motionId=" + open.MotionId + " site=" + ev.site,
                        ev.index);
                }
            }
            else if (ev.kind == PerfTraceKinds.VisChange && ev.uid > 0)
            {
                if (sCombatantUids.Contains(ev.uid)
                    && DiagBeatClock.CurrentBeatKind == DiagBeatKinds.CombatHit
                    && ev.payload != null)
                {
                    if (ev.payload.TryGetValue("active", out var active) && active == "0")
                    {
                        EmitAnomaly(
                            PerfTraceAnomalyCodes.VisOffWhileCombatant,
                            ev.uid,
                            "combatant active=0 site=" + ev.site,
                            ev.index);
                    }
                    else if (ev.payload.TryGetValue("renderOn", out var renderOn) && renderOn == "0")
                    {
                        EmitAnomaly(
                            PerfTraceAnomalyCodes.VisOffWhileCombatant,
                            ev.uid,
                            "combatant renderOn=0 site=" + ev.site,
                            ev.index);
                    }
                }
            }
            else if (ev.kind == PerfTraceKinds.CombatHitFrame
                && ev.uid > 0
                && sCombatantUids.Contains(ev.uid)
                && DiagBeatClock.CurrentBeatKind == DiagBeatKinds.CombatHit
                && ev.payload != null
                && !ShouldIgnoreOffscreenCombatant(ev.uid))
            {
                if (ev.payload.TryGetValue("renderOn", out var hitRenderOn) && hitRenderOn == "0")
                {
                    EmitAnomaly(
                        PerfTraceAnomalyCodes.VisOffWhileCombatant,
                        ev.uid,
                        "combatant renderOn=0 at hit frame site=" + ev.site,
                        ev.index);
                }
            }
            else if (ev.kind == PerfTraceKinds.DeathCallback
                && ev.uid > 0
                && ev.payload != null)
            {
                if (ev.payload.TryGetValue("outcome", out var outcome)
                    && outcome == "fired"
                    && ev.payload.TryGetValue("confirmedKill", out var confirmed)
                    && confirmed == "0")
                {
                    EmitAnomaly(
                        PerfTraceAnomalyCodes.DeathCallbackOnSurvivor,
                        ev.uid,
                        "death callback fired without confirmed kill site=" + ev.site,
                        ev.index);
                }
                else if (ev.payload.TryGetValue("outcome", out var skippedOutcome)
                    && skippedOutcome == "skipped"
                    && DiagBeatClock.CurrentBeatKind == DiagBeatKinds.CombatHit
                    && ev.payload.TryGetValue("armed", out var armed)
                    && armed == "1")
                {
                    EmitAnomaly(
                        PerfTraceAnomalyCodes.LethalEstimateMismatch,
                        ev.uid,
                        "armed death callback skipped at site=" + ev.site,
                        ev.index);
                }
            }
            else if (ev.kind == PerfTraceKinds.BattleBind
                && ev.payload != null
                && ev.payload.TryGetValue("estKill", out var estKill)
                && estKill == "1"
                && ev.payload.TryGetValue("bindDeath", out var bindDeath)
                && bindDeath == "1"
                && ev.payload.TryGetValue("counter", out var counterFlag)
                && counterFlag == "1")
            {
                ev.payload.TryGetValue("profileId", out var profileId);
                EmitAnomaly(
                    PerfTraceAnomalyCodes.LethalEstimateMismatch,
                    ev.uid,
                    "counter routed with lethal/death bind profile=" + (profileId ?? string.Empty),
                    ev.index);
            }
            else if (ev.uid > 0
                && sCombatantUids.Contains(ev.uid)
                && DiagBeatClock.CurrentBeatKind == DiagBeatKinds.CombatHit
                && (ev.kind == PerfTraceKinds.MotionEnd
                    || ev.kind == PerfTraceKinds.SnapSet
                    || ev.kind == PerfTraceKinds.CombatHitFrame))
            {
                TryDetectCombatantOffscreen(ev);
            }
        }

        private static bool ShouldIgnoreOffscreenCombatant(int uid)
        {
            var cards = CardManagerSingleton.Instance;
            if (cards?.CardsByUid == null || !cards.CardsByUid.TryGetValue(uid, out var card))
            {
                return false;
            }

            return card.IsFieldDead || card.DisplayMode == CardDisplayMode.RemovedMode;
        }

        private static void TryDetectCombatantOffscreen(PerfTraceEvent ev)
        {
            if (ev?.payload == null || ShouldIgnoreOffscreenCombatant(ev.uid))
            {
                return;
            }

            if (!ev.payload.TryGetValue("x", out var xRaw)
                || !ev.payload.TryGetValue("y", out var yRaw)
                || !float.TryParse(xRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                || !float.TryParse(yRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            {
                return;
            }

            if (IsWithinCombatBoardBounds(x, y))
            {
                return;
            }

            var phase = ev.kind == PerfTraceKinds.CombatHitFrame ? " at hit frame" : string.Empty;
            EmitAnomaly(
                PerfTraceAnomalyCodes.CombatantOffscreenWhileHit,
                ev.uid,
                "xy=" + xRaw + "," + yRaw + " site=" + ev.site + phase,
                ev.index);
        }

        private static bool IsWithinCombatBoardBounds(float x, float y)
        {
            var field = GroundFieldManagerSingleton.Instance;
            if (field == null)
            {
                return Mathf.Abs(x) <= 15f && y >= -8f && y <= 15f;
            }

            var minX = float.MaxValue;
            var maxX = float.MinValue;
            var minY = float.MaxValue;
            var maxY = float.MinValue;
            var hasAnchor = false;
            for (var slot = 1; slot <= 9; slot++)
            {
                var anchor = field.GetGroundAnchor(slot);
                if (anchor == null)
                {
                    continue;
                }

                hasAnchor = true;
                minX = Mathf.Min(minX, anchor.position.x);
                maxX = Mathf.Max(maxX, anchor.position.x);
                minY = Mathf.Min(minY, anchor.position.y);
                maxY = Mathf.Max(maxY, anchor.position.y);
            }

            if (!hasAnchor)
            {
                return Mathf.Abs(x) <= 15f && y >= -8f && y <= 15f;
            }

            return x >= minX - CombatBoardMargin
                && x <= maxX + CombatBoardMargin
                && y >= minY - CombatBoardMargin
                && y <= maxY + CombatBoardMargin;
        }

        private static void DetectCombatantOffscreenAtBeatClose()
        {
            if (DiagBeatClock.CurrentBeatKind != DiagBeatKinds.CombatHit || sCombatantUids.Count == 0)
            {
                return;
            }

            var cards = CardManagerSingleton.Instance;
            if (cards == null)
            {
                return;
            }

            foreach (var uid in sCombatantUids)
            {
                if (ShouldIgnoreOffscreenCombatant(uid))
                {
                    continue;
                }

                if (cards.CardsByUid == null
                    || !cards.CardsByUid.TryGetValue(uid, out var card)
                    || card?.Transform == null)
                {
                    continue;
                }

                var pos = card.Transform.position;
                if (IsWithinCombatBoardBounds(pos.x, pos.y))
                {
                    continue;
                }

                EmitAnomaly(
                    PerfTraceAnomalyCodes.CombatantOffscreenWhileHit,
                    uid,
                    "xy=" + CardPresentationProbe.FormatXy(pos.x) + ","
                    + CardPresentationProbe.FormatXy(pos.y) + " at beat close",
                    -1);
            }
        }

        private static void DetectBeatCloseAnomalies(string beatKind = null)
        {
            beatKind ??= DiagBeatClock.CurrentBeatKind;
            DetectCombatantOffscreenAtBeatClose();

            var openSnapshot = new List<KeyValuePair<int, OpenMotion>>(sOpenMotions);
            foreach (var kv in openSnapshot)
            {
                var open = kv.Value;
                var elapsedMs = (Time.realtimeSinceStartup - open.BeginRealtime) * 1000f;
                var overExpected = open.ExpectMs > 0 && elapsedMs > open.ExpectMs * 2f;
                var strictBeat = beatKind == DiagBeatKinds.PostKillDrain
                    || beatKind == DiagBeatKinds.BoardChoreo;

                Record(
                    PerfTraceKinds.MotionEnd,
                    kv.Key,
                    open.Site,
                    new Dictionary<string, string>
                    {
                        ["motionId"] = open.MotionId.ToString(CultureInfo.InvariantCulture),
                        ["endHow"] = "beatClose",
                    });

                if (overExpected || strictBeat)
                {
                    EmitAnomaly(
                        PerfTraceAnomalyCodes.MissingMotionEnd,
                        kv.Key,
                        "open motionId=" + open.MotionId + " at beat close",
                        open.EventIndex);
                }
            }

            sOpenMotions.Clear();

            foreach (var kv in sPlans)
            {
                if (!kv.Value.Began)
                {
                    EmitAnomaly(
                        PerfTraceAnomalyCodes.MotionPlanWithoutBegin,
                        kv.Key,
                        "plan toSlot=" + kv.Value.ToSlot + " never began",
                        kv.Value.EventIndex);
                }
            }

            sPlans.Clear();
        }

        private static void DetectBoardAnomalies(Dictionary<int, BoardCardSnap> cards)
        {
            var field = GroundFieldManagerSingleton.Instance;
            if (field == null)
            {
                return;
            }

            // SlotWorldMismatch: registered slot vs world
            foreach (var kv in cards)
            {
                var c = kv.Value;
                if (c.Slot <= 0 || !c.Active)
                {
                    continue;
                }

                var anchor = field.GetGroundAnchor(c.Slot);
                if (anchor == null)
                {
                    continue;
                }

                var ax = CardPresentationProbe.QuantizeCm(anchor.position.x);
                var ay = CardPresentationProbe.QuantizeCm(anchor.position.y);
                var dx = (c.XCm - ax) / 100f;
                var dy = (c.YCm - ay) / 100f;
                if (dx * dx + dy * dy > SlotMismatchThreshold * SlotMismatchThreshold)
                {
                    // BeginDeal/Redirect 途中视觉未到锚属预期，避免 L0/途中假阳刷屏。
                    if (field.IsDealInFlight(c.Uid))
                    {
                        continue;
                    }

                    EmitAnomaly(
                        PerfTraceAnomalyCodes.SlotWorldMismatch,
                        c.Uid,
                        "slot=" + c.Slot + " xy=" + CmToStr(c.XCm) + "," + CmToStr(c.YCm)
                        + " anchor=" + CmToStr(ax) + "," + CmToStr(ay),
                        -1);
                }
            }

            // FieldOccupancyWithoutView / ViewWithoutFieldOccupancy
            var cardManager = CardManagerSingleton.Instance;
            if (cardManager != null)
            {
                var snap = field.GetSnapshot();
                for (var i = 0; i < snap.Slots.Length; i++)
                {
                    var occ = snap.Slots[i];
                    if (occ.IsEmpty || occ.IsAvatarReserved)
                    {
                        continue;
                    }

                    if (!cardManager.TryGet(occ.Uid, out _))
                    {
                        EmitAnomaly(
                            PerfTraceAnomalyCodes.FieldOccupancyWithoutView,
                            occ.Uid,
                            "slot=" + occ.Slot + " fieldHasUid viewMissing",
                            -1);
                    }
                }

                foreach (var card in cardManager.EnumerateCards())
                {
                    if (card == null
                        || card.IsFieldDead
                        || card.DisplayMode != CardDisplayMode.GroundCardMode)
                    {
                        continue;
                    }

                    if (!field.TryGetSlotOf(card.Uid, out _))
                    {
                        EmitAnomaly(
                            PerfTraceAnomalyCodes.ViewWithoutFieldOccupancy,
                            card.Uid,
                            "mode=" + card.DisplayMode + " noFieldSlot",
                            -1);
                    }
                }
            }

            // OrphanAtWrongAnchor: empty registered slot but another card sits on its anchor
            for (var slot = 1; slot <= 9; slot++)
            {
                if (GroundSlotTopology.IsAvatarReserved(slot))
                {
                    continue;
                }

                if (!field.IsEmpty(slot))
                {
                    continue;
                }

                var anchor = field.GetGroundAnchor(slot);
                if (anchor == null)
                {
                    continue;
                }

                var ax = CardPresentationProbe.QuantizeCm(anchor.position.x);
                var ay = CardPresentationProbe.QuantizeCm(anchor.position.y);
                foreach (var kv in cards)
                {
                    var c = kv.Value;
                    if (!c.Active || c.Slot == slot)
                    {
                        continue;
                    }

                    // 击杀尸体故意暂留视图：不算 Orphan；若仍贴在空槽锚点则单独记 DeadCorpse。
                    if (c.FieldDead)
                    {
                        var deadDx = (c.XCm - ax) / 100f;
                        var deadDy = (c.YCm - ay) / 100f;
                        if (deadDx * deadDx + deadDy * deadDy <= 0.08f * 0.08f)
                        {
                            EmitAnomaly(
                                PerfTraceAnomalyCodes.DeadCorpseAtVacatedSlot,
                                c.Uid,
                                "emptySlot=" + slot + " cardSlot=" + c.Slot
                                + " at empty anchor",
                                -1);
                        }

                        continue;
                    }

                    var dx = (c.XCm - ax) / 100f;
                    var dy = (c.YCm - ay) / 100f;
                    if (dx * dx + dy * dy <= 0.08f * 0.08f)
                    {
                        EmitAnomaly(
                            PerfTraceAnomalyCodes.OrphanAtWrongAnchor,
                            c.Uid,
                            "emptySlot=" + slot + " cardSlot=" + c.Slot
                            + " at empty anchor",
                            -1);
                    }
                }
            }
        }

        private static void EmitAnomaly(string code, int uid, string detail, int refIndex)
        {
            sAnomalyCountThisBeat++;
            Record(
                PerfTraceKinds.Anomaly,
                uid,
                PerfTraceSites.AnomalyDetect,
                new Dictionary<string, string>
                {
                    { "code", code ?? string.Empty },
                    { "detail", detail ?? string.Empty },
                    { "refIndex", refIndex.ToString(CultureInfo.InvariantCulture) },
                });
        }

        private static Dictionary<int, BoardCardSnap> CaptureLiveBoard()
        {
            var result = new Dictionary<int, BoardCardSnap>();
            var cards = CardManagerSingleton.Instance;
            var field = GroundFieldManagerSingleton.Instance;
            if (cards == null)
            {
                return result;
            }

            foreach (var card in cards.EnumerateCards())
            {
                if (card?.Transform == null)
                {
                    continue;
                }

                var go = card.GameObject;
                var active = go != null && go.activeInHierarchy;
                var slot = -1;
                if (field != null && field.TryGetSlotOf(card.Uid, out var found))
                {
                    slot = found;
                }

                var tween = sOpenMotions.ContainsKey(card.Uid);
                var pos = SlotFrameConvergence.GetVisualWorldPosition(card);
                result[card.Uid] = new BoardCardSnap
                {
                    Uid = card.Uid,
                    Slot = slot,
                    XCm = CardPresentationProbe.QuantizeCm(pos.x),
                    YCm = CardPresentationProbe.QuantizeCm(pos.y),
                    Active = active,
                    FieldDead = card.IsFieldDead,
                    Mode = card.DisplayMode.ToString(),
                    Tween = tween,
                };
            }

            return result;
        }

        private static void AppendCardLine(StringBuilder sb, BoardCardSnap c)
        {
            sb.Append(c.Uid)
                .Append(",slot=").Append(c.Slot)
                .Append(",xy=").Append(CmToStr(c.XCm)).Append(',').Append(CmToStr(c.YCm))
                .Append(",active=").Append(c.Active ? '1' : '0')
                .Append(",fieldDead=").Append(c.FieldDead ? '1' : '0')
                .Append(",mode=").Append(c.Mode ?? string.Empty)
                .Append(",tween=").Append(c.Tween ? '1' : '0');
        }

        private static string CmToStr(int cm)
        {
            return (cm / 100f).ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static int ParseInt(Dictionary<string, string> payload, string key, int fallback)
        {
            if (payload == null || !payload.TryGetValue(key, out var raw) || string.IsNullOrEmpty(raw))
            {
                return fallback;
            }

            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
                ? v
                : fallback;
        }

        private static int ElapsedMs()
        {
            return Mathf.Max(0, Mathf.RoundToInt((Time.realtimeSinceStartup - sSessionStartRealtime) * 1000f));
        }
    }
}
