using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Cards;
using NineGrid.Content.CardPresentation;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Flow.BoardBriefTip;
using NineGrid.Flow.InRoomBoard;
using NineGrid.Flow.RoomIcons;
using NineGrid.Presentation.Systems;
using QFramework;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NineGrid.Flow.TavernBoard
{
    /// <summary>
    /// 卡店房场地：3 就地服务选项 + 刷新 + 离开；「道具卡固定」二级选择铺空格候选（#93 / ADR-0020）。
    /// </summary>
    public sealed class TavernBoardPresenter
    {
        public const string CancelNestedTip = "取消选择";

        public static TavernBoardPresenter Current { get; private set; } = new TavernBoardPresenter();

        private readonly List<ManagedCard> mCandidateCards = new List<ManagedCard>(6);
        private readonly List<GameObject> mExtras = new List<GameObject>(5);
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

            for (var i = 0; i < mExtras.Count; i++)
            {
                if (mExtras[i] != null)
                {
                    UnityEngine.Object.Destroy(mExtras[i]);
                }
            }

            mExtras.Clear();
            RoomIconOccupancy.Current.Clear();
            BoardBriefTipPresenter.InstanceOrNull()?.ClearHover();
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

            return mActive;
        }

        public void ResyncFromPending(IArchitecture arch)
        {
            if (!mActive)
            {
                TrySpawnFromPending(arch);
                return;
            }

            Bind(arch);
            var pending = arch?.GetModel<PendingChoiceModel>();
            if (pending == null
                || !PendingChoiceModel.IsConsumerBoardPool(pending.PoolId.Value)
                || PendingChoiceModel.IsShopPool(pending.PoolId.Value))
            {
                DespawnAll();
                return;
            }

            TrySpawnFromPending(arch);
        }

        private void SpawnServices(
            PendingChoiceModel pending,
            IGroundFieldGeometrySystem geometry,
            IContentSystem content)
        {
            var options = pending.RewardOptions;
            for (var i = 0; i < options.Count && i < TavernBoardSlotResolver.ServiceSlots.Length; i++)
            {
                var entry = options[i];
                if (entry == null || string.IsNullOrEmpty(entry.DefId))
                {
                    continue;
                }

                var slot = TavernBoardSlotResolver.ServiceSlotAt(i);
                RoomIconOccupancy.Current.Register(slot, i, entry.DefId);

                var go = TryInstantiate(
                    CardChassisPaths.RoomOptionFacePrefab,
                    geometry,
                    slot,
                    entry.DefId);
                if (go == null)
                {
                    continue;
                }

                FitRoomIcon(go, geometry);
                var tip = BuildServiceTip(entry.DefId, content);
                AttachClickProxy(go, TavernBoardHitKind.SelectService, i, tip);
                mExtras.Add(go);
            }
        }

        private void SpawnFixCandidates(
            PendingChoiceModel pending,
            IGroundFieldGeometrySystem geometry,
            IContentSystem content)
        {
            var cards = CardEntityLifecycleHook.CardsOrNull()
                        ?? UnityEngine.Object.FindFirstObjectByType<CardManagerSingleton>();
            var options = pending.RewardOptions;
            for (var i = 0; i < options.Count && i < TavernBoardSlotResolver.CandidateSlots.Length; i++)
            {
                var entry = options[i];
                if (entry == null || string.IsNullOrEmpty(entry.DefId))
                {
                    continue;
                }

                var slot = TavernBoardSlotResolver.CandidateSlotAt(i);
                RoomIconOccupancy.Current.Register(slot, i, entry.DefId);

                if (cards == null)
                {
                    continue;
                }

                Transform parent = null;
                if (geometry != null)
                {
                    parent = geometry.GetGroundAnchor(slot);
                }

                var managed = cards.SpawnPresentationOnly(
                    entry.DefId,
                    parent,
                    CardDisplayMode.GroundCardMode,
                    CardPresentationKind.HelpCard);
                if (managed?.View == null)
                {
                    continue;
                }

                if (parent != null)
                {
                    managed.View.transform.localPosition = Vector3.zero;
                    managed.View.transform.localRotation = Quaternion.identity;
                }

                CoreCardPresentationMapper.ApplyVisualsByDefId(managed, CardPresentationKind.HelpCard);
                FitRoomIcon(managed.View.gameObject, geometry);
                var tip = BuildCandidateTip(entry.DefId, content);
                AttachClickProxy(managed.View.gameObject, TavernBoardHitKind.SelectFixCandidate, i, tip);
                mCandidateCards.Add(managed);
            }
        }

        private void SpawnRefresh(IGroundFieldGeometrySystem geometry, int refreshPrice)
        {
            var slot = TavernBoardSlotResolver.RefreshSlot;
            RoomIconOccupancy.Current.Register(
                slot,
                -1,
                TavernBoardSlotResolver.RefreshContentId);

            var go = TryInstantiate(
                CardChassisPaths.RoomOptionFacePrefab,
                geometry,
                slot,
                TavernBoardSlotResolver.RefreshContentId);
            if (go == null)
            {
                return;
            }

            FitRoomIcon(go, geometry);
            var tip = BoardBriefTipCopy.ForOptionOrShelf("刷新货架", refreshPrice);
            AttachClickProxy(go, TavernBoardHitKind.Refresh, -1, tip);
            mExtras.Add(go);
        }

        private void SpawnLeave(IGroundFieldGeometrySystem geometry, bool nested)
        {
            var slot = TavernBoardSlotResolver.LeaveSlot;
            RoomIconOccupancy.Current.Register(
                slot,
                -2,
                TavernBoardSlotResolver.LeaveContentId);

            var path = CardChassisPaths.ResolveRoomIconPrefab(TavernBoardSlotResolver.LeaveContentId, null);
            var go = TryInstantiate(path, geometry, slot, TavernBoardSlotResolver.LeaveContentId);
            if (go == null)
            {
                return;
            }

            var tip = nested ? CancelNestedTip : BoardBriefTipCopy.LeaveTip;
            FitRoomIcon(go, geometry);
            AttachBriefTipOnly(go, tip, slot, geometry);
            mExtras.Add(go);
        }

        private static string BuildServiceTip(string defId, IContentSystem content)
        {
            string name = defId;
            string brief = string.Empty;
            int? price = RewardSystem.TavernServicePriceGold;

            if (content != null && content.HasCatalog
                && content.Catalog.Cards.TryGetValue(defId, out var card)
                && card != null)
            {
                if (!string.IsNullOrWhiteSpace(card.DisplayName))
                {
                    name = card.DisplayName;
                }

                if (card.Price > 0)
                {
                    price = card.Price;
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

                if (dto.gold > 0)
                {
                    price = dto.gold;
                }
            }

            var body = string.IsNullOrWhiteSpace(brief) ? name : name + "：" + brief.Trim();
            return BoardBriefTipCopy.ForOptionOrShelf(body, price);
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
            return BoardBriefTipCopy.ForOptionOrShelf(body, RewardSystem.TavernServicePriceGold);
        }

        private void AttachClickProxy(GameObject go, TavernBoardHitKind kind, int optionIndex, string tip)
        {
            if (go == null)
            {
                return;
            }

            var proxy = go.GetComponent<TavernBoardHitProxy>();
            if (proxy == null)
            {
                proxy = go.AddComponent<TavernBoardHitProxy>();
            }

            proxy.Configure(kind, optionIndex, tip, HandleHit);
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

            var hitBox = geometry?.LayoutSettings != null
                ? geometry.LayoutSettings.slotHitBoxSize
                : new Vector2(1.6f, 2.2f);
            proxy.Configure(tip, walkBoardSlot, hitBox);
        }

        private static void FitRoomIcon(GameObject go, IGroundFieldGeometrySystem geometry)
        {
            var hitBox = geometry?.LayoutSettings != null
                ? geometry.LayoutSettings.slotHitBoxSize
                : new Vector2(1.6f, 2.2f);
            RoomIconVisualFit.FitToTarget(go, RoomIconVisualFit.ResolveTargetSize(hitBox));
        }

        private void HandleHit(TavernBoardHitKind kind, int optionIndex)
        {
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
                ShowNotice("卡店输入未接线");
                return;
            }

            var logStart = InRoomGoldPresentation.CaptureEventLogCount(arch);
            var result = RewardChoiceCoreHook.SelectReward(optionIndex);
            if (result == null || !result.Accepted)
            {
                var reason = result?.Reason ?? string.Empty;
                if (string.Equals(reason, "Not enough gold", StringComparison.Ordinal))
                {
                    ShowNotice("金币不足");
                }
                else if (string.Equals(reason, "No item source pool", StringComparison.Ordinal))
                {
                    ShowNotice("暂无可固定的道具卡");
                }
                else if (!string.IsNullOrEmpty(reason))
                {
                    ShowNotice(reason);
                }

                return;
            }

            InRoomGoldPresentation.PresentGoldChangesSince(arch, logStart);
            ResyncFromPending(arch);
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
                if (string.Equals(result?.Reason, "Not enough gold", StringComparison.Ordinal))
                {
                    ShowNotice("金币不足");
                }

                return;
            }

            InRoomGoldPresentation.PresentGoldChangesSince(arch, logStart);
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
                // 取消二级选择：消耗本次驻留，须先跳走再踩离开才能出店。
                ResyncFromPending(arch);
                mLeaveDwell.Cancel();
                mLastAvatarSlot = TavernBoardSlotResolver.LeaveSlot;
                return true;
            }

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

            if (TryLeaveOrCancel(out var leftShop))
            {
                if (leftShop)
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

            Transform parent = null;
            if (geometry != null)
            {
                parent = geometry.GetGroundAnchor(slot);
            }

            var go = UnityEngine.Object.Instantiate(prefab, parent);
            go.name = "TavernBoard_" + contentId + "_@" + slot;
            if (parent != null)
            {
                go.transform.localPosition = Vector3.zero;
            }

            var sorting = go.GetComponent<SortingGroup>();
            if (sorting != null)
            {
                sorting.sortingOrder = 40 + slot;
            }

            return go;
        }

        private static GameObject LoadPrefab(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
#else
            return null;
#endif
        }
    }
}
