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
    /// 骷髅军团牌组专用表现管理器：合体、散架等。由 <see cref="GroundFieldManagerSingleton"/> 按时机调用。
    /// </summary>
    public sealed class SkeletonDeckPresentationManager : MonoBehaviour
    {
        private static SkeletonDeckPresentationManager _instance;

        [Header("Layout")]
        [Tooltip("骷髅卡组合体等动效参数。")]
        [SerializeField] private SkeletonDeckLayoutSettings layoutSettings = new();

        public static SkeletonDeckPresentationManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<SkeletonDeckPresentationManager>();
                }

                return _instance;
            }
        }

        public static SkeletonDeckPresentationManager TryGetInstance()
        {
            if (_instance != null)
            {
                return _instance;
            }

            return FindFirstObjectByType<SkeletonDeckPresentationManager>();
        }

        public SkeletonDeckLayoutSettings LayoutSettings => layoutSettings;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>
        /// 播放骷髅合体：互撞重叠 → 闪白替换结果卡 → 场地离场入组。</summary>
        /// <paramref name="onFusionStarted"/> 在参与卡脱离场地格位后调用（用于触发补牌）。</summary>
        public async UniTask PresentFusionAsync(
            SkeletonFusionPresentationRequest request,
            Func<CancellationToken, UniTask> onFusionStarted,
            CancellationToken cancellationToken = default)
        {
            var fieldManager = GroundFieldManagerSingleton.Instance;
            var cardManager = CardManagerSingleton.Instance;
            var deckManager = CardDeckManagerSingleton.Instance;
            if (fieldManager == null || cardManager == null || deckManager == null)
            {
                Debug.LogWarning("[SkeletonDeckPresentation] PresentFusion 缺少 Field/Card/Deck 管理器。");
                return;
            }

            var participants = ResolveParticipants(request, fieldManager, cardManager);
            if (participants.Count < 2)
            {
                Debug.LogWarning(
                    $"[SkeletonDeckPresentation] PresentFusion 参与卡不足 action={request.ActionId} skill={request.SkillId} resolved={participants.Count}/{request.ParticipantUids.Length}");
                return;
            }

            var mergeCenter = ComputeMergeCenter(participants);
            VacateFusionParticipants(fieldManager, cardManager, participants);
            for (var i = 0; i < participants.Count; i++)
            {
                FlightSortingChannel.Raise(participants[i].Card);
            }

            if (onFusionStarted != null)
            {
                await onFusionStarted(cancellationToken);
            }

            await CrashParticipantsToCenterAsync(participants, mergeCenter, cancellationToken);

            if (layoutSettings.fusionOverlapHoldDuration > 0f)
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(layoutSettings.fusionOverlapHoldDuration),
                    cancellationToken: cancellationToken);
            }

            await PlayParticipantFlashAsync(participants, cancellationToken);
            HideParticipants(participants);

            var resultCard = await RevealResultCardAsync(
                request,
                mergeCenter,
                cardManager,
                cancellationToken);
            if (resultCard == null)
            {
                for (var i = 0; i < participants.Count; i++)
                {
                    FlightSortingChannel.Restore(participants[i].Card);
                }

                ReleaseParticipants(cardManager, participants);
                return;
            }

            FlightSortingChannel.Raise(resultCard);

            if (layoutSettings.fusionResultHoldDuration > 0f)
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(layoutSettings.fusionResultHoldDuration),
                    cancellationToken: cancellationToken);
            }

            await ExitResultToDeckAsync(resultCard, deckManager, fieldManager, cancellationToken);
            for (var i = 0; i < participants.Count; i++)
            {
                FlightSortingChannel.Restore(participants[i].Card);
            }

            FlightSortingChannel.Restore(resultCard);
            ReleaseParticipants(cardManager, participants);
        }

        private static List<ParticipantView> ResolveParticipants(
            SkeletonFusionPresentationRequest request,
            GroundFieldManagerSingleton fieldManager,
            CardManagerSingleton cardManager)
        {
            var participants = new List<ParticipantView>(request.ParticipantUids.Length);
            var seen = new HashSet<int>();
            for (var i = 0; i < request.ParticipantUids.Length; i++)
            {
                var uid = request.ParticipantUids[i];
                if (uid <= 0 || !seen.Add(uid))
                {
                    continue;
                }

                if (!cardManager.TryGet(uid, out var card) || card?.Transform == null)
                {
                    continue;
                }

                var slot = 0;
                if (!fieldManager.TryGetSlotOf(uid, out slot))
                {
                    slot = 0;
                }

                participants.Add(new ParticipantView(card, slot));
            }

            return participants;
        }

        private static Vector3 ComputeMergeCenter(IReadOnlyList<ParticipantView> participants)
        {
            var sum = Vector3.zero;
            var count = 0;
            for (var i = 0; i < participants.Count; i++)
            {
                var transform = participants[i].Card.Transform;
                if (transform == null)
                {
                    continue;
                }

                sum += transform.position;
                count++;
            }

            return count > 0 ? sum / count : Vector3.zero;
        }

        private static void VacateFusionParticipants(
            GroundFieldManagerSingleton fieldManager,
            CardManagerSingleton cardManager,
            IReadOnlyList<ParticipantView> participants)
        {
            for (var i = 0; i < participants.Count; i++)
            {
                var participant = participants[i];
                cardManager.MarkFieldDead(participant.Card);
                if (participant.Slot > 0)
                {
                    fieldManager.ClearSlotOccupancy(participant.Slot, skipBusyGuard: true);
                }
            }
        }

        private async UniTask CrashParticipantsToCenterAsync(
            IReadOnlyList<ParticipantView> participants,
            Vector3 mergeCenter,
            CancellationToken cancellationToken)
        {
            var duration = Mathf.Max(0.01f, layoutSettings.fusionCrashDuration);
            var tasks = new List<UniTask>(participants.Count);
            for (var i = 0; i < participants.Count; i++)
            {
                var card = participants[i].Card;
                if (card?.Transform == null)
                {
                    continue;
                }

                // 编排大脑：共享 sourceTime，双卡 L3 同时撞中点；完成后 Park L0、L3 回零（非 SetParent）。
                if (EffectFrameConvergence.TryGetDriver(card, out _))
                {
                    tasks.Add(EffectFrameConvergence.ConvergeVisualToWorldAsync(
                        card,
                        mergeCenter,
                        duration,
                        cancellationToken,
                        parkRootOnComplete: true));
                    continue;
                }

                CardDeckTween.KillMotion(card.Transform, "SkeletonFusion.Crash", card.Uid);
                var tween = card.Transform
                    .DOMove(mergeCenter, duration)
                    .SetEase(Ease.InBack, layoutSettings.fusionCrashOvershoot)
                    .SetLink(card.Transform.gameObject, LinkBehaviour.KillOnDestroy);
                tasks.Add(AwaitTweenAsync(tween, cancellationToken));
            }

            if (tasks.Count > 0)
            {
                await UniTask.WhenAll(tasks);
            }
        }

        private static async UniTask PlayParticipantFlashAsync(
            IReadOnlyList<ParticipantView> participants,
            CancellationToken cancellationToken)
        {
            var tasks = new List<UniTask>(participants.Count);
            for (var i = 0; i < participants.Count; i++)
            {
                tasks.Add(participants[i].Card.TryPlayHitFlashAsync(cancellationToken: cancellationToken));
            }

            if (tasks.Count > 0)
            {
                await UniTask.WhenAll(tasks);
            }
        }

        private static void HideParticipants(IReadOnlyList<ParticipantView> participants)
        {
            for (var i = 0; i < participants.Count; i++)
            {
                var transform = participants[i].Card.Transform;
                if (transform == null)
                {
                    continue;
                }

                transform.localScale = Vector3.zero;
            }
        }

        private async UniTask<ManagedCard> RevealResultCardAsync(
            SkeletonFusionPresentationRequest request,
            Vector3 mergeCenter,
            CardManagerSingleton cardManager,
            CancellationToken cancellationToken)
        {
            if (!cardManager.TryGet(request.ResultUid, out var resultCard) || resultCard == null)
            {
                resultCard = cardManager.SpawnView(
                    request.ResultUid,
                    request.ResultDefId,
                    initialMode: CardDisplayMode.GroundCardMode);
            }

            if (resultCard?.Transform == null)
            {
                return null;
            }

            cardManager.SetDisplayMode(resultCard, CardDisplayMode.GroundCardMode);
            cardManager.RefreshDisplayMode(resultCard);
            cardManager.EnsureComplexDomainStack(resultCard);
            if (!SlotFrameConvergence.TryEnsureInfrastructure(
                    resultCard,
                    out _,
                    out _,
                    "Skeleton.RevealResult"))
            {
                CardPresentationProbe.Anomaly(
                    resultCard.Uid,
                    "forceSnap",
                    "noTower/noDriver",
                    "Skeleton.RevealResult",
                    layer: "L2",
                    verdict: "hardSet");
                resultCard.Transform.position = mergeCenter;
            }
            else
            {
                SlotFrameConvergence.SnapHome(
                    resultCard,
                    mergeCenter,
                    "Skeleton.RevealResult",
                    resultCard.Uid);
            }

            var baseScale = resultCard.Transform.localScale;
            if (baseScale == Vector3.zero)
            {
                baseScale = Vector3.one;
            }

            await RunScaleAppearAsync(resultCard.Transform, baseScale, cancellationToken);
            await resultCard.TryPlayHitFlashAsync(cancellationToken: cancellationToken);
            return resultCard;
        }

        private static async UniTask ExitResultToDeckAsync(
            ManagedCard resultCard,
            CardDeckManagerSingleton deckManager,
            GroundFieldManagerSingleton fieldManager,
            CancellationToken cancellationToken)
        {
            if (resultCard == null || resultCard.Transform == null)
            {
                return;
            }

            var fieldLayout = fieldManager.LayoutSettings;
            var exitDuration = fieldLayout != null ? fieldLayout.fieldExitDuration : 0.35f;

            // 若洗入通道曾提前登记入组，合体 reveal 会把 Transform 留在场上；需先卸下再走上飞入组。
            if (deckManager.ContainsUid(resultCard.Uid))
            {
                deckManager.TryDetachByUid(resultCard.Uid, out _);
            }

            if (!deckManager.LaunchReturnFieldCardToDeck(resultCard))
            {
                Debug.LogWarning(
                    $"[SkeletonDeckPresentation] LaunchReturnFieldCardToDeck 失败 uid={resultCard.Uid} mode={resultCard.DisplayMode}");
                return;
            }

            if (exitDuration > 0f)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(exitDuration), cancellationToken: cancellationToken);
            }
        }

        private async UniTask RunScaleAppearAsync(
            Transform target,
            Vector3 finalScale,
            CancellationToken cancellationToken)
        {
            if (target == null)
            {
                return;
            }

            var duration = Mathf.Max(0.01f, layoutSettings.fusionResultAppearDuration);
            target.localScale = Vector3.zero;
            var tween = target
                .DOScale(finalScale, duration)
                .SetEase(Ease.OutBack)
                .SetLink(target.gameObject, LinkBehaviour.KillOnDestroy);
            await AwaitTweenAsync(tween, cancellationToken);
        }

        private static void ReleaseParticipants(
            CardManagerSingleton cardManager,
            IReadOnlyList<ParticipantView> participants)
        {
            for (var i = 0; i < participants.Count; i++)
            {
                var uid = participants[i].Card.Uid;
                if (uid > 0)
                {
                    cardManager.Release(uid, "SkeletonFusion.Participant");
                }
            }
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

        private readonly struct ParticipantView
        {
            public ParticipantView(ManagedCard card, int slot)
            {
                Card = card;
                Slot = slot;
            }

            public ManagedCard Card { get; }
            public int Slot { get; }
        }
    }
}
