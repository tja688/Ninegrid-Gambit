using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.Cards
{
    internal enum DigitAlignment
    {
        Center,
        /// <summary>百十个固定槽位，个位始终在最右槽。</summary>
        FixedSlotsOnesRight,
    }

    internal sealed class PixelDigitDisplay
    {
        private readonly Transform _anchor;
        private readonly PixelCardPackSpriteLibrary _library;
        private readonly float _spacing;
        private readonly DigitAlignment _alignment;
        private readonly float[] _fixedSlotOffsetsFromOnes;
        private readonly Vector3 _digitScale;
        private readonly Color _digitColor;
        private readonly int _sortingLayerId;
        private readonly int _sortingOrder;
        private readonly MonoBehaviour _runner;
        private readonly List<SpriteRenderer> _renderers = new();
        private int _value = -1;
        private Coroutine _punchRoutine;

        public PixelDigitDisplay(
            Transform anchor,
            PixelCardPackSpriteLibrary library,
            float spacing,
            int sortingLayerId,
            int sortingOrder,
            MonoBehaviour runner,
            DigitAlignment alignment = DigitAlignment.Center,
            float[] fixedSlotOffsetsFromOnes = null,
            Vector3? digitScale = null,
            Color? digitColor = null)
        {
            _anchor = anchor;
            _library = library;
            _spacing = spacing;
            _alignment = alignment;
            _fixedSlotOffsetsFromOnes = fixedSlotOffsetsFromOnes;
            _digitScale = digitScale ?? Vector3.one;
            _digitColor = digitColor ?? Color.white;
            _sortingLayerId = sortingLayerId;
            _sortingOrder = sortingOrder;
            _runner = runner;
        }

        public void SetValue(int value, bool animate)
        {
            value = Mathf.Max(0, value);
            if (_value == value)
            {
                return;
            }

            _value = value;
            RenderDigits(value);

            if (animate && _runner != null && _anchor != null)
            {
                if (_punchRoutine != null)
                {
                    _runner.StopCoroutine(_punchRoutine);
                }

                _punchRoutine = _runner.StartCoroutine(CardViewTween.PunchScale(_anchor));
            }
        }

        public void SetVisible(bool visible)
        {
            if (_anchor != null)
            {
                _anchor.gameObject.SetActive(visible);
            }
        }

        private void RenderDigits(int value)
        {
            var digits = BuildDigits(value);
            EnsureRendererCount(digits.Count);

            for (var i = 0; i < _renderers.Count; i++)
            {
                var renderer = _renderers[i];
                var active = i < digits.Count;
                renderer.gameObject.SetActive(active);
                if (!active)
                {
                    continue;
                }

                renderer.sprite = _library.GetDigit(digits[i]);
                renderer.color = _digitColor;
                renderer.transform.localPosition = new Vector3(ResolveDigitX(i, digits.Count), 0f, 0f);
                renderer.transform.localScale = _digitScale;
            }
        }

        private float ResolveDigitX(int digitIndex, int digitCount)
        {
            if (_alignment == DigitAlignment.FixedSlotsOnesRight)
            {
                var slotCount = _fixedSlotOffsetsFromOnes?.Length ?? 0;
                if (slotCount == 0)
                {
                    return 0f;
                }

                var slotIndex = slotCount - digitCount + digitIndex;
                return _fixedSlotOffsetsFromOnes[Mathf.Clamp(slotIndex, 0, slotCount - 1)];
            }

            var spacing = ResolveSpacing();
            var totalWidth = digitCount > 0 ? (digitCount - 1) * spacing : 0f;
            var startX = -totalWidth * 0.5f;
            return startX + digitIndex * spacing;
        }

        private float ResolveSpacing()
        {
            var sample = _library.GetDigit(0);
            if (sample == null)
            {
                return _spacing;
            }

            return Mathf.Max(_spacing, sample.bounds.size.x * 1.05f);
        }

        private void EnsureRendererCount(int count)
        {
            while (_renderers.Count < count)
            {
                var index = _renderers.Count;
                var child = new GameObject($"Digit_{index}");
                child.transform.SetParent(_anchor, false);
                var renderer = child.AddComponent<SpriteRenderer>();
                renderer.sortingLayerID = _sortingLayerId;
                renderer.sortingOrder = _sortingOrder;
                renderer.color = _digitColor;
                child.transform.localScale = _digitScale;
                _renderers.Add(renderer);
            }
        }

        private static List<int> BuildDigits(int value)
        {
            var digits = new List<int>();
            if (value <= 0)
            {
                digits.Add(0);
                return digits;
            }

            while (value > 0)
            {
                digits.Insert(0, value % 10);
                value /= 10;
            }

            return digits;
        }
    }
}
