using System;
using System.Collections.Generic;
using NineGrid.Cards.Anim;
using UnityEngine;
using UnityEngine.Rendering;

namespace NineGrid.Cards
{
    /// <summary>
    /// 单张卡牌在 ripple 重排中的移动描述。
    /// </summary>
    public readonly struct CardDeckRippleMove
    {
        public CardDeckRippleMove(ManagedCard card, int fromSlot, int toSlot, Vector3 targetPosition, float delay)
        {
            Card = card;
            FromSlot = fromSlot;
            ToSlot = toSlot;
            TargetPosition = targetPosition;
            Delay = delay;
        }

        public ManagedCard Card { get; }
        public int FromSlot { get; }
        public int ToSlot { get; }
        public Vector3 TargetPosition { get; }
        public float Delay { get; }
    }

    /// <summary>
    /// InGame 卡槽容器：维护左侧致密的卡牌列表，计算布局与 ripple 移动计划。
    /// 视觉槽位受 maxSlots 限制；超出部分叠在末位，逻辑上不截断。
    /// </summary>
    public sealed class CardDeckSlotContainer
    {
        private const string DefaultSortingLayerName = "Main";

        private readonly List<ManagedCard> _slots = new();
        private readonly CardDeckLayoutSettings _settings;
        private readonly int _maxSlots;
        private Vector3[] _layoutAnchorPositions;
        private float _layoutLeftX;
        private float _layoutBaseY;
        private float _layoutBaseZ;
        private string _sortingLayerName = DefaultSortingLayerName;

        public CardDeckSlotContainer(CardDeckLayoutSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _maxSlots = Mathf.Max(1, settings.maxSlots);
        }

        /// <summary>
        /// 卡组域 SortingLayer（默认 Main）。回收 UI 激活时可临时切到 BG，使 Notice 压住卡组。
        /// </summary>
        public void SetSortingLayerName(string sortingLayerName)
        {
            _sortingLayerName = string.IsNullOrEmpty(sortingLayerName)
                ? DefaultSortingLayerName
                : sortingLayerName;
        }

        /// <summary>视觉槽位上限（场景锚点数），不是逻辑容量上限。</summary>
        public int MaxSlots => _maxSlots;

        public int Count => _slots.Count;

        public void SetLayoutOrigin(float leftX, float baseY, float baseZ)
        {
            _layoutLeftX = leftX;
            _layoutBaseY = baseY;
            _layoutBaseZ = baseZ;
        }

        /// <summary>
        /// 注入场景 CardDeckAnchors 槽位世界坐标；动态布局优先使用锚点以实现部分遮挡间距。
        /// </summary>
        public void SetLayoutAnchorPositions(IReadOnlyList<Transform> anchors)
        {
            if (anchors == null || anchors.Count == 0)
            {
                _layoutAnchorPositions = null;
                return;
            }

            _layoutAnchorPositions = new Vector3[anchors.Count];
            for (var i = 0; i < anchors.Count; i++)
            {
                var anchor = anchors[i];
                _layoutAnchorPositions[i] = anchor != null ? anchor.position : Vector3.zero;
            }
        }

        public void Clear()
        {
            _slots.Clear();
        }

        public bool TryGetCardAt(int slotIndex, out ManagedCard card)
        {
            if (!IsOccupiedSlot(slotIndex))
            {
                card = null;
                return false;
            }

            card = _slots[slotIndex];
            return card != null;
        }

        public ManagedCard GetCardAt(int slotIndex)
        {
            return IsOccupiedSlot(slotIndex) ? _slots[slotIndex] : null;
        }

        public void SetCardsDense(IReadOnlyList<ManagedCard> cards)
        {
            Clear();
            if (cards == null)
            {
                return;
            }

            for (var i = 0; i < cards.Count; i++)
            {
                if (cards[i] != null)
                {
                    _slots.Add(cards[i]);
                }
            }
        }

