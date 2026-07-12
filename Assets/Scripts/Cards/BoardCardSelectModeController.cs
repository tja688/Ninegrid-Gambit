using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

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
        private static bool _active;
        private static bool _committing;

        public static bool IsActive => _active;

        public static int ItemUid => _itemUid;

        public static int RequiredCount => _requiredCount;

        public static IReadOnlyList<int> SelectedUidsReadOnly => SelectedUids;

        /// <summary>选满 N 张后触发；由 Flow 注册并完成 Core 写入与表现。</summary>
        public static event Func<int, int[], UniTask> SelectionCompletedAsync;

        public static bool Begin(int itemUid, string itemDefId, int requiredCount)
        {
            if (_active || _committing || itemUid <= 0 || requiredCount <= 0)
            {
                return false;
            }

            if (CombatHitSink.PresentationLocked || CombatHitSink.ChoiceOverlayActive)
            {
                return false;
            }

            _itemUid = itemUid;
            _itemDefId = itemDefId ?? string.Empty;
            _requiredCount = requiredCount;
            SelectedUids.Clear();
            _active = true;
            CombatHitSink.BoardSelectModeActive = true;
            Debug.Log(
                $"[BoardCardSelectMode] Begin itemUid={itemUid} defId={_itemDefId} required={requiredCount}");
            return true;
        }

        public static void End()
        {
            if (!_active && !_committing)
            {
                return;
            }

            ClearSelectedVisuals();
            SelectedUids.Clear();
            _itemUid = 0;
            _itemDefId = string.Empty;
            _requiredCount = 0;
            _active = false;
            _committing = false;
            CombatHitSink.BoardSelectModeActive = false;
            Debug.Log("[BoardCardSelectMode] End");
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

            var field = GroundFieldManagerSingleton.Instance;
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
                    End();
                }

                return true;
            }

            if (SelectedUids.Count >= _requiredCount)
            {
                return false;
            }

            SelectedUids.Add(uid);
            driver?.SetTarget(CardVisualTarget.Selected);

            if (SelectedUids.Count >= _requiredCount)
            {
                CommitSelectionAsync().Forget();
            }

            return true;
        }

        private static async UniTaskVoid CommitSelectionAsync()
        {
            if (!_active || _committing || SelectedUids.Count < _requiredCount)
            {
                return;
            }

            _committing = true;
            var itemUid = _itemUid;
            var selected = SelectedUids.ToArray();
            _active = false;
            CombatHitSink.BoardSelectModeActive = false;

            var handler = SelectionCompletedAsync;
            if (handler == null)
            {
                Debug.LogWarning("[BoardCardSelectMode] SelectionCompletedAsync 未注册，选卡结果丢弃。");
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
            }
            finally
            {
                End();
            }
        }

        private static void ClearSelectedVisuals()
        {
            var cardManager = CardManagerSingleton.Instance;
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
