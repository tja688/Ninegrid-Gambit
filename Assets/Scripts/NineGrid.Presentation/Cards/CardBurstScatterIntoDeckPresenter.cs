using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using NineGrid.Cards.Convergence;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 通用炸牌入组：从原点并行飞到圆周散点 → 同步停稳 → 集体上飞入组。
    /// </summary>
    public static class CardBurstScatterIntoDeckPresenter
    {
        public static async UniTask PresentAsync(
            IReadOnlyList<ManagedCard> cards,
            Vector3 origin,
            float radius,
            float burstDuration,
            float burstHoldDuration,
            float fieldExitDuration,
            CardDeckManagerSingleton deckManager,
            CancellationToken cancellationToken = default,
            float? phaseRadians = null)
        {
            if (cards == null || cards.Count == 0 || deckManager == null)
            {
                return;
            }

            var cardManager = CardEntityLifecycleHook.CardsOrNull();
            var active = new List<ManagedCard>(cards.Count);
            for (var i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                if (card?.Transform == null)
                {
                    continue;
                }

                if (cardManager != null)
                {
                    cardManager.SetDisplayMode(card, CardDisplayMode.GroundCardMode);
                    cardManager.RefreshDisplayMode(card);
                    cardManager.EnsureComplexDomainStack(card);
                }

                PlaceAtOrigin(card, origin);
                FlightSortingChannel.Raise(card);
                active.Add(card);
            }

            if (active.Count == 0)
            {
                return;
            }

            var phase = phaseRadians ?? BurstScatterPointSampler.RandomPhaseRadians();
            var targets = BurstScatterPointSampler.SampleOnCircle(origin, radius, active.Count, phase);
            var duration = Mathf.Max(0.01f, burstDuration);
            var scatterTasks = new List<UniTask>(active.Count);
            for (var i = 0; i < active.Count; i++)
            {
                scatterTasks.Add(MoveToPointAsync(active[i], targets[i], duration, cancellationToken));
            }

            await UniTask.WhenAll(scatterTasks);

            // 中途被 Release（如 BounceFan teardown）时剔除，避免后续入组挂死。
            active.RemoveAll(c => c?.Transform == null);

            if (active.Count == 0)
            {
                return;
            }

            if (burstHoldDuration > 0f)
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(burstHoldDuration),
                    cancellationToken: cancellationToken);
            }

            for (var i = 0; i < active.Count; i++)
            {
                var card = active[i];
                if (card?.Transform == null)
                {
                    continue;
                }

                if (deckManager.ContainsUid(card.Uid))
                {
                    deckManager.TryDetachByUid(card.Uid, out _);
                }

                if (!deckManager.LaunchReturnFieldCardToDeck(card))
                {
                    Debug.LogWarning(
                        $"[BurstScatterIntoDeck] LaunchReturnFieldCardToDeck 失败 uid={card.Uid} mode={card.DisplayMode}");
                }
            }

            if (fieldExitDuration > 0f)
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(fieldExitDuration),
                    cancellationToken: cancellationToken);
            }

            for (var i = 0; i < active.Count; i++)
            {
                var card = active[i];
                if (card?.Transform == null)
                {
                    continue;
                }

                FlightSortingChannel.Restore(card);
            }
        }

        private static void PlaceAtOrigin(ManagedCard card, Vector3 origin)
        {
            if (card?.Transform == null)
            {
                return;
            }

            if (!SlotFrameConvergence.TryEnsureInfrastructure(
                    card,
                    out _,
                    out _,
                    "BurstScatter.PlaceOrigin"))
            {
                card.Transform.position = origin;
                return;
            }

            SlotFrameConvergence.SnapHome(
                card,
                origin,
                "BurstScatter.PlaceOrigin",
                card.Uid);
        }

        private static async UniTask MoveToPointAsync(
            ManagedCard card,
            Vector3 target,
            float duration,
            CancellationToken cancellationToken)
        {
            if (card?.Transform == null)
            {
                return;
            }

            if (EffectFrameConvergence.TryGetDriver(card, out _))
            {
                await EffectFrameConvergence.ConvergeVisualToWorldAsync(
                    card,
                    target,
                    duration,
                    cancellationToken,
                    parkRootOnComplete: true);
                return;
            }

            if (card.Transform == null)
            {
                return;
            }

            CardDeckTween.KillMotion(card.Transform, "BurstScatter.Move", card.Uid);
            var tween = card.Transform
                .DOMove(target, duration)
                .SetEase(Ease.OutCubic)
                .SetLink(card.Transform.gameObject, LinkBehaviour.KillOnDestroy);
            await AwaitTweenAsync(tween, cancellationToken);
        }

        private static async UniTask AwaitTweenAsync(Tween tween, CancellationToken cancellationToken)
        {
            if (tween == null || !tween.IsActive())
            {
                return;
            }

            var tcs = new UniTaskCompletionSource();
            tween.OnComplete(() => tcs.TrySetResult());
            tween.OnKill(() => tcs.TrySetResult());
            await tcs.Task.AttachExternalCancellation(cancellationToken);
        }
    }
}
