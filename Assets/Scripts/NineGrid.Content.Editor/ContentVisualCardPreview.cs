using NineGrid.Content;
using UnityEditor;
using UnityEngine;

namespace NineGrid.Content.Editor
{
    /// <summary>
    /// 编辑器内标准卡预览：IMGUI 叠图，避免 PreviewRenderUtility 在 URP 下无法渲染 SpriteRenderer。
    /// </summary>
    public sealed class ContentVisualCardPreview : System.IDisposable
    {
        private static readonly Color PanelBg = new Color(0.09f, 0.075f, 0.06f, 1f);
        private const float CardAspect = 1.625f / 2.0625f;

        public void Draw(Rect rect, ContentVisualResolvedView view)
        {
            if (rect.width <= 1f || rect.height <= 1f)
            {
                return;
            }

            EditorGUI.DrawRect(rect, PanelBg);

            if (view == null)
            {
                EditorGUI.LabelField(rect, "选择一条内容以预览", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            Sprite iconSprite;
            Sprite faceSprite;
            TryLoadPreviewSprite(view.IconVisualId, view.IconAssetKey, out iconSprite);
            TryLoadPreviewSprite(view.FaceVisualId, view.FaceAssetKey, out faceSprite);

            var cardRect = FitAspectRect(rect, CardAspect, 0.9f);
            var frameColor = new Color(view.FrameColor.R, view.FrameColor.G, view.FrameColor.B, view.FrameColor.A);
            EditorGUI.DrawRect(cardRect, frameColor);

            var faceRect = Inset(cardRect, cardRect.width * 0.045f, cardRect.height * 0.045f);
            if (faceSprite != null)
            {
                DrawSprite(faceRect, faceSprite);
            }

            if (iconSprite != null)
            {
                var iconSize = cardRect.width * 0.44f;
                var iconRect = new Rect(
                    cardRect.x + (cardRect.width - iconSize) * 0.5f,
                    cardRect.y + cardRect.height * 0.22f,
                    iconSize,
                    iconSize);
                DrawSprite(iconRect, iconSprite);
            }

            if (faceSprite == null && iconSprite == null)
            {
                EditorGUI.LabelField(cardRect, "未能加载预览图", EditorStyles.centeredGreyMiniLabel);
            }
        }

        public void Dispose()
        {
        }

        private static bool TryLoadPreviewSprite(string visualId, string conventionAssetKey, out Sprite sprite)
        {
            sprite = null;
            if (ContentVisualSpriteLoader.TryLoad(visualId, conventionAssetKey, out sprite) && sprite != null)
            {
                return true;
            }

            if (!string.IsNullOrEmpty(visualId)
                && ContentVisualSpriteKeyCodec.TryDecode(visualId, out sprite)
                && sprite != null)
            {
                return true;
            }

            if (!string.IsNullOrEmpty(conventionAssetKey)
                && VisualIdNaming.IsLegacyPathKey(conventionAssetKey)
                && ContentVisualSpriteKeyCodec.TryDecodeLegacy(conventionAssetKey, out sprite)
                && sprite != null)
            {
                return true;
            }

            return false;
        }

        private static Rect FitAspectRect(Rect outer, float widthOverHeight, float fill)
        {
            var maxWidth = outer.width * fill;
            var maxHeight = outer.height * fill;
            float width;
            float height;
            if (maxWidth / maxHeight > widthOverHeight)
            {
                height = maxHeight;
                width = height * widthOverHeight;
            }
            else
            {
                width = maxWidth;
                height = width / widthOverHeight;
            }

            return new Rect(
                outer.x + (outer.width - width) * 0.5f,
                outer.y + (outer.height - height) * 0.5f,
                width,
                height);
        }

        private static Rect Inset(Rect rect, float horizontal, float vertical)
        {
            return new Rect(
                rect.x + horizontal,
                rect.y + vertical,
                Mathf.Max(0f, rect.width - horizontal * 2f),
                Mathf.Max(0f, rect.height - vertical * 2f));
        }

        private static void DrawSprite(Rect rect, Sprite sprite)
        {
            if (sprite == null || sprite.texture == null)
            {
                return;
            }

            var texture = sprite.texture;
            var textureRect = sprite.textureRect;
            var uv = new Rect(
                textureRect.x / texture.width,
                textureRect.y / texture.height,
                textureRect.width / texture.width,
                textureRect.height / texture.height);
            GUI.DrawTextureWithTexCoords(rect, texture, uv, true);
        }
    }
}
