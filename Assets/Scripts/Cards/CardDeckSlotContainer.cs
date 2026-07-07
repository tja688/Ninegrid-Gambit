using System;
using System.Collections.Generic;
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
    /// </summary>
    public sealed class CardDeckSlotContainer
    {
        private readonly ManagedCard[] _slots;
        private readonly CardDeckLayoutSettings _settings;
        private float _layoutLeftX;
        private float _layoutBaseY;
        private float _layoutBaseZ;

        public CardDeckSlotContainer(CardDeckLayoutSettings settings)
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

        public void Clear()
        {
            Array.Clear(_slots, 0, _slots.Length);
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

        public ManagedCard GetCardAt(int slotIndex)
        {
            return IsValidSlot(slotIndex) ? _slots[slotIndex] : null;
        }

        public void SetCardsDense(IReadOnlyList<ManagedCard> cards)
        {
            Clear();
            if (cards == null)
            {
                return;
            }

            var limit = Mathf.Min(cards.Count, _slots.Length);
            for (var i = 0; i < limit; i++)
            {
                _slots[i] = cards[i];
            }
        }

        public IReadOnlyList<ManagedCard> SnapshotCards()
        {
            var list = new List<ManagedCard>(Count);
            for (var i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] != null)
                {
                    list.Add(_slots[i]);
                }
            }

            return list;
        }

        public Vector3 GetLayoutPosition(int slotIndex)
        {
            return new Vector3(
                _layoutLeftX + slotIndex * _settings.cardSpacing,
                _layoutBaseY,
                _layoutBaseZ);
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
            for (var i = 0; i < _slots.Length; i++)
            {
                var card = _slots[i];
                if (card?.View == null)
                {
                    continue;
                }

                var sortingGroup = card.View.GetComponent<SortingGroup>();
                if (sortingGroup != null)
                {
                    sortingGroup.sortingOrder = _settings.sortingOrderBase - i * _settings.sortingOrderStep;
                }
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
