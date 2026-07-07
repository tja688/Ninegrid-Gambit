using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NineGrid.Cards
{
    /// <summary>
    /// 手牌槽容器：维护最多 5 张致密列表，计算布局与 ripple 移动计划。
    /// </summary>
    public sealed class CardHandSlotContainer
    {
        private readonly ManagedCard[] _slots;
        private readonly CardHandLayoutSettings _settings;
        private Vector3[] _layoutAnchorPositions;
        private float _layoutLeftX;
        private float _layoutBaseY;
        private float _layoutBaseZ;

        public CardHandSlotContainer(CardHandLayoutSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            var maxSlots = Mathf.Max(1, settings.maxSlots);
            _slots = new ManagedCard[maxSlots];
        }

        public int MaxSlots => _slots.Length;

        public int Count
        {
            get
            {
                var count = 0;
                for (var i = 0; i < _slots.Length; i++)
                {
                    if (_slots[i] != null)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public void SetLayoutOrigin(float leftX, float baseY, float baseZ)
        {
            _layoutLeftX = leftX;
            _layoutBaseY = baseY;
            _layoutBaseZ = baseZ;
        }

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
            Array.Clear(_slots, 0, _slots.Length);
        }

        public bool TryGetSlotOf(ManagedCard card, out int slotIndex)
        {
            slotIndex = -1;
            if (card == null)
            {
                return false;
            }

            for (var i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == card)
                {
                    slotIndex = i;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetCardAt(int slotIndex, out ManagedCard card)
        {
            if (!IsValidSlot(slotIndex))
            {
                card = null;
                return false;
            }

            card = _slots[slotIndex];
            return card != null;
        }

        public Vector3 GetLayoutPosition(int slotIndex)
        {
            if (_layoutAnchorPositions != null &&
                slotIndex >= 0 &&
                slotIndex < _layoutAnchorPositions.Length)
            {
                return _layoutAnchorPositions[slotIndex];
            }

            return new Vector3(
                _layoutLeftX + slotIndex * _settings.cardSpacing,
                _layoutBaseY,
                _layoutBaseZ);
        }

        public void ApplySortingOrders()
        {
            for (var i = 0; i < _slots.Length; i++)
            {
                ApplySortingOrder(_slots[i], i);
            }
        }

        /// <summary>
        /// 按槽位索引设置单卡 sortingOrder：左起（索引小）更高，右下更低（与卡组一致）。
        /// </summary>
        public void ApplySortingOrder(ManagedCard card, int slotIndex)
        {
            if (card?.View == null || card.DisplayMode != CardDisplayMode.HandCardMode)
            {
                return;
            }

            var sortingGroup = card.View.GetComponent<SortingGroup>();
            if (sortingGroup != null)
            {
                sortingGroup.sortingOrder = _settings.sortingOrderBase - slotIndex * _settings.sortingOrderStep;
            }
        }

        public bool TryRemoveAt(int slotIndex, out ManagedCard removed, out IReadOnlyList<CardDeckRippleMove> rippleMoves)
        {
            removed = null;
            rippleMoves = Array.Empty<CardDeckRippleMove>();

            if (!IsValidSlot(slotIndex) || _slots[slotIndex] == null)
            {
                return false;
            }

            removed = _slots[slotIndex];
            CompactFrom(slotIndex);
            rippleMoves = BuildRippleMoves(pivotSlot: slotIndex);
            ApplySortingOrders();
            return true;
        }

        public bool TryInsertAt(int slotIndex, ManagedCard card, out IReadOnlyList<CardDeckRippleMove> rippleMoves)
        {
            rippleMoves = Array.Empty<CardDeckRippleMove>();

            if (card == null || !IsValidSlot(slotIndex))
            {
                return false;
            }

            if (Count >= _slots.Length)
            {
                return false;
            }

            var clampedSlot = Mathf.Clamp(slotIndex, 0, Count);
            ExpandFrom(clampedSlot);
            _slots[clampedSlot] = card;
            rippleMoves = BuildRippleMoves(pivotSlot: clampedSlot);
            ApplySortingOrders();
            return true;
        }

        private void CompactFrom(int removedSlot)
        {
            for (var i = removedSlot; i < _slots.Length - 1; i++)
            {
                _slots[i] = _slots[i + 1];
            }

            _slots[_slots.Length - 1] = null;
        }

        private void ExpandFrom(int insertSlot)
        {
            for (var i = _slots.Length - 1; i > insertSlot; i--)
            {
                _slots[i] = _slots[i - 1];
            }

            _slots[insertSlot] = null;
        }

        private List<CardDeckRippleMove> BuildRippleMoves(int pivotSlot)
        {
            var moves = new List<CardDeckRippleMove>();
            for (var i = 0; i < _slots.Length; i++)
            {
                var card = _slots[i];
                if (card?.Transform == null)
                {
                    continue;
                }

                var target = GetLayoutPosition(i);
                var current = card.Transform.position;
                if (Vector3.SqrMagnitude(current - target) < 0.0001f)
                {
                    continue;
                }

                var delay = Mathf.Abs(i - pivotSlot) * _settings.rippleDelayPerSlot;
                moves.Add(new CardDeckRippleMove(card, i, i, target, delay));
            }

            return moves;
        }

        private bool IsValidSlot(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < _slots.Length;
        }
    }
}
