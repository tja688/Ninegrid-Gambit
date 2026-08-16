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
using NineGrid.Flow.PurchaseAmountTip;
using NineGrid.Flow.RoomIcons;
using NineGrid.Flow.Transitions;
using NineGrid.Presentation;
using NineGrid.Presentation.Systems;
using QFramework;
using UnityEngine;
using UnityEngine.Rendering;

namespace NineGrid.Flow.TavernBoard
{
    /// <summary>
    /// 卡店房场地：服务选项 + 刷新 + 离开；「道具卡固定」二级时隐藏主面、铺候选；确认后未选碎裂并下架 FixItem 直至刷新（#93 / ADR-0020）。
    /// </summary>
    public sealed class TavernBoardPresenter
    {
        public static string CancelNestedTip =>
            NineGrid.Core.Localization.L10n.Tr("briefTip.cancel_select", "取消选择");

        public static TavernBoardPresenter Current { get; private set; } = new TavernBoardPresenter();

        /// <summary>当前二级候选真卡（表现权威只读投影，供房内装饰层 InRoomCardLifeFx 聚合）。</summary>
        public IReadOnlyList<ManagedCard> CandidateCards => mCandidateCards;

        /// <summary>当前服务选项卡（非塔型选项，供 InRoomCardLifeFx 聚合）。</summary>
        public IReadOnlyList<GameObject> ServiceGos => mServiceGos;

        private readonly List<ManagedCard> mCandidateCards = new List<ManagedCard>(6);
        private readonly List<string> mCandidateDefIds = new List<string>(6);
        private readonly List<GameObject> mServiceGos = new List<GameObject>(3);
        private readonly List<string> mServiceDefIds = new List<string>(3);
        private readonly List<GameObject> mExtras = new List<GameObject>(2);
        private readonly Dictionary<string, int> mServiceSlotByDefId = new Dictionary<string, int>(3);
        private readonly List<int> mCandidateSlots = new List<int>(6);
        private readonly RoomIconDwellSession mLeaveDwell = new RoomIconDwellSession();
        private bool mReplanServiceSlots;
        private GameObject mRefreshGo;
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
            Current = new TavernBoardPresenter();
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
            for (var i = 0; i < mCandidateCards.Count; i++)
            {
                var card = mCandidateCards[i];
                if (card == null)
                {
                    continue;
                }

                if (cards != null)
                {
                    cards.Release(card, "TavernBoard.Despawn");
                }
            }

            mCandidateCards.Clear();
            mCandidateDefIds.Clear();

            for (var i = 0; i < mServiceGos.Count; i++)
            {
                if (mServiceGos[i] != null)
                {
                    UnityEngine.Object.Destroy(mServiceGos[i]);
                }
            }

            mServiceGos.Clear();
            mServiceDefIds.Clear();
            mServiceSlotByDefId.Clear();
            mCandidateSlots.Clear();
            mReplanServiceSlots = false;

            for (var i = 0; i < mExtras.Count; i++)
            {
                if (mExtras[i] != null)
                {
                    UnityEngine.Object.Destroy(mExtras[i]);
                }
            }

            mExtras.Clear();
            mRefreshGo = null;
            mLeaveGo = null;
            PurchaseAmountTipPresenter.HideAll();
            RoomIconOccupancy.Current.Clear();
            RoomIconOccupancySlotHits.Refresh(mArch);
            BoardBriefTipPresenter.InstanceOrNull()?.HardClear();
        }

        /// <summary>按 PendingChoice 卡店会话刷板；失败返回 false。</summary>
        public bool TrySpawnFromPending(IArchitecture arch)
        {
            Bind(arch);
            DespawnAll();
            if (arch == null)
            {
                return false;
            }

            var pending = arch.GetModel<PendingChoiceModel>();
            if (pending == null || pending.Kind.Value != PendingChoiceKind.Reward)
            {
                return false;
            }

            var poolId = pending.PoolId.Value;
            var geometry = arch.GetSystem<IGroundFieldGeometrySystem>();
            var content = arch.GetSystem<IContentSystem>();

            if (PendingChoiceModel.IsTavernFixItemPool(poolId))
            {
                SpawnFixCandidates(pending, geometry, content);
                SpawnLeave(geometry, nested: true);
            }
            else if (PendingChoiceModel.IsTavernPool(poolId))
            {
                mReplanServiceSlots = true;
                SpawnServices(pending, geometry, content);
                SpawnRefresh(geometry, pending.ShopRefreshPriceGold.Value);
                SpawnLeave(geometry, nested: false);
            }
            else
            {
                return false;
            }

            // 须先置 Active：Watch 循环在首个 await 前检查 mActive，否则离开驻留永不明火。
            mActive = RoomIconOccupancy.Current.HasAny
                      || mExtras.Count > 0
                      || mCandidateCards.Count > 0;
            if (mActive)
            {
                StartAvatarWatch();
            }

            RoomIconOccupancySlotHits.Refresh(arch);
            return mActive;
        }

