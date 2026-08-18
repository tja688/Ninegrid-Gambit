using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using NineGrid.Cards.Vfx;
using NineGrid.Core;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation;
using NineGrid.Presentation.Systems;

namespace NineGrid.Cards
{
    /// <summary>
    /// 场地多选选卡模式：道具卡打出后累积 N 个盘面目标，选满后提交 Core UseItem。
    /// </summary>
    public static class BoardCardSelectModeController
    {
        private static readonly List<int> SelectedUids = new(4);

        private static int _itemUid;
        private static string _itemDefId;
        private static int _requiredCount;
        private static int _parkedItemUid;
        private static bool _active;
        private static bool _committing;

        public static bool IsActive => _active;

        public static int ItemUid => _itemUid;

        public static string ItemDefId => _itemDefId;

        public static int RequiredCount => _requiredCount;

        public static int ParkedItemUid => _parkedItemUid;

        public static IReadOnlyList<int> SelectedUidsReadOnly => SelectedUids;

        /// <summary>选满 N 张后触发；由 Flow 注册并完成 Core 写入与表现。</summary>
        public static event Func<int, int[], UniTask> SelectionCompletedAsync;

        /// <summary>提交失败且未执行 UseItem 时触发；由 Flow 注册并回手兜底。</summary>
        public static event Func<int, string, string, UniTask> SelectionAbortedAsync;

        public static bool Begin(int itemUid, string itemDefId, int requiredCount)
        {
            if (_active || _committing || itemUid <= 0 || requiredCount < 2)
            {
                return false;
            }

            var intake = IntentIntakeSystem.EnsureRegistered();
            bool preview;
            if (intake.Submit(
                    new InputIntent(InputIntentKinds.BoardSelectBegin, itemUid),
                    InputOwner.ProtectedField,
                    out preview) != IntentDisposition.Allow)
            {
                return false;
            }

            _itemUid = itemUid;
            _itemDefId = itemDefId ?? string.Empty;
            _requiredCount = requiredCount;
            _parkedItemUid = 0;
            SelectedUids.Clear();
            _active = true;
            PresentationInputGates.SetBoardSelect(true);
            RegistryTraceSink.NotifyUserInteraction?.Invoke("BoardSelectBegin");
            RefreshCandidateGlowVisuals();
            Debug.Log(
                $"[BoardCardSelectMode] Begin itemUid={itemUid} defId={_itemDefId} required={requiredCount}");
            return true;
        }

        public static void SetParkedItem(int itemUid)
        {
            if (!_active || itemUid <= 0 || itemUid != _itemUid)
            {
                return;
            }

            _parkedItemUid = itemUid;
        }

        public static void ClearParkedItem()
        {
            _parkedItemUid = 0;
        }

        public static void End()
        {
            // RequestAbort / ParkedAbort 会先清 _active 再异步 End；必须仍能清掉门禁布尔，
            // 否则 CurrentOwner 粘在 BoardSelect，场地点击全部静默失效（hover 仍可用）。
            var hadSession = _active || _committing;
            var gateStuck = PresentationInputGates.BoardSelectModeActive;
            if (!hadSession && !gateStuck)
            {
                return;
            }

            BoardRangeGlowFx.Hide(BoardRangeGlowRunner.InstanceOrNull());

            if (hadSession)
            {
                ClearSelectedVisuals();
            }

            SelectedUids.Clear();
            _itemUid = 0;
            _itemDefId = string.Empty;
            _requiredCount = 0;
            _parkedItemUid = 0;
            _active = false;
            _committing = false;
            PresentationInputGates.SetBoardSelect(false);
            if (hadSession || gateStuck)
            {
                Debug.Log("[BoardCardSelectMode] End");
            }
        }

