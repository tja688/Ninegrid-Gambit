using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 鐗岀粍鐩稿叧 DOTween 缂撳姩宸ュ叿銆?
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
                CardEntityLifecycleHook.CardsOrNull()?.TryResolveUid(target, out resolvedUid);
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

        /// <summary>目标 Transform 上是否仍有未完成的 DeckTween（含 fieldExit / ripple）。</summary>
        public static bool IsMotionActive(Transform target) =>
            target != null && DOTween.IsTweening(target);

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
                CardEntityLifecycleHook.CardsOrNull()?.TryResolveUid(target, out resolvedUid);
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

        public static async UniTask ScaleAppearAsync(
            Transform target,
            Vector3 finalScale,
            float duration = 0.2f,
            CancellationToken cancellationToken = default)
        {
            if (target == null)
            {
                return;
            }

            KillMotion(target, "DeckTween.ScaleAppear");
            target.localScale = Vector3.zero;
            target.DOScale(finalScale, duration)
                .SetEase(Ease.OutBack)
                .SetLink(target.gameObject, LinkBehaviour.KillOnDestroy);
            await UniTask.Delay(
                System.TimeSpan.FromSeconds(duration),
                cancellationToken: cancellationToken);
            if (target != null)
            {
                target.localScale = finalScale;
            }
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
            CardEntityLifecycleHook.CardsOrNull()?.TryResolveUid(target, out resolvedUid);
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
            var motionClosed = false;
            sequence.OnComplete(() =>
            {
                motionClosed = true;
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
                        "complete",
                        "DeckTween.Hop");
                }

                onComplete?.Invoke();
            });
            sequence.OnKill(() =>
            {
                completed = true;
                if (motionClosed || resolvedUid <= 0 || target == null)
                {
                    return;
                }

                CardPresentationProbe.MotionEnd(
                    resolvedUid,
                    motionId,
                    target.position,
                    "kill",
                    "DeckTween.Hop",
                    killedBySite: "DeckTween.Kill");
            });
            await UniTask.WaitUntil(() => completed, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// 鍦哄湴鍗″瀭鐩翠笂椋炵鐢诲悗鍥炶皟鍏ョ粍锛堝彂灏勫悗涓嶇锛岀敱璋冪敤鏂瑰湪 onExitComplete 鍐呮彃鍏ュ崱缁?ripple锛夈€?
        /// </summary>
        public static void LaunchFieldExitThenDeckInsert(
            Transform target,
            float exitY,
            float exitDuration,
            Action onExitComplete,
            int uid = 0)
        {
            if (target == null)
            {
                onExitComplete?.Invoke();
                return;
            }

            var pos = target.position;
            var exitPos = new Vector3(pos.x, Mathf.Max(pos.y, exitY), pos.z);
            var effectiveDuration = pos.y >= exitY - 0.001f ? 0f : exitDuration;
            MoveToWorld(
                target,
                exitPos,
                effectiveDuration,
                onComplete: () => onExitComplete?.Invoke(),
                uid: uid,
                reason: "fieldExit");
        }

        public static async UniTask WaitOneFrameAsync(CancellationToken cancellationToken = default)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }
    }
}
