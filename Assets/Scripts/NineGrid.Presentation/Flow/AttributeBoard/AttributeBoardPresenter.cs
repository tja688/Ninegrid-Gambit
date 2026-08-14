using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Cards;
using NineGrid.Content.CardPresentation;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Flow;
using NineGrid.Flow.BoardBriefTip;
using NineGrid.Flow.Presentation;
using NineGrid.Flow.RoomIcons;
using NineGrid.Flow.Transitions;
using NineGrid.Presentation;
using NineGrid.Presentation.Systems;
using QFramework;
using UnityEngine;
using UnityEngine.Rendering;

namespace NineGrid.Flow.AttributeBoard
{
    /// <summary>
    /// 属性房三选二场地：3 张候选真卡 + 离开图标；任意距离点击选择，
    /// 选满两张播放获得反馈并推进房间，离开驻留放弃未选完候选（#137 / ADR-0031 / ADR-0020）。
    /// 点击经 RewardChoiceCoreHook → IntentIntake → Core SelectReward，不由 View 直接改 Model。
    /// </summary>
    public sealed class AttributeBoardPresenter
    {
        public static AttributeBoardPresenter Current { get; private set; } = new AttributeBoardPresenter();

        /// <summary>当前候选真卡（表现权威只读投影，供房内装饰层 InRoomCardLifeFx 聚合）。</summary>
        public IReadOnlyList<ManagedCard> CandidateCards => mCandidateCards;

        private readonly List<ManagedCard> mCandidateCards = new List<ManagedCard>(3);
        private readonly List<bool> mSelectedFlags = new List<bool>(3);
        private readonly List<GameObject> mExtras = new List<GameObject>(1);
        private readonly RoomIconDwellSession mLeaveDwell = new RoomIconDwellSession();
        private CancellationTokenSource mWatchCts;
        private IArchitecture mArch;
        private Func<float, CancellationToken, UniTask> mDelayAsync;
        private bool mWatching;
        private int mLastAvatarSlot;
        private bool mActive;
        private Action<string> mNotice;

        public bool IsActive => mActive;

        public static void ResetForTests()
        {
            Current?.DespawnAll();
            Current = new AttributeBoardPresenter();
        }

        public void SetDelayAsyncForTests(Func<float, CancellationToken, UniTask> delayAsync)
        {
            mDelayAsync = delayAsync;
        }

        public void SetNoticeHandlerForTests(Action<string> notice)
        {
            mNotice = notice;
        }

        public void Bind(IArchitecture architecture)
        {
            mArch = architecture;
        }

        public void DespawnAll()
        {
            CancelWatch();
            mLeaveDwell.Cancel();
            mActive = false;

            var cards = CardEntityLifecycleHook.CardsOrNull()
                        ?? UnityEngine.Object.FindFirstObjectByType<CardManagerSingleton>();
            for (var i = 0; i < mCandidateCards.Count; i++)
            {
                var card = mCandidateCards[i];
                if (card == null)
                {
                    continue;
                }

                if (cards != null)
                {
                    cards.Release(card, "AttributeBoard.Despawn");
                }
            }

            mCandidateCards.Clear();
            mSelectedFlags.Clear();

            for (var i = 0; i < mExtras.Count; i++)
            {
                if (mExtras[i] != null)
                {
                    UnityEngine.Object.Destroy(mExtras[i]);
                }
            }

            mExtras.Clear();
            RoomIconOccupancy.Current.Clear();
            RoomIconOccupancySlotHits.Refresh(mArch);
            BoardBriefTipPresenter.InstanceOrNull()?.HardClear();
        }

        /// <summary>按 PendingChoice 属性房会话刷板；失败返回 false。</summary>
        public bool TrySpawnFromPending(IArchitecture arch)
        {
            Bind(arch);
            DespawnAll();
            if (arch == null)
            {
                return false;
            }

            var pending = arch.GetModel<PendingChoiceModel>();
            if (pending == null
                || pending.Kind.Value != PendingChoiceKind.AttributePick
                || !PendingChoiceModel.IsAttributePickPool(pending.PoolId.Value))
            {
                return false;
            }

            var geometry = arch.GetSystem<IGroundFieldGeometrySystem>();
            var content = arch.GetSystem<IContentSystem>();
            SpawnCandidates(pending, geometry, content);
            SpawnLeave(geometry);
            mActive = RoomIconOccupancy.Current.HasAny || mCandidateCards.Count > 0;
            if (mActive)
            {
                StartAvatarWatch();
            }

            RoomIconOccupancySlotHits.Refresh(arch);
            return mActive;
        }

