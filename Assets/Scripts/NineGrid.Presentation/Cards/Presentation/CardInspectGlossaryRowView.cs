using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NineGrid.Cards.Presentation
{
    /// <summary>
    /// 右键详情中的单条词条行（名字 + 详细介绍）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CardInspectGlossaryRowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text body;

        private bool _hasDefaultColor;
        private Color _defaultColor;
        private bool _hasBaseMargin;
        private Vector4 _baseMargin;
        private TMP_SpriteAsset _spriteAsset;

        /// <summary>
        /// 强制本行正文默认色（模板色为白色半透明；浅色底面板行须改黑保证可读）。
        /// 先于 Bind 调用，避免 EnsureBody 捕获模板色覆盖。
        /// </summary>
        public void SetDefaultBodyColor(Color color)
        {
            _defaultColor = color;
            _hasDefaultColor = true;
        }

        public void Bind(string displayName, string explanation, bool hasColor, Color color, string inlineCode = null)
        {
            EnsureBody();
            CacheBaseMargin();
            if (body == null)
            {
                return;
            }

            var title = displayName ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(inlineCode))
            {
                title = "[" + inlineCode.Trim() + "] " + title;
            }

            var detail = explanation ?? string.Empty;
            string raw;
            if (string.IsNullOrWhiteSpace(detail))
            {
                raw = title;
            }
            else
            {
                raw = title + "\n" + detail.Trim();
            }

            ApplyComposedText(raw);
            body.color = hasColor ? color : _defaultColor;
        }

        public void Clear()
        {
            EnsureBody();
            if (body != null)
            {
                body.text = string.Empty;
            }
        }

        /// <summary>
        /// mesh TMP 不受 uGUI Mask 裁切：按 Viewport 世界包围盒动态扩 margin + Masking overflow，
        /// 整行在视口外则关 Renderer。
        /// </summary>
        public void ApplyViewportClip(RectTransform viewport)
        {
            EnsureBody();
            CacheBaseMargin();
            if (body == null || viewport == null)
            {
                return;
            }

            var rowRt = body.rectTransform;
            var vpCorners = new Vector3[4];
            viewport.GetWorldCorners(vpCorners);
            var rowCorners = new Vector3[4];
            rowRt.GetWorldCorners(rowCorners);

            var vpMinX = vpCorners[0].x;
            var vpMinY = vpCorners[0].y;
            var vpMaxX = vpCorners[2].x;
            var vpMaxY = vpCorners[2].y;
            var rowMinX = rowCorners[0].x;
            var rowMinY = rowCorners[0].y;
            var rowMaxX = rowCorners[2].x;
            var rowMaxY = rowCorners[2].y;

            var intersects =
                rowMaxY > vpMinY && rowMinY < vpMaxY &&
                rowMaxX > vpMinX && rowMinX < vpMaxX;

            var renderers = GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = intersects;
            }

            if (!intersects)
            {
                return;
            }

            var hiddenBelow = Mathf.Max(0f, vpMinY - rowMinY);
            var hiddenAbove = Mathf.Max(0f, rowMaxY - vpMaxY);
            var hiddenLeft = Mathf.Max(0f, vpMinX - rowMinX);
            var hiddenRight = Mathf.Max(0f, rowMaxX - vpMaxX);

            var sx = Mathf.Max(0.0001f, rowRt.lossyScale.x);
            var sy = Mathf.Max(0.0001f, rowRt.lossyScale.y);

            body.overflowMode = TextOverflowModes.Masking;
            body.margin = new Vector4(
                _baseMargin.x + hiddenLeft / sx,
                _baseMargin.y + hiddenAbove / sy,
                _baseMargin.z + hiddenRight / sx,
                _baseMargin.w + hiddenBelow / sy);
            body.ForceMeshUpdate(true, true);
        }

        private void CacheBaseMargin()
        {
            EnsureBody();
            if (body != null && !_hasBaseMargin)
            {
                _baseMargin = body.margin;
                _hasBaseMargin = true;
            }
        }

        private void EnsureBody()
        {
            if (body == null)
            {
                body = GetComponent<TMP_Text>();
                if (body == null)
                {
                    body = GetComponentInChildren<TMP_Text>(true);
                }
            }

            if (body != null && !_hasDefaultColor)
            {
                _defaultColor = body.color;
                _hasDefaultColor = true;
            }
        }

        private void ApplyComposedText(string raw)
        {
            var catalog = CardFacePresentationBinder.PeekDescriptionIconCatalog();
            if (catalog == null || string.IsNullOrEmpty(raw))
            {
                ReleaseSpriteAsset();
                body.text = raw ?? string.Empty;
                return;
            }

            var composed = CardFaceDescriptionComposer.Compose(
                raw,
                assembledIcons: null,
                registry: CardFacePresentationBinder.GetDefaultRegistry(),
                catalog: catalog);

            ReleaseSpriteAsset();
            if (composed.Icons != null && composed.Icons.Count > 0)
            {
                _spriteAsset = CardFaceDescriptionSpriteAssetBuilder.Build(
                    composed.Icons,
                    CardFacePresentationBinder.GetInlineIconStyle());
                body.spriteAsset = _spriteAsset;
            }
            else
            {
                body.spriteAsset = null;
            }

            body.text = composed.TmpRichText;
        }

        private void ReleaseSpriteAsset()
        {
            if (_spriteAsset == null)
            {
                return;
            }

            CardFaceDescriptionSpriteAssetBuilder.DestroyBuilt(_spriteAsset);
            _spriteAsset = null;
            if (body != null)
            {
                body.spriteAsset = null;
            }
        }

        private void OnDestroy()
        {
            ReleaseSpriteAsset();
        }

        private void OnValidate()
        {
            EnsureBody();
        }

        /// <summary>为布局组准备：优先吃高、宽拉伸。</summary>
        public void EnsureLayoutElement()
        {
            var le = GetComponent<LayoutElement>();
            if (le == null)
            {
                le = gameObject.AddComponent<LayoutElement>();
            }

            le.minHeight = 40f;
            le.preferredHeight = -1f;
            le.flexibleWidth = 1f;
            le.flexibleHeight = 0f;
        }
    }
}
