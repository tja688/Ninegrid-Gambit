using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 牌组相关 DOTween 缓动工具。
    /// </summary>
    public static class CardDeckTween
    {
        public static void KillMotion(Transform target, string site = null, int uid = 0)
        {
            if (target == null)
            {
                return;
            }

            var resolvedUid = uid;
            if (resolvedUid <= 0)
            {
                CardManagerSingleton.Instance?.TryResolveUid(target, out resolvedUid);
            }

            if (resolvedUid > 0 && DOTween.IsTweening(target))
            {
                CardPresentationProbe.MotionEnd(
                    resolvedUid,
                    motionId: 0,
                    at: target.position,
                    endHow: "kill",
                    site: site ?? "DeckTween.Kill",
                    killedBySite: site ?? "DeckTween.Kill");
            }

            target.DOKill(complete: false);
        }

        public static Tween MoveToWorld(
            Transform target,
            Vector3 worldPosition,
            float duration,
            float delay = 0f,
            TweenCallback onComplete = null,
            int uid = 0,
            string reason = null)
        {
            if (target == null)
            {
                return null;
            }

            var resolvedUid = uid;
            if (resolvedUid <= 0)
            {
                CardManagerSingleton.Instance?.TryResolveUid(target, out resolvedUid);
            }

            KillMotion(target, "DeckTween.Move", resolvedUid);

            var motionId = 0;
            var from = target.position;
            if (resolvedUid > 0)
            {
                motionId = CardPresentationProbe.NextMotionId();
                CardPresentationProbe.MotionBegin(
                    resolvedUid,
                    motionId,
                    from,
                    worldPosition,
                    "DeckTween.Move",
                    reason: reason,
                    expectMs: duration);
            }

            var tween = target
                .DOMove(worldPosition, duration)
                .SetDelay(delay)
                .SetEase(Ease.OutCubic)
                .SetLink(target.gameObject, LinkBehaviour.KillOnDestroy);

            tween.OnComplete(() =>
            {
                if (target == null)
                {
                    return;
                }

                if (resolvedUid > 0)
                {
                    CardPresentationProbe.MotionEnd(
                        resolvedUid,
                        motionId,
                        target.position,
                        "complete",
                        "DeckTween.Move");
                }

                onComplete?.Invoke();
            });

            return tween;
        }

        public static async UniTask MoveRippleAsync(
            IReadOnlyList<CardDeckRippleMove> moves,
            float duration,
            CancellationToken cancellationToken = default)
        {
            if (moves == null || moves.Count == 0)
            {
                return;
            }

            var maxEndTime = 0f;
            for (var i = 0; i < moves.Count; i++)
            {
                var move = moves[i];
                if (move.Card?.Transform == null)
                {
                    continue;
                }

                MoveToWorld(move.Card.Transform, move.TargetPosition, duration, move.Delay);
                maxEndTime = Mathf.Max(maxEndTime, move.Delay + duration);
            }

            await UniTask.Delay(
                System.TimeSpan.FromSeconds(maxEndTime),
                cancellationToken: cancellationToken);
        }

        public static async UniTask MoveAllAsync(
            IReadOnlyList<(Transform transform, Vector3 target, float delay)> moves,
            float duration,
            CancellationToken cancellationToken = default)
        {
            if (moves == null || moves.Count == 0)
            {
                return;
            }

            var maxEndTime = 0f;
            for (var i = 0; i < moves.Count; i++)
            {
                var (transform, target, delay) = moves[i];
                if (transform == null)
                {
                    continue;
                }

                MoveToWorld(transform, target, duration, delay);
                maxEndTime = Mathf.Max(maxEndTime, delay + duration);
            }

            await UniTask.Delay(
                System.TimeSpan.FromSeconds(maxEndTime),
                cancellationToken: cancellationToken);
        }

        public static async UniTask MoveHopToWorldAsync(
            Transform target,
            Vector3 worldStart,
            Vector3 worldMid,
            Vector3 worldEnd,
            float duration,
            float peakScaleIntensity,
            float landScaleIntensity,
            CancellationToken cancellationToken = default,
            TweenCallback onComplete = null)
        {
            if (target == null)
            {
                return;
            }

            KillMotion(target, "DeckTween.Hop");
            target.position = worldStart;

            var resolvedUid = 0;
            CardManagerSingleton.Instance?.TryResolveUid(target, out resolvedUid);
            var motionId = 0;
            if (resolvedUid > 0)
            {
                motionId = CardPresentationProbe.NextMotionId();
                CardPresentationProbe.MotionBegin(
                    resolvedUid,
                    motionId,
                    worldStart,
                    worldEnd,
                    "DeckTween.Hop",
                    reason: "hop",
                    expectMs: duration);
            }

            var baseScale = target.localScale;
            var peakScale = baseScale * (1f + peakScaleIntensity);
            var landScale = baseScale * (1f - landScaleIntensity);
            var halfDuration = duration * 0.5f;
            var landDuration = halfDuration * 0.72f;
            var settleDuration = halfDuration - landDuration;

            var sequence = DOTween.Sequence()
                .SetLink(target.gameObject, LinkBehaviour.KillOnDestroy);

            sequence.Append(target.DOMove(worldMid, halfDuration).SetEase(Ease.OutQuad));
            sequence.Join(target.DOScale(peakScale, halfDuration).SetEase(Ease.OutSine));
            sequence.Append(target.DOMove(worldEnd, halfDuration).SetEase(Ease.InQuad));
            sequence.Join(
                DOTween.Sequence()
                    .Append(target.DOScale(landScale, landDuration).SetEase(Ease.InQuad))
                    .Append(target.DOScale(baseScale, settleDuration).SetEase(Ease.OutSine)));

            var completed = false;
            var endHow = "complete";
            sequence.OnComplete(() =>
            {
                completed = true;
                if (target == null)
                {
                    return;
                }

                target.localScale = baseScale;
                if (resolvedUid > 0)
                {
                    CardPresentationProbe.MotionEnd(
                        resolvedUid,
                        motionId,
                        target.position,
                        endHow,
                        "DeckTween.Hop");
                }

                onComplete?.Invoke();
            });
            sequence.OnKill(() =>
            {
                endHow = "kill";
                completed = true;
                if (resolvedUid > 0 && target != null)
                {
                    CardPresentationProbe.MotionEnd(
                        resolvedUid,
                        motionId,
                        target.position,
                        "kill",
                        "DeckTween.Hop",
                        killedBySite: "DeckTween.Kill");
                }
            });
            await UniTask.WaitUntil(() => completed, cancellationToken: cancellationToken);
        }

        public static async UniTask WaitOneFrameAsync(CancellationToken cancellationToken = default)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }

        /// <summary>
        /// 指数缓动追可变锚点：远时快、近时慢（OutCubic 手感）；锚点跳变后会自动再加速。
        /// 位移公式：pos = Lerp(pos, dest, 1 - exp(-responsiveness * dt))。
        /// </summary>
        public static async UniTask ChaseAnchorAsync(
            Transform target,
            Func<Vector3> getTarget,
            float responsiveness,
            float arriveThreshold,
            CancellationToken cancellationToken = default,
            Func<bool> shouldContinue = null,
            float maxStep = 0f)
        {
            if (target == null || getTarget == null)
            {
                return;
            }

            KillMotion(target);

            var thresholdSqr = arriveThreshold * arriveThreshold;
            var lambda = Mathf.Max(0.01f, responsiveness);
            while (!cancellationToken.IsCancellationRequested)
            {
                if (target == null)
                {
                    return;
                }

                if (shouldContinue != null && !shouldContinue())
                {
                    return;
                }

                var destination = getTarget();
                var current = target.position;
                var delta = destination - current;
                if (delta.sqrMagnitude <= thresholdSqr)
                {
                    target.position = destination;
                    return;
                }

                var t = 1f - Mathf.Exp(-lambda * Time.deltaTime);
                var step = delta * Mathf.Clamp01(t);
                if (maxStep > 0f)
                {
                    var stepLen = step.magnitude;
                    if (stepLen > maxStep)
                    {
                        step *= maxStep / stepLen;
                    }
                }

                target.position = current + step;
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
        }
    }
}