        /// <summary>会话变更（Generation / 离开 / 被清）后按 Pending 收尾。</summary>
        public void ResyncFromPending(IArchitecture arch)
        {
            if (!mActive)
            {
                return;
            }

            Bind(arch);
            var pending = arch?.GetModel<PendingChoiceModel>();
            if (pending == null
                || pending.Kind.Value != PendingChoiceKind.AttributePick
                || !PendingChoiceModel.IsAttributePickPool(pending.PoolId.Value))
            {
                DespawnAll();
            }
        }

        private void SpawnCandidates(
            PendingChoiceModel pending,
            IGroundFieldGeometrySystem geometry,
            IContentSystem content)
        {
            var cards = CardEntityLifecycleHook.CardsOrNull()
                        ?? UnityEngine.Object.FindFirstObjectByType<CardManagerSingleton>();
            var options = pending.RewardOptions;
            for (var i = 0; i < options.Count && i < AttributeBoardSlotResolver.CandidateSlots.Length; i++)
            {
                var entry = options[i];
                if (entry == null || string.IsNullOrEmpty(entry.DefId))
                {
                    continue;
                }

                var slot = AttributeBoardSlotResolver.CandidateSlotAt(i);
                RoomIconOccupancy.Current.Register(
                    slot, i, entry.DefId, RoomIconWalkRole.SoftBlockOnly);

                if (cards == null)
                {
                    continue;
                }

                // GroundCardMode：预制体原生尺寸；不 SetParent 到 ×2 格位锚点（ADR-0024）。
                var managed = cards.SpawnPresentationOnly(
                    entry.DefId,
                    parent: null,
                    CardDisplayMode.GroundCardMode,
                    CardPresentationKind.HelpCard);
                if (managed?.View == null)
                {
                    continue;
                }

                BoardSlotWorldPlacement.TryAlignToSlot(managed.View.transform, geometry, slot);
                managed.View.transform.rotation = Quaternion.identity;

                CoreCardPresentationMapper.ApplyVisualsByDefId(managed, CardPresentationKind.HelpCard);
                var tip = BuildCandidateTip(entry.DefId, content);
                AttachClickProxy(managed.View.gameObject, i, tip, slot);
                mCandidateCards.Add(managed);
                mSelectedFlags.Add(false);
            }
        }

        private void SpawnLeave(IGroundFieldGeometrySystem geometry)
        {
            var slot = AttributeBoardSlotResolver.LeaveSlot;
            RoomIconOccupancy.Current.Register(
                slot,
                -2,
                AttributeBoardSlotResolver.LeaveContentId,
                RoomIconWalkRole.WalkDestination);

            var path = CardChassisPaths.ResolveRoomIconPrefab(AttributeBoardSlotResolver.LeaveContentId, null);
            var go = TryInstantiate(path, geometry, slot, AttributeBoardSlotResolver.LeaveContentId);
            if (go == null)
            {
                return;
            }

            AttachBriefTipOnly(go, BoardBriefTipCopy.LeaveTip, slot, geometry);
            mExtras.Add(go);
        }

        private static string BuildCandidateTip(string defId, IContentSystem content)
        {
            string name = defId;
            string brief = string.Empty;
            if (content != null && content.HasCatalog
                && content.Catalog.Cards.TryGetValue(defId, out var card)
                && card != null
                && !string.IsNullOrWhiteSpace(card.DisplayName))
            {
                name = card.DisplayName;
            }

            if (CardPresentationConfigCatalog.TryGet(defId, out var dto) && dto != null)
            {
                if (!string.IsNullOrWhiteSpace(dto.displayName))
                {
                    name = dto.displayName;
                }

                if (!string.IsNullOrWhiteSpace(dto.description))
                {
                    brief = dto.description;
                }
            }

            var body = string.IsNullOrWhiteSpace(brief) ? name : name + "：" + brief.Trim();
            return BoardBriefTipCopy.ForOptionOrShelf(body);
        }

        private void AttachClickProxy(
            GameObject go,
            int candidateIndex,
            string tip,
            int boardSlot)
        {
            if (go == null)
            {
                return;
            }

            var groundHit = go.GetComponent<GroundCardHitProxy>();
            if (groundHit != null)
            {
                groundHit.enabled = false;
            }

            var proxy = go.GetComponent<AttributeBoardHitProxy>();
            if (proxy == null)
            {
                proxy = go.AddComponent<AttributeBoardHitProxy>();
            }

            proxy.Configure(candidateIndex, tip, HandleCandidateHit, boardSlot);
        }

        private static void AttachBriefTipOnly(
            GameObject go,
            string tip,
            int walkBoardSlot,
            IGroundFieldGeometrySystem geometry)
        {
            if (go == null)
            {
                return;
            }

            var proxy = go.GetComponent<BoardBriefTipHitProxy>();
            if (proxy == null)
            {
                proxy = go.AddComponent<BoardBriefTipHitProxy>();
            }

            proxy.Configure(tip, walkBoardSlot);
        }

