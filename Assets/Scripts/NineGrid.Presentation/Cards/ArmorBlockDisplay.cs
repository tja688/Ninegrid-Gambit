using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.Presentation.Cards
{
    internal sealed class ArmorBlockDisplay
    {
        private readonly Transform _anchor;
        private readonly PixelCardPackSpriteLibrary _library;
        private readonly Vector3 _blockScale;
        private readonly float _spacing;
        private readonly int _sortingLayerId;
        private readonly int _sortingOrder;
        private readonly MonoBehaviour _runner;
        private readonly List<BlockEntry> _blocks = new();
        private int _visibleCount;

        private sealed class BlockEntry
        {
            public Transform Transform;
            public SpriteRenderer Renderer;
            public Vector3 BaseScale;
            public Coroutine Routine;
        }

        public ArmorBlockDisplay(
            Transform anchor,
            PixelCardPackSpriteLibrary library,
            Vector3 blockScale,
            float spacing,
            int sortingLayerId,
            int sortingOrder,
            MonoBehaviour runner)
        {
            _anchor = anchor;
            _library = library;
            _blockScale = blockScale;
            _spacing = spacing;
            _sortingLayerId = sortingLayerId;
            _sortingOrder = sortingOrder;
            _runner = runner;
        }

        public void SetCount(int count, bool animate)
        {
            count = Mathf.Max(0, count);
            if (_visibleCount == count)
            {
                return;
            }

            if (count > _visibleCount)
            {
                for (var i = _visibleCount; i < count; i++)
                {
                    AddBlock(i, animate);
                }
            }
            else
            {
                for (var i = _visibleCount - 1; i >= count; i--)
                {
                    RemoveBlock(i, animate);
                }
            }

            _visibleCount = count;
            LayoutBlocks(count);
        }

        private void AddBlock(int index, bool animate)
        {
            var entry = GetOrCreateBlock(index);
            entry.Renderer.sprite = _library.ArmorBlockSprite;
            entry.Transform.gameObject.SetActive(true);
            LayoutBlocks(Mathf.Max(_visibleCount, index + 1));

            if (!animate || _runner == null)
            {
                entry.Transform.localScale = entry.BaseScale;
                return;
            }

            StartRoutine(entry, _runner.StartCoroutine(CardViewTween.ScaleAppear(entry.Transform, entry.BaseScale)));
        }

        private void RemoveBlock(int index, bool animate)
        {
            if (index < 0 || index >= _blocks.Count)
            {
                return;
            }

            var entry = _blocks[index];
            if (!animate || _runner == null)
            {
                entry.Transform.gameObject.SetActive(false);
                entry.Transform.localScale = entry.BaseScale;
                return;
            }

            StartRoutine(entry, _runner.StartCoroutine(ScaleOutAndHide(entry)));
        }

        private IEnumerator ScaleOutAndHide(BlockEntry entry)
        {
            yield return CardViewTween.ScaleDisappear(entry.Transform, entry.BaseScale);
            entry.Transform.gameObject.SetActive(false);
            entry.Transform.localScale = entry.BaseScale;
        }

        private BlockEntry GetOrCreateBlock(int index)
        {
            while (_blocks.Count <= index)
            {
                var child = new GameObject($"ArmorBlock_{_blocks.Count}");
                child.transform.SetParent(_anchor, false);
                var renderer = child.AddComponent<SpriteRenderer>();
                renderer.sortingLayerID = _sortingLayerId;
                renderer.sortingOrder = _sortingOrder;
                _blocks.Add(new BlockEntry
                {
                    Transform = child.transform,
                    Renderer = renderer,
                    BaseScale = _blockScale,
                });
            }

            return _blocks[index];
        }

        private void LayoutBlocks(int count)
        {
            if (count <= 0)
            {
                return;
            }

            for (var i = 0; i < count && i < _blocks.Count; i++)
            {
                _blocks[i].Transform.localPosition = new Vector3(i * _spacing, 0f, 0f);
            }
        }

        private static void StartRoutine(BlockEntry entry, Coroutine routine)
        {
            if (entry.Routine != null && entry.Transform != null)
            {
                var runner = entry.Transform.GetComponentInParent<MonoBehaviour>();
                if (runner != null)
                {
                    runner.StopCoroutine(entry.Routine);
                }
            }

            entry.Routine = routine;
        }
    }
}
