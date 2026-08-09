using UnityEngine;

namespace NineGrid.Flow.BattleInfoPreview
{
    /// <summary>预览槽悬停亮边：按固定槽框缩放（不跟 Cover 后的大图走）。</summary>
    [DisallowMultipleComponent]
    public sealed class BattleInfoPreviewHighlight : MonoBehaviour
    {
        private const string ChildName = "__Highlight";

        [SerializeField] private SpriteRenderer ring;
        [SerializeField] private Color highlightColor = new Color(1f, 0.92f, 0.2f, 0.95f);
        [SerializeField] private float scaleMultiplier = 1.18f;
        [SerializeField] private Sprite overrideSprite;

        private SpriteRenderer _sortSource;
        private Sprite _frameSprite;
        private Vector2 _slotLocalSize = new Vector2(0.8f, 0.8f);
        private bool _visible;

        public Color HighlightColor
        {
            get => highlightColor;
            set
            {
                highlightColor = value;
                if (ring != null)
                {
                    ring.color = highlightColor;
                }
            }
        }

        public void Configure(Color color, Sprite sprite = null, float scaleMul = 1.18f)
        {
            highlightColor = color;
            overrideSprite = sprite;
            scaleMultiplier = Mathf.Max(1.01f, scaleMul);
            if (ring != null)
            {
                ring.color = highlightColor;
            }
        }

        /// <summary>旧接口：仅有图标时回退用图标 sprite。</summary>
        public void BindIcon(SpriteRenderer icon)
        {
            _sortSource = icon;
            _frameSprite = null;
            EnsureRing();
            Hide();
        }

        /// <summary>按槽框 sprite / 本地尺寸画黄边，排序跟图标。</summary>
        public void BindSlotFrame(Sprite frameSprite, Vector2 slotLocalSize, SpriteRenderer sortSource)
        {
            _frameSprite = frameSprite;
            _slotLocalSize = slotLocalSize.x > 0.0001f && slotLocalSize.y > 0.0001f
                ? slotLocalSize
                : new Vector2(0.8f, 0.8f);
            _sortSource = sortSource;
            EnsureRing();
            Hide();
        }

        public void Show()
        {
            EnsureRing();
            if (ring == null)
            {
                return;
            }

            SyncFromSlot();
            ring.enabled = true;
            _visible = true;
        }

        public void Hide()
        {
            if (ring != null)
            {
                ring.enabled = false;
            }

            _visible = false;
        }

        private void LateUpdate()
        {
            if (_visible)
            {
                SyncFromSlot();
            }
        }

        private void EnsureRing()
        {
            if (ring != null)
            {
                return;
            }

            var existing = transform.Find(ChildName);
            if (existing != null)
            {
                ring = existing.GetComponent<SpriteRenderer>();
            }

            if (ring == null)
            {
                var go = new GameObject(ChildName);
                go.transform.SetParent(transform, false);
                go.transform.SetAsFirstSibling();
                ring = go.AddComponent<SpriteRenderer>();
            }

            ring.color = highlightColor;
            ring.maskInteraction = SpriteMaskInteraction.None;
            ring.enabled = false;
        }

        private void SyncFromSlot()
        {
            if (ring == null)
            {
                return;
            }

            Sprite sprite = overrideSprite != null ? overrideSprite : _frameSprite;
            if (sprite == null && _sortSource != null)
            {
                sprite = _sortSource.sprite;
            }

            ring.sprite = sprite;
            ring.color = highlightColor;

            if (_sortSource != null)
            {
                ring.sortingLayerID = _sortSource.sortingLayerID;
                ring.sortingOrder = _sortSource.sortingOrder - 1;
                ring.flipX = _sortSource.flipX;
                ring.flipY = _sortSource.flipY;
            }

            ring.transform.localPosition = Vector3.zero;
            ring.transform.localRotation = Quaternion.identity;

            if (sprite != null)
            {
                var sz = sprite.bounds.size;
                var sx = _slotLocalSize.x / Mathf.Max(0.0001f, sz.x) * scaleMultiplier;
                var sy = _slotLocalSize.y / Mathf.Max(0.0001f, sz.y) * scaleMultiplier;
                ring.transform.localScale = new Vector3(sx, sy, 1f);
            }
            else
            {
                ring.transform.localScale = Vector3.one * scaleMultiplier;
            }
        }
    }
}