        private void HandleCandidateHit(int candidateIndex)
        {
            Debug.Log(
                "[AttributeBoard] Hit candidateIndex=" + candidateIndex
                + " choiceOverlay=" + PresentationInputGates.ChoiceOverlayActive
                + " owner=" + PresentationInputGates.CurrentOwner);

            TrySelect(candidateIndex);
        }

        private void TrySelect(int candidateIndex)
        {
            if (candidateIndex < 0 || candidateIndex >= mCandidateCards.Count)
            {
                return;
            }

            if (mSelectedFlags[candidateIndex])
            {
                Debug.Log("[AttributeBoard] 重复点击已选候选 candidateIndex=" + candidateIndex);
                return;
            }

            var arch = mArch ?? NineGridArchitecture.Current;
            if (arch == null)
            {
                return;
            }

            RewardChoiceCoreHook.RequestWire();
            if (RewardChoiceCoreHook.SelectReward == null)
            {
                ShowNotice(NineGrid.Core.Localization.L10n.Tr("notice.attribute_not_wired", "属性房输入未接线"));
                return;
            }

            var pending = arch.GetModel<PendingChoiceModel>();
            var pendingIndex = AttributePickIndexResolver.ResolveCurrentPendingIndex(
                mSelectedFlags,
                candidateIndex);
            if (pendingIndex < 0
                || pending == null
                || pending.Kind.Value != PendingChoiceKind.AttributePick
                || pendingIndex >= pending.RewardOptions.Count)
            {
                Debug.LogWarning("[AttributeBoard] 候选索引已过期，重建会话 candidateIndex=" + candidateIndex);
                ResyncFromPending(arch);
                return;
            }

            var result = RewardChoiceCoreHook.SelectReward(pendingIndex);
            if (result == null || !result.Accepted)
            {
                Debug.LogWarning(
                    "[AttributeBoard] Select rejected pendingIndex=" + pendingIndex
                    + " reason=" + (result?.Reason ?? string.Empty));
                if (!string.IsNullOrEmpty(result?.Reason))
                {
                    ShowNotice(result.Reason);
                }

                return;
            }

            var contentId = pending.RewardOptions[pendingIndex]?.DefId ?? string.Empty;
            FlowRoomEconomyAudioCues.Pulse(
                FlowRoomEconomyAudioCues.AttributePick,
                "AttributeBoardPresenter.TrySelect",
                contentId);
            Debug.Log("[AttributeBoard] Select accepted pendingIndex=" + pendingIndex);
            mSelectedFlags[candidateIndex] = true;
            MarkSelected(managed: mCandidateCards[candidateIndex]);

            // 第一次选择：保留视觉确认，继续等第二次。
            if (SelectedCount() < RewardSystem.AttributePickCount)
            {
                return;
            }

            // 第二次成功后播放获得反馈、清理候选并推进房间（房间已由 Core 推进）。
            // 卡面会随清理销毁，反馈走不占主线的 Notice 通道（非锁步，与房内其余反馈一致）。
            DespawnAll();
            ShowNotice(BoardBriefTipCopy.AttributePickCompleteNotice);
        }

        private int SelectedCount()
        {
            var count = 0;
            for (var i = 0; i < mSelectedFlags.Count; i++)
            {
                if (mSelectedFlags[i])
                {
                    count++;
                }
            }

            return count;
        }

        private static void MarkSelected(ManagedCard managed)
        {
            if (managed?.View == null)
            {
                return;
            }

            managed.View.SetFrameColor(SelectedFrameColor);
            PlayHopPulse(managed.View.transform);
        }

        private static void PlayHopPulse(Transform target)
        {
            if (target == null)
            {
                return;
            }

            CardDeckTween.PlayHopScalePulseAsync(
                target,
                duration: 0.22f,
                peakScaleIntensity: 0.08f,
                landScaleIntensity: 0.05f,
                cancellationToken: CancellationToken.None).Forget();
        }

        private bool TryLeave()
        {
            RewardChoiceCoreHook.RequestWire();
            if (RewardChoiceCoreHook.SkipHelpChoice == null)
            {
                Debug.LogWarning("[AttributeBoard] SkipHelpChoice hook not wired; abort leave.");
                return false;
            }

            var result = RewardChoiceCoreHook.SkipHelpChoice();
            if (result != null && result.Accepted)
            {
                FlowRoomEconomyAudioCues.Pulse(
                    FlowRoomEconomyAudioCues.RoomLeave,
                    "AttributeBoardPresenter.TryLeave");
                DespawnAll();
                return true;
            }

            return false;
        }

