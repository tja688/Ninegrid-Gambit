using NUnit.Framework;
using NineGrid.Cards.Presentation;
using NineGrid.Cards.Slots;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// 描述内联图标：em 归一 metrics 与 style 解析。
    /// </summary>
    public sealed class CardFaceDescriptionSpriteAssetBuilderTests
    {
        [Test]
        public void ComputeEmMetrics_NormalizesHeightToBaseScale_KeepsAspect()
        {
            var metrics = CardFaceDescriptionSpriteAssetBuilder.ComputeEmMetrics(
                pixelWidth: 20,
                pixelHeight: 10,
                bearingX: 0.1f,
                bearingY: -0.05f,
                baseScale: 1.2f);

            Assert.AreEqual(1.2f, metrics.height, 0.0001f);
            Assert.AreEqual(2.4f, metrics.width, 0.0001f);
            Assert.AreEqual(0.1f, metrics.horizontalBearingX, 0.0001f);
            Assert.AreEqual(
                1.2f * CardFaceDescriptionSpriteAssetBuilder.DefaultBaselineBearingFactor - 0.05f,
                metrics.horizontalBearingY,
                0.0001f);
            Assert.AreEqual(2.4f, metrics.horizontalAdvance, 0.0001f);
        }

        [Test]
        public void Build_AppliesStyleToGlyphMetrics()
        {
            var style = ScriptableObject.CreateInstance<CardFaceDescriptionInlineIconStyleSO>();
            var sprite = CreateSprite(8, 8);
            TMP_SpriteAsset asset = null;
            try
            {
                style.SetEntry(CardFaceSlotCodes.ActionIcon, 0.2f, 0.1f, 0.75f);
                var icons = new[]
                {
                    new CardFaceDescriptionComposer.InlineIcon(CardFaceSlotCodes.ActionIcon, sprite)
                };

                asset = CardFaceDescriptionSpriteAssetBuilder.Build(icons, style);
                Assert.AreEqual(1, asset.spriteGlyphTable.Count);
                var metrics = asset.spriteGlyphTable[0].metrics;
                var expected = CardFaceDescriptionSpriteAssetBuilder.ComputeEmMetrics(
                    8, 8, 0.2f, 0.1f, 0.75f);
                Assert.AreEqual(expected.height, metrics.height, 0.0001f);
                Assert.AreEqual(expected.width, metrics.width, 0.0001f);
                Assert.AreEqual(expected.horizontalBearingX, metrics.horizontalBearingX, 0.0001f);
                Assert.AreEqual(expected.horizontalBearingY, metrics.horizontalBearingY, 0.0001f);
                Assert.AreEqual(
                    CardFaceDescriptionSpriteAssetBuilder.EmPointSize,
                    asset.faceInfo.pointSize,
                    0.0001f);
            }
            finally
            {
                CardFaceDescriptionSpriteAssetBuilder.DestroyBuilt(asset);
                Object.DestroyImmediate(style);
                DestroySprite(sprite);
            }
        }

        [Test]
        public void Style_Resolve_FallsBackToDefaults()
        {
            var style = ScriptableObject.CreateInstance<CardFaceDescriptionInlineIconStyleSO>();
            try
            {
                style.Resolve("Unknown_Code", out var bx, out var by, out var scale);
                Assert.AreEqual(0f, bx);
                Assert.AreEqual(0f, by);
                Assert.AreEqual(1f, scale);
            }
            finally
            {
                Object.DestroyImmediate(style);
            }
        }

        private static Sprite CreateSprite(int w, int h)
        {
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color[w * h];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.white;
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        private static void DestroySprite(Sprite sprite)
        {
            if (sprite == null)
            {
                return;
            }

            var texture = sprite.texture;
            Object.DestroyImmediate(sprite);
            if (texture != null)
            {
                Object.DestroyImmediate(texture);
            }
        }
    }
}
