using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 战斗表现终态守卫：播前快照，播后/取消/Kill 后强制对齐格位锚点与显示模式。
    /// </summary>
    public static class BattleFinalStateGuard
    {
        public const float AlignDistanceThreshold = 0.02f;
        public const float SoftRestoreDuration = 0.12f;
        public const float SoftRestoreTimeoutSeconds = 0.25f;

        public readonly struct ParticipantSnapshot
        {
            public ParticipantSnapshot(
                ManagedCard card,
                int? slotId,
                Vector3 worldPosition,
                Vector3 localScale,
                Quaternion rotation,
                CardDisplayMode displayMode)
            {
                Card = card;
                SlotId = slotId;
                WorldPosition = worldPosition;
                LocalScale = localScale;
                Rotation = rotation;
                DisplayMode = displayMode;
            }

            public ManagedCard Card { get; }

            public int? SlotId { get; }

            public Vector3 WorldPosition { get; }

            public Vector3 LocalScale { get; }

            public Quaternion Rotation { get; }

            public CardDisplayMode DisplayMode { get; }

            public Transform Transform => Card?.Transform;
        }

        public static ParticipantSnapshot Capture(
            ManagedCard card,
            GroundFieldManagerSingleton field,
            int? preferredSlot = null)
        {
            if (card?.Transform == null)
            {
                return default;
            }

            int? slot = preferredSlot;
            if (!slot.HasValue && field != null && field.TryGetSlotOf(card.Uid, out var found))
            {
                slot = found;
            }

            var transform = card.Transform;
            return new ParticipantSnapshot(
                card,
                slot,
                transform.position,
                transform.localScale,
                transform.rotation,
                card.DisplayMode);
        }

        /// <summary>
        /// 强制对齐：Kill tween → 回锚点（或快照位置）→ rotation identity → RefreshDisplayMode。
        /// </summary>
        public static async UniTask RestoreAsync(
            ParticipantSnapshot snapshot,
            GroundFieldManagerSingleton field,
            bool restoreToSlot,
            bool allowSoftTween,
            bool strictWarn,
            CancellationToken cancellationToken = default)
        {
            var card = snapshot.Card;
            var transform = card?.Transform;
            if (transform == null)
            {
                return;
            }

            CardDeckTween.KillMotion(transform);

            if (!restoreToSlot)
            {
                transform.rotation = Quaternion.identity;
                return;
            }

            var targetPosition = snapshot.WorldPosition;
            if (snapshot.SlotId.HasValue && field != null)
            {
                var anchor = field.GetGroundAnchor(snapshot.SlotId.Value);
                if (anchor != null)
                {
                    targetPosition = anchor.position;
                }
            }

            var distance = Vector3.Distance(transform.position, targetPosition);
            if (distance > AlignDistanceThreshold && allowSoftTween)
            {
                await SoftMoveOrSnapAsync(transform, targetPosition, cancellationToken);
                CardPresentationProbe.SnapSet(
                    card.Uid,
                    transform.position,
                    "FinalStateGuard.SoftSnap",
                    slot: snapshot.SlotId,
                    reason: "restoreSoft");
            }
            else
            {
                transform.position = targetPosition;
                CardPresentationProbe.SnapSet(
                    card.Uid,
                    targetPosition,
                    "FinalStateGuard.HardSnap",
                    slot: snapshot.SlotId,
                    killedTween: true,
                    reason: "restoreHard");
            }

            transform.rotation = Quaternion.identity;

            distance = Vector3.Distance(transform.position, targetPosition);
            if (distance > AlignDistanceThreshold)
            {
                transform.position = targetPosition;
                CardPresentationProbe.SnapSet(
                    card.Uid,
                    targetPosition,
                    "FinalStateGuard.HardSnap",
                    slot: snapshot.SlotId,
                    killedTween: true,
                    reason: "restoreForce");
                if (strictWarn)
                {
                    Debug.LogWarning(
                        $"[BattleFinalStateGuard] 卡 {card.Uid} 终态未对齐锚点，已硬 Snap。distance={distance:F3}",
                        transform);
                }
            }

            CardManagerSingleton.Instance?.RefreshDisplayMode(card);
        }

        public static async UniTask RestorePairAsync(
            ParticipantSnapshot attacker,
            ParticipantSnapshot victim,
            GroundFieldManagerSingleton field,
            BattleBindParams bind,
            CancellationToken cancellationToken = default,
            bool? restoreVictimOverride = null)
        {
            var restoreVictim = restoreVictimOverride ?? bind.RestoreVictimToSlot;

            if (!bind.RequireFinalStateGuard)
            {
                if (bind.RestoreAttackerToSlot)
                {
                    await RestoreAsync(attacker, field, restoreToSlot: true, allowSoftTween: true, strictWarn: false, cancellationToken);
                }

                if (restoreVictim)
                {
                    await RestoreAsync(victim, field, restoreToSlot: true, allowSoftTween: true, strictWarn: false, cancellationToken);
                }

                return;
            }

            await RestoreAsync(
                attacker,
                field,
                restoreToSlot: bind.RestoreAttackerToSlot,
                allowSoftTween: true,
                strictWarn: true,
                cancellationToken);

            await RestoreAsync(
                victim,
                field,
                restoreToSlot: restoreVictim,
                allowSoftTween: true,
                strictWarn: true,
                cancellationToken);
        }

        /// <summary>
        /// 纯同步硬对齐（单测 / 紧急兜底，无补间）。
        /// </summary>
        public static void SnapImmediate(
            Transform transform,
            Vector3 targetPosition,
            bool resetRotation = true)
        {
            if (transform == null)
            {
                return;
            }

            CardDeckTween.KillMotion(transform);
            transform.position = targetPosition;
            if (resetRotation)
            {
                transform.rotation = Quaternion.identity;
            }

            if (CardManagerSingleton.Instance != null
                && CardManagerSingleton.Instance.TryResolveUid(transform, out var uid)
                && uid > 0)
            {
                CardPresentationProbe.SnapSet(
                    uid,
                    targetPosition,
                    "FinalStateGuard.HardSnap",
                    killedTween: true,
                    reason: "snapImmediate");
            }
        }

        private static async UniTask SoftMoveOrSnapAsync(
            Transform transform,
            Vector3 targetPosition,
            CancellationToken cancellationToken)
        {
            var completed = false;
            var tween = transform
                .DOMove(targetPosition, SoftRestoreDuration)
                .SetEase(Ease.OutCubic)
                .SetLink(transform.gameObject, LinkBehaviour.KillOnDestroy)
                .OnComplete(() => completed = true)
                .OnKill(() => completed = true);

            var deadline = Time.realtimeSinceStartup + SoftRestoreTimeoutSeconds;
            while (!completed)
            {
                if (cancellationToken.IsCancellationRequested
                    || Time.realtimeSinceStartup >= deadline)
                {
                    if (tween != null && tween.IsActive())
                    {
                        tween.Kill(complete: false);
                    }

                    transform.position = targetPosition;
                    return;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, CancellationToken.None);
            }

            if (Vector3.Distance(transform.position, targetPosition) > AlignDistanceThreshold)
            {
                transform.position = targetPosition;
            }
        }
    }
}
