using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 统一飞牌协调器：Drain 补牌 + 空位 Explore 探求，二阶贝塞尔追踪 + 动态就位预算。
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

        public bool TryReleaseFlightForHop(int uid, out Vector3 currentPosition)
        {
            currentPosition = default;
            if (!_drainByUid.TryGetValue(uid, out var probe) || probe.Card?.Transform == null)
            {
                return false;
            }

            currentPosition = probe.Card.Transform.position;
            probe.LinkedCts?.Cancel();
            probe.LinkedCts?.Dispose();
            probe.LinkedCts = null;
            _drainByUid.Remove(uid);
            probe.Handle.Complete(true);
            ChoreoTraceSink.SafeEmitAnomaly(
                "DealFlightRotateHopTakeover",
                uid,
                "slot=" + probe.TrackedSlot);
            TraceProbe(probe, "hopTakeover", uid);
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

        private static void ShiftProbes(IEnumerable<DealFlightProbe> probes, bool clockwise)
        {
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
                probe.LinkedCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                TraceDealFlightBegin(probe);

                TraceProbe(probe, "flightStart", card.Uid);
                await RunBezierFlightAsync(probe, settings, probe.LinkedCts.Token);
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
                var settings = Settings;
                probe.LinkedCts = CancellationTokenSource.CreateLinkedTokenSource(_destroyToken);
                await RunBezierFlightAsync(probe, settings, probe.LinkedCts.Token);

                if (_destroyToken.IsCancellationRequested || probe.Card?.Transform == null)
                {
                    outcome = "cancel";
                    probe.Handle.Complete(false);
                    return;
                }

                if (_field.TryGetExploreAnchorPosition(probe.TrackedSlot, out var anchorPos))
                {
                    probe.Card.Transform.position = anchorPos;
                }

                CardManagerSingleton.Instance?.RefreshDisplayMode(probe.Card);
                probe.Handle.Complete(true);
            }
            catch (OperationCanceledException)
            {
                outcome = "hopTakeover";
                probe.Handle.Complete(true);
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

        private async UniTask RunBezierFlightAsync(
            DealFlightProbe probe,
            DealFlightLayoutSettings settings,
            CancellationToken token)
        {
            if (probe.Card?.Transform == null)
            {
                return;
            }

            var uid = probe.Card.Uid;
            await CardDeckTween.TrackQuadraticBezierAnchorAsync(
                probe.Card.Transform,
                probe.LaunchPos,
                () => _field.TryGetExploreAnchorPosition(probe.TrackedSlot, out var pos)
                    ? pos
                    : probe.Card.Transform.position,
                settings,
                probe.Budget,
                token,
                () => probe.Card != null
                      && (probe.Kind == DealFlightKind.Drain
                          || _field.IsPlaceable(probe.TrackedSlot)),
                probe.TrackedSlot,
                uid,
                (jumpSqr, penalty) =>
                {
                    ChoreoTraceSink.SafeExploreTrace(
                        uid,
                        "anchorJump",
                        probe.BirthSlot,
                        probe.TrackedSlot,
                        "jumpSqr", jumpSqr.ToString("F3"),
                        "penalty", penalty.ToString("F3"));
                });
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

            CardDeckTween.KillMotion(card.Transform);

            var deck = CardDeckManagerSingleton.Instance;
            if (deck != null && deck.LaunchReturnFieldCardToDeck(card, 0))
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
