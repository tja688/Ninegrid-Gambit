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
    /// 通用炸牌入组：不依赖任何宿主位置，在 GroundPanel 范围内分散落点；
    /// 逐卡由极小缩放弹性放大出现（泡泡菜单同款 back.out 弹出 + 随机倾斜），先后错峰；
    /// 最后一张出现后停顿，集体回正并垂直上飞入组。
    /// </summary>
    public static class CardBurstScatterIntoDeckPresenter
    {
        /// <summary>出现前的最小缩放（相对基准，≈0 但避开零缩放渲染问题）。</summary>
        private const float PopStartScale = 0.02f;

        /// <summary>弹出起始倾角相对目标倾角的倍率（先更歪再回正，增加灵动感）。</summary>
        private const float TiltOvershootFactor = 1.7f;

        /// <summary>集体上飞前回正倾角的时长（秒）。</summary>
        private const float StraightenDuration = 0.16f;

        /// <summary>逐卡出现延迟的随机抖动（秒），泡泡菜单 staggerDelay 同款 ±0.05。</summary>
        private const float RandomDelayVariance = 0.05f;

        public static async UniTask PresentAsync(
            IReadOnlyList<ManagedCard> cards,
            GroundFieldLayoutSettings fieldLayout,
            CardDeckManagerSingleton deckManager,
            CancellationToken cancellationToken = default,
            float fieldToDeckDwellDuration = 0f)
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

                active.Add(card);
            }

            if (active.Count == 0)
            {
                return;
            }

            // 打散出场顺序：同批卡每次炸开的位置与先后都不同。
            ShuffleList(active);

            var layout = fieldLayout;
            var popDuration = layout != null ? Mathf.Max(0.01f, layout.burstScatterDuration) : 0.5f;
            var postPopHold = layout != null ? Mathf.Max(0f, layout.burstScatterHoldDuration) : 0.1f;
            var staggerDelay = layout != null ? Mathf.Max(0f, layout.burstScatterStaggerDelay) : 0.12f;
            var tiltAngle = layout != null ? Mathf.Max(0f, layout.burstScatterTiltAngle) : 8f;
            var padding = layout != null ? Mathf.Max(0f, layout.burstScatterPadding) : 1f;

            var rect = BurstScatterFieldBounds.ResolveWorldRect(padding);
            var points = BurstScatterPointSampler.SampleInRect(rect, active.Count);

            var popTasks = new List<UniTask>(active.Count);
            for (var i = 0; i < active.Count; i++)
            {
                var card = active[i];
                if (card?.Transform == null)
                {
                    continue;
                }

                PlaceAtPoint(card, points[i]);
                FlightSortingChannel.Raise(card);

                var t = card.Transform;
                CardDeckTween.KillMotion(t, "BurstScatter.Pop", card.Uid);

                var baseScale = t.localScale;
                if (baseScale.sqrMagnitude <= 0.0001f)
                {
                    baseScale = Vector3.one;
                }

                var tilt = UnityEngine.Random.Range(-tiltAngle, tiltAngle);
                var startTilt = tilt * TiltOvershootFactor;

                // 极小 + 更歪的起始姿态，随后弹性放大 + 回正到目标倾角。
                t.localScale = baseScale * PopStartScale;
                t.rotation = Quaternion.Euler(0f, 0f, startTilt);

                var delay = Mathf.Max(
                    0f,
                    i * staggerDelay + UnityEngine.Random.Range(-RandomDelayVariance, RandomDelayVariance));
                var scaleTween = t
                    .DOScale(baseScale, popDuration)
                    .SetDelay(delay)
                    .SetEase(Ease.OutBack)
                    .SetLink(t.gameObject, LinkBehaviour.KillOnDestroy);
                var rotateTween = t
                    .DORotateQuaternion(Quaternion.Euler(0f, 0f, tilt), popDuration)
                    .SetDelay(delay)
                    .SetEase(Ease.OutCubic)
                    .SetLink(t.gameObject, LinkBehaviour.KillOnDestroy);
                popTasks.Add(AwaitTweensAsync(scaleTween, rotateTween, cancellationToken));
            }

            // 等最后一张卡放大出现完毕。
            await UniTask.WhenAll(popTasks);

            // 中途被 Release（如 BounceFan teardown）时剔除，避免后续入组挂死。
            active.RemoveAll(c => c?.Transform == null);

            if (active.Count == 0)
            {
                return;
            }

            // 最后一张出现后停顿，再集体上飞。
            if (postPopHold > 0f)
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(postPopHold),
                    cancellationToken: cancellationToken);
            }

            // 集体回正：上飞前把倾斜收掉，保证入组时姿态与卡组一致。
            var straightenTasks = new List<UniTask>(active.Count);
            for (var i = 0; i < active.Count; i++)
            {
                var card = active[i];
                if (card?.Transform == null)
                {
                    continue;
                }

                var t = card.Transform;
                CardDeckTween.KillMotion(t, "BurstScatter.Straighten", card.Uid);
                var tween = t
                    .DORotateQuaternion(Quaternion.identity, StraightenDuration)
                    .SetEase(Ease.OutCubic)
                    .SetLink(t.gameObject, LinkBehaviour.KillOnDestroy);
                straightenTasks.Add(AwaitTweenAsync(tween, cancellationToken));
            }

            await UniTask.WhenAll(straightenTasks);

            var launchedUids = new List<int>(active.Count);
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
                    continue;
                }

                launchedUids.Add(card.Uid);
            }

            // 等 fieldExit + addAnchor + ripple 真正落地，不再只 Delay(fieldExit)。
            if (launchedUids.Count > 0)
            {
                await deckManager.WaitReturnsSettledAsync(launchedUids, cancellationToken);
            }

            if (fieldToDeckDwellDuration > 0f)
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(fieldToDeckDwellDuration),
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

        private static void ShuffleList<T>(List<T> list)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private static void PlaceAtPoint(ManagedCard card, Vector3 point)
        {
            if (card?.Transform == null)
            {
                return;
            }

            if (!SlotFrameConvergence.TryEnsureInfrastructure(
                    card,
                    out _,
                    out _,
                    "BurstScatter.PlacePoint"))
            {
                card.Transform.position = point;
                return;
            }

            SlotFrameConvergence.SnapHome(
                card,
                point,
                "BurstScatter.PlacePoint",
                card.Uid);
            // Park/Snap L2 后清 L3，避免视觉位偏离炸开落点。
            EffectFrameConvergence.SnapHome(card, "BurstScatter.PlacePoint");
        }

        private static async UniTask AwaitTweensAsync(
            Tween first,
            Tween second,
            CancellationToken cancellationToken)
        {
            await UniTask.WhenAll(
                AwaitTweenAsync(first, cancellationToken),
                AwaitTweenAsync(second, cancellationToken));
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