        /// <summary>会话变更（服务购买 / 二级确认 / 刷新 / 取消）后按 Pending 收尾（补位入场，不整板瞬移）。</summary>
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

            // 房内开宝箱遗物三选一：卡店 Pending 被挂起，勿拆板。
            if (!PendingChoiceModel.IsConsumerBoardPool(pending.PoolId.Value)
                || PendingChoiceModel.IsShopPool(pending.PoolId.Value))
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
                if (PendingChoiceModel.IsTavernFixItemPool(pending.PoolId.Value))
                {
                    // 二级选择：隐藏服务/刷新，只留候选 + 取消离开。
                    HideMainSurfaceExceptLeave();
                    EnsureLeave(arch, nested: true);
                    await ResyncCandidatesAsync(arch, pending, ct);
                }
                else
                {
                    // 回主面：清掉候选（确认时已碎裂的跳过），重建服务/刷新。
                    ClearCandidatesQuiet();
                    await ResyncServicesAsync(arch, pending, ct);
                    EnsureRefresh(arch, pending.ShopRefreshPriceGold.Value);
                    EnsureLeave(arch, nested: false);
                    RefreshRefreshTip(pending);
                }

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
            var nested = PendingChoiceModel.IsTavernFixItemPool(pending.PoolId.Value);
            for (var i = 0; i < options.Count; i++)
            {
                var entry = options[i];
                if (entry == null || string.IsNullOrEmpty(entry.DefId))
                {
                    continue;
                }

                var slot = nested
                    ? ResolveCandidateSlot(i)
                    : ResolveServiceSlot(entry.DefId);
                if (slot <= 0)
                {
                    continue;
                }

                RoomIconOccupancy.Current.Register(
                    slot,
                    i,
                    entry.DefId,
                    RoomIconWalkRole.SoftBlockOnly);
            }

            if (!nested)
            {
                RoomIconOccupancy.Current.Register(
                    TavernBoardSlotResolver.RefreshSlot,
                    -1,
                    TavernBoardSlotResolver.RefreshContentId,
                    RoomIconWalkRole.SoftBlockOnly);
            }

