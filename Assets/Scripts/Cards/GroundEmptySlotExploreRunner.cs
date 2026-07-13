using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 空牌位探求：每条空位独立异步追锚发牌，外圈旋转时迁移 trackedSlot。
    /// </summary>
    internal sealed class GroundEmptySlotExploreRunner
    {
        private sealed class EmptySlotProbe
        {
            public int BirthSlot;
            public int TrackedSlot;
            public ManagedCard Card;
        }

        private readonly GroundFieldManagerSingleton _field;
        private readonly CancellationToken _destroyToken;
        private readonly Dictionary<int, EmptySlotProbe> _activeByBirthSlot = new();
        private float _lastStartTime = float.NegativeInfinity;

        public GroundEmptySlotExploreRunner(GroundFieldManagerSingleton field, CancellationToken destroyToken)
        {
            _field = field;
            _destroyToken = destroyToken;
        }

        public void StartExplore(int birthSlot)
        {
            if (!_field.IsPlaceable(birthSlot) || _activeByBirthSlot.ContainsKey(birthSlot))
            {
                return;
            }

            var probe = new EmptySlotProbe
            {
                BirthSlot = birthSlot,
                TrackedSlot = birthSlot,
            };

            _activeByBirthSlot[birthSlot] = probe;
            ChoreoTraceSink.SafeBeginChoreo("explore", "birthSlot", birthSlot.ToString());
            TraceProbe(probe, "start", probe.Card?.Uid ?? -1);
            RunProbeAsync(probe).Forget();
        }

        public void OnRingShifted(bool clockwise)
        {
            foreach (var probe in _activeByBirthSlot.Values)
            {
                if (!GroundSlotTopology.IsOuterRing(probe.TrackedSlot))
                {
                    continue;
                }

                var prev = probe.TrackedSlot;
                probe.TrackedSlot = GroundSlotTopology.GetClockwiseRingTargetSlot(probe.TrackedSlot, clockwise);
                TraceProbe(
                    probe,
                    "ringShift",
                    probe.Card?.Uid ?? -1,
                    "prevSlot", prev.ToString(),
                    "clockwise", clockwise ? "1" : "0");
            }
        }

        public void CancelAll()
        {
            foreach (var pair in _activeByBirthSlot)
            {
                RollbackCard(pair.Value.Card);
            }

            _activeByBirthSlot.Clear();
        }

        private async UniTaskVoid RunProbeAsync(EmptySlotProbe probe)
        {
            var exploreOutcome = "ok";
            try
            {
                var settings = _field.LayoutSettings;
                var token = _destroyToken;

                var elapsed = Time.time - _lastStartTime;
                if (elapsed < settings.exploreDealInterval)
                {
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(settings.exploreDealInterval - elapsed),
                        cancellationToken: token);
                }

                _lastStartTime = Time.time;

                var deck = CardDeckManagerSingleton.Instance;
                if (deck == null || !deck.TryWithdrawFirstCard(out var card, out var ripple))
                {
                    exploreOutcome = "withdrawFail";
                    TraceProbe(probe, "withdrawFail", -1);
                    return;
                }

                TraceProbe(probe, "withdraw", card.Uid);
                probe.Card = card;
                if (ripple != null && ripple.Count > 0)
                {
                    CardDeckTween.MoveRippleAsync(ripple, deck.LayoutSettings.moveDuration).Forget();
                }

                TraceProbe(probe, "chaseStart", card.Uid);
                await CardDeckTween.ChaseAnchorAsync(
                    card.Transform,
                    () => _field.TryGetExploreAnchorPosition(probe.TrackedSlot, out var pos) ? pos : card.Transform.position,
                    settings.exploreChaseResponsiveness,
                    settings.exploreArriveThreshold,
                    token,
                    () => probe.Card != null && _field.IsPlaceable(probe.TrackedSlot),
                    settings.exploreChaseMaxStep,
                    trackedSlot: probe.TrackedSlot,
                    uid: card.Uid);
                TraceProbe(probe, "chaseEnd", card.Uid);

                if (token.IsCancellationRequested || probe.Card == null)
                {
                    exploreOutcome = "cancel";
                    TraceProbe(probe, "rollback", probe.Card?.Uid ?? -1, "reason", "cancelled");
                    RollbackCard(probe.Card);
                    probe.Card = null;
                    return;
                }

                if (!_field.IsPlaceable(probe.TrackedSlot))
                {
                    exploreOutcome = "placeFail";
                    Debug.LogWarning(
                        $"[GroundEmptySlotExplore] 目标格位已被占满，回滚卡组: slot={probe.TrackedSlot}");
                    TraceProbe(probe, "placeFail", probe.Card.Uid);
                    if (_field.IsBusy || CardDeckManagerSingleton.Instance?.IsBusy == true)
                    {
                        ChoreoTraceSink.SafeEmitAnomaly(
                            "ExplorePlaceWhileBusy",
                            probe.Card.Uid,
                            "slot=" + probe.TrackedSlot);
                    }

                    RollbackCard(probe.Card);
                    probe.Card = null;
                    return;
                }

                if (!_field.PlaceForExplore(probe.TrackedSlot, probe.Card))
                {
                    exploreOutcome = "placeFail";
                    Debug.LogWarning(
                        $"[GroundEmptySlotExplore] 就位放置失败，回滚卡组: slot={probe.TrackedSlot}");
                    TraceProbe(probe, "placeFail", probe.Card.Uid);
                    RollbackCard(probe.Card);
                    probe.Card = null;
                    return;
                }

                TraceProbe(probe, "placeOk", card.Uid);
                probe.Card = null;
            }
            catch (OperationCanceledException)
            {
                exploreOutcome = "cancel";
                TraceProbe(probe, "rollback", probe.Card?.Uid ?? -1, "reason", "cancelled");
                RollbackCard(probe.Card);
                probe.Card = null;
            }
            finally
            {
                _activeByBirthSlot.Remove(probe.BirthSlot);
                ChoreoTraceSink.SafeEndChoreo(exploreOutcome);
            }
        }

        private static void TraceProbe(
            EmptySlotProbe probe,
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

            CardManagerSingleton.Instance.Release(card.Uid, "Explore.Complete");
        }
    }
}