        /// <summary>
        /// 按 Core 抽牌堆 uid 序重排已入组卡。slot 0 = 下一张。
        /// 缺席 uid 跳过并 Warning；组内多余卡（Core 已移出、通常为本批待飞出的发牌）
        /// 前置到队首——它们才是接下来真正要飞出的「下一张」。序未变返回 false。
        /// <paramref name="pendingDealUids"/> 为本批 Core 发牌序：批内若发生洗牌，
        /// 多余卡的旧视觉相对序已不等于 Core 抽出序，必须按发牌序排队首，
        /// 否则 slot 0 显示的牌与下一张真正飞出的牌不是同一张。
        /// </summary>
        public bool TryReorderToUids(
            IReadOnlyList<int> orderedUids,
            IReadOnlyList<int> pendingDealUids = null)
        {
            if (orderedUids == null || _slots.Count == 0)
            {
                return false;
            }

            var byUid = new Dictionary<int, ManagedCard>(_slots.Count);
            for (var i = 0; i < _slots.Count; i++)
            {
                var card = _slots[i];
                if (card == null || card.Uid <= 0)
                {
                    continue;
                }

                if (!byUid.ContainsKey(card.Uid))
                {
                    byUid.Add(card.Uid, card);
                }
            }

            var reordered = new List<ManagedCard>(_slots.Count);
            var placed = new HashSet<int>();
            for (var i = 0; i < orderedUids.Count; i++)
            {
                var uid = orderedUids[i];
                if (uid <= 0 || !byUid.TryGetValue(uid, out var card))
                {
                    if (uid > 0)
                    {
                        Debug.LogWarning(
                            $"[CardDeckSlotContainer] TryReorderToUids 缺席 uid={uid}（Core 有、视觉无）。");
                    }

                    continue;
                }

                reordered.Add(card);
                placed.Add(uid);
            }

            var extras = new List<ManagedCard>();
            for (var i = 0; i < _slots.Count; i++)
            {
                var card = _slots[i];
                if (card == null || card.Uid <= 0 || placed.Contains(card.Uid))
                {
                    continue;
                }

                if (!IsPendingDealUid(pendingDealUids, card.Uid))
                {
                    Debug.LogWarning(
                        $"[CardDeckSlotContainer] TryReorderToUids 多余视觉卡 uid={card.Uid}（Core 已移出且非本批发牌），前置队首待飞出。");
                }

                extras.Add(card);
                placed.Add(card.Uid);
            }

            if (extras.Count > 1)
            {
                SortExtrasByPendingDealOrder(extras, pendingDealUids);
            }

            if (extras.Count > 0)
            {
                reordered.InsertRange(0, extras);
            }

            if (reordered.Count != _slots.Count)
            {
                return false;
            }

            var changed = false;
            for (var i = 0; i < reordered.Count; i++)
            {
                if (_slots[i] != reordered[i])
                {
                    changed = true;
                    break;
                }
            }

            if (!changed)
            {
                return false;
            }

            _slots.Clear();
            _slots.AddRange(reordered);
            return true;
        }

        public IReadOnlyList<ManagedCard> SnapshotCards()
        {
            return new List<ManagedCard>(_slots);
        }

        /// <summary>
        /// 重排后 slot 0 的期望 uid：本批还没飞出的第一张发牌；无发牌序时取 0（不可无歧义判定）。
        /// </summary>
        public int ResolvePendingTopUid(IReadOnlyList<int> pendingDealUids)
        {
            if (pendingDealUids == null)
            {
                return 0;
            }

            for (var i = 0; i < pendingDealUids.Count; i++)
            {
                var uid = pendingDealUids[i];
                if (uid <= 0)
                {
                    continue;
                }

                for (var j = 0; j < _slots.Count; j++)
                {
                    var card = _slots[j];
                    if (card != null && card.Uid == uid)
                    {
                        return uid;
                    }
                }
            }

            return 0;
        }

