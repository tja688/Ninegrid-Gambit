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
using NineGrid.Flow.InRoomBoard;
using NineGrid.Flow.Presentation;
using NineGrid.Flow.RoomIcons;
using NineGrid.Flow.Transitions;
using NineGrid.Presentation;
using NineGrid.Presentation.Systems;
using QFramework;
using UnityEngine;
using UnityEngine.Rendering;

namespace NineGrid.Flow.RewardBoard
{
    /// <summary>
    /// 特殊奖励房场地：真卡货架 + 离开图标；任意距离点击拿走，踩离开放弃（#94 / ADR-0020）。
    /// </summary>
    public sealed class RewardBoardPresenter
    {
        public static RewardBoardPresenter Current { get; private set; } = new RewardBoardPresenter();

        /// <summary>当前货架真卡（表现权威只读投影，供房内装饰层 InRoomCardLifeFx 聚合）。</summary>
        public IReadOnlyList<ManagedCard> ShelfCards => mShelfCards;

        private readonly List<ManagedCard> mShelfCards = new List<ManagedCard>(5);
        private readonly List<string> mShelfDefIds = new List<string>(5);
        private readonly List<GameObject> mExtras = new List<GameObject>(1);
        private readonly RoomIconDwellSession mLeaveDwell = new RoomIconDwellSession();
        private GameObject mLeaveGo;
        private CancellationTokenSource mResyncCts;
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
            Current = new RewardBoardPresenter();
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
            mResyncCts?.Cancel();
            mResyncCts?.Dispose();
            mResyncCts = null;

            var cards = CardEntityLifecycleHook.CardsOrNull()
                        ?? UnityEngine.Object.FindFirstObjectByType<CardManagerSingleton>();
            for (var i = 0; i < mShelfCards.Count; i++)
            {
                var card = mShelfCards[i];
                if (card == null)
                {
                    continue;
                }

                InRoomOfferClaimLifecycle.ReleaseNow(card);
                if (cards != null)
                {
                    cards.Release(card, "RewardBoard.Despawn");
                }
            }

            mShelfCards.Clear();
            mShelfDefIds.Clear();

            for (var i = 0; i < mExtras.Count; i++)
            {
                if (mExtras[i] != null)
                {
                    InRoomOfferClaimLifecycle.ReleaseNow(mExtras[i]);
                    UnityEngine.Object.Destroy(mExtras[i]);
                }
            }

            mExtras.Clear();
            mLeaveGo = null;
            RoomIconOccupancy.Current.Clear();
            RoomIconOccupancySlotHits.Refresh(mArch);
            BoardBriefTipPresenter.InstanceOrNull()?.HardClear();
        }

