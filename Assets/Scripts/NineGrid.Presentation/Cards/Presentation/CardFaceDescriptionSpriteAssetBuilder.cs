using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;

namespace NineGrid.Cards.Presentation
{
    /// <summary>
    /// 将装配得到的图标打成运行时 <see cref="TMP_SpriteAsset"/>，供基础描述 `<sprite name="…">` 消费。
    /// 像素框归一到 1em × aspect，再乘 style.baseScale；bearing 为 em 相对偏移，随字号/描述节点缩放自动跟从。
    /// </summary>
    public static class CardFaceDescriptionSpriteAssetBuilder
    {
        /// <summary>SpriteAsset faceInfo.pointSize；glyph 高度以 em 计，渲染时 × (fontSize / EmPointSize)。</summary>
        public const float EmPointSize = 1f;

        /// <summary>未加 bearingY 偏移时，图标顶相对基线的默认高度比例（≈西文大写高度）。</summary>
        public const float DefaultBaselineBearingFactor = 0.85f;

        private static Shader _spriteShader;

        public static TMP_SpriteAsset Build(IReadOnlyList<CardFaceDescriptionComposer.InlineIcon> icons)
        {
            return Build(icons, style: null);
        }

        public static TMP_SpriteAsset Build(
            IReadOnlyList<CardFaceDescriptionComposer.InlineIcon> icons,
            CardFaceDescriptionInlineIconStyleSO style)
        {
            var asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            asset.name = "CardFaceDescriptionSprites";
            asset.hideFlags = HideFlags.HideAndDontSave;
            ApplyFaceInfo(asset);

            if (icons == null || icons.Count == 0)
            {
                asset.UpdateLookupTables();
                return asset;
            }

            var cells = new List<PackedCell>(icons.Count);
            var maxH = 1;
            var totalW = 0;
            for (var i = 0; i < icons.Count; i++)
            {
                var icon = icons[i];
                if (icon.Sprite == null || string.IsNullOrEmpty(icon.SlotCode))
                {
                    continue;
                }

                var cell = PackSprite(icon.Sprite);
                if (cell.Pixels == null)
                {
                    continue;
                }

                cell.SlotCode = icon.SlotCode;
                cell.Source = icon.Sprite;
                cell.X = totalW;
                ResolveLayout(style, icon.SlotCode, cell.Width, cell.Height, out cell.Metrics);
                cells.Add(cell);
                totalW += cell.Width;
                if (cell.Height > maxH)
                {
                    maxH = cell.Height;
                }
            }

            if (cells.Count == 0 || totalW <= 0)
            {
                asset.UpdateLookupTables();
                return asset;
            }

            var atlas = new Texture2D(totalW, maxH, TextureFormat.RGBA32, false);
            atlas.name = "CardFaceDescriptionAtlas";
            atlas.hideFlags = HideFlags.HideAndDontSave;
            atlas.filterMode = FilterMode.Point;
            atlas.wrapMode = TextureWrapMode.Clamp;
            ClearAtlas(atlas);

            asset.spriteGlyphTable.Clear();
            asset.spriteCharacterTable.Clear();

            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                atlas.SetPixels(cell.X, 0, cell.Width, cell.Height, cell.Pixels);

                var glyph = new TMP_SpriteGlyph
                {
                    index = (uint)i,
                    sprite = cell.Source,
                    metrics = cell.Metrics,
                    glyphRect = new GlyphRect(cell.X, 0, cell.Width, cell.Height),
                    scale = 1f,
                    atlasIndex = 0
                };

                var character = new TMP_SpriteCharacter((uint)(0xE000 + i), glyph)
                {
                    name = cell.SlotCode,
                    scale = 1f
                };

                asset.spriteGlyphTable.Add(glyph);
                asset.spriteCharacterTable.Add(character);
            }

            atlas.Apply(false, false);
            asset.spriteSheet = atlas;
            // 必须先 UpdateLookupTables，再挂 material。
            // 若 material 已有且 version 为空，TMP 会走 UpgradeSpriteAsset 并 NRE（spriteInfoList 为 null）。
            asset.UpdateLookupTables();
            EnsureMaterial(asset, atlas);
            return asset;
        }

