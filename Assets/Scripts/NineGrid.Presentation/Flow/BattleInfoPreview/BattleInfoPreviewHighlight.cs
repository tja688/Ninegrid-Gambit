using UnityEngine;

namespace NineGrid.Flow.BattleInfoPreview
{
    /// <summary>预览槽悬停亮边：可改色黄边（默认缩放衬底）。</summary>
    [DisallowMultipleComponent]
    public sealed class BattleInfoPreviewHighlight : MonoBehaviour
    {
        private const string ChildName = "__Highlight";

        [SerializeField] private SpriteRenderer ring;
        [SerializeField] private Color highlightColor = new Color(1f, 0.92f, 0.2f, 0.95f);
        [SerializeField] private float scaleMultiplier = 1.18f;
        [SerializeField] private Sprite overrideSprite;

        private SpriteRenderer _icon;
        private Vector3 _baseScale = Vector3.one;
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

        public void BindIcon(SpriteRenderer icon)
        {
            _icon = icon;
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

            SyncFromIcon();
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
                SyncFromIcon();
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

            _baseScale = Vector3.one;
            ring.color = highlightColor;
            ring.enabled = false;
        }

        private void SyncFromIcon()
        {
            if (ring == null)
            {
                return;
            }

            Sprite sprite = overrideSprite;
            if (sprite == null && _icon != null)
            {
                sprite = _icon.sprite;
            }

            ring.sprite = sprite;
            ring.color = highlightColor;
            ring.flipX = _icon != null && _icon.flipX;
            ring.flipY = _icon != null && _icon.flipY;

            if (_icon != null)
            {
                ring.sortingLayerID = _icon.sortingLayerID;
                ring.sortingOrder = _icon.sortingOrder - 1;
                ring.transform.localPosition = Vector3.zero;
                ring.transform.localRotation = Quaternion.identity;
                ring.transform.localScale = _baseScale * scaleMultiplier;
            }
        }
    }
}
