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
using NineGrid.Flow.RoomIcons;
using NineGrid.Presentation;
using NineGrid.Presentation.Systems;
using QFramework;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NineGrid.Flow.RewardBoard
{
    /// <summary>
    /// 特殊奖励房场地：真卡货架 + 离开图标；任意距离点击拿走，踩离开放弃（#94 / ADR-0020）。
    /// </summary>
    public sealed class RewardBoardPresenter
    {
        public static RewardBoardPresenter Current { get; private set; } = new RewardBoardPresenter();

        private readonly List<ManagedCard> mShelfCards = new List<ManagedCard>(5);
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

            var cards = CardEntityLifecycleHook.CardsOrNull()
                        ?? UnityEngine.Object.FindFirstObjectByType<CardManagerSingleton>();
            for (var i = 0; i < mShelfCards.Count; i++)
            {
                var card = mShelfCards[i];
                if (card == null)
                {
                    continue;
                }

                if (cards != null)
                {
                    cards.Release(card, "RewardBoard.Despawn");
                }
            }

            mShelfCards.Clear();

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
            BoardBriefTipPresenter.InstanceOrNull()?.ClearHover();
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

        /// <summary>拿走后按 Pending 重建货架。</summary>
        public void ResyncFromPending(IArchitecture arch)
        {
            if (!mActive)
            {
                TrySpawnFromPending(arch);
                return;
            }

            Bind(arch);
            var pending = arch?.GetModel<PendingChoiceModel>();
            if (pending == null || !PendingChoiceModel.IsSpecialRewardPool(pending.PoolId.Value))
            {
                DespawnAll();
                return;
            }

            TrySpawnFromPending(arch);
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
                if (entry == null || string.IsNullOrEmpty(entry.DefId))
                {
                    continue;
                }

                var slot = RewardBoardSlotResolver.ShelfSlotAt(i);
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

            var path = CardChassisPaths.ResolveRoomIconPrefab(RewardBoardSlotResolver.LeaveContentId, null);
            var go = TryInstantiate(path, geometry, slot, RewardBoardSlotResolver.LeaveContentId);
            if (go == null)
            {
                return;
            }

            AttachBriefTipOnly(go, BoardBriefTipCopy.LeaveTip, slot, geometry);
            mExtras.Add(go);
        }

        private static string BuildShelfTip(string defId, IContentSystem content)
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
                ShowNotice("奖励房输入未接线");
                return;
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

            Debug.Log("[RewardBoard] Take accepted shelfIndex=" + shelfIndex);
            ShatterShelfVisual(shelfIndex);
            ResyncFromPending(arch);
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
                DespawnAll();
                return true;
            }

            return false;
        }

        private void ShatterShelfVisual(int shelfIndex)
        {
            if (shelfIndex < 0 || shelfIndex >= mShelfCards.Count)
            {
                return;
            }

            var card = mShelfCards[shelfIndex];
            if (card == null)
            {
                return;
            }

            var cards = CardEntityLifecycleHook.CardsOrNull()
                        ?? UnityEngine.Object.FindFirstObjectByType<CardManagerSingleton>();
            if (cards != null)
            {
                cards.Release(card, "RewardBoard.TakeShatter");
            }

            mShelfCards[shelfIndex] = null;
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

            if (TryLeave())
            {
                mLeaveDwell.MarkSubmitted();
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