        /// <summary>流程打断时由 Flow 调用；未 commit 则回手。</summary>
        public static void RequestAbort(string reason)
        {
            BoardRangeGlowFx.Hide(BoardRangeGlowRunner.InstanceOrNull());

            if (!_active || _committing)
            {
                // 静态会话已关但门禁布尔残留：强制收口，避免粘住所有权轴。
                if (PresentationInputGates.BoardSelectModeActive)
                {
                    End();
                }

                return;
            }

            var itemUid = _itemUid;
            var defId = _itemDefId;
            // 保持 _active 直至 End：否则 End 早退会留下 BoardSelect 门禁粘连。
            InvokeSelectionAbortedAsync(itemUid, defId, reason ?? "interrupt").Forget();
        }

        /// <summary>点击驻留效果卡反悔取消。</summary>
        public static bool TryAbortByParkedItemClick(int uid)
        {
            if (!_active || _committing || uid <= 0 || uid != _parkedItemUid || uid != _itemUid)
            {
                return false;
            }

            var itemUid = _itemUid;
            var defId = _itemDefId;
            BoardRangeGlowFx.Hide(BoardRangeGlowRunner.InstanceOrNull());
            ClearSelectedVisuals();
            SelectedUids.Clear();
            RegistryTraceSink.NotifyUserInteraction?.Invoke("BoardSelectCancelParked");
            Debug.Log(
                $"[BoardCardSelectMode] AbortByParkedClick itemUid={itemUid} defId={defId}");
            // 勿先清 _active：交由 InvokeSelectionAbortedAsync → End 统一收口门禁。
            InvokeSelectionAbortedAsync(itemUid, defId, "parked-click").Forget();
            return true;
        }

        public static bool IsSelected(int uid) => uid > 0 && SelectedUids.Contains(uid);

        public static bool IsEligibleTarget(ManagedCard card)
        {
            if (!_active || card == null || card.Uid <= 0)
            {
                return false;
            }

            if (card.DisplayMode != CardDisplayMode.GroundCardMode)
            {
                return false;
            }

            if (card.CoreKind == CardPresentationKind.Avatar)
            {
                return false;
            }

            if (NineGrid.Flow.HelpCardBoardSelectResolver.IsImmuneToItemTargeting(card.Uid, card.DefId))
            {
                return false;
            }

            var field = GroundFieldGeometryHook.FieldOrNull();
            return field != null && field.TryGetSlotOf(card.Uid, out _);
        }

        /// <summary>点击场地卡 toggle；选满时异步提交。</summary>
        public static bool TryToggleSelection(ManagedCard card)
        {
            if (!_active || _committing || card == null)
            {
                return false;
            }

            if (!IsEligibleTarget(card))
            {
                return false;
            }

            var uid = card.Uid;
            var driver = card.View != null ? card.View.GetComponent<CardVisualDriver>() : null;

            if (IsSelected(uid))
            {
                SelectedUids.Remove(uid);
                driver?.SetTarget(CardVisualTarget.Base);
                if (SelectedUids.Count == 0)
                {
                    RegistryTraceSink.NotifyUserInteraction?.Invoke("BoardSelectDeselectAll");
                    Debug.Log(
                        $"[BoardCardSelectMode] deselect-all-remain-active itemUid={_itemUid} defId={_itemDefId}");
                }

                RefreshCandidateGlowVisuals();
                return true;
            }

            if (SelectedUids.Count >= _requiredCount)
            {
                return false;
            }

            SelectedUids.Add(uid);
            driver?.SetTarget(CardVisualTarget.Selected);
            InteractionAudioCues.PulseCard(
                InteractionAudioCues.CardSelect,
                "BoardCardSelectModeController.TryToggleSelection",
                card.DefId);

            if (SelectedUids.Count >= _requiredCount)
            {
                _committing = true;
                var itemUid = _itemUid;
                var itemDefId = _itemDefId;
                var selected = SelectedUids.ToArray();
                _active = false;
                BoardRangeGlowFx.Hide(BoardRangeGlowRunner.InstanceOrNull());
                RegistryTraceSink.NotifyUserInteraction?.Invoke("BoardSelectCommit");
                Debug.Log(
                    $"[BoardCardSelectMode] Commit itemUid={itemUid} defId={itemDefId} selected={string.Join(",", selected)}");
                CommitSelectionAsync(itemUid, itemDefId, selected).Forget();
            }
            else
            {
                RefreshCandidateGlowVisuals();
            }

            return true;
        }

