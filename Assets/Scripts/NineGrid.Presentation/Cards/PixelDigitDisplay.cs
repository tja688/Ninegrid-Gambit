using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.Presentation.Cards
{
    internal sealed class PixelDigitDisplay
    {
        private readonly Transform _anchor;
        private readonly PixelCardPackSpriteLibrary _library;
        private readonly float _spacing;
        private readonly int _sortingOrder;
        private readonly MonoBehaviour _runner;
        private readonly List<SpriteRenderer> _renderers = new();
        private int _value = -1;
        private Coroutine _punchRoutine;

        public PixelDigitDisplay(
            Transform anchor,
            PixelCardPackSpriteLibrary library,
            float spacing,
            int sortingOrder,
            MonoBehaviour runner)
        {
            _anchor = anchor;
            _library = library;
            _spacing = spacing;
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

            var spacing = ResolveSpacing();
            var totalWidth = digits.Count > 0 ? (digits.Count - 1) * spacing : 0f;
            var startX = -totalWidth * 0.5f;

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
                renderer.transform.localPosition = new Vector3(startX + i * spacing, 0f, 0f);
            }
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
                renderer.sortingOrder = _sortingOrder;
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
