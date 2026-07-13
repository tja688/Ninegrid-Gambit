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
        /// 场地卡垂直上飞离画后回调入组（发射后不管，由调用方在 onExitComplete 内插入卡组 ripple）。
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
            MoveToWorld(
                target,
                exitPos,
                exitDuration,
                onComplete: () => onExitComplete?.Invoke(),
                uid: uid,
                reason: "fieldExit");
        }

        public static async UniTask WaitOneFrameAsync(CancellationToken cancellationToken = default)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }

        /// <summary>
        /// 二阶贝塞尔可变缓动追踪可变锚点：远快近慢，锚点跳变时 boost 并消费预算。
        /// </summary>
        public static async UniTask<float> TrackQuadraticBezierAnchorAsync(
            Transform target,
            Vector3 launchPos,
            Func<Vector3> getTarget,
            DealFlightLayoutSettings settings,
            DealSettleBudget budget,
            CancellationToken cancellationToken = default,
            Func<bool> shouldContinue = null,
            int trackedSlot = -1,
            int uid = 0,
            Action<float, float> onAnchorJump = null)
        {
            if (target == null || getTarget == null || settings == null || budget == null)
            {
                return 0f;
            }

            KillMotion(target, "DeckTween.BezierTrack", uid);

            var resolvedUid = uid;
            if (resolvedUid <= 0)
            {
                CardManagerSingleton.Instance?.TryResolveUid(target, out resolvedUid);
            }

            var choreoSeqId = ChoreoTraceSink.SafeCurrentSeqId();
            var motionId = 0;
            if (resolvedUid > 0)
            {
                motionId = CardPresentationProbe.NextMotionId();
                var initialDest = getTarget();
                CardPresentationProbe.MotionBegin(
                    resolvedUid,
                    motionId,
                    launchPos,
                    initialDest,
                    "DeckTween.BezierTrack",
                    reason: "dealFlight",
                    choreoSeqId: choreoSeqId);
            }

            var threshold = settings.arriveThreshold;
            var thresholdSqr = threshold * threshold;
            var progressU = 0f;
            var lastDest = getTarget();
            var jumpBoostTimer = 0f;
            var softLandFrames = 0;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (target == null)
                    {
                        return progressU;
                    }

                    if (shouldContinue != null && !shouldContinue())
                    {
                        return progressU;
                    }

                    var destination = getTarget();
                    var current = target.position;
                    var toTarget = destination - current;
                    var remainingDist = toTarget.magnitude;

                    var anchorJumpSqr = (destination - lastDest).sqrMagnitude;
                    if (anchorJumpSqr > settings.anchorJumpThresholdSqr)
                    {
                        jumpBoostTimer = 0.12f;
                        budget.ApplyJumpPenalty(settings.rotationJumpCost);
                        onAnchorJump?.Invoke(anchorJumpSqr, settings.rotationJumpCost);
                        lastDest = destination;
                    }

                    if (remainingDist * remainingDist <= thresholdSqr && progressU >= settings.arriveMinU)
                    {
                        target.position = destination;
                        progressU = 1f;
                        return progressU;
                    }

                    var distFactor = Mathf.Clamp01(remainingDist / Mathf.Max(0.01f, settings.refDistance));
                    var control = DealFlightMath.ComputeControlPoint(
                        current,
                        destination,
                        settings.arcHeight,
                        distFactor);
                    var speedFactor = DealFlightMath.ComputeSpeedFactor(
                        remainingDist,
                        settings.speedNearDistance,
                        settings.speedFarDistance,
                        settings.easeNear,
                        settings.easeFar);

                    if (jumpBoostTimer > 0f)
                    {
                        speedFactor *= settings.rotationJumpBoost;
                        jumpBoostTimer -= Time.deltaTime;
                    }

                    var budgetFactor = DealFlightMath.ComputeBudgetFactor(budget.Remaining, budget.Total);
                    if (budget.IsExhausted)
                    {
                        speedFactor *= settings.exhaustedSnapBlend;
                        softLandFrames++;
                        if (softLandFrames >= 2 && remainingDist * remainingDist <= thresholdSqr * 4f)
                        {
                            target.position = destination;
                            progressU = 1f;
                            return progressU;
                        }
                    }

                    var du = DealFlightMath.ComputeProgressDelta(
                        1f,
                        speedFactor,
                        budgetFactor,
                        Time.deltaTime,
                        settings.baseDuration);
                    progressU = Mathf.Clamp01(progressU + du);
                    target.position = DealFlightMath.EvaluateQuadraticBezier(launchPos, control, destination, progressU);

                    budget.Consume(Time.deltaTime);
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }
            }
            finally
            {
                if (resolvedUid > 0 && motionId > 0 && target != null)
                {
                    CardPresentationProbe.MotionEnd(
                        resolvedUid,
                        motionId,
                        target.position,
                        cancellationToken.IsCancellationRequested ? "cancel" : "complete",
                        "DeckTween.BezierTrack",
                        choreoSeqId: choreoSeqId);
                }
            }

            return progressU;
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
            float maxStep = 0f,
            int trackedSlot = -1,
            int uid = 0)
        {
            if (target == null || getTarget == null)
            {
                return;
            }

            KillMotion(target, "DeckTween.Chase", uid);

            var resolvedUid = uid;
            if (resolvedUid <= 0)
            {
                CardManagerSingleton.Instance?.TryResolveUid(target, out resolvedUid);
            }

            var choreoSeqId = ChoreoTraceSink.SafeCurrentSeqId();
            var motionId = 0;
            if (resolvedUid > 0)
            {
                motionId = CardPresentationProbe.NextMotionId();
                var initialDest = getTarget();
                CardPresentationProbe.MotionBegin(
                    resolvedUid,
                    motionId,
                    target.position,
                    initialDest,
                    "DeckTween.Chase",
                    reason: "exploreChase",
                    choreoSeqId: choreoSeqId);
            }

            var thresholdSqr = arriveThreshold * arriveThreshold;
            var lambda = Mathf.Max(0.01f, responsiveness);
            var lastSampleTime = Time.time;
            var lastDest = getTarget();
            try
            {
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

                    if (resolvedUid > 0 && Time.time - lastSampleTime >= 0.1f)
                    {
                        var anchorJump = (destination - lastDest).sqrMagnitude;
                        if (anchorJump > 0.25f || delta.magnitude > 0.5f)
                        {
                            CardPresentationProbe.ChaseSample(
                                resolvedUid,
                                trackedSlot,
                                current,
                                destination,
                                delta.magnitude,
                                choreoSeqId);
                            lastDest = destination;
                        }

                        lastSampleTime = Time.time;
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
            finally
            {
                if (resolvedUid > 0 && motionId > 0 && target != null)
                {
                    CardPresentationProbe.MotionEnd(
                        resolvedUid,
                        motionId,
                        target.position,
                        cancellationToken.IsCancellationRequested ? "cancel" : "complete",
                        "DeckTween.Chase",
                        choreoSeqId: choreoSeqId);
                }
            }
        }
    }
}