        private static bool IsPendingDealUid(IReadOnlyList<int> pendingDealUids, int uid)
        {
            if (pendingDealUids == null || uid <= 0)
            {
                return false;
            }

            for (var i = 0; i < pendingDealUids.Count; i++)
            {
                if (pendingDealUids[i] == uid)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 待飞出卡按本批 Core 发牌序排前，其余（去向不明的多余卡）保持视觉相对序在后。
        /// </summary>
        private static void SortExtrasByPendingDealOrder(
            List<ManagedCard> extras,
            IReadOnlyList<int> pendingDealUids)
        {
            if (pendingDealUids == null || pendingDealUids.Count == 0)
            {
                return;
            }

            var rankByUid = new Dictionary<int, int>(pendingDealUids.Count);
            for (var i = 0; i < pendingDealUids.Count; i++)
            {
                var uid = pendingDealUids[i];
                if (uid > 0 && !rankByUid.ContainsKey(uid))
                {
                    rankByUid.Add(uid, i);
                }
            }

            if (rankByUid.Count == 0)
            {
                return;
            }

            var ranked = new List<ManagedCard>(extras.Count);
            var unranked = new List<ManagedCard>(extras.Count);
            for (var i = 0; i < extras.Count; i++)
            {
                var card = extras[i];
                if (card != null && rankByUid.ContainsKey(card.Uid))
                {
                    ranked.Add(card);
                }
                else
                {
                    unranked.Add(card);
                }
            }

            if (ranked.Count <= 1)
            {
                return;
            }

            ranked.Sort((left, right) => rankByUid[left.Uid].CompareTo(rankByUid[right.Uid]));
            extras.Clear();
            extras.AddRange(ranked);
            extras.AddRange(unranked);
        }

        public Vector3 GetLayoutPosition(int slotIndex)
        {
            var layoutIndex = Mathf.Clamp(slotIndex, 0, _maxSlots - 1);
            var basePosition = GetBaseLayoutPosition(layoutIndex);
            if (slotIndex <= _maxSlots - 1)
            {
                return basePosition;
            }

            var overflowDepth = slotIndex - (_maxSlots - 1);
            return new Vector3(
                basePosition.x,
                basePosition.y,
                basePosition.z + overflowDepth * _settings.overflowStackZStep);
        }

        public IReadOnlyList<Vector3> ComputeLayoutPositions(int count)
        {
            var positions = new List<Vector3>(count);
            for (var i = 0; i < count; i++)
            {
                positions.Add(GetLayoutPosition(i));
            }

            return positions;
        }

        public void ApplySortingOrders()
        {
            for (var i = 0; i < _slots.Count; i++)
            {
                ApplySortingOrder(_slots[i], i);
            }
        }

        /// <summary>
        /// 卡组槽位权威 sortingOrder：左起（索引小）更高，右下更低。
        /// </summary>
        public int ComputeSortingOrder(int slotIndex) =>
            _settings.sortingOrderBase - slotIndex * _settings.sortingOrderStep;

        /// <summary>
        /// 按槽位索引设置单卡 sortingOrder：左起（索引小）更高，右下更低。
        /// 同步写入当前域 SortingLayer，并 Propagate 到子 Renderer / Mask。
        /// </summary>
        public void ApplySortingOrder(ManagedCard card, int slotIndex)
        {
            if (card?.View == null || card.DisplayMode != CardDisplayMode.CardDeckMode)
            {
                return;
            }

            var sortingGroup = card.View.GetComponent<SortingGroup>();
            if (sortingGroup == null)
            {
                return;
            }

            sortingGroup.sortingOrder = ComputeSortingOrder(slotIndex);
            // 新入组牌（离开机关洗入等）SG 层名可能已是目标层，但仍须 Propagate：
            // URP Mask 比对的是子 Renderer/Mask 的 sortingLayerID；只改 SG 不 Propagate
            // 时子节点会留在 Main，表现为逃出卡组 BG 约束、始终压在最上。
            if (sortingGroup.sortingLayerName != _sortingLayerName)
            {
                sortingGroup.sortingLayerName = _sortingLayerName;
            }

            CardMainVisualMaskAnchor.PropagateSortingLayerFromGroup(sortingGroup);
        }

        public bool TryRemoveAt(int slotIndex, out ManagedCard removed, out IReadOnlyList<CardDeckRippleMove> rippleMoves)
        {
            removed = null;
            rippleMoves = Array.Empty<CardDeckRippleMove>();

            if (!IsOccupiedSlot(slotIndex))
            {
                return false;
            }

            removed = _slots[slotIndex];
            _slots.RemoveAt(slotIndex);
            rippleMoves = BuildRippleMoves(pivotSlot: slotIndex);
            ApplySortingOrders();
            return true;
        }

        public bool TryInsertAt(int slotIndex, ManagedCard card, out IReadOnlyList<CardDeckRippleMove> rippleMoves)
        {
            rippleMoves = Array.Empty<CardDeckRippleMove>();

            if (card == null || slotIndex < 0)
            {
                return false;
            }

            var clampedSlot = Mathf.Clamp(slotIndex, 0, _slots.Count);
            _slots.Insert(clampedSlot, card);
            rippleMoves = BuildRippleMoves(pivotSlot: clampedSlot);
            ApplySortingOrders();
            return true;
        }

        private Vector3 GetBaseLayoutPosition(int layoutIndex)
        {
            if (_layoutAnchorPositions != null &&
                layoutIndex >= 0 &&
                layoutIndex < _layoutAnchorPositions.Length)
            {
                return _layoutAnchorPositions[layoutIndex];
            }

            return new Vector3(
                _layoutLeftX + layoutIndex * _settings.cardSpacing,
                _layoutBaseY,
                _layoutBaseZ);
        }

        private List<CardDeckRippleMove> BuildRippleMoves(int pivotSlot)
        {
            var moves = new List<CardDeckRippleMove>();
            for (var i = 0; i < _slots.Count; i++)
            {
                var card = _slots[i];
                if (card?.Transform == null)
                {
                    continue;
                }

                var target = GetLayoutPosition(i);
                var current = card.Transform.position;
                // 已在目标位但仍有在飞的旧缓动时不能跳过：那条缓动的落点是改序前的旧槽位，
                // 放它跑完会把牌拖离本槽（视觉最左 ≠ slot 0）。收进 moves 由 MoveToWorld 掐死重瞄。
                if (Vector3.SqrMagnitude(current - target) < 0.0001f
                    && !CardDeckTween.IsMotionActive(card.Transform))
                {
                    continue;
                }

                var delay = Mathf.Abs(i - pivotSlot) * _settings.rippleDelayPerSlot;
                moves.Add(new CardDeckRippleMove(card, i, i, target, delay));
            }

            return moves;
        }

        private bool IsOccupiedSlot(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < _slots.Count && _slots[slotIndex] != null;
        }
    }
}
