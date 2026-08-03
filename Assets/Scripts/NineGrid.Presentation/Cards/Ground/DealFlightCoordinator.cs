using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Cards.Convergence;
using NineGrid.Cards.Vfx;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 补牌飞牌协调器：Drain / Explore 均走 L2 五次收敛；换格 = Redirect，无替身、无握手。
    /// Deal→Rotate 时起飞即瞄准预解最终格（逻辑占格仍 birth）。
    /// 宿主经 <see cref="IDealFlightHost"/>，不直接依赖具体 MonoBehaviour 业务状态。
    /// </summary>
    internal sealed class DealFlightCoordinator
    {
        private sealed class DealFlightProbe
        {
            public int BirthSlot;
            public int TrackedSlot;
            /// <summary>当前视觉收敛瞄准格（BeginDeal / Redirect 写入）。</summary>
            public int VisualAimSlot;
            public int PendingNetAtLaunch;
            public float FlightBeginUnscaledTime;
            public bool LoggedFirstRedirect;
            /// <summary>
            /// CancelFlightForUid 已同步 EndChoreo：finally 不得再 End，避免弹错栈顶。
            /// </summary>
            public bool ChoreoClosedEagerly;
            public ManagedCard Card;
            public DealSettleBudget Budget;
            public DealFlightKind Kind;
            public DealFlightHandle Handle;
            public Vector3 LaunchPos;
            public float SourceTime;
            public CancellationTokenSource LinkedCts;
        }

        private readonly IDealFlightHost _host;
        private readonly CancellationToken _destroyToken;
        private readonly Dictionary<int, DealFlightProbe> _exploreByBirthSlot = new();
        private readonly Dictionary<int, DealFlightProbe> _drainByUid = new();
        private float _lastExploreStartTime = float.NegativeInfinity;

        public DealFlightCoordinator(
            IDealFlightHost host,
            CancellationToken destroyToken)
        {
            _host = host;
            _destroyToken = destroyToken;
        }

        public int ActiveCount => _exploreByBirthSlot.Count + _drainByUid.Count;

        public DealFlightLayoutSettings Settings =>
            _host?.LayoutSettings?.dealFlight ?? new DealFlightLayoutSettings();

        public void StartExplore(int birthSlot)
        {
            if (_host == null
                || !_host.IsPlaceable(birthSlot)
                || _exploreByBirthSlot.ContainsKey(birthSlot))
            {
                return;
            }

            var probe = new DealFlightProbe
            {
                BirthSlot = birthSlot,
                TrackedSlot = birthSlot,
                VisualAimSlot = birthSlot,
                Kind = DealFlightKind.Explore,
                Handle = new DealFlightHandle(-1, birthSlot, DealFlightKind.Explore),
            };

            _exploreByBirthSlot[birthSlot] = probe;
            ChoreoTraceSink.SafeBeginChoreo("dealFlight", "kind", "explore", "birthSlot", birthSlot.ToString());
            TraceProbe(probe, "start", probe.Card?.Uid ?? -1);
            RunExploreProbeAsync(probe).Forget();
        }

        public DealFlightHandle LaunchDrainFlight(
            ManagedCard card,
            int birthSlot,
            Vector3 launchPos,
            DealFlightContext context)
        {
            if (_host == null || card == null || card.Uid <= 0)
            {
                return null;
            }

            if (_drainByUid.ContainsKey(card.Uid))
            {
                return _drainByUid[card.Uid].Handle;
            }

            var visualSlot = context.VisualTargetSlot > 0 ? context.VisualTargetSlot : birthSlot;
            if (!_host.TryGetExploreAnchorPosition(visualSlot, out var targetPos))
            {
                if (!_host.TryGetExploreAnchorPosition(birthSlot, out targetPos))
                {
                    targetPos = launchPos;
                }

                visualSlot = birthSlot;
            }

            var settings = Settings;
            var budget = DealSettleBudget.Compute(launchPos, targetPos, context, settings);
            var probe = new DealFlightProbe
            {
                BirthSlot = birthSlot,
                TrackedSlot = birthSlot,
                VisualAimSlot = visualSlot,
                PendingNetAtLaunch = context.PendingRotateSteps,
                Card = card,
                Budget = budget,
                Kind = DealFlightKind.Drain,
                LaunchPos = launchPos,
                SourceTime = budget.Total,
                Handle = new DealFlightHandle(card.Uid, birthSlot, DealFlightKind.Drain),
            };

            _drainByUid[card.Uid] = probe;
            ChoreoTraceSink.SafeBeginChoreo("dealFlight", "kind", "drain", "uid", card.Uid.ToString());
            TraceDealFlightBegin(probe);
            EmitAimAnomalyIfNeeded(probe, card.Uid);
            RunDrainProbeAsync(probe).Forget();
            return probe.Handle;
        }

        public void OnRingShifted(bool clockwise)
        {
            ShiftProbes(_exploreByBirthSlot.Values, clockwise);
            ShiftProbes(_drainByUid.Values, clockwise);
        }

        public bool IsInFlight(int uid)
        {
            if (uid <= 0)
            {
                return false;
            }

            if (_drainByUid.ContainsKey(uid))
            {
                return true;
            }

            foreach (var probe in _exploreByBirthSlot.Values)
            {
                if (probe.Card != null && probe.Card.Uid == uid)
                {
                    return true;
                }

                if (probe.Handle != null && probe.Handle.Uid == uid)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 大盘 hop/换格接管：更新追踪格并 Redirect L2 目标。同目标 no-op。
        /// </summary>
        public bool TryRedirectFlightToSlot(int uid, int toSlot, float sourceTime)
        {
            if (!_drainByUid.TryGetValue(uid, out var probe) || probe.Card == null)
            {
                return false;
            }

            return RedirectProbeToSlot(probe, toSlot, sourceTime, uid);
        }

        public static async UniTask WaitAllSettledAsync(
            IReadOnlyList<DealFlightHandle> handles,
            CancellationToken cancellationToken)
        {
            if (handles == null || handles.Count == 0)
            {
                return;
            }

            var tasks = new UniTask[handles.Count];
            for (var i = 0; i < handles.Count; i++)
            {
                var handle = handles[i];
                tasks[i] = handle != null
                    ? handle.WaitSettleAsync(cancellationToken)
                    : UniTask.CompletedTask;
            }

            await UniTask.WhenAll(tasks);
        }

        public void CancelAll()
        {
            var explore = new List<DealFlightProbe>(_exploreByBirthSlot.Values);
            var drain = new List<DealFlightProbe>(_drainByUid.Values);
            _exploreByBirthSlot.Clear();
            _drainByUid.Clear();

            for (var i = 0; i < explore.Count; i++)
            {
                CancelProbe(explore[i], rollback: true);
            }

            for (var i = 0; i < drain.Count; i++)
            {
                CancelProbe(drain[i], rollback: false);
            }
        }

        /// <summary>
        /// 场上移除 / Vacate 前取消该 uid 的 Drain/Explore 飞牌。
        /// 同步卸 ActiveCount 并 EndChoreo，避免同批 Deal→Remove 后 WaitAll 挂死、choreo 泄漏。
        /// </summary>
        public bool CancelFlightForUid(int uid, string reason)
        {
            if (uid <= 0)
            {
                return false;
            }

            DealFlightProbe probe = null;
            if (_drainByUid.TryGetValue(uid, out probe))
            {
                _drainByUid.Remove(uid);
            }
            else
            {
                var birthKey = 0;
                foreach (var kv in _exploreByBirthSlot)
                {
                    var candidate = kv.Value;
                    if (candidate == null)
                    {
                        continue;
                    }

                    var probeUid = candidate.Card != null
                        ? candidate.Card.Uid
                        : candidate.Handle != null ? candidate.Handle.Uid : 0;
                    if (probeUid != uid)
                    {
                        continue;
                    }

                    birthKey = kv.Key;
                    probe = candidate;
                    break;
                }

                if (probe == null)
                {
                    return false;
                }

                _exploreByBirthSlot.Remove(birthKey);
            }

            var outcome = string.IsNullOrEmpty(reason) ? "cancel" : "cancel:" + reason;
            probe.ChoreoClosedEagerly = true;
            if (probe.Card != null)
            {
                SlotFrameConvergence.SanitizeForSanctuary(probe.Card, "DealFlight.Cancel." + (reason ?? "uid"));
            }

            CancelProbe(probe, rollback: false);
            TraceDealFlightEnd(probe, outcome);
            ChoreoTraceSink.SafeEndChoreo(outcome);
            return true;
        }

        private void ShiftProbes(IEnumerable<DealFlightProbe> probes, bool clockwise)
        {
            var duration = _host.LayoutSettings != null ? _host.LayoutSettings.moveDuration : 0.35f;
            foreach (var probe in probes)
            {
                if (!GroundSlotTopology.IsOuterRing(probe.TrackedSlot))
                {
                    continue;
                }

                var prev = probe.TrackedSlot;
                probe.TrackedSlot = GroundSlotTopology.GetClockwiseRingTargetSlot(probe.TrackedSlot, clockwise);
                if (probe.Handle != null)
                {
                    probe.Handle.TrackedSlot = probe.TrackedSlot;
                }

                TraceProbe(
                    probe,
                    "ringShift",
                    probe.Card?.Uid ?? probe.Handle?.Uid ?? -1,
                    "prevSlot", prev.ToString(),
                    "clockwise", clockwise ? "1" : "0");

                if (probe.Card == null)
                {
                    continue;
                }

                RedirectProbeToSlot(
                    probe,
                    probe.TrackedSlot,
                    duration,
                    probe.Card.Uid);
            }
        }

        private bool RedirectProbeToSlot(DealFlightProbe probe, int toSlot, float sourceTime, int uid)
        {
            probe.TrackedSlot = toSlot;
            if (probe.Handle != null)
            {
                probe.Handle.TrackedSlot = toSlot;
            }

            if (probe.VisualAimSlot == toSlot)
            {
                TraceProbe(probe, "l2RedirectSkip", uid, "toSlot", toSlot.ToString(), "reason", "sameAim");
                return true;
            }

            if (!_host.TryGetExploreAnchorPosition(toSlot, out var targetWorld))
            {
                return true;
            }

            var duration = sourceTime > 0f
                ? sourceTime
                : (_host.LayoutSettings != null ? _host.LayoutSettings.moveDuration : 0.35f);
            probe.SourceTime = duration;
            probe.VisualAimSlot = toSlot;
            LogRedirectLatency(probe, uid);
            SlotFrameConvergence.RedirectVisualToWorld(probe.Card, targetWorld, duration);
            TraceProbe(probe, "l2Redirect", uid, "toSlot", toSlot.ToString());
            return true;
        }

        private async UniTaskVoid RunExploreProbeAsync(DealFlightProbe probe)
        {
            var outcome = "ok";
            try
            {
                var settings = Settings;
                var token = _destroyToken;
                if (token.IsCancellationRequested)
                {
                    outcome = "cancel";
                    return;
                }

                var elapsed = Time.time - _lastExploreStartTime;
                if (elapsed < settings.exploreDealInterval)
                {
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(settings.exploreDealInterval - elapsed),
                        cancellationToken: token);
                }

                _lastExploreStartTime = Time.time;

                var deck = CardEntityLifecycleHook.DeckOrNull();
                if (deck == null || !deck.TryWithdrawFirstCard(out var card, out var ripple))
                {
                    outcome = "withdrawFail";
                    TraceProbe(probe, "withdrawFail", -1);
                    return;
                }

                probe.Card = card;
                probe.Handle = new DealFlightHandle(card.Uid, probe.TrackedSlot, DealFlightKind.Explore);
                TraceProbe(probe, "withdraw", card.Uid);

                if (ripple != null && ripple.Count > 0)
                {
                    CardDeckTween.MoveRippleAsync(ripple, deck.LayoutSettings.moveDuration).Forget();
                }

                var launchPos = card.Transform.position;
                // 等待期间 OnRingShifted 可能已改 TrackedSlot：起飞瞄准当前追踪格。
                var visualSlot = probe.TrackedSlot;
                if (!_host.TryGetExploreAnchorPosition(visualSlot, out var targetPos))
                {
                    targetPos = launchPos;
                }

                var pendingNet = CountRingDistance(probe.BirthSlot, visualSlot);
                probe.VisualAimSlot = visualSlot;
                probe.PendingNetAtLaunch = pendingNet;
                var context = new DealFlightContext(
                    _host.IsFieldBusy,
                    ActiveCount,
                    pendingNet,
                    visualSlot);
                probe.Budget = DealSettleBudget.Compute(launchPos, targetPos, context, settings);
                probe.LaunchPos = launchPos;
                probe.SourceTime = probe.Budget.Total;
                probe.LinkedCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                TraceDealFlightBegin(probe);
                EmitAimAnomalyIfNeeded(probe, card.Uid);

                TraceProbe(probe, "flightStart", card.Uid);
                probe.FlightBeginUnscaledTime = Time.unscaledTime;
                SlotFrameConvergence.BeginDealFromLaunch(
                    card,
                    launchPos,
                    targetPos,
                    probe.SourceTime);
                CardEdgeDustFx.BeginTrail(card);
                try
                {
                    await WaitProbeConvergenceAsync(probe, probe.LinkedCts.Token);
                }
                finally
                {
                    CardEdgeDustFx.EndTrail(card);
                }
                TraceProbe(probe, "flightEnd", card.Uid);

                if (token.IsCancellationRequested || probe.Card == null)
                {
                    outcome = "cancel";
                    RollbackCard(probe.Card);
                    probe.Card = null;
                    return;
                }

                if (!_host.IsPlaceable(probe.TrackedSlot))
                {
                    outcome = "placeFail";
                    TraceProbe(probe, "placeFail", probe.Card.Uid);
                    RollbackCard(probe.Card);
                    probe.Card = null;
                    return;
                }

                var settleSlot = ResolveSettleSlot(probe);
                if (!_host.TryGetExploreAnchorPosition(settleSlot, out var settlePos))
                {
                    settlePos = targetPos;
                }

                SlotFrameConvergence.SnapHome(probe.Card, settlePos, "DealFlight.ExploreSettle", probe.Card.Uid);
                CardEdgeDustFx.PlayPlace(probe.Card);

                // 先卸 in-flight，再落格认领（ADR-0023：落地才登记；IsDealInFlight 会拒领）。
                _exploreByBirthSlot.Remove(probe.BirthSlot);
                if (!_host.PlaceForExplore(probe.TrackedSlot, probe.Card))
                {
                    outcome = "placeFail";
                    TraceProbe(probe, "placeFail", probe.Card.Uid);
                    RollbackCard(probe.Card);
                    probe.Card = null;
                    return;
                }

                TraceProbe(probe, "placeOk", card.Uid);
                probe.Handle.Complete(true);
                probe.Card = null;
            }
            catch (OperationCanceledException)
            {
                outcome = "cancel";
                RollbackCard(probe.Card);
                probe.Card = null;
            }
            finally
            {
                _exploreByBirthSlot.Remove(probe.BirthSlot);
                probe.LinkedCts?.Dispose();
                if (!probe.ChoreoClosedEagerly)
                {
                    TraceDealFlightEnd(probe, outcome);
                    ChoreoTraceSink.SafeEndChoreo(outcome);
                }
            }
        }

        private async UniTaskVoid RunDrainProbeAsync(DealFlightProbe probe)
        {
            var outcome = "ok";
            var uid = probe.Card?.Uid ?? -1;
            try
            {
                if (_destroyToken.IsCancellationRequested || probe.Card?.Transform == null)
                {
                    outcome = "cancel";
                    probe.Handle.Complete(false);
                    return;
                }

                var aimSlot = probe.VisualAimSlot > 0 ? probe.VisualAimSlot : probe.TrackedSlot;
                if (!_host.TryGetExploreAnchorPosition(aimSlot, out var slotPos))
                {
                    slotPos = probe.LaunchPos;
                }

                probe.LinkedCts = CancellationTokenSource.CreateLinkedTokenSource(_destroyToken);
                probe.FlightBeginUnscaledTime = Time.unscaledTime;
                SlotFrameConvergence.BeginDealFromLaunch(
                    probe.Card,
                    probe.LaunchPos,
                    slotPos,
                    probe.SourceTime);
                TraceProbe(probe, "flightStart", uid);
                CardEdgeDustFx.BeginTrail(probe.Card);
                try
                {
                    await WaitProbeConvergenceAsync(probe, probe.LinkedCts.Token);
                }
                finally
                {
                    CardEdgeDustFx.EndTrail(probe.Card);
                }
                TraceProbe(probe, "flightEnd", uid);

                if (_destroyToken.IsCancellationRequested || probe.Card?.Transform == null)
                {
                    outcome = "cancel";
                    probe.Handle.Complete(false);
                    return;
                }

                var settleSlot = ResolveSettleSlot(probe);
                if (!_host.TryGetExploreAnchorPosition(settleSlot, out var settlePos))
                {
                    settlePos = slotPos;
                }

                SlotFrameConvergence.SnapHome(probe.Card, settlePos, "DealFlight.DrainSettle", uid);
                CardEdgeDustFx.PlayPlace(probe.Card);
                CardEntityLifecycleHook.CardsOrNull()?.RefreshDisplayMode(probe.Card);

                // 开局/Drain 发牌：起飞时已 ReleaseClaim，落地后须回登记，否则场地面点击全走「未认领→Explore」。
                _drainByUid.Remove(uid);
                _host.NotifyDealFlightLanded(probe.Card, settleSlot);
                probe.Handle.Complete(true);
            }
            catch (OperationCanceledException)
            {
                outcome = "cancel";
                probe.Handle.Complete(false);
            }
            finally
            {
                if (uid > 0)
                {
                    _drainByUid.Remove(uid);
                }

                probe.LinkedCts?.Dispose();
                if (probe.Budget != null && probe.Budget.IsExhausted && outcome == "ok")
                {
                    ChoreoTraceSink.SafeEmitAnomaly(
                        "DealFlightBudgetExhausted",
                        uid,
                        "slot=" + probe.TrackedSlot);
                }

                if (!probe.ChoreoClosedEagerly)
                {
                    TraceDealFlightEnd(probe, outcome);
                    ChoreoTraceSink.SafeEndChoreo(outcome);
                }
            }
        }

        private async UniTask WaitProbeConvergenceAsync(DealFlightProbe probe, CancellationToken token)
        {
            if (probe.Card == null
                || probe.Card.Transform == null
                || !SlotFrameConvergence.TryGetDriver(probe.Card, out var driver))
            {
                return;
            }

            var tracked = probe.TrackedSlot;
            while (!token.IsCancellationRequested)
            {
                if (probe.Card == null || probe.Card.Transform == null)
                {
                    return;
                }

                if (probe.TrackedSlot != tracked)
                {
                    tracked = probe.TrackedSlot;
                    RedirectProbeToSlot(
                        probe,
                        tracked,
                        probe.SourceTime > 0f
                            ? probe.SourceTime
                            : (_host.LayoutSettings != null ? _host.LayoutSettings.moveDuration : 0.35f),
                        probe.Card.Uid);
                }

                if (!driver.IsActive)
                {
                    // Redirect 可能刚结束又被环移改槽：再确认目标格。
                    if (probe.TrackedSlot == tracked)
                    {
                        return;
                    }

                    continue;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, token);
                probe.Budget?.Consume(Time.deltaTime);
            }
        }

        private static int ResolveSettleSlot(DealFlightProbe probe)
        {
            // Tracked 已跟上环移时用 Tracked；否则保留预解视觉瞄准，避免 Snap 回 birth。
            if (probe.VisualAimSlot > 0
                && probe.TrackedSlot == probe.BirthSlot
                && probe.VisualAimSlot != probe.BirthSlot)
            {
                return probe.VisualAimSlot;
            }

            return probe.TrackedSlot > 0 ? probe.TrackedSlot : probe.VisualAimSlot;
        }

        private static int CountRingDistance(int fromSlot, int toSlot)
        {
            if (fromSlot == toSlot
                || !GroundSlotTopology.IsOuterRing(fromSlot)
                || !GroundSlotTopology.IsOuterRing(toSlot))
            {
                return 0;
            }

            var steps = 0;
            var slot = fromSlot;
            for (var i = 0; i < GroundSlotTopology.ClockwiseRing.Count; i++)
            {
                if (slot == toSlot)
                {
                    return steps;
                }

                slot = GroundSlotTopology.GetClockwiseRingTargetSlot(slot, clockwise: true);
                steps++;
            }

            return 0;
        }

        private static void EmitAimAnomalyIfNeeded(DealFlightProbe probe, int uid)
        {
            if (probe.PendingNetAtLaunch <= 0)
            {
                return;
            }

            if (probe.VisualAimSlot == probe.BirthSlot)
            {
                ChoreoTraceSink.SafeEmitAnomaly(
                    "DealAimedBirthWithPendingRotate",
                    uid,
                    "birth=" + probe.BirthSlot
                    + ";visual=" + probe.VisualAimSlot
                    + ";pendingNet=" + probe.PendingNetAtLaunch);
            }
        }

        private static void LogRedirectLatency(DealFlightProbe probe, int uid)
        {
            if (probe.LoggedFirstRedirect || probe.FlightBeginUnscaledTime <= 0f)
            {
                return;
            }

            probe.LoggedFirstRedirect = true;
            var latencyMs = (Time.unscaledTime - probe.FlightBeginUnscaledTime) * 1000f;
            TraceProbe(
                probe,
                "redirectLatency",
                uid,
                "latencyMs", latencyMs.ToString("F0"));
        }

        private static void CancelProbe(DealFlightProbe probe, bool rollback)
        {
            probe.LinkedCts?.Cancel();
            probe.LinkedCts?.Dispose();
            if (rollback)
            {
                RollbackCard(probe.Card);
            }

            probe.Handle?.Complete(false);
        }

        private static void RollbackCard(ManagedCard card)
        {
            if (card == null)
            {
                return;
            }

            SlotFrameConvergence.SanitizeForSanctuary(card, "DealFlight.Rollback.Sanitize");

            var deck = CardEntityLifecycleHook.DeckOrNull();
            if (deck != null && deck.LaunchReturnFieldCardToDeck(card))
            {
                return;
            }

            // 引用身份保护：禁止仅凭可复用 uid 释放新实体。
            CardEntityLifecycleHook.CardsOrNull()?.Release(card, "DealFlight.Rollback");
        }

        private static void TraceProbe(
            DealFlightProbe probe,
            string phase,
            int uid,
            params string[] extraPairs)
        {
            ChoreoTraceSink.SafeExploreTrace(
                uid,
                phase,
                probe.BirthSlot,
                probe.TrackedSlot,
                extraPairs);
        }

        private static void TraceDealFlightBegin(DealFlightProbe probe)
        {
            var uid = probe.Card?.Uid ?? probe.Handle?.Uid ?? -1;
            ChoreoTraceSink.SafeExploreTrace(
                uid,
                "dealFlightBegin",
                probe.BirthSlot,
                probe.TrackedSlot,
                "kind", probe.Kind.ToString(),
                "budgetTotal", probe.Budget?.Total.ToString("F3") ?? "0",
                "visualTargetSlot", probe.VisualAimSlot.ToString(),
                "pendingNet", probe.PendingNetAtLaunch.ToString());
        }

        private static void TraceDealFlightEnd(DealFlightProbe probe, string outcome)
        {
            var uid = probe.Card?.Uid ?? probe.Handle?.Uid ?? -1;
            ChoreoTraceSink.SafeExploreTrace(
                uid,
                "dealFlightEnd",
                probe.BirthSlot,
                probe.TrackedSlot,
                "kind", probe.Kind.ToString(),
                "outcome", outcome,
                "budgetConsumed", probe.Budget?.Consumed.ToString("F3") ?? "0",
                "visualTargetSlot", probe.VisualAimSlot.ToString());
            // #45：发牌发起→落位/中止成对 settle（与 dealFlightBegin 同 choreoSeqId + chainId）。
            ChoreoTraceSink.SafeExploreTrace(
                uid,
                "deal.settled",
                probe.BirthSlot,
                probe.TrackedSlot,
                "kind", probe.Kind.ToString(),
                "outcome", outcome ?? string.Empty,
                "visualTargetSlot", probe.VisualAimSlot.ToString());
        }
    }
}
