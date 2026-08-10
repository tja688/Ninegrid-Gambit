using System;
using System.Collections.Generic;
using NineGrid.Content.CardPresentation;
using UnityEngine;

namespace NineGrid.Content.Vfx
{
    /// <summary>从 visual_effects 索引解析 sheetPath，经 Resources 加载并排序帧；运行时不合并索引 defaultFps/defaultScale。</summary>
    public static class VfxMaterialFrameSource
    {
        private static readonly Sprite[] Empty = Array.Empty<Sprite>();

        public static bool TryLoadFrames(string materialKey, out Sprite[] frames, out string failureReason)
        {
            frames = Empty;
            failureReason = string.Empty;
            if (string.IsNullOrWhiteSpace(materialKey))
            {
                failureReason = "materialKey 为空。";
                return false;
            }

            var key = materialKey.Trim();
            string sheetPath = null;
            if (VisualEffectCatalog.TryGet(key, out var entry) && entry != null
                && !string.IsNullOrWhiteSpace(entry.sheetPath))
            {
                sheetPath = entry.sheetPath;
            }

            if (!string.IsNullOrWhiteSpace(sheetPath))
            {
                frames = LoadSpritesFromAssetPath(sheetPath);
                if (frames.Length > 0)
                {
                    return true;
                }
            }

            frames = LoadSpritesFromAssetPath(key);
            if (frames.Length > 0)
            {
                return true;
            }

            failureReason = string.IsNullOrWhiteSpace(sheetPath)
                ? "无法解析素材路径：" + key
                : "无法加载精灵表帧：" + sheetPath;
            return false;
        }

        public static Sprite[] LoadSpritesFromAssetPath(string assetPath)
        {
            if (!CardPresentationContentArt.TryGetResourcesRelativeKey(assetPath, out var resourcesKey))
            {
                return Empty;
            }

            var loaded = Resources.LoadAll<Sprite>(resourcesKey);
            if (loaded == null || loaded.Length == 0)
            {
                return Empty;
            }

            var sprites = new List<Sprite>(loaded.Length);
            for (var i = 0; i < loaded.Length; i++)
            {
                if (loaded[i] != null)
                {
                    sprites.Add(loaded[i]);
                }
            }

            if (sprites.Count == 0)
            {
                return Empty;
            }

            sprites.Sort((a, b) => VfxSpriteSheetFrameOrder.CompareNames(
                a != null ? a.name : string.Empty,
                b != null ? b.name : string.Empty));
            return sprites.ToArray();
        }
    }
}