        /// <summary>按 PendingChoice 特殊房货架刷板；失败返回 false。</summary>
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
                || pending.Kind.Value != PendingChoiceKind.Reward
                || !PendingChoiceModel.IsSpecialRewardPool(pending.PoolId.Value))
            {
                return false;
            }

            var geometry = arch.GetSystem<IGroundFieldGeometrySystem>();
            var content = arch.GetSystem<IContentSystem>();
            SpawnShelves(pending, geometry, content);
            SpawnLeave(geometry);
            mActive = RoomIconOccupancy.Current.HasAny || mShelfCards.Count > 0;
            if (mActive)
            {
                StartAvatarWatch();
            }

            RoomIconOccupancySlotHits.Refresh(arch);
            return mActive;
        }

        /// <summary>拿走后按 Pending 收尾货架（补位走标准跳格，不整板瞬移）。</summary>
        public void ResyncFromPending(IArchitecture arch)
        {
            if (!mActive)
            {
                TrySpawnFromPending(arch);
                return;
            }

            Bind(arch);
            var pending = arch?.GetModel<PendingChoiceModel>();
            if (pending == null)
            {
                DespawnAll();
                return;
            }

            // 房内开宝箱遗物三选一：特殊房 Pending 被挂起，勿拆板。
            if (!PendingChoiceModel.IsSpecialRewardPool(pending.PoolId.Value))
            {
                if (pending.HasSuspendedConsumerSession)
                {
                    return;
                }

                DespawnAll();
                return;
            }

            RunResyncAsync(arch, pending).Forget();
        }

        private async UniTaskVoid RunResyncAsync(IArchitecture arch, PendingChoiceModel pending)
        {
            mResyncCts?.Cancel();
            mResyncCts?.Dispose();
            mResyncCts = new CancellationTokenSource();
            var ct = mResyncCts.Token;
            try
            {
                RegisterOccupancy(pending);
                await ResyncShelfAsync(arch, pending, ct);
                RoomIconOccupancySlotHits.Refresh(arch);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                mResyncCts?.Dispose();
                mResyncCts = null;
            }
        }

        private void RegisterOccupancy(PendingChoiceModel pending)
        {
            RoomIconOccupancy.Current.Clear();
            var options = pending.RewardOptions;
            for (var i = 0; i < options.Count; i++)
            {
                var entry = options[i];
                if (entry == null || string.IsNullOrEmpty(entry.DefId))
                {
                    continue;
                }

                RoomIconOccupancy.Current.Register(
                    RewardBoardSlotResolver.ShelfSlotAt(i),
                    i,
                    entry.DefId,
                    RoomIconWalkRole.SoftBlockOnly);
            }

            RoomIconOccupancy.Current.Register(
                RewardBoardSlotResolver.LeaveSlot,
                -2,
                RewardBoardSlotResolver.LeaveContentId,
                RoomIconWalkRole.WalkDestination);
        }

        /// <summary>
        /// 货架 diff：保留未拿走货架（换格走标准跳格），新建货架（格上方落下入场），
        /// 释放已拿走货架——不再整板销毁重建造成瞬移（#94 补位美化）。
        /// </summary>
        private async UniTask ResyncShelfAsync(
            IArchitecture arch,
            PendingChoiceModel pending,
            CancellationToken ct)
        {
            var geometry = arch.GetSystem<IGroundFieldGeometrySystem>();
            var content = arch.GetSystem<IContentSystem>();
            var cards = CardEntityLifecycleHook.CardsOrNull()
                        ?? UnityEngine.Object.FindFirstObjectByType<CardManagerSingleton>();
            var newOptions = pending.RewardOptions;

            var oldCards = new List<ManagedCard>(mShelfCards);
            var oldDefIds = new List<string>(mShelfDefIds);
            var keptOld = new bool[oldCards.Count];

            for (var i = 0; i < newOptions.Count; i++)
            {
                var entry = newOptions[i];
                if (entry == null || string.IsNullOrEmpty(entry.DefId))
                {
                    continue;
                }

                InRoomShelfAnimation.TryMatchOldIndex(oldDefIds, keptOld, entry.DefId, i);
            }

            InRoomOfferClaimLifecycle.ReleaseUnkeptShelfClaims(oldCards, null, keptOld);
            keptOld = new bool[oldCards.Count];

            var newCards = new List<ManagedCard>(newOptions.Count);
            var newDefIds = new List<string>(newOptions.Count);
            var animTasks = new List<UniTask>(newOptions.Count);

            for (var i = 0; i < newOptions.Count; i++)
            {
                var entry = newOptions[i];
                if (entry == null || string.IsNullOrEmpty(entry.DefId))
                {
                    newDefIds.Add(null);
                    newCards.Add(null);
                    continue;
                }

                var slot = RewardBoardSlotResolver.ShelfSlotAt(i);
                var oldIndex = InRoomShelfAnimation.TryMatchOldIndex(oldDefIds, keptOld, entry.DefId, i);
                if (oldIndex >= 0 && oldIndex < oldCards.Count && oldCards[oldIndex] != null)
                {
                    var card = oldCards[oldIndex];
                    newCards.Add(card);
                    newDefIds.Add(entry.DefId);
                    var oldSlot = RewardBoardSlotResolver.ShelfSlotAt(oldIndex);
                    var tip = BuildShelfTip(entry.DefId, content);
                    AttachClickProxy(card.View.gameObject, i, tip, slot);
                    if (oldSlot != slot)
                    {
                        animTasks.Add(InRoomShelfAnimation.HopToSlotAsync(card, geometry, slot, ct));
                    }

                    continue;
                }

                newDefIds.Add(entry.DefId);
                ManagedCard managed = null;
                if (cards != null)
                {
                    managed = cards.SpawnPresentationOnly(
                        entry.DefId,
                        parent: null,
                        CardDisplayMode.GroundCardMode,
                        CardPresentationKind.HelpCard);
                }

                newCards.Add(managed);
                if (managed?.View != null)
                {
                    BoardSlotWorldPlacement.TryAlignToSlot(managed.View.transform, geometry, slot);
                    managed.View.transform.rotation = Quaternion.identity;
                    CoreCardPresentationMapper.ApplyVisualsByDefId(managed, CardPresentationKind.HelpCard);
                    var tip = BuildShelfTip(entry.DefId, content);
                    AttachClickProxy(managed.View.gameObject, i, tip, slot);
                    animTasks.Add(InRoomShelfAnimation.DropInToSlotAsync(managed, geometry, slot, ct));
                }
            }

            for (var j = 0; j < oldCards.Count; j++)
            {
                if (!keptOld[j] && oldCards[j] != null)
                {
                    cards?.Release(oldCards[j], "RewardBoard.ResyncRelease");
                }
            }

            mShelfCards.Clear();
            mShelfCards.AddRange(newCards);
            mShelfDefIds.Clear();
            mShelfDefIds.AddRange(newDefIds);

            if (animTasks.Count > 0)
            {
                await UniTask.WhenAll(animTasks);
            }
        }

        private void SpawnShelves(
            PendingChoiceModel pending,
            IGroundFieldGeometrySystem geometry,
            IContentSystem content)
        {
            var cards = CardEntityLifecycleHook.CardsOrNull()
                        ?? UnityEngine.Object.FindFirstObjectByType<CardManagerSingleton>();
            var options = pending.RewardOptions;
            for (var i = 0; i < options.Count && i < RewardBoardSlotResolver.ShelfSlots.Length; i++)
            {
                var entry = options[i];
                mShelfDefIds.Add(entry == null ? null : entry.DefId);
                if (entry == null || string.IsNullOrEmpty(entry.DefId))
                {
                    mShelfCards.Add(null);
                    continue;
                }

                var slot = RewardBoardSlotResolver.ShelfSlotAt(i);
                RoomIconOccupancy.Current.Register(
                    slot, i, entry.DefId, RoomIconWalkRole.SoftBlockOnly);

                if (cards == null)
                {
                    mShelfCards.Add(null);
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
                    mShelfCards.Add(null);
                    continue;
                }

                BoardSlotWorldPlacement.TryAlignToSlot(managed.View.transform, geometry, slot);
                managed.View.transform.rotation = Quaternion.identity;

                CoreCardPresentationMapper.ApplyVisualsByDefId(managed, CardPresentationKind.HelpCard);
                var tip = BuildShelfTip(entry.DefId, content);
                AttachClickProxy(managed.View.gameObject, i, tip, slot);
                mShelfCards.Add(managed);
            }
        }

        private void SpawnLeave(IGroundFieldGeometrySystem geometry)
        {
            var slot = RewardBoardSlotResolver.LeaveSlot;
            RoomIconOccupancy.Current.Register(
                slot,
                -2,
                RewardBoardSlotResolver.LeaveContentId,
                RoomIconWalkRole.WalkDestination);

            var path = BoardNavigationIconResolver.ResolveLeaveIconPrefab(mArch);
            var go = TryInstantiate(path, geometry, slot, RewardBoardSlotResolver.LeaveContentId);
            if (go == null)
            {
                return;
            }

            AttachBriefTipOnly(go, BoardBriefTipCopy.ForLeave(mArch), slot, geometry);
            mExtras.Add(go);
            mLeaveGo = go;
        }

        private static string BuildShelfTip(string defId, IContentSystem content)
        {
            return BoardBriefTipCopy.ForCard(defId, content, null);
        }

        private void AttachClickProxy(
            GameObject go,
            int shelfIndex,
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

            var proxy = go.GetComponent<RewardBoardHitProxy>();
            if (proxy == null)
            {
                proxy = go.AddComponent<RewardBoardHitProxy>();
            }

            proxy.Configure(shelfIndex, tip, HandleTake, boardSlot);
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

        private void HandleTake(int shelfIndex)
        {
            var arch = mArch ?? NineGridArchitecture.Current;
            if (arch == null)
            {
                return;
            }

            Debug.Log(
                "[RewardBoard] Take shelfIndex=" + shelfIndex
                + " choiceOverlay=" + PresentationInputGates.ChoiceOverlayActive
                + " owner=" + PresentationInputGates.CurrentOwner);

            RewardChoiceCoreHook.RequestWire();
            if (RewardChoiceCoreHook.SelectReward == null)
            {
                ShowNotice(NineGrid.Core.Localization.L10n.Tr("notice.reward_not_wired", "奖励房输入未接线"));
                return;
            }

            var logStart = InRoomGoldPresentation.CaptureEventLogCount(arch);
            var pending = arch.GetModel<PendingChoiceModel>();
            var contentId = string.Empty;
            if (pending != null
                && shelfIndex >= 0
                && shelfIndex < pending.RewardOptions.Count)
            {
                contentId = pending.RewardOptions[shelfIndex]?.DefId ?? string.Empty;
            }

            var result = RewardChoiceCoreHook.SelectReward(shelfIndex);
            if (result == null || !result.Accepted)
            {
                Debug.LogWarning(
                    "[RewardBoard] Take rejected shelfIndex=" + shelfIndex
                    + " reason=" + (result?.Reason ?? string.Empty));
                if (!string.IsNullOrEmpty(result?.Reason))
                {
                    ShowNotice(result.Reason);
                }

                return;
            }

            FlowRoomEconomyAudioCues.Pulse(
                FlowRoomEconomyAudioCues.RewardClaim,
                "RewardBoardPresenter.HandleTake",
                contentId);
            Debug.Log("[RewardBoard] Take accepted shelfIndex=" + shelfIndex);
            // ADR-0025：领取直写 ItemSlots；货架纯表现卡须换成 Core uid 并接入手牌，勿只碎裂。
            PresentShelfAcquireOrShatter(shelfIndex, arch, logStart);
            ResyncFromPending(arch);
        }

        private void PresentShelfAcquireOrShatter(int shelfIndex, IArchitecture arch, int logStart)
        {
            ManagedCard shelf = null;
            if (shelfIndex >= 0 && shelfIndex < mShelfCards.Count)
            {
                shelf = mShelfCards[shelfIndex];
                mShelfCards[shelfIndex] = null;
            }

            if (InRoomItemAcquirePresentation.TryAcquireShelfHelpCardToHand(arch, logStart, shelf))
            {
                return;
            }

            if (shelf == null)
            {
                return;
            }

            InRoomShelfAnimation.PlayConsumeDeath(shelf);
            var cards = CardEntityLifecycleHook.CardsOrNull()
                        ?? UnityEngine.Object.FindFirstObjectByType<CardManagerSingleton>();
            cards?.Release(shelf, "RewardBoard.TakeShatter");
        }

        private bool TryLeave()
        {
            RewardChoiceCoreHook.RequestWire();
            if (RewardChoiceCoreHook.SkipHelpChoice == null)
            {
                Debug.LogWarning("[RewardBoard] SkipHelpChoice hook not wired; abort leave.");
                return false;
            }

            var result = RewardChoiceCoreHook.SkipHelpChoice();
            if (result != null && result.Accepted)
            {
                FlowRoomEconomyAudioCues.Pulse(
                    FlowRoomEconomyAudioCues.RewardAbandon,
                    "RewardBoardPresenter.TryLeave");
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
            if (slot != RewardBoardSlotResolver.LeaveSlot)
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
                Debug.LogWarning("[RewardBoard] missing prefab for " + contentId + " path=" + prefabPath);
                return null;
            }

            var go = UnityEngine.Object.Instantiate(prefab);
            go.name = "RewardBoard_" + contentId + "_@" + slot;
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
    }
}
