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

namespace NineGrid.Flow.ShopBoard
{
    /// <summary>
    /// 商店房场地：最多 5 真卡货架 + 道具牌格升级选项 + 刷新就地选项 + 离开图标（#92 / #109 / #211 / ADR-0020）。
    /// </summary>
    public sealed class ShopBoardPresenter
    {
        public static ShopBoardPresenter Current { get; private set; } = new ShopBoardPresenter();

        /// <summary>当前货架真卡（表现权威只读投影，供房内装饰层 InRoomCardLifeFx 聚合）。</summary>
        public IReadOnlyList<ManagedCard> ShelfCards => mShelfCards;

        /// <summary>当前货架选项卡（道具牌格升级等非塔型选项，供 InRoomCardLifeFx 聚合）。</summary>
        public IReadOnlyList<GameObject> ShelfOptionGos => mShelfOptionGos;

        private readonly List<ManagedCard> mShelfCards = new List<ManagedCard>(5);
        private readonly List<GameObject> mShelfOptionGos = new List<GameObject>(5);
        private readonly List<string> mShelfDefIds = new List<string>(5);
        private readonly List<int> mShelfSlots = new List<int>(5);
        private readonly List<GameObject> mExtras = new List<GameObject>(2);
        private readonly RoomIconDwellSession mLeaveDwell = new RoomIconDwellSession();
        private bool mReplanShelfSlots;
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
            Current = new ShopBoardPresenter();
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
                    cards.Release(card, "ShopBoard.Despawn");
                }
            }

            mShelfCards.Clear();
            mShelfDefIds.Clear();

            for (var i = 0; i < mShelfOptionGos.Count; i++)
            {
                if (mShelfOptionGos[i] != null)
                {
                    InRoomOfferClaimLifecycle.ReleaseNow(mShelfOptionGos[i]);
                    UnityEngine.Object.Destroy(mShelfOptionGos[i]);
                }
            }

            mShelfOptionGos.Clear();
            mShelfSlots.Clear();
            mReplanShelfSlots = false;

            for (var i = 0; i < mExtras.Count; i++)
            {
                if (mExtras[i] != null)
                {
                    InRoomOfferClaimLifecycle.ReleaseNow(mExtras[i]);
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

        /// <summary>按 PendingChoice 商店货架刷板；失败返回 false。</summary>
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
                || !PendingChoiceModel.IsShopPool(pending.PoolId.Value))
            {
                return false;
            }

            var geometry = arch.GetSystem<IGroundFieldGeometrySystem>();
            var content = arch.GetSystem<IContentSystem>();
            SpawnShelves(pending, geometry, content);
            SpawnRefresh(geometry, pending.ShopRefreshPriceGold.Value);
            SpawnLeave(geometry);
            // 须先置 Active：Watch 循环在首个 await 前检查 mActive，否则会立刻退出，离开驻留永不明火。
            mActive = RoomIconOccupancy.Current.HasAny || mShelfCards.Count > 0;
            if (mActive)
            {
                StartAvatarWatch();
            }

            Debug.Log(
                "[ShopBoard] Spawn active=" + mActive
                + " shelves=" + mShelfCards.Count
                + " extras=" + mExtras.Count
                + " leaveSlot=" + ShopBoardSlotResolver.LeaveSlot
                + " avatarSlot=" + (arch.GetModel<BoardModel>()?.AvatarSlot.Value.Index ?? -1));
            RoomIconOccupancySlotHits.Refresh(arch);
            return mActive;
        }

        /// <summary>购买/刷新后按 Pending 重建货架与刷新价文案（补位走标准跳格，能力卡购买碎裂退场）。</summary>
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

            // 房内开宝箱遗物三选一：商店 Pending 被挂起，勿拆板。
            if (!PendingChoiceModel.IsShopPool(pending.PoolId.Value))
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
                RefreshRefreshTip(pending);
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

                var slot = ResolveShelfSlot(i, entry.DefId);
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

            RoomIconOccupancy.Current.Register(
                ShopBoardSlotResolver.RefreshSlot,
                -1,
                ShopBoardSlotResolver.RefreshContentId,
                RoomIconWalkRole.SoftBlockOnly);
            RoomIconOccupancy.Current.Register(
                ShopBoardSlotResolver.LeaveSlot,
                -2,
                ShopBoardSlotResolver.LeaveContentId,
                RoomIconWalkRole.WalkDestination);
        }

        private int ResolveAvatarSlot()
        {
            var board = mArch?.GetModel<BoardModel>() ?? NineGridArchitecture.Current?.GetModel<BoardModel>();
            if (board != null && board.AvatarSlot.Value.IsBoardSlot)
            {
                return board.AvatarSlot.Value.Index;
            }

            return ShopBoardSlotResolver.AvatarSlot;
        }

        private int ResolveShelfSlot(int shelfIndex, string defId)
        {
            if (shelfIndex >= 0 && shelfIndex < mShelfSlots.Count && mShelfSlots[shelfIndex] > 0)
            {
                return mShelfSlots[shelfIndex];
            }

            if (shelfIndex >= 0 && shelfIndex < ShopBoardSlotResolver.ShelfSlots.Length)
            {
                return ShopBoardSlotResolver.ShelfSlotAt(shelfIndex);
            }

            return 0;
        }

        private void PlanShelfSlots(PendingChoiceModel pending)
        {
            mShelfSlots.Clear();
            var options = pending.RewardOptions;
            var preferred = new List<int>(options.Count);
            for (var i = 0; i < options.Count; i++)
            {
                preferred.Add(i < ShopBoardSlotResolver.ShelfSlots.Length
                    ? ShopBoardSlotResolver.ShelfSlotAt(i)
                    : 0);
            }

            var board = mArch?.GetModel<BoardModel>() ?? NineGridArchitecture.Current?.GetModel<BoardModel>();
            var avatarSlot = ResolveAvatarSlot();
            var planned = InRoomOfferSlotPlanner.Plan(
                board,
                options.Count,
                avatarSlot,
                preferred,
                ShopBoardSlotResolver.ShelfFallbackPool,
                ShopBoardSlotResolver.LeaveSlot,
                ShopBoardSlotResolver.RefreshSlot);

            for (var i = 0; i < planned.Length; i++)
            {
                mShelfSlots.Add(planned[i]);
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
                ShopBoardHitKind.Refresh,
                -1,
                tip,
                ShopBoardSlotResolver.RefreshSlot,
                pending.ShopRefreshPriceGold.Value);
        }

        /// <summary>
        /// 货架 diff：保留仍在售的货架（换格走标准跳格），新建新货架（格上方落下入场），
        /// 释放已离场货架——不再整板销毁重建造成瞬移（#92 补位美化）。
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
            var oldOptionGos = new List<GameObject>(mShelfOptionGos);
            var oldDefIds = new List<string>(mShelfDefIds);
            var oldSlots = new List<int>(mShelfSlots);
            var keptOld = new bool[oldCards.Count];
            var replan = mReplanShelfSlots;

            if (replan)
            {
                PlanShelfSlots(pending);
                mReplanShelfSlots = false;
            }
            else
            {
                mShelfSlots.Clear();
                for (var i = 0; i < newOptions.Count; i++)
                {
                    mShelfSlots.Add(0);
                }

                for (var i = 0; i < newOptions.Count; i++)
                {
                    var entry = newOptions[i];
                    if (entry == null || string.IsNullOrEmpty(entry.DefId))
                    {
                        continue;
                    }

                    var oldIndex = InRoomShelfAnimation.TryMatchOldIndex(oldDefIds, keptOld, entry.DefId, i);
                    if (oldIndex >= 0 && oldIndex < oldSlots.Count && oldSlots[oldIndex] > 0)
                    {
                        mShelfSlots[i] = oldSlots[oldIndex];
                    }
                    else
                    {
                        mShelfSlots[i] = i < ShopBoardSlotResolver.ShelfSlots.Length
                            ? ShopBoardSlotResolver.ShelfSlotAt(i)
                            : 0;
                    }
                }
            }

            if (replan)
            {
                InRoomOfferClaimLifecycle.ReleaseAllShelfClaims(oldCards, oldOptionGos);
            }
            else
            {
                for (var i = 0; i < newOptions.Count; i++)
                {
                    var entry = newOptions[i];
                    if (entry == null || string.IsNullOrEmpty(entry.DefId))
                    {
                        continue;
                    }

                    InRoomShelfAnimation.TryMatchOldIndex(oldDefIds, keptOld, entry.DefId, i);
                }

                InRoomOfferClaimLifecycle.ReleaseUnkeptShelfClaims(oldCards, oldOptionGos, keptOld);
                keptOld = new bool[oldCards.Count];
            }

            var newCards = new List<ManagedCard>(newOptions.Count);
            var newOptionGos = new List<GameObject>(newOptions.Count);
            var newDefIds = new List<string>(newOptions.Count);
            var animTasks = new List<UniTask>(newOptions.Count);
            keptOld = new bool[oldCards.Count];

            for (var i = 0; i < newOptions.Count; i++)
            {
                var entry = newOptions[i];
                if (entry == null || string.IsNullOrEmpty(entry.DefId))
                {
                    newDefIds.Add(null);
                    newCards.Add(null);
                    newOptionGos.Add(null);
                    continue;
                }

                var slot = ResolveShelfSlot(i, entry.DefId);
                if (!replan)
                {
                    var oldIndex = InRoomShelfAnimation.TryMatchOldIndex(oldDefIds, keptOld, entry.DefId, i);
                    if (oldIndex >= 0
                        && oldIndex < oldCards.Count
                        && (oldCards[oldIndex] != null || oldOptionGos[oldIndex] != null))
                    {
                        var tip = BuildShelfTip(entry.DefId, content);
                        if (oldOptionGos[oldIndex] != null)
                        {
                            var go = oldOptionGos[oldIndex];
                            newOptionGos.Add(go);
                            newCards.Add(null);
                            newDefIds.Add(entry.DefId);
                            AttachClickProxy(go, ShopBoardHitKind.BuyShelf, i, tip, slot, ResolveShelfPriceGold(entry.DefId, content));
                            continue;
                        }

                        var card = oldCards[oldIndex];
                        newCards.Add(card);
                        newOptionGos.Add(null);
                        newDefIds.Add(entry.DefId);
                        AttachClickProxy(card.View.gameObject, ShopBoardHitKind.BuyShelf, i, tip, slot, ResolveShelfPriceGold(entry.DefId, content));
                        continue;
                    }
                }

                // 新建：刷新换货。
                newDefIds.Add(entry.DefId);
                if (IsShopSlotUpgradeOption(entry.DefId))
                {
                    var go = TryInstantiate(
                        CardChassisPaths.RoomOptionFacePrefab,
                        geometry,
                        slot,
                        entry.DefId);
                    newOptionGos.Add(go);
                    newCards.Add(null);
                    if (go != null)
                    {
                        var tip = BuildShelfTip(entry.DefId, content);
                        AttachClickProxy(go, ShopBoardHitKind.BuyShelf, i, tip, slot, ResolveShelfPriceGold(entry.DefId, content));
                        animTasks.Add(InRoomShelfAnimation.DropOptionGoInAsync(go, geometry, slot, ct));
                    }
                }
                else
                {
                    newOptionGos.Add(null);
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
                        AttachClickProxy(managed.View.gameObject, ShopBoardHitKind.BuyShelf, i, tip, slot, ResolveShelfPriceGold(entry.DefId, content));
                        animTasks.Add(InRoomShelfAnimation.DropInToSlotAsync(managed, geometry, slot, ct));
                    }
                }
            }

            // 释放未保留的旧货架（购买离场 / 刷新换货）。
            for (var j = 0; j < oldCards.Count; j++)
            {
                if (keptOld[j])
                {
                    continue;
                }

                if (oldCards[j] != null)
                {
                    cards?.Release(oldCards[j], "ShopBoard.ResyncRelease");
                }

                if (oldOptionGos[j] != null)
                {
                    UnityEngine.Object.Destroy(oldOptionGos[j]);
                }
            }

            mShelfCards.Clear();
            mShelfCards.AddRange(newCards);
            mShelfOptionGos.Clear();
            mShelfOptionGos.AddRange(newOptionGos);
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
            PlanShelfSlots(pending);
            mReplanShelfSlots = false;

            var cards = CardEntityLifecycleHook.CardsOrNull()
                        ?? UnityEngine.Object.FindFirstObjectByType<CardManagerSingleton>();
            var options = pending.RewardOptions;
            for (var i = 0; i < options.Count; i++)
            {
                var entry = options[i];
                mShelfDefIds.Add(entry == null ? null : entry.DefId);
                if (entry == null || string.IsNullOrEmpty(entry.DefId))
                {
                    mShelfCards.Add(null);
                    mShelfOptionGos.Add(null);
                    continue;
                }

                var slot = ResolveShelfSlot(i, entry.DefId);
                if (slot <= 0)
                {
                    mShelfCards.Add(null);
                    mShelfOptionGos.Add(null);
                    continue;
                }

                RoomIconOccupancy.Current.Register(
                    slot, i, entry.DefId, RoomIconWalkRole.SoftBlockOnly);

                var tip = BuildShelfTip(entry.DefId, content);
                if (IsShopSlotUpgradeOption(entry.DefId))
                {
                    var go = TryInstantiate(
                        CardChassisPaths.RoomOptionFacePrefab,
                        geometry,
                        slot,
                        entry.DefId);
                    mShelfOptionGos.Add(go);
                    if (go == null)
                    {
                        mShelfCards.Add(null);
                        continue;
                    }

                    AttachClickProxy(go, ShopBoardHitKind.BuyShelf, i, tip, slot, ResolveShelfPriceGold(entry.DefId, content));
                    // 占位对齐 Pending 索引，避免 PresentShelfAcquireOrShatter 错位。
                    mShelfCards.Add(null);
                    continue;
                }

                mShelfOptionGos.Add(null);
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
                AttachClickProxy(managed.View.gameObject, ShopBoardHitKind.BuyShelf, i, tip, slot, ResolveShelfPriceGold(entry.DefId, content));
                mShelfCards.Add(managed);
            }
        }

        private void SpawnRefresh(IGroundFieldGeometrySystem geometry, int refreshPrice)
        {
            var slot = ShopBoardSlotResolver.RefreshSlot;
            RoomIconOccupancy.Current.Register(
                slot,
                -1,
                ShopBoardSlotResolver.RefreshContentId,
                RoomIconWalkRole.SoftBlockOnly);

            var path = CardChassisPaths.RoomOptionFacePrefab;
            var go = TryInstantiate(path, geometry, slot, ShopBoardSlotResolver.RefreshContentId);
            if (go == null)
            {
                return;
            }

            var tip = BoardBriefTipCopy.ForOptionOrShelf(
                NineGrid.Core.Localization.L10n.Tr("briefTip.refresh_shelf", "刷新货架"),
                refreshPrice);
            AttachClickProxy(go, ShopBoardHitKind.Refresh, -1, tip, ShopBoardSlotResolver.RefreshSlot, refreshPrice);
            mExtras.Add(go);
            mRefreshGo = go;
        }

        private void SpawnLeave(IGroundFieldGeometrySystem geometry)
        {
            var slot = ShopBoardSlotResolver.LeaveSlot;
            RoomIconOccupancy.Current.Register(
                slot,
                -2,
                ShopBoardSlotResolver.LeaveContentId,
                RoomIconWalkRole.WalkDestination);

            var path = CardChassisPaths.RoomIconLeave;
            var go = TryInstantiate(path, geometry, slot, ShopBoardSlotResolver.LeaveContentId);
            if (go == null)
            {
                return;
            }

            AttachBriefTipOnly(go, BoardBriefTipCopy.ForLeave(mArch), slot, geometry);
            mExtras.Add(go);
            mLeaveGo = go;
        }

        private static bool IsShopSlotUpgradeOption(string defId)
        {
            return string.Equals(
                defId,
                RewardSystem.ShopExpandItemSlotsDefId,
                StringComparison.Ordinal);
        }

        private static int ResolveShelfPriceGold(string defId, IContentSystem content)
        {
            if (IsShopSlotUpgradeOption(defId))
            {
                var catalogPrice = ResolveCatalogPriceGold(defId, content);
                return catalogPrice > 0 ? catalogPrice : RewardSystem.ShopExpandItemSlotsPriceGold;
            }

            return ResolveCatalogPriceGold(defId, content);
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

        private static string BuildShelfTip(string defId, IContentSystem content)
        {
            var price = ResolveShelfPriceGold(defId, content);
            return BoardBriefTipCopy.ForCard(defId, content, price > 0 ? (int?)price : null);
        }

        private void AttachClickProxy(
            GameObject go,
            ShopBoardHitKind kind,
            int shelfIndex,
            string tip,
            int boardSlot,
            int amountGold = 0)
        {
            if (go == null)
            {
                return;
            }

            // 货架真卡底盘常带 GroundCardHitProxy；店内任意距离购买由 ShopBoardHitProxy 独占，
            // 禁 Pickup 抢点（ChoiceOverlay 关掉后更易误入入手路径）。
            var groundHit = go.GetComponent<GroundCardHitProxy>();
            if (groundHit != null)
            {
                groundHit.enabled = false;
            }

            var proxy = go.GetComponent<ShopBoardHitProxy>();
            if (proxy == null)
            {
                proxy = go.AddComponent<ShopBoardHitProxy>();
            }

            proxy.Configure(kind, shelfIndex, tip, HandleHit, boardSlot, amountGold);
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

        private void HandleHit(ShopBoardHitKind kind, int shelfIndex)
        {
            Debug.Log(
                "[ShopBoard] Hit kind=" + kind
                + " shelfIndex=" + shelfIndex
                + " active=" + mActive
                + " choiceOverlay=" + PresentationInputGates.ChoiceOverlayActive
                + " owner=" + PresentationInputGates.CurrentOwner);
            switch (kind)
            {
                case ShopBoardHitKind.BuyShelf:
                    TryBuy(shelfIndex);
                    break;
                case ShopBoardHitKind.Refresh:
                    TryRefresh();
                    break;
            }
        }

        private void TryBuy(int shelfIndex)
        {
            var arch = mArch ?? NineGridArchitecture.Current;
            if (arch == null)
            {
                Debug.LogWarning("[ShopBoard] TryBuy abort: no architecture");
                return;
            }

            // ChoiceOverlay 仅局内宝箱等浮层短暂持有；店内购买走 ProtectedField（ADR-0020）。
            RewardChoiceCoreHook.RequestWire();
            if (RewardChoiceCoreHook.SelectReward == null)
            {
                ShowNotice(NineGrid.Core.Localization.L10n.Tr("notice.shop_not_wired", "商店输入未接线"));
                Debug.LogWarning("[ShopBoard] TryBuy abort: SelectReward hook not wired");
                return;
            }

            var logStart = InRoomGoldPresentation.CaptureEventLogCount(arch);
            var pending = arch.GetModel<PendingChoiceModel>();
            var defId = string.Empty;
            if (pending != null
                && shelfIndex >= 0
                && shelfIndex < pending.RewardOptions.Count)
            {
                defId = pending.RewardOptions[shelfIndex]?.DefId ?? string.Empty;
            }

            var isUpgrade = IsShopSlotUpgradeOption(defId);
            var result = RewardChoiceCoreHook.SelectReward(shelfIndex);
            if (result == null || !result.Accepted)
            {
                var reason = result?.Reason ?? string.Empty;
                Debug.LogWarning(
                    "[ShopBoard] Buy rejected shelfIndex=" + shelfIndex + " reason=" + reason);
                if (FlowRoomEconomyAudioCues.IsInsufficientGoldReason(reason))
                {
                    FlowRoomEconomyAudioCues.Pulse(
                        FlowRoomEconomyAudioCues.ShopInsufficientGold,
                        "ShopBoardPresenter.TryBuy");
                    ShowNotice(NineGrid.Core.Localization.L10n.Tr("notice.gold_insufficient", "金币不足"));
                }
                else if (!string.IsNullOrEmpty(reason))
                {
                    ShowNotice(reason);
                }

                return;
            }

            FlowRoomEconomyAudioCues.Pulse(
                isUpgrade ? FlowRoomEconomyAudioCues.ShopUpgrade : FlowRoomEconomyAudioCues.ShopBuy,
                "ShopBoardPresenter.TryBuy",
                defId);
            Debug.Log("[ShopBoard] Buy accepted shelfIndex=" + shelfIndex);
            // 购后旧货架即将撤场：金额提示随即隐藏，防止残留旧价（代数制仍会兜底脏写）。
            PurchaseAmountTipPresenter.HideAll();
            InRoomGoldPresentation.PresentGoldChangesSince(arch, logStart);
            // ADR-0025：购入直写 ItemSlots；货架纯表现卡须换成 Core uid 并接入手牌，勿只碎裂。
            PresentShelfAcquireOrShatter(shelfIndex, arch, logStart);
            ResyncFromPending(arch);
        }

        private void PresentShelfAcquireOrShatter(int shelfIndex, IArchitecture arch, int logStart)
        {
            ManagedCard shelf = null;
            GameObject optionGo = null;
            if (shelfIndex >= 0 && shelfIndex < mShelfCards.Count)
            {
                shelf = mShelfCards[shelfIndex];
                mShelfCards[shelfIndex] = null;
            }

            if (shelfIndex >= 0 && shelfIndex < mShelfOptionGos.Count)
            {
                optionGo = mShelfOptionGos[shelfIndex];
                mShelfOptionGos[shelfIndex] = null;
            }

            if (optionGo != null)
            {
                // 能力卡（道具牌格升级）购买：走标准 Death 碎裂退场，勿凭空消失。
                InRoomShelfAnimation.PlayConsumeDeath(optionGo.transform);
                UnityEngine.Object.Destroy(optionGo);
                return;
            }

            if (InRoomItemAcquirePresentation.TryAcquireShelfHelpCardToHand(arch, logStart, shelf))
            {
                return;
            }

            // 无 ItemSlots 授予或接手失败：退回碎裂释放。
            if (shelf == null)
            {
                return;
            }

            InRoomShelfAnimation.PlayConsumeDeath(shelf);
            var cards = CardEntityLifecycleHook.CardsOrNull()
                        ?? UnityEngine.Object.FindFirstObjectByType<CardManagerSingleton>();
            cards?.Release(shelf, "ShopBoard.BuyShatter");
        }

        private void TryRefresh()
        {
            var arch = mArch ?? NineGridArchitecture.Current;
            if (arch == null)
            {
                Debug.LogWarning("[ShopBoard] TryRefresh abort: no architecture");
                return;
            }

            RewardChoiceCoreHook.RequestWire();
            if (RewardChoiceCoreHook.RefreshShop == null)
            {
                Debug.LogWarning("[ShopBoard] RefreshShop hook not wired; abort.");
                return;
            }

            var logStart = InRoomGoldPresentation.CaptureEventLogCount(arch);
            var result = RewardChoiceCoreHook.RefreshShop();
            if (result == null || !result.Accepted)
            {
                Debug.LogWarning(
                    "[ShopBoard] Refresh rejected reason=" + (result?.Reason ?? string.Empty));
                if (FlowRoomEconomyAudioCues.IsInsufficientGoldReason(result?.Reason))
                {
                    FlowRoomEconomyAudioCues.Pulse(
                        FlowRoomEconomyAudioCues.ShopInsufficientGold,
                        "ShopBoardPresenter.TryRefresh");
                    ShowNotice(NineGrid.Core.Localization.L10n.Tr("notice.gold_insufficient", "金币不足"));
                }

                return;
            }

            FlowRoomEconomyAudioCues.Pulse(
                FlowRoomEconomyAudioCues.ShopRefresh,
                "ShopBoardPresenter.TryRefresh");
            Debug.Log("[ShopBoard] Refresh accepted");
            PurchaseAmountTipPresenter.HideAll();
            InRoomGoldPresentation.PresentGoldChangesSince(arch, logStart);
            mReplanShelfSlots = true;
            ResyncFromPending(arch);
        }

        private bool TryLeave()
        {
            RewardChoiceCoreHook.RequestWire();
            if (RewardChoiceCoreHook.SkipHelpChoice == null)
            {
                Debug.LogWarning("[ShopBoard] SkipHelpChoice hook not wired; abort leave.");
                return false;
            }

            var result = RewardChoiceCoreHook.SkipHelpChoice();
            if (result != null && result.Accepted)
            {
                FlowRoomEconomyAudioCues.Pulse(
                    FlowRoomEconomyAudioCues.RoomLeave,
                    "ShopBoardPresenter.TryLeave");
                Debug.Log("[ShopBoard] Leave accepted (SkipHelpChoice)");
                DespawnAll();
                return true;
            }

            Debug.LogWarning(
                "[ShopBoard] Leave rejected reason=" + (result?.Reason ?? string.Empty));
            return false;
        }

        private void ShowNotice(string message)
        {
            if (mNotice != null)
            {
                mNotice(message);
                return;
            }

            NineGrid.Flow.InfoNotice.InfoNoticePresenter.Show(message ?? string.Empty);
        }

        private void StartAvatarWatch()
        {
            CancelWatch();
            mWatching = true;
            // -1：首帧强制走 HandleAvatarSlot（含已踩在离开格的情况）。
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
            if (slot != ShopBoardSlotResolver.LeaveSlot)
            {
                mLeaveDwell.Cancel();
                return;
            }

            if (mLeaveDwell.IsArmed && mLeaveDwell.ArmedSlot == slot)
            {
                return;
            }

            mLeaveDwell.Begin(slot, 0);
            Debug.Log("[ShopBoard] Leave dwell armed slot=" + slot);
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
                Debug.Log("[ShopBoard] Leave dwell cancelled slot=" + slot);
                return;
            }

            if (!mLeaveDwell.IsArmed || mLeaveDwell.ArmedSlot != slot)
            {
                Debug.Log(
                    "[ShopBoard] Leave dwell stale slot=" + slot
                    + " armed=" + mLeaveDwell.IsArmed
                    + " armedSlot=" + mLeaveDwell.ArmedSlot);
                return;
            }

            if (!mLeaveDwell.TryConsumeArmed(out _))
            {
                return;
            }

            Debug.Log("[ShopBoard] Leave dwell commit slot=" + slot);
            var left = await TryLeaveWithTransitionAsync();
            if (left)
            {
                mLeaveDwell.MarkSubmitted();
            }
            else
            {
                // 失败须重开计时；仅 Begin 不跑 RunLeaveDwellAsync 会永久卡在离开格。
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
                Debug.LogWarning("[ShopBoard] missing prefab for " + contentId + " path=" + prefabPath);
                return null;
            }

            var go = UnityEngine.Object.Instantiate(prefab);
            go.name = "ShopBoard_" + contentId + "_@" + slot;
            // 特色选项（道具牌格升级/刷新）按 defId 应用 JSON 主图标，勿恒显模板默认图标。
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

    public enum ShopBoardHitKind
    {
        BuyShelf,
        Refresh
    }
}
