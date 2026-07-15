using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Cards.Convergence;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 统一飞牌协调器：Drain / Explore 补牌均走 L2 五次收敛；换格 = Redirect，无替身、无握手。
    /// </summary>
    internal sealed class GroundSlotDealFlightCoordinator
    {
        private sealed class DealFlightProbe
        {
            public int BirthSlot;
            public int TrackedSlot;
            public ManagedCard Card;
            public DealSettleBudget Budget;
            public DealFlightKind Kind;
            public DealFlightHandle Handle;
            public Vector3 LaunchPos;
            public float SourceTime;
            public CancellationTokenSource LinkedCts;
        }

        private readonly GroundFieldManagerSingleton _field;
        private readonly CancellationToken _destroyToken;
        private readonly Dictionary<int, DealFlightProbe> _exploreByBirthSlot = new();
        private readonly Dictionary<int, DealFlightProbe> _drainByUid = new();
        private float _lastExploreStartTime = float.NegativeInfinity;

        public GroundSlotDealFlightCoordinator(
            GroundFieldManagerSingleton field,
            CancellationToken destroyToken)
        {
            _field = field;
            _destroyToken = destroyToken;
        }

        public int ActiveCount => _exploreByBirthSlot.Count + _drainByUid.Count;

        public DealFlightLayoutSettings Settings =>
            _field?.LayoutSettings?.dealFlight ?? new DealFlightLayoutSettings();

        public void StartExplore(int birthSlot)
        {
            if (_field == null
                || !_field.IsPlaceable(birthSlot)
                || _exploreByBirthSlot.ContainsKey(birthSlot))
            {
                return;
            }

            var probe = new DealFlightProbe
            {
                BirthSlot = birthSlot,
                TrackedSlot = birthSlot,
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
            int targetSlot,
            Vector3 launchPos,
            DealFlightContext context)
        {
            if (_field == null || card == null || card.Uid <= 0)
            {
                return null;
            }

            if (_drainByUid.ContainsKey(card.Uid))
            {
                return _drainByUid[card.Uid].Handle;
            }

            if (!_field.TryGetExploreAnchorPosition(targetSlot, out var targetPos))
            {
                targetPos = launchPos;
            }

            var settings = Settings;
            var budget = DealSettleBudget.Compute(launchPos, targetPos, context, settings);
            var probe = new DealFlightProbe
            {
                BirthSlot = targetSlot,
                TrackedSlot = targetSlot,
                Card = card,
                Budget = budget,
                Kind = DealFlightKind.Drain,
                LaunchPos = launchPos,
                SourceTime = budget.Total,
                Handle = new DealFlightHandle(card.Uid, targetSlot, DealFlightKind.Drain),
            };

            _drainByUid[card.Uid] = probe;
            ChoreoTraceSink.SafeBeginChoreo("dealFlight", "kind", "drain", "uid", card.Uid.ToString());
            TraceDealFlightBegin(probe);
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
            return uid > 0 && _drainByUid.ContainsKey(uid);
        }

        /// <summary>
        /// 大盘 hop/换格接管：更新追踪格并 Redirect L2 目标。无暂停/恢复握手。
        /// </summary>
        public bool TryRedirectFlightToSlot(int uid, int toSlot, float sourceTime)
        {
            if (!_drainByUid.TryGetValue(uid, out var probe) || probe.Card == null)
            {
                return false;
            }

            probe.TrackedSlot = toSlot;
            if (probe.Handle != null)
            {
                probe.Handle.TrackedSlot = toSlot;
            }

            if (!_field.TryGetExploreAnchorPosition(toSlot, out var targetWorld))
            {
                return true;
            }

            var duration = sourceTime > 0f
                ? sourceTime
                : (_field.LayoutSettings != null ? _field.LayoutSettings.moveDuration : 0.35f);
            probe.SourceTime = duration;
            SlotFrameConvergence.RedirectVisualToWorld(probe.Card, targetWorld, duration);
            TraceProbe(probe, "l2Redirect", uid, "toSlot", toSlot.ToString());
            return true;
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

        private void ShiftProbes(IEnumerable<DealFlightProbe> probes, bool clockwise)
        {
            var duration = _field.LayoutSettings != null ? _field.LayoutSettings.moveDuration : 0.35f;
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

                if (probe.Card == null
                    || !_field.TryGetExploreAnchorPosition(probe.TrackedSlot, out var targetWorld))
                {
                    continue;
                }

                probe.SourceTime = duration;
                SlotFrameConvergence.RedirectVisualToWorld(probe.Card, targetWorld, duration);
            }
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

                var deck = CardDeckManagerSingleton.Instance;
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
                if (!_field.TryGetExploreAnchorPosition(probe.TrackedSlot, out var targetPos))
                {
                    targetPos = launchPos;
                }

                var context = new DealFlightContext(
                    _field.IsFieldBusy,
                    ActiveCount,
                    pendingRotateSteps: 0);
                probe.Budget = DealSettleBudget.Compute(launchPos, targetPos, context, settings);
                probe.LaunchPos = launchPos;
                probe.SourceTime = probe.Budget.Total;
                probe.LinkedCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                TraceDealFlightBegin(probe);

                TraceProbe(probe, "flightStart", card.Uid);
                SlotFrameConvergence.BeginDealFromLaunch(
                    card,
                    launchPos,
                    targetPos,
                    probe.SourceTime);
                await WaitProbeConvergenceAsync(probe, probe.LinkedCts.Token);
                TraceProbe(probe, "flightEnd", card.Uid);

                if (token.IsCancellationRequested || probe.Card == null)
                {
                    outcome = "cancel";
                    RollbackCard(probe.Card);
                    probe.Card = null;
                    return;
                }

                if (!_field.IsPlaceable(probe.TrackedSlot))
                {
                    outcome = "placeFail";
                    TraceProbe(probe, "placeFail", probe.Card.Uid);
                    RollbackCard(probe.Card);
                    probe.Card = null;
                    return;
                }

                if (!_field.TryGetExploreAnchorPosition(probe.TrackedSlot, out var settlePos))
                {
                    settlePos = targetPos;
                }

                SlotFrameConvergence.SnapHome(probe.Card, settlePos, "DealFlight.ExploreSettle", probe.Card.Uid);

                if (!_field.PlaceForExplore(probe.TrackedSlot, probe.Card))
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
                TraceDealFlightEnd(probe, outcome);
                ChoreoTraceSink.SafeEndChoreo(outcome);
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

                if (!_field.TryGetExploreAnchorPosition(probe.TrackedSlot, out var slotPos))
                {
                    slotPos = probe.LaunchPos;
                }

                probe.LinkedCts = CancellationTokenSource.CreateLinkedTokenSource(_destroyToken);
                SlotFrameConvergence.BeginDealFromLaunch(
                    probe.Card,
                    probe.LaunchPos,
                    slotPos,
                    probe.SourceTime);
                TraceProbe(probe, "flightStart", uid);
                await WaitProbeConvergenceAsync(probe, probe.LinkedCts.Token);
                TraceProbe(probe, "flightEnd", uid);

                if (_destroyToken.IsCancellationRequested || probe.Card?.Transform == null)
                {
                    outcome = "cancel";
                    probe.Handle.Complete(false);
                    return;
                }

                if (!_field.TryGetExploreAnchorPosition(probe.TrackedSlot, out var settlePos))
                {
                    settlePos = slotPos;
                }

                SlotFrameConvergence.SnapHome(probe.Card, settlePos, "DealFlight.DrainSettle", uid);
                CardManagerSingleton.Instance?.RefreshDisplayMode(probe.Card);
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

                TraceDealFlightEnd(probe, outcome);
                ChoreoTraceSink.SafeEndChoreo(outcome);
            }
        }

        private async UniTask WaitProbeConvergenceAsync(DealFlightProbe probe, CancellationToken token)
        {
            if (probe.Card == null || !SlotFrameConvergence.TryGetDriver(probe.Card, out var driver))
            {
                return;
            }

            var tracked = probe.TrackedSlot;
            while (!token.IsCancellationRequested)
            {
                if (probe.TrackedSlot != tracked)
                {
                    tracked = probe.TrackedSlot;
                    if (_field.TryGetExploreAnchorPosition(tracked, out var targetWorld))
                    {
                        var duration = probe.SourceTime > 0f
                            ? probe.SourceTime
                            : (_field.LayoutSettings != null ? _field.LayoutSettings.moveDuration : 0.35f);
                        SlotFrameConvergence.RedirectVisualToWorld(probe.Card, targetWorld, duration);
                    }
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
            }
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

            if (SlotFrameConvergence.TryGetDriver(card, out var driver))
            {
                driver.Stop();
            }

            CardDeckTween.KillMotion(card.Transform);

            var deck = CardDeckManagerSingleton.Instance;
            if (deck != null && deck.LaunchReturnFieldCardToDeck(card))
            {
                return;
            }

            CardManagerSingleton.Instance?.Release(card.Uid, "DealFlight.Rollback");
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
                "budgetTotal", probe.Budget?.Total.ToString("F3") ?? "0");
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
                "budgetConsumed", probe.Budget?.Consumed.ToString("F3") ?? "0");
        }
    }
}
