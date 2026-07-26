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

        [Test]
        public void SanitizePackedPixels_UnpremultipliesBlackFringe_AndClearsZeroAlphaToWhite()
        {
            var pixels = new[]
            {
                new Color(0f, 0f, 0f, 0f),
                new Color(0.4f, 0.3f, 0.2f, 0.5f), // 相对黑底预乘
                new Color(0.8f, 0.6f, 0.4f, 1f),
            };

            CardFaceDescriptionSpriteAssetBuilder.SanitizePackedPixels(pixels);

            Assert.AreEqual(1f, pixels[0].r, 0.0001f);
            Assert.AreEqual(1f, pixels[0].g, 0.0001f);
            Assert.AreEqual(1f, pixels[0].b, 0.0001f);
            Assert.AreEqual(0f, pixels[0].a, 0.0001f);

            Assert.AreEqual(0.8f, pixels[1].r, 0.0001f);
            Assert.AreEqual(0.6f, pixels[1].g, 0.0001f);
            Assert.AreEqual(0.4f, pixels[1].b, 0.0001f);
            Assert.AreEqual(0.5f, pixels[1].a, 0.0001f);

            Assert.AreEqual(0.8f, pixels[2].r, 0.0001f);
            Assert.AreEqual(1f, pixels[2].a, 0.0001f);
        }

#if UNITY_EDITOR
        [Test]
        public void Build_NonReadableAtlasSprite_PacksRealPixelsNotSolidWhite()
        {
            const string path = "Assets/Arts/Images/Multiple/CardTemplateSprites.png";
            var sprites = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
            Sprite icon = null;
            for (var i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] is Sprite s && s.name == "Desc_Field_Icon_4")
                {
                    icon = s;
                    break;
                }
            }

            Assert.IsNotNull(icon, "应能加载 Desc_Field_Icon_4");
            Assert.IsFalse(icon.texture.isReadable, "回归前提：图集默认不可读");

            TMP_SpriteAsset asset = null;
            try
            {
                asset = CardFaceDescriptionSpriteAssetBuilder.Build(
                    new[] { new CardFaceDescriptionComposer.InlineIcon("action", icon) });
                Assert.IsNotNull(asset.spriteSheet);
                var atlas = asset.spriteSheet as Texture2D;
                Assert.IsNotNull(atlas);
                var pixels = atlas.GetPixels();
                var nonWhite = 0;
                var lowAlphaBlack = 0;
                for (var i = 0; i < pixels.Length; i++)
                {
                    var c = pixels[i];
                    if (c.a > 0.01f && (c.r < 0.99f || c.g < 0.99f || c.b < 0.99f))
                    {
                        nonWhite++;
                    }

                    if (c.a <= 0.001f && c.r + c.g + c.b < 0.1f)
                    {
                        lowAlphaBlack++;
                    }
                }

                Assert.Greater(
                    nonWhite,
                    0,
                    "不可读图集应经 Blit 打进真实像素，不能再退成纯白块");
                Assert.AreEqual(
                    0,
                    lowAlphaBlack,
                    "透明像素不得残留黑 RGB（否则 TMP 缩放时冒黑点）");
            }
            finally
            {
                CardFaceDescriptionSpriteAssetBuilder.DestroyBuilt(asset);
            }
        }
#endif

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
