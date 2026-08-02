using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Cards;
using NineGrid.Content.CardPresentation;
using NineGrid.Core;
using NineGrid.Core.Systems;
using NineGrid.Flow;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation;
using NineGrid.Presentation.Systems;
using QFramework;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NineGrid.Flow.RoomIcons
{
    /// <summary>
    /// 场地图标 Spawn / 登记 / 驻留提交 / 进房硬切（ADR-0020）。不进 CardKind 五套 Spawn。
    /// </summary>
    public sealed class RoomIconBoardPresenter
    {
        public static RoomIconBoardPresenter Current { get; private set; } = new RoomIconBoardPresenter();

        private readonly List<GameObject> mSpawned = new List<GameObject>(4);
        private readonly RoomIconDwellSession mDwell = new RoomIconDwellSession();
        private CancellationTokenSource mDwellCts;
        private IArchitecture mArch;
        private Func<float, CancellationToken, UniTask> mDelayAsync;
        private bool mWatchingAvatar;
        private int mLastAvatarSlot;

        public RoomIconDwellSession Dwell => mDwell;

        public static void ResetForTests()
        {
            Current?.DespawnAll();
            RoomIconOccupancy.ResetForTests();
            Current = new RoomIconBoardPresenter();
        }

        /// <summary>测试可注入 delay（默认 UniTask.Delay）。</summary>
        public void SetDelayAsyncForTests(Func<float, CancellationToken, UniTask> delayAsync)
        {
            mDelayAsync = delayAsync;
        }

        public void Bind(IArchitecture architecture)
        {
            mArch = architecture;
        }

        public void DespawnAll()
        {
            CancelDwellWatch();
            mDwell.Cancel();
            for (var i = 0; i < mSpawned.Count; i++)
            {
                if (mSpawned[i] != null)
                {
                    UnityEngine.Object.Destroy(mSpawned[i]);
                }
            }

            mSpawned.Clear();
            RoomIconOccupancy.Current.Clear();
        }

        /// <summary>按 PendingChoice 刷出房间/导航图标。</summary>
        public bool TrySpawnFromPending(IArchitecture arch)
        {
            Bind(arch);
            DespawnAll();
            if (arch == null)
            {
                return false;
            }

            var pending = arch.GetModel<PendingChoiceModel>();
            if (pending == null)
            {
                return false;
            }

            var contentIds = new List<string>(2);
            if (pending.Kind.Value == PendingChoiceKind.Room
                && pending.RoomOptions != null
                && pending.RoomOptions.Count > 0)
            {
                for (var i = 0; i < pending.RoomOptions.Count; i++)
                {
                    contentIds.Add(pending.RoomOptions[i].ToString());
                }
            }
            else if (pending.Kind.Value == PendingChoiceKind.Navigation
                     && pending.NavigationOffer.Value != NavigationKind.None)
            {
                contentIds.Add(pending.NavigationOffer.Value.ToString());
            }
            else
            {
                return false;
            }

            var slots = RoomIconBoardSlotResolver.ResolveSlots(contentIds);
            var geometry = arch.GetSystem<IGroundFieldGeometrySystem>();
            for (var i = 0; i < contentIds.Count; i++)
            {
                var contentId = contentIds[i];
                var slot = slots[i];
                var overridePrefab = string.Empty;
                if (CardPresentationConfigCatalog.TryGet(contentId, out var dto) && dto != null)
                {
                    overridePrefab = dto.iconPrefab;
                }

                var path = CardChassisPaths.ResolveRoomIconPrefab(contentId, overridePrefab);
                var go = TryInstantiateIcon(path, geometry, slot, contentId);
                if (go != null)
                {
                    mSpawned.Add(go);
                }

                RoomIconOccupancy.Current.Register(slot, i, contentId);
            }

            StartAvatarWatch();
            return RoomIconOccupancy.Current.HasAny;
        }

        /// <summary>进房后：清图标 + Avatar 硬切格 5（无走回）。</summary>
        public void HardCutAfterEnter(IArchitecture arch)
        {
            Bind(arch);
            DespawnAll();
            if (arch == null)
            {
                return;
            }

            var board = arch.GetModel<BoardModel>();
            var registry = arch.GetModel<CardRegistry>();
            if (board == null || registry == null)
            {
                return;
            }

            var avatarUid = board.AvatarUid.Value;
            if (avatarUid <= 0 || !registry.TryGet(avatarUid, out var avatar) || avatar == null)
            {
                return;
            }

            var target = SlotId.Board(5);
            if (board.AvatarSlot.Value == target)
            {
                SnapAvatarView(arch, 5);
                return;
            }

            var pipeline = arch.GetSystem<IActionPipelineSystem>();
            if (pipeline != null)
            {
                pipeline.Enqueue(new MoveAvatarAction(target));
                pipeline.RunToCompletion();
            }
            else
            {
                board.SetAvatar(avatar, target);
            }

            SnapAvatarView(arch, 5);
        }

        private void StartAvatarWatch()
        {
            CancelDwellWatch();
            mWatchingAvatar = true;
            mLastAvatarSlot = 0;
            WatchAvatarLoopAsync().Forget();
        }

        private void CancelDwellWatch()
        {
            mWatchingAvatar = false;
            mDwellCts?.Cancel();
            mDwellCts?.Dispose();
            mDwellCts = null;
        }

        private async UniTaskVoid WatchAvatarLoopAsync()
        {
            mDwellCts = new CancellationTokenSource();
            var ct = mDwellCts.Token;
            try
            {
                while (mWatchingAvatar && !ct.IsCancellationRequested && RoomIconOccupancy.Current.HasAny)
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
            if (!RoomIconOccupancy.Current.TryGet(slot, out var entry))
            {
                mDwell.Cancel();
                return;
            }

            if (mDwell.IsArmed && mDwell.ArmedSlot == slot)
            {
                return;
            }

            mDwell.Begin(slot, entry.OptionIndex);
            RunDwellCountdownAsync(slot, entry.OptionIndex, parentCt).Forget();
        }

        private async UniTaskVoid RunDwellCountdownAsync(int slot, int optionIndex, CancellationToken parentCt)
        {
            try
            {
                await DelayAsync(RoomIconDwellSession.DefaultDwellSeconds, parentCt);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (!mDwell.IsArmed || mDwell.ArmedSlot != slot)
            {
                return;
            }

            if (!mDwell.TryConsumeArmed(out var submitIndex))
            {
                return;
            }

            _ = optionIndex;
            if (SubmitSelectAndEnter(submitIndex))
            {
                mDwell.MarkSubmitted();
            }
            else
            {
                // 失败可再驻留：同格重新武装
                mDwell.Begin(slot, submitIndex);
            }
        }

        private bool SubmitSelectAndEnter(int optionIndex)
        {
            var arch = mArch ?? NineGridArchitecture.Current;
            if (arch == null)
            {
                return false;
            }

            global::NineGrid.Presentation.PresentationInputGates.SetChoiceOverlay(true);
            try
            {
                RoomChoiceCoreHook.RequestWire();
                if (RoomChoiceCoreHook.SelectRoom == null || RoomChoiceCoreHook.EnterRoom == null)
                {
                    Debug.LogWarning("[RoomIcon] RoomChoiceInputController not wired; abort submit.");
                    return false;
                }

                var select = RoomChoiceCoreHook.SelectRoom(optionIndex);
                if (select == null || !select.Accepted)
                {
                    Debug.LogWarning("[RoomIcon] SelectRoom rejected: " + select?.Reason);
                    return false;
                }

                var enter = RoomChoiceCoreHook.EnterRoom();
                if (enter == null || !enter.Accepted)
                {
                    Debug.LogWarning("[RoomIcon] EnterRoom rejected: " + enter?.Reason);
                    return false;
                }

                HardCutAfterEnter(arch);
                return true;
            }
            finally
            {
                global::NineGrid.Presentation.PresentationInputGates.SetChoiceOverlay(false);
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

        private static GameObject TryInstantiateIcon(
            string prefabPath,
            IGroundFieldGeometrySystem geometry,
            int slot,
            string contentId)
        {
            var prefab = LoadPrefab(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[RoomIcon] missing prefab for " + contentId + " path=" + prefabPath);
                // 仍登记占格，便于走格/驻留在无美术时测通。
                return null;
            }

            Transform parent = null;
            Vector3 pos = Vector3.zero;
            if (geometry != null)
            {
                var anchor = geometry.GetGroundAnchor(slot);
                if (anchor != null)
                {
                    parent = anchor;
                    pos = anchor.position;
                }
            }

            var go = UnityEngine.Object.Instantiate(prefab, parent);
            go.name = "RoomIcon_" + contentId + "_@" + slot;
            if (parent == null)
            {
                go.transform.position = pos;
            }
            else
            {
                go.transform.localPosition = Vector3.zero;
            }

            return go;
        }

        private static GameObject LoadPrefab(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
#else
            return null;
#endif
        }

        private static void SnapAvatarView(IArchitecture arch, int slot)
        {
            var geometry = arch?.GetSystem<IGroundFieldGeometrySystem>();
            var board = arch?.GetModel<BoardModel>();
            if (geometry == null || board == null)
            {
                return;
            }

            var avatarUid = board.AvatarUid.Value;
            if (avatarUid <= 0)
            {
                return;
            }

            if (geometry.TryFindOccupiedSlotForUid(avatarUid, out var fromSlot) && fromSlot != slot)
            {
                if (geometry.RequestMoveCard(fromSlot, slot, animate: false))
                {
                    return;
                }
            }

            if (!CardEntityLifecycleHook.TryGetCard(avatarUid, out var card)
                || card?.Transform == null)
            {
                return;
            }

            var anchor = geometry.GetGroundAnchor(slot);
            if (anchor != null)
            {
                card.Transform.position = anchor.position;
            }
        }
    }
}
