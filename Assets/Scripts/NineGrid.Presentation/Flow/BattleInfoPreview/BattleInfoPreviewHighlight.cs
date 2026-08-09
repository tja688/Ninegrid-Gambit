using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NineGrid.Flow.BattleInfoPreview
{
    /// <summary>
    /// 预览槽悬停：九宫四角框（中间透明），轻轻框住当前图标，不跟描边 / Cover。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleInfoPreviewHighlight : MonoBehaviour
    {
        public const string DefaultCornerFrameAssetPath =
            "Assets/Arts/Images/Png/2D Pixel Quest Vol3_ The UI-GUI/Panels/Frames/F_U_Frame3.png";

        private const string ChildName = "__Highlight";
        private const float MinSize = 0.12f;

        [SerializeField] private SpriteRenderer ring;
        [SerializeField] private Color highlightColor = Color.white;
        [SerializeField] private Sprite cornerFrameSprite;
        [Tooltip("相对图标包围盒的额外边距（本地单位）。")]
        [SerializeField] private float padding = 0.06f;
        [Tooltip("框相对图标再放大一点的比例（1 = 刚好贴边+padding）。")]
        [SerializeField] private float sizeMultiplier = 1.05f;

        private SpriteRenderer _icon;
        private Vector2 _fallbackLocalSize = new Vector2(0.8f, 0.8f);
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

        public void Configure(Color color, Sprite cornerFrame = null, float pad = -1f, float sizeMul = -1f)
        {
            // 四角框自带金色，默认白 tint；若传入接近默认黄则视为未定制。
            highlightColor = IsLegacyYellow(color) ? Color.white : color;
            if (cornerFrame != null)
            {
                cornerFrameSprite = cornerFrame;
            }

            if (pad > 0f)
            {
                padding = pad;
            }

            if (sizeMul > 0.01f)
            {
                sizeMultiplier = sizeMul;
            }

            if (ring != null)
            {
                ring.color = highlightColor;
            }
        }

        /// <summary>绑定图标与槽兜底尺寸；悬停时按图标包围盒九宫框选。</summary>
        public void BindTarget(SpriteRenderer icon, Vector2 fallbackSlotLocalSize)
        {
            _icon = icon;
            _fallbackLocalSize = fallbackSlotLocalSize.x > 0.0001f && fallbackSlotLocalSize.y > 0.0001f
                ? fallbackSlotLocalSize
                : new Vector2(0.8f, 0.8f);
            EnsureRing();
            Hide();
        }

        /// <summary>兼容旧调用名。</summary>
        public void BindSlotFrame(Sprite unusedFrameSprite, Vector2 slotLocalSize, SpriteRenderer sortSource)
        {
            BindTarget(sortSource, slotLocalSize);
        }

        public void BindIcon(SpriteRenderer icon)
        {
            BindTarget(icon, new Vector2(0.8f, 0.8f));
        }

        public void Show()
        {
            EnsureRing();
            if (ring == null || ring.sprite == null)
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
            if (ring == null)
            {
                var existing = transform.Find(ChildName);
                if (existing != null)
                {
                    ring = existing.GetComponent<SpriteRenderer>();
                }
            }

            if (ring == null)
            {
                var go = new GameObject(ChildName);
                go.transform.SetParent(transform, false);
                go.transform.SetAsLastSibling();
                ring = go.AddComponent<SpriteRenderer>();
            }

            if (cornerFrameSprite == null)
            {
                cornerFrameSprite = TryLoadDefaultCornerFrame();
            }

            ring.sprite = cornerFrameSprite;
            ring.color = highlightColor;
            ring.maskInteraction = SpriteMaskInteraction.None;
            ring.drawMode = SpriteDrawMode.Sliced;
            ring.enabled = false;
            ring.transform.localRotation = Quaternion.identity;
            ring.transform.localScale = Vector3.one;
        }

        private void SyncFromIcon()
        {
            if (ring == null)
            {
                return;
            }

            if (cornerFrameSprite == null)
            {
                cornerFrameSprite = TryLoadDefaultCornerFrame();
            }

            if (cornerFrameSprite == null)
            {
                ring.enabled = false;
                return;
            }

            ring.sprite = cornerFrameSprite;
            ring.drawMode = SpriteDrawMode.Sliced;
            ring.color = highlightColor;
            ring.flipX = false;
            ring.flipY = false;
            ring.transform.localScale = Vector3.one;
            ring.transform.localRotation = Quaternion.identity;

            if (_icon != null)
            {
                ring.sortingLayerID = _icon.sortingLayerID;
                ring.sortingOrder = _icon.sortingOrder + 2;
            }

            if (!TryMeasureIconLocal(out var center, out var size))
            {
                center = Vector3.zero;
                size = _fallbackLocalSize;
            }

            var mul = Mathf.Max(0.01f, sizeMultiplier);
            var w = Mathf.Max(MinSize, size.x * mul + padding * 2f);
            var h = Mathf.Max(MinSize, size.y * mul + padding * 2f);

            // 九宫最小边不小于边角像素，避免切成一团。
            if (cornerFrameSprite != null)
            {
                var border = cornerFrameSprite.border;
                var ppu = Mathf.Max(0.01f, cornerFrameSprite.pixelsPerUnit);
                var minW = (border.x + border.z) / ppu;
                var minH = (border.y + border.w) / ppu;
                w = Mathf.Max(w, minW);
                h = Mathf.Max(h, minH);
            }

            ring.transform.localPosition = new Vector3(center.x, center.y, 0f);
            ring.size = new Vector2(w, h);
        }

        private bool TryMeasureIconLocal(out Vector3 localCenter, out Vector2 localSize)
        {
            localCenter = Vector3.zero;
            localSize = _fallbackLocalSize;
            if (_icon == null || _icon.sprite == null || !_icon.enabled)
            {
                return false;
            }

            var bounds = _icon.sprite.bounds;
            var scale = _icon.transform.localScale;
            var sx = Mathf.Abs(scale.x);
            var sy = Mathf.Abs(scale.y);
            localSize = new Vector2(
                Mathf.Max(MinSize, bounds.size.x * sx),
                Mathf.Max(MinSize, bounds.size.y * sy));

            // __Art 在槽位本地；中心 = 子节点位 + 缩放后的 sprite 中心。
            var artLocal = _icon.transform.localPosition;
            localCenter = new Vector3(
                artLocal.x + bounds.center.x * scale.x,
                artLocal.y + bounds.center.y * scale.y,
                0f);
            return true;
        }

        private static Sprite TryLoadDefaultCornerFrame()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<Sprite>(DefaultCornerFrameAssetPath);
#else
            return null;
#endif
        }

        private static bool IsLegacyYellow(Color color)
        {
            return Mathf.Abs(color.r - 1f) < 0.02f
                && Mathf.Abs(color.g - 0.92f) < 0.05f
                && color.b < 0.35f;
        }
    }
}