        public static void RefreshCandidateGlowVisuals()
        {
            if (!_active || _committing)
            {
                BoardRangeGlowFx.Hide(BoardRangeGlowRunner.InstanceOrNull());
                return;
            }

            var runner = BoardRangeGlowRunner.Ensure();
            var field = GroundFieldGeometryHook.FieldOrNull();
            if (field == null)
            {
                BoardRangeGlowFx.Hide(runner);
                return;
            }

            NineGrid.Flow.HelpCardBoardSelectResolver.TryGetPlayKind(_itemDefId, out _, out var spec);

            var arch = NineGridArchitecture.Current;
            var registry = arch?.GetModel<CardRegistry>();

            var candidateSlots = new List<int>(9);
            var selectedSlots = new List<int>(4);

            for (var slot = GroundSlotTopology.MinSlot; slot <= GroundSlotTopology.MaxSlot; slot++)
            {
                if (!field.TryGetCardAt(slot, out var boardCard) || boardCard == null)
                {
                    continue;
                }

                if (!IsEligibleTarget(boardCard))
                {
                    continue;
                }

                if (spec.RequiresTrueMonster && boardCard.CoreKind != CardPresentationKind.Monster)
                {
                    continue;
                }

                if (spec.RequiresCombatTarget
                    && boardCard.CoreKind != CardPresentationKind.Monster
                    && boardCard.CoreKind != CardPresentationKind.Trap)
                {
                    continue;
                }

                if (spec.RequiresFaceUp)
                {
                    var coreCard = registry?.Get(boardCard.Uid);
                    var isFaceUp = coreCard != null
                        ? coreCard.FaceUp
                        : (boardCard.CommittedPresentation?.FaceUp ?? true);
                    if (!isFaceUp)
                    {
                        continue;
                    }
                }

                candidateSlots.Add(slot);
                if (IsSelected(boardCard.Uid))
                {
                    selectedSlots.Add(slot);
                }
            }

            BoardRangeGlowFx.ShowMultiSelectCandidates(runner, candidateSlots, selectedSlots);
        }

        private static async UniTaskVoid CommitSelectionAsync(int itemUid, string itemDefId, int[] selected)
        {
            var handler = SelectionCompletedAsync;
            if (handler == null)
            {
                Debug.LogWarning("[BoardCardSelectMode] SelectionCompletedAsync 未注册，选卡结果丢弃。");
                await InvokeSelectionAbortedAsync(itemUid, itemDefId, "handler-missing");
                End();
                return;
            }

            try
            {
                await handler(itemUid, selected);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                await InvokeSelectionAbortedAsync(itemUid, itemDefId, "handler-exception");
            }
            finally
            {
                if (BoardCardSelectModeController.IsActive || _committing)
                {
                    End();
                }
            }
        }

        private static async UniTask InvokeSelectionAbortedAsync(int itemUid, string itemDefId, string reason)
        {
            var aborter = SelectionAbortedAsync;
            if (aborter == null)
            {
                End();
                return;
            }

            try
            {
                await aborter(itemUid, itemDefId, reason);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            finally
            {
                End();
            }
        }

        private static void ClearSelectedVisuals()
        {
            var cardManager = CardEntityLifecycleHook.CardsOrNull();
            if (cardManager == null)
            {
                return;
            }

            for (var i = 0; i < SelectedUids.Count; i++)
            {
                var uid = SelectedUids[i];
                if (!cardManager.TryGet(uid, out var card) || card?.View == null)
                {
                    continue;
                }

                var driver = card.View.GetComponent<CardVisualDriver>();
                driver?.SetTarget(CardVisualTarget.Base);
            }
        }
    }
}