        private void ShowNotice(string message)
        {
            if (mNotice != null)
            {
                mNotice(message);
                return;
            }

            var tip = BoardBriefTipPresenter.EnsureExists();
            tip.ShowNotice(message ?? string.Empty);
        }

        private void StartAvatarWatch()
        {
            CancelWatch();
            mWatching = true;
            mLastAvatarSlot = -1;
            WatchAvatarLoopAsync().Forget();
        }

        private void CancelWatch()
        {
            mWatching = false;
            mWatchCts?.Cancel();
            mWatchCts?.Dispose();
            mWatchCts = null;
        }

        private async UniTaskVoid WatchAvatarLoopAsync()
        {
            mWatchCts = new CancellationTokenSource();
            var ct = mWatchCts.Token;
            try
            {
                while (mWatching && !ct.IsCancellationRequested && mActive)
                {
                    var arch = mArch ?? NineGridArchitecture.Current;
                    var board = arch?.GetModel<BoardModel>();
                    if (board == null || !board.AvatarSlot.Value.IsBoardSlot)
                    {
                        await DelayAsync(0.05f, ct);
                        continue;
                    }

                    var slot = board.AvatarSlot.Value.Index;
                    if (slot != mLastAvatarSlot)
                    {
                        mLastAvatarSlot = slot;
                        HandleAvatarSlot(slot, ct);
                    }

                    await DelayAsync(0.05f, ct);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void HandleAvatarSlot(int slot, CancellationToken parentCt)
        {
            if (slot != AttributeBoardSlotResolver.LeaveSlot)
            {
                mLeaveDwell.Cancel();
                return;
            }

            if (mLeaveDwell.IsArmed && mLeaveDwell.ArmedSlot == slot)
            {
                return;
            }

            mLeaveDwell.Begin(slot, 0);
            RunLeaveDwellAsync(slot, parentCt).Forget();
        }

        private async UniTaskVoid RunLeaveDwellAsync(int slot, CancellationToken parentCt)
        {
            try
            {
                await DelayAsync(RoomIconDwellSession.DefaultDwellSeconds, parentCt);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (!mLeaveDwell.IsArmed || mLeaveDwell.ArmedSlot != slot)
            {
                return;
            }

            if (!mLeaveDwell.TryConsumeArmed(out _))
            {
                return;
            }

            if (await TryLeaveWithTransitionAsync())
            {
                mLeaveDwell.MarkSubmitted();
            }
            else
            {
                mLeaveDwell.Begin(slot, 0);
                RunLeaveDwellAsync(slot, parentCt).Forget();
            }
        }

        private async UniTask<bool> TryLeaveWithTransitionAsync()
        {
            var arch = mArch ?? NineGridArchitecture.Current;
            var transition = RunSceneTransitionService.InstanceOrNull;
            if (transition == null || !transition.IsEnabled)
            {
                return TryLeave();
            }

            var crossFloor = RunSceneTransitionService.WillCrossFloor(arch);
            try
            {
                await transition.BeginCoverAsync(crossFloor, CancellationToken.None);
                if (!TryLeave())
                {
                    transition.ForceClearFaders();
                    return false;
                }

                await transition.CompleteRevealAsync(CancellationToken.None);
                return true;
            }
            catch (OperationCanceledException)
            {
                transition.ForceClearFaders();
                return false;
            }
        }

        private async UniTask DelayAsync(float seconds, CancellationToken ct)
        {
            if (mDelayAsync != null)
            {
                await mDelayAsync(seconds, ct);
                return;
            }

            await UniTask.Delay(TimeSpan.FromSeconds(seconds), cancellationToken: ct);
        }

        private static GameObject TryInstantiate(
            string prefabPath,
            IGroundFieldGeometrySystem geometry,
            int slot,
            string contentId)
        {
            var prefab = LoadPrefab(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[AttributeBoard] missing prefab for " + contentId + " path=" + prefabPath);
                return null;
            }

            var go = UnityEngine.Object.Instantiate(prefab);
            go.name = "AttributeBoard_" + contentId + "_@" + slot;
            BoardSlotWorldPlacement.TryAlignToSlot(go.transform, geometry, slot);

            var sorting = go.GetComponent<SortingGroup>();
            if (sorting != null)
            {
                sorting.sortingOrder = 40 + slot;
            }

            return go;
        }

        private static GameObject LoadPrefab(string path)
        {
            return CardChassisPaths.LoadGameObject(path);
        }

        private static readonly Color SelectedFrameColor = new Color(1f, 0.84f, 0.25f, 1f);
    }
}
