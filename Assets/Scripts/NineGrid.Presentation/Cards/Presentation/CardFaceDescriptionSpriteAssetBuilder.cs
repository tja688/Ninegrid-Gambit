using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;

namespace NineGrid.Cards.Presentation
{
    /// <summary>
    /// 将装配得到的图标打成运行时 <see cref="TMP_SpriteAsset"/>，供基础描述 `<sprite name="…">` 消费。
    /// </summary>
    public static class CardFaceDescriptionSpriteAssetBuilder
    {
        private static Shader _spriteShader;

        public static TMP_SpriteAsset Build(IReadOnlyList<CardFaceDescriptionComposer.InlineIcon> icons)
        {
            var asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            asset.name = "CardFaceDescriptionSprites";
            asset.hideFlags = HideFlags.HideAndDontSave;

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
                    metrics = new GlyphMetrics(cell.Width, cell.Height, 0, cell.Height - 1, cell.Width),
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
        }
    }
}
