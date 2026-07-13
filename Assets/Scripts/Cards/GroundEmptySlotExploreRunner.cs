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

                probe.TrackedSlot = GroundSlotTopology.GetClockwiseRingTargetSlot(probe.TrackedSlot, clockwise);
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
                    return;
                }

                probe.Card = card;
                if (ripple != null && ripple.Count > 0)
                {
                    CardDeckTween.MoveRippleAsync(ripple, deck.LayoutSettings.moveDuration).Forget();
                }

                await CardDeckTween.ChaseAnchorAsync(
                    card.Transform,
                    () => _field.TryGetExploreAnchorPosition(probe.TrackedSlot, out var pos) ? pos : card.Transform.position,
                    settings.exploreChaseResponsiveness,
                    settings.exploreArriveThreshold,
                    token,
                    () => probe.Card != null && _field.IsPlaceable(probe.TrackedSlot),
                    settings.exploreChaseMaxStep);

                if (token.IsCancellationRequested || probe.Card == null)
                {
                    RollbackCard(probe.Card);
                    probe.Card = null;
                    return;
                }

                if (!_field.IsPlaceable(probe.TrackedSlot))
                {
                    Debug.LogWarning(
                        $"[GroundEmptySlotExplore] 目标格位已被占满，回滚卡组: slot={probe.TrackedSlot}");
                    RollbackCard(probe.Card);
                    probe.Card = null;
                    return;
                }

                if (!_field.PlaceForExplore(probe.TrackedSlot, probe.Card))
                {
                    Debug.LogWarning(
                        $"[GroundEmptySlotExplore] 就位放置失败，回滚卡组: slot={probe.TrackedSlot}");
                    RollbackCard(probe.Card);
                    probe.Card = null;
                    return;
                }

                probe.Card = null;
            }
            catch (OperationCanceledException)
            {
                RollbackCard(probe.Card);
                probe.Card = null;
            }
            finally
            {
                _activeByBirthSlot.Remove(probe.BirthSlot);
            }
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