        /// <summary>
        /// 将像素框归一到 em：高度 = baseScale，宽度按宽高比；bearing 为 em 相对偏移。
        /// </summary>
        public static GlyphMetrics ComputeEmMetrics(
            int pixelWidth,
            int pixelHeight,
            float bearingX,
            float bearingY,
            float baseScale)
        {
            var scale = baseScale > 0.0001f ? baseScale : 1f;
            var pw = Mathf.Max(1, pixelWidth);
            var ph = Mathf.Max(1, pixelHeight);
            var height = scale;
            var width = height * (pw / (float)ph);
            var bx = bearingX;
            var by = height * DefaultBaselineBearingFactor + bearingY;
            return new GlyphMetrics(width, height, bx, by, width);
        }

        public static void DestroyBuilt(TMP_SpriteAsset asset)
        {
            if (asset == null)
            {
                return;
            }

            DestroyOwned(asset.material);
            DestroyOwned(asset.spriteSheet);
            DestroyOwned(asset);
        }

        private static void ResolveLayout(
            CardFaceDescriptionInlineIconStyleSO style,
            string slotCode,
            int pixelWidth,
            int pixelHeight,
            out GlyphMetrics metrics)
        {
            float bearingX;
            float bearingY;
            float baseScale;
            if (style != null)
            {
                style.Resolve(slotCode, out bearingX, out bearingY, out baseScale);
            }
            else
            {
                bearingX = 0f;
                bearingY = 0f;
                baseScale = 1f;
            }

            metrics = ComputeEmMetrics(pixelWidth, pixelHeight, bearingX, bearingY, baseScale);
        }

        private static void ApplyFaceInfo(TMP_SpriteAsset asset)
        {
            var face = new FaceInfo();
            face.familyName = "CardFaceDescription";
            face.styleName = "Regular";
            face.pointSize = EmPointSize;
            face.scale = 1f;
            face.lineHeight = 1.25f;
            face.ascentLine = 1f;
            face.capLine = DefaultBaselineBearingFactor;
            face.meanLine = 0.5f;
            face.baseline = 0f;
            face.descentLine = -0.25f;
            face.superscriptOffset = 0.5f;
            face.subscriptOffset = -0.2f;
            face.underlineOffset = -0.1f;
            face.underlineThickness = 0.05f;
            face.strikethroughOffset = 0.3f;
            face.strikethroughThickness = 0.05f;
            face.tabWidth = 1f;
            asset.faceInfo = face;
        }

        private static void DestroyOwned(Object obj)
        {
            if (obj == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(obj);
            }
            else
            {
                Object.DestroyImmediate(obj);
            }
        }

        private static void EnsureMaterial(TMP_SpriteAsset asset, Texture atlas)
        {
            if (_spriteShader == null)
            {
                _spriteShader = Shader.Find("TextMeshPro/Sprite");
            }

            if (_spriteShader == null)
            {
                return;
            }

            var material = new Material(_spriteShader)
            {
                name = "CardFaceDescriptionSpriteMaterial",
                hideFlags = HideFlags.HideAndDontSave,
                mainTexture = atlas
            };
            asset.material = material;
        }

        private static PackedCell PackSprite(Sprite sprite)
        {
            var cell = new PackedCell();
            if (sprite == null || sprite.texture == null)
            {
                return cell;
            }

            var rect = sprite.textureRect;
            var width = Mathf.Max(1, Mathf.RoundToInt(rect.width));
            var height = Mathf.Max(1, Mathf.RoundToInt(rect.height));
            var x = Mathf.RoundToInt(rect.x);
            var y = Mathf.RoundToInt(rect.y);

            try
            {
                cell.Pixels = sprite.texture.GetPixels(x, y, width, height);
            }
            catch
            {
                // 不可读贴图时退化为纯色块，仍保留代号→槽位契约（非裸路径）。
                cell.Pixels = new Color[width * height];
                for (var i = 0; i < cell.Pixels.Length; i++)
                {
                    cell.Pixels[i] = Color.white;
                }
            }

            cell.Width = width;
            cell.Height = height;
            return cell;
        }

        private static void ClearAtlas(Texture2D atlas)
        {
            var clear = new Color[atlas.width * atlas.height];
            atlas.SetPixels(clear);
        }

        private struct PackedCell
        {
            public string SlotCode;
            public Sprite Source;
            public Color[] Pixels;
            public int Width;
            public int Height;
            public int X;
            public GlyphMetrics Metrics;
        }
    }
}