            RoomIconOccupancy.Current.Register(
                TavernBoardSlotResolver.LeaveSlot,
                -2,
                TavernBoardSlotResolver.LeaveContentId,
                RoomIconWalkRole.WalkDestination);
        }

        private int ResolveAvatarSlot()
        {
            var board = mArch?.GetModel<BoardModel>() ?? NineGridArchitecture.Current?.GetModel<BoardModel>();
            if (board != null && board.AvatarSlot.Value.IsBoardSlot)
            {
                return board.AvatarSlot.Value.Index;
            }

            return TavernBoardSlotResolver.AvatarSlot;
        }

        private int ResolveServiceSlot(string defId)
        {
            if (string.IsNullOrEmpty(defId))
            {
                return 0;
            }

            if (mServiceSlotByDefId.TryGetValue(defId, out var assigned))
            {
                return assigned;
            }

            return TavernBoardSlotResolver.HomeSlotForService(defId);
        }

        private int ResolveCandidateSlot(int candidateIndex)
        {
            if (candidateIndex >= 0 && candidateIndex < mCandidateSlots.Count)
            {
                return mCandidateSlots[candidateIndex];
            }

            return TavernBoardSlotResolver.CandidateSlotAt(candidateIndex);
        }

        private void PlanServiceSlots(PendingChoiceModel pending)
        {
            mServiceSlotByDefId.Clear();
            var options = pending.RewardOptions;
            var preferred = new List<int>(options.Count);
            for (var i = 0; i < options.Count; i++)
            {
                var entry = options[i];
                preferred.Add(entry == null ? 0 : TavernBoardSlotResolver.HomeSlotForService(entry.DefId));
            }

            var board = mArch?.GetModel<BoardModel>() ?? NineGridArchitecture.Current?.GetModel<BoardModel>();
            var avatarSlot = ResolveAvatarSlot();
            var planned = InRoomOfferSlotPlanner.Plan(
                board,
                options.Count,
                avatarSlot,
                preferred,
                TavernBoardSlotResolver.ServiceSlots,
                TavernBoardSlotResolver.LeaveSlot,
                TavernBoardSlotResolver.RefreshSlot);

            for (var i = 0; i < options.Count && i < planned.Length; i++)
            {
                var entry = options[i];
                if (entry == null || string.IsNullOrEmpty(entry.DefId) || planned[i] <= 0)
                {
                    continue;
                }

                mServiceSlotByDefId[entry.DefId] = planned[i];
            }
        }

        private void PlanCandidateSlots(PendingChoiceModel pending)
        {
            mCandidateSlots.Clear();
            var options = pending.RewardOptions;
            var preferred = new List<int>(options.Count);
            for (var i = 0; i < options.Count; i++)
            {
                preferred.Add(TavernBoardSlotResolver.CandidateSlotAt(i));
            }

            var board = mArch?.GetModel<BoardModel>() ?? NineGridArchitecture.Current?.GetModel<BoardModel>();
            var avatarSlot = ResolveAvatarSlot();
            var planned = InRoomOfferSlotPlanner.Plan(
                board,
                options.Count,
                avatarSlot,
                preferred,
                TavernBoardSlotResolver.CandidateFallbackPool,
                TavernBoardSlotResolver.LeaveSlot,
                TavernBoardSlotResolver.RefreshSlot,
                reserveRefresh: false);

            for (var i = 0; i < planned.Length; i++)
            {
                mCandidateSlots.Add(planned[i]);
            }
        }

        private void RefreshRefreshTip(PendingChoiceModel pending)
        {
            if (mRefreshGo == null)
            {
                return;
            }

            var tip = BoardBriefTipCopy.ForOptionOrShelf(
                NineGrid.Core.Localization.L10n.Tr("briefTip.refresh_shelf", "刷新货架"),
                pending.ShopRefreshPriceGold.Value);
            AttachClickProxy(
                mRefreshGo,
                TavernBoardHitKind.Refresh,
                -1,
                tip,
                TavernBoardSlotResolver.RefreshSlot,
                pending.ShopRefreshPriceGold.Value);
        }

        /// <summary>
        /// 服务面 diff：保留仍在售服务，重建被购服务（格上方落下入场），释放已离场服务。
        /// </summary>
        private async UniTask ResyncServicesAsync(
            IArchitecture arch,
            PendingChoiceModel pending,
            CancellationToken ct)
        {
            var replan = mReplanServiceSlots;
            if (replan)
            {
                PlanServiceSlots(pending);
                mReplanServiceSlots = false;
            }

            var geometry = arch.GetSystem<IGroundFieldGeometrySystem>();
            var content = arch.GetSystem<IContentSystem>();
            var newOptions = pending.RewardOptions;

            var oldGos = new List<GameObject>(mServiceGos);
            var oldDefIds = new List<string>(mServiceDefIds);
            var keptOld = new bool[oldGos.Count];

            var newGos = new List<GameObject>(newOptions.Count);
            var newDefIds = new List<string>(newOptions.Count);
            var animTasks = new List<UniTask>(newOptions.Count);

            for (var i = 0; i < newOptions.Count; i++)
            {
                var entry = newOptions[i];
                if (entry == null || string.IsNullOrEmpty(entry.DefId))
                {
                    newDefIds.Add(null);
                    newGos.Add(null);
                    continue;
                }

                var slot = ResolveServiceSlot(entry.DefId);
                if (!replan)
                {
                    var oldIndex = InRoomShelfAnimation.TryMatchOldIndex(oldDefIds, keptOld, entry.DefId, i);
                    if (oldIndex >= 0 && oldIndex < oldGos.Count && oldGos[oldIndex] != null)
                    {
                        var go = oldGos[oldIndex];
                        newGos.Add(go);
                        newDefIds.Add(entry.DefId);
                        var tip = BuildServiceTip(entry.DefId, content);
                        AttachClickProxy(go, TavernBoardHitKind.SelectService, i, tip, slot, ResolveServicePriceGold(entry.DefId, content));
                        mServiceSlotByDefId[entry.DefId] = slot;
                        continue;
                    }
                }

                newDefIds.Add(entry.DefId);
                var fresh = TryInstantiate(
                    CardChassisPaths.RoomOptionFacePrefab,
                    geometry,
                    slot,
                    entry.DefId);
                newGos.Add(fresh);
                if (fresh != null)
                {
                    var tip = BuildServiceTip(entry.DefId, content);
                    AttachClickProxy(fresh, TavernBoardHitKind.SelectService, i, tip, slot, ResolveServicePriceGold(entry.DefId, content));
                    mServiceSlotByDefId[entry.DefId] = slot;
                    animTasks.Add(InRoomShelfAnimation.DropOptionGoInAsync(fresh, geometry, slot, ct));
                }
            }

            for (var j = 0; j < oldGos.Count; j++)
            {
                if (!keptOld[j] && oldGos[j] != null)
                {
                    UnityEngine.Object.Destroy(oldGos[j]);
                }
            }

            mServiceGos.Clear();
            mServiceGos.AddRange(newGos);
            mServiceDefIds.Clear();
            mServiceDefIds.AddRange(newDefIds);

            if (animTasks.Count > 0)
            {
                await UniTask.WhenAll(animTasks);
            }
        }

        /// <summary>
        /// 候选面 diff：保留未确认候选（换格走标准跳格），新建候选入场，释放已确认候选。
        /// </summary>
        private async UniTask ResyncCandidatesAsync(
            IArchitecture arch,
            PendingChoiceModel pending,
            CancellationToken ct)
        {
            PlanCandidateSlots(pending);

            var geometry = arch.GetSystem<IGroundFieldGeometrySystem>();
            var content = arch.GetSystem<IContentSystem>();
            var cards = CardEntityLifecycleHook.CardsOrNull()
                        ?? UnityEngine.Object.FindFirstObjectByType<CardManagerSingleton>();
            var newOptions = pending.RewardOptions;

            var oldCards = new List<ManagedCard>(mCandidateCards);
            var oldDefIds = new List<string>(mCandidateDefIds);
            var keptOld = new bool[oldCards.Count];

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

                var slot = ResolveCandidateSlot(i);
                var oldIndex = InRoomShelfAnimation.TryMatchOldIndex(oldDefIds, keptOld, entry.DefId, i);
                if (oldIndex >= 0 && oldIndex < oldCards.Count && oldCards[oldIndex] != null)
                {
                    var card = oldCards[oldIndex];
                    newCards.Add(card);
                    newDefIds.Add(entry.DefId);
                    var oldSlot = ResolveCandidateSlot(oldIndex);
                    var tip = BuildCandidateTip(entry.DefId, content);
                    AttachClickProxy(
                        card.View.gameObject,
                        TavernBoardHitKind.SelectFixCandidate,
                        i,
                        tip,
                        slot,
                        ResolveServicePriceGold(RewardSystem.TavernFixItemDefId, content));
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
                    var tip = BuildCandidateTip(entry.DefId, content);
                    AttachClickProxy(
                        managed.View.gameObject,
                        TavernBoardHitKind.SelectFixCandidate,
                        i,
                        tip,
                        slot,
                        ResolveServicePriceGold(RewardSystem.TavernFixItemDefId, content));
                    animTasks.Add(InRoomShelfAnimation.DropInToSlotAsync(managed, geometry, slot, ct));
                }
            }

            for (var j = 0; j < oldCards.Count; j++)
            {
                if (!keptOld[j] && oldCards[j] != null)
                {
                    cards?.Release(oldCards[j], "TavernBoard.ResyncRelease");
                }
            }

            mCandidateCards.Clear();
            mCandidateCards.AddRange(newCards);
            mCandidateDefIds.Clear();
            mCandidateDefIds.AddRange(newDefIds);

            if (animTasks.Count > 0)
            {
                await UniTask.WhenAll(animTasks);
            }
        }

        private void SpawnServices(
            PendingChoiceModel pending,
            IGroundFieldGeometrySystem geometry,
            IContentSystem content)
        {
            PlanServiceSlots(pending);
            mReplanServiceSlots = false;

            var options = pending.RewardOptions;
            for (var i = 0; i < options.Count; i++)
            {
                var entry = options[i];
                mServiceDefIds.Add(entry == null ? null : entry.DefId);
                if (entry == null || string.IsNullOrEmpty(entry.DefId))
                {
                    mServiceGos.Add(null);
                    continue;
                }

                var slot = ResolveServiceSlot(entry.DefId);
                if (slot <= 0)
                {
                    mServiceGos.Add(null);
                    continue;
                }

                RoomIconOccupancy.Current.Register(
                    slot, i, entry.DefId, RoomIconWalkRole.SoftBlockOnly);

                var go = TryInstantiate(
                    CardChassisPaths.RoomOptionFacePrefab,
                    geometry,
                    slot,
                    entry.DefId);
                mServiceGos.Add(go);
                if (go == null)
                {
                    continue;
                }

                var tip = BuildServiceTip(entry.DefId, content);
                AttachClickProxy(go, TavernBoardHitKind.SelectService, i, tip, slot, ResolveServicePriceGold(entry.DefId, content));
            }
        }

        private void SpawnFixCandidates(
            PendingChoiceModel pending,
            IGroundFieldGeometrySystem geometry,
            IContentSystem content)
        {
            PlanCandidateSlots(pending);

            var cards = CardEntityLifecycleHook.CardsOrNull()
                        ?? UnityEngine.Object.FindFirstObjectByType<CardManagerSingleton>();
            var options = pending.RewardOptions;
            for (var i = 0; i < options.Count; i++)
            {
                var entry = options[i];
                mCandidateDefIds.Add(entry == null ? null : entry.DefId);
                if (entry == null || string.IsNullOrEmpty(entry.DefId))
                {
                    mCandidateCards.Add(null);
                    continue;
                }

                var slot = ResolveCandidateSlot(i);
                if (slot <= 0)
                {
                    mCandidateCards.Add(null);
                    continue;
                }

                RoomIconOccupancy.Current.Register(
                    slot, i, entry.DefId, RoomIconWalkRole.SoftBlockOnly);

                if (cards == null)
                {
                    mCandidateCards.Add(null);
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
                    mCandidateCards.Add(null);
                    continue;
                }

                BoardSlotWorldPlacement.TryAlignToSlot(managed.View.transform, geometry, slot);
                managed.View.transform.rotation = Quaternion.identity;

                CoreCardPresentationMapper.ApplyVisualsByDefId(managed, CardPresentationKind.HelpCard);
                var tip = BuildCandidateTip(entry.DefId, content);
                AttachClickProxy(
                    managed.View.gameObject,
                    TavernBoardHitKind.SelectFixCandidate,
                    i,
                    tip,
                    slot,
                    ResolveServicePriceGold(RewardSystem.TavernFixItemDefId, content));
                mCandidateCards.Add(managed);
            }
        }

        private void SpawnRefresh(IGroundFieldGeometrySystem geometry, int refreshPrice)
        {
            var slot = TavernBoardSlotResolver.RefreshSlot;
            RoomIconOccupancy.Current.Register(
                slot,
                -1,
                TavernBoardSlotResolver.RefreshContentId,
                RoomIconWalkRole.SoftBlockOnly);

            var go = TryInstantiate(
                CardChassisPaths.RoomOptionFacePrefab,
                geometry,
                slot,
                TavernBoardSlotResolver.RefreshContentId);
            if (go == null)
            {
                return;
            }

            var tip = BoardBriefTipCopy.ForOptionOrShelf(
                NineGrid.Core.Localization.L10n.Tr("briefTip.refresh_shelf", "刷新货架"),
                refreshPrice);
            AttachClickProxy(go, TavernBoardHitKind.Refresh, -1, tip, TavernBoardSlotResolver.RefreshSlot, refreshPrice);
            mExtras.Add(go);
            mRefreshGo = go;
        }

        private void SpawnLeave(IGroundFieldGeometrySystem geometry, bool nested)
        {
            var slot = TavernBoardSlotResolver.LeaveSlot;
            RoomIconOccupancy.Current.Register(
                slot,
                -2,
                TavernBoardSlotResolver.LeaveContentId,
                RoomIconWalkRole.WalkDestination);

            var path = CardChassisPaths.ResolveRoomIconPrefab(TavernBoardSlotResolver.LeaveContentId, null);
            var go = TryInstantiate(path, geometry, slot, TavernBoardSlotResolver.LeaveContentId);
            if (go == null)
            {
                return;
            }

            var tip = nested ? CancelNestedTip : BoardBriefTipCopy.LeaveTip;
            AttachBriefTipOnly(go, tip, slot, geometry);
            mExtras.Add(go);
            mLeaveGo = go;
        }

        /// <summary>
        /// 卡店服务当前价（与 Core 扣费同口径：RewardSystem.ResolveTavernServicePrice，
        /// 底价取目录 JSON gold，强化/扩容按本局已购次数步进）。
        /// </summary>
        private int ResolveServicePriceGold(string defId, IContentSystem content)
        {
            var catalogPrice = ResolveCatalogPriceGold(defId, content);
            var player = mArch?.GetModel<PlayerModel>() ?? NineGridArchitecture.Current?.GetModel<PlayerModel>();
            return RewardSystem.ResolveTavernServicePrice(player, defId, catalogPrice);
        }

        private static int ResolveCatalogPriceGold(string defId, IContentSystem content)
        {
            if (content != null && content.HasCatalog
                && content.Catalog.Cards.TryGetValue(defId, out var card)
                && card != null
                && card.Price > 0)
            {
                return card.Price;
            }

            if (CardPresentationConfigCatalog.TryGet(defId, out var dto) && dto != null && dto.gold > 0)
            {
                return dto.gold;
            }

            return 0;
        }

        private string BuildServiceTip(string defId, IContentSystem content)
        {
            string name = defId;
            string brief = string.Empty;
            var price = ResolveServicePriceGold(defId, content);

            if (content != null && content.HasCatalog
                && content.Catalog.Cards.TryGetValue(defId, out var card)
                && card != null)
            {
                if (!string.IsNullOrWhiteSpace(card.DisplayName))
                {
                    name = card.DisplayName;
                }
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
            return BoardBriefTipCopy.ForOptionOrShelf(body, price > 0 ? (int?)price : null);
        }

        private string BuildCandidateTip(string defId, IContentSystem content)
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

            var price = ResolveServicePriceGold(RewardSystem.TavernFixItemDefId, content);
            var body = string.IsNullOrWhiteSpace(brief) ? name : name + "：" + brief.Trim();
            return BoardBriefTipCopy.ForOptionOrShelf(body, price > 0 ? (int?)price : null);
        }

        private void AttachClickProxy(
            GameObject go,
            TavernBoardHitKind kind,
            int optionIndex,
            string tip,
            int boardSlot,
            int amountGold = 0)
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

            var proxy = go.GetComponent<TavernBoardHitProxy>();
            if (proxy == null)
            {
                proxy = go.AddComponent<TavernBoardHitProxy>();
            }

            proxy.Configure(kind, optionIndex, tip, HandleHit, boardSlot, amountGold);
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

        private void HandleHit(TavernBoardHitKind kind, int optionIndex)
        {
            Debug.Log(
                "[TavernBoard] Hit kind=" + kind
                + " optionIndex=" + optionIndex
                + " choiceOverlay=" + PresentationInputGates.ChoiceOverlayActive
                + " owner=" + PresentationInputGates.CurrentOwner);
            switch (kind)
            {
                case TavernBoardHitKind.SelectService:
                case TavernBoardHitKind.SelectFixCandidate:
                    TrySelect(optionIndex);
                    break;
                case TavernBoardHitKind.Refresh:
                    TryRefresh();
                    break;
            }
        }

        private void TrySelect(int optionIndex)
        {
            var arch = mArch ?? NineGridArchitecture.Current;
            if (arch == null)
            {
                return;
            }

            RewardChoiceCoreHook.RequestWire();
            if (RewardChoiceCoreHook.SelectReward == null)
            {
                ShowNotice(NineGrid.Core.Localization.L10n.Tr("notice.tavern_not_wired", "卡店输入未接线"));
                return;
            }

            var logStart = InRoomGoldPresentation.CaptureEventLogCount(arch);
            var pendingBefore = arch.GetModel<PendingChoiceModel>();
            var wasNested = pendingBefore != null
                            && PendingChoiceModel.IsTavernFixItemPool(pendingBefore.PoolId.Value);
            var contentId = string.Empty;
            if (pendingBefore != null
                && optionIndex >= 0
                && optionIndex < pendingBefore.RewardOptions.Count)
            {
                contentId = pendingBefore.RewardOptions[optionIndex]?.DefId ?? string.Empty;
            }

            var result = RewardChoiceCoreHook.SelectReward(optionIndex);
            if (result == null || !result.Accepted)
            {
                var reason = result?.Reason ?? string.Empty;
                if (FlowRoomEconomyAudioCues.IsInsufficientGoldReason(reason))
                {
                    FlowRoomEconomyAudioCues.Pulse(
                        FlowRoomEconomyAudioCues.TavernInsufficientGold,
                        "TavernBoardPresenter.TrySelect");
                    ShowNotice(NineGrid.Core.Localization.L10n.Tr("notice.gold_insufficient", "金币不足"));
                }
                else if (string.Equals(reason, "No item source pool", StringComparison.Ordinal))
                {
                    ShowNotice(NineGrid.Core.Localization.L10n.Tr("notice.no_fixable_item", "暂无可固定的道具卡"));
                }
                else if (string.Equals(reason, "Item deck budget full", StringComparison.Ordinal))
                {
                    ShowNotice(NineGrid.Core.Localization.L10n.Tr("notice.fix_budget_full", "塞卡预算已满"));
                }
                else if (!string.IsNullOrEmpty(reason))
                {
                    ShowNotice(reason);
                }

                return;
            }

            FlowRoomEconomyAudioCues.Pulse(
                FlowRoomEconomyAudioCues.TavernBuy,
                "TavernBoardPresenter.TrySelect",
                contentId);
            PurchaseAmountTipPresenter.HideAll();
            InRoomGoldPresentation.PresentGoldChangesSince(arch, logStart);

            if (wasNested)
            {
                // 确认固定：选中卡碎裂，剩余未选候选一并碎裂退场，再回主面（FixItem 已从货架移除）。
                ConsumeCandidate(optionIndex);
                ShatterRemainingCandidates();
            }
            else
            {
                // 消耗表演：服务购买（扩容/强化）走标准 Death；点「固定」进二级时直接退场该选项。
                ConsumeService(optionIndex);
            }

            ResyncFromPending(arch);
        }

        private void ConsumeService(int optionIndex)
        {
            if (optionIndex < 0 || optionIndex >= mServiceGos.Count)
            {
                return;
            }

            var defId = optionIndex < mServiceDefIds.Count ? mServiceDefIds[optionIndex] : null;
            var go = mServiceGos[optionIndex];
            mServiceGos[optionIndex] = null;
            if (go == null)
            {
                return;
            }

            // 「道具卡固定」是进入二级选择而非消耗，直接退场即可。
            if (string.Equals(defId, RewardSystem.TavernFixItemDefId, StringComparison.Ordinal))
            {
                UnityEngine.Object.Destroy(go);
                return;
            }

            InRoomShelfAnimation.PlayConsumeDeath(go.transform);
            UnityEngine.Object.Destroy(go);
        }

        private void ConsumeCandidate(int optionIndex)
        {
            if (optionIndex < 0 || optionIndex >= mCandidateCards.Count)
            {
                return;
            }

            var card = mCandidateCards[optionIndex];
            mCandidateCards[optionIndex] = null;
            if (optionIndex < mCandidateDefIds.Count)
            {
                mCandidateDefIds[optionIndex] = null;
            }

            if (card == null)
            {
                return;
            }

            InRoomShelfAnimation.PlayConsumeDeath(card);
            var cards = CardEntityLifecycleHook.CardsOrNull()
                        ?? UnityEngine.Object.FindFirstObjectByType<CardManagerSingleton>();
            cards?.Release(card, "TavernBoard.FixConsumed");
        }

        /// <summary>确认固定后：剩余未选候选一律碎裂退场。</summary>
        private void ShatterRemainingCandidates()
        {
            var cards = CardEntityLifecycleHook.CardsOrNull()
                        ?? UnityEngine.Object.FindFirstObjectByType<CardManagerSingleton>();
            for (var i = 0; i < mCandidateCards.Count; i++)
            {
                var card = mCandidateCards[i];
                if (card == null)
                {
                    continue;
                }

                mCandidateCards[i] = null;
                if (i < mCandidateDefIds.Count)
                {
                    mCandidateDefIds[i] = null;
                }

                InRoomShelfAnimation.PlayConsumeDeath(card);
                cards?.Release(card, "TavernBoard.FixUnselectedShatter");
            }

            mCandidateCards.Clear();
            mCandidateDefIds.Clear();
        }

        /// <summary>回主面 / 取消二级：静默释放仍在场的候选（确认路径已碎裂过的会是空槽）。</summary>
        private void ClearCandidatesQuiet()
        {
            var cards = CardEntityLifecycleHook.CardsOrNull()
                        ?? UnityEngine.Object.FindFirstObjectByType<CardManagerSingleton>();
            for (var i = 0; i < mCandidateCards.Count; i++)
            {
                var card = mCandidateCards[i];
                if (card == null)
                {
                    continue;
                }

                cards?.Release(card, "TavernBoard.ClearCandidates");
            }

            mCandidateCards.Clear();
            mCandidateDefIds.Clear();
        }

        /// <summary>进入二级选择：销毁服务选项与刷新，保留离开（tip 改为取消）。</summary>
        private void HideMainSurfaceExceptLeave()
        {
            PurchaseAmountTipPresenter.HideAll();
            for (var i = 0; i < mServiceGos.Count; i++)
            {
                if (mServiceGos[i] != null)
                {
                    UnityEngine.Object.Destroy(mServiceGos[i]);
                }
            }

            mServiceGos.Clear();
            mServiceDefIds.Clear();

            if (mRefreshGo != null)
            {
                UnityEngine.Object.Destroy(mRefreshGo);
                mExtras.Remove(mRefreshGo);
                mRefreshGo = null;
            }
        }

        private void EnsureRefresh(IArchitecture arch, int refreshPrice)
        {
            if (mRefreshGo != null)
            {
                return;
            }

            var geometry = arch?.GetSystem<IGroundFieldGeometrySystem>();
            if (geometry == null)
            {
                return;
            }

            SpawnRefresh(geometry, refreshPrice);
        }

        private void EnsureLeave(IArchitecture arch, bool nested)
        {
            var geometry = arch?.GetSystem<IGroundFieldGeometrySystem>();
            if (geometry == null)
            {
                return;
            }

            if (mLeaveGo == null)
            {
                SpawnLeave(geometry, nested);
                return;
            }

            var tip = nested ? CancelNestedTip : BoardBriefTipCopy.LeaveTip;
            AttachBriefTipOnly(mLeaveGo, tip, TavernBoardSlotResolver.LeaveSlot, geometry);
            RoomIconOccupancy.Current.Register(
                TavernBoardSlotResolver.LeaveSlot,
                -2,
                TavernBoardSlotResolver.LeaveContentId,
                RoomIconWalkRole.WalkDestination);
        }

        private void TryRefresh()
        {
            var arch = mArch ?? NineGridArchitecture.Current;
            if (arch == null)
            {
                return;
            }

            RewardChoiceCoreHook.RequestWire();
            if (RewardChoiceCoreHook.RefreshShop == null)
            {
                Debug.LogWarning("[TavernBoard] RefreshShop hook not wired; abort.");
                return;
            }

            var logStart = InRoomGoldPresentation.CaptureEventLogCount(arch);
            var result = RewardChoiceCoreHook.RefreshShop();
            if (result == null || !result.Accepted)
            {
                if (FlowRoomEconomyAudioCues.IsInsufficientGoldReason(result?.Reason))
                {
                    FlowRoomEconomyAudioCues.Pulse(
                        FlowRoomEconomyAudioCues.TavernInsufficientGold,
                        "TavernBoardPresenter.TryRefresh");
                    ShowNotice(NineGrid.Core.Localization.L10n.Tr("notice.gold_insufficient", "金币不足"));
                }

                return;
            }

            FlowRoomEconomyAudioCues.Pulse(
                FlowRoomEconomyAudioCues.TavernRefresh,
                "TavernBoardPresenter.TryRefresh");
            PurchaseAmountTipPresenter.HideAll();
            InRoomGoldPresentation.PresentGoldChangesSince(arch, logStart);
            mReplanServiceSlots = true;
            ResyncFromPending(arch);
        }

        private bool TryLeaveOrCancel(out bool leftShop)
        {
            leftShop = false;
            RewardChoiceCoreHook.RequestWire();
            if (RewardChoiceCoreHook.SkipHelpChoice == null)
            {
                Debug.LogWarning("[TavernBoard] SkipHelpChoice hook not wired; abort leave.");
                return false;
            }

            var arch = mArch ?? NineGridArchitecture.Current;
            var pending = arch?.GetModel<PendingChoiceModel>();
            var wasNested = pending != null
                            && PendingChoiceModel.IsTavernFixItemPool(pending.PoolId.Value);

            var result = RewardChoiceCoreHook.SkipHelpChoice();
            if (result == null || !result.Accepted)
            {
                return false;
            }

            if (wasNested)
            {
                // 取消二级选择：回主面；若仍站在离开格则重武装驻留。
                ResyncFromPending(arch);
                mLeaveDwell.Cancel();
                mLastAvatarSlot = -1;
                return true;
            }

            FlowRoomEconomyAudioCues.Pulse(
                FlowRoomEconomyAudioCues.RoomLeave,
                "TavernBoardPresenter.TryLeaveOrCancel");
            DespawnAll();
            leftShop = true;
            return true;
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
            if (slot != TavernBoardSlotResolver.LeaveSlot)
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

            var leaveResult = await TryLeaveWithTransitionAsync();
            if (leaveResult.Accepted)
            {
                if (leaveResult.LeftShop)
                {
                    mLeaveDwell.MarkSubmitted();
                }
            }
            else
            {
                mLeaveDwell.Begin(slot, 0);
                RunLeaveDwellAsync(slot, parentCt).Forget();
            }
        }

        private readonly struct LeaveTransitionResult
        {
            public readonly bool Accepted;
            public readonly bool LeftShop;

            public LeaveTransitionResult(bool accepted, bool leftShop)
            {
                Accepted = accepted;
                LeftShop = leftShop;
            }
        }

        private async UniTask<LeaveTransitionResult> TryLeaveWithTransitionAsync()
        {
            var arch = mArch ?? NineGridArchitecture.Current;
            var pending = arch?.GetModel<PendingChoiceModel>();
            var wasNested = pending != null
                            && PendingChoiceModel.IsTavernFixItemPool(pending.PoolId.Value);

            // 二级取消不是离店，不要过场。
            if (wasNested)
            {
                var okNested = TryLeaveOrCancel(out var leftNested);
                return new LeaveTransitionResult(okNested, leftNested);
            }

            var transition = RunSceneTransitionService.InstanceOrNull;
            if (transition == null || !transition.IsEnabled)
            {
                var okPlain = TryLeaveOrCancel(out var leftPlain);
                return new LeaveTransitionResult(okPlain, leftPlain);
            }

            var crossFloor = RunSceneTransitionService.WillCrossFloor(arch);
            try
            {
                await transition.BeginCoverAsync(crossFloor, CancellationToken.None);
                if (!TryLeaveOrCancel(out var leftShop))
                {
                    transition.ForceClearFaders();
                    return new LeaveTransitionResult(false, false);
                }

                if (!leftShop)
                {
                    transition.ForceClearFaders();
                    return new LeaveTransitionResult(true, false);
                }

                await transition.CompleteRevealAsync(CancellationToken.None);
                return new LeaveTransitionResult(true, true);
            }
            catch (OperationCanceledException)
            {
                transition.ForceClearFaders();
                return new LeaveTransitionResult(false, false);
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
                Debug.LogWarning("[TavernBoard] missing prefab for " + contentId + " path=" + prefabPath);
                return null;
            }

            var go = UnityEngine.Object.Instantiate(prefab);
            go.name = "TavernBoard_" + contentId + "_@" + slot;
            // 三项服务/刷新选项按 defId 应用 JSON 主图标，勿恒显模板默认图标。
            RoomOptionFaceVisuals.ApplyMainIcon(contentId, go);
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
