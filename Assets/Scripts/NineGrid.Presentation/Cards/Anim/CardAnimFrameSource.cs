using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using NineGrid.Content.CardPresentation;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NineGrid.Cards.Anim
{
    /// <summary>
    /// 从 folder / atlas 源加载帧序列。路径约定见 <see cref="CardPresentationContentArt"/>。
    /// Editor 优先 AssetDatabase；Player（及 Resources 回退）走 <c>Resources.LoadAll</c> + 按名排序保帧序。
    /// </summary>
    public static class CardAnimFrameSource
    {
        private static readonly Regex FrameIndexRegex = new Regex(
            @"_(\d+)\.(png|PNG)$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Sprite[] Empty = Array.Empty<Sprite>();

        public static Sprite[] LoadFrames(string sourceType, string path)
        {
            var type = CardPresentationAnimResolve.NormalizeSourceType(sourceType);
            if (type == "none" || string.IsNullOrWhiteSpace(path))
            {
                return Empty;
            }

            var normalized = path.Replace('\\', '/').Trim();
            if (type == "folder")
            {
                return LoadFolderFrames(normalized);
            }

            if (type == "atlas")
            {
                return LoadAtlasFrames(normalized);
            }

            return Empty;
        }

        private static Sprite[] LoadFolderFrames(string folderAssetPath)
        {
#if UNITY_EDITOR
            var editorFrames = LoadFolderFramesEditor(folderAssetPath);
            if (editorFrames.Length > 0)
            {
                return editorFrames;
            }
#endif
            return LoadFolderFramesFromResources(folderAssetPath);
        }

        private static Sprite[] LoadAtlasFrames(string atlasAssetPath)
        {
#if UNITY_EDITOR
            var editorFrames = LoadAtlasFramesEditor(atlasAssetPath);
            if (editorFrames.Length > 0)
            {
                return editorFrames;
            }
#endif
            return LoadAtlasFramesFromResources(atlasAssetPath);
        }

        /// <summary>
        /// Player / Resources 回退：对 Resources 相对键 <c>LoadAll&lt;Sprite&gt;</c> 后按帧名排序。
        /// </summary>
        public static Sprite[] LoadFolderFramesFromResources(string folderAssetPath)
        {
            if (!CardPresentationContentArt.TryGetResourcesRelativeKey(folderAssetPath, out var key))
            {
                return Empty;
            }

            var loaded = Resources.LoadAll<Sprite>(key);
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

            sprites.Sort((a, b) => CompareFramePaths(
                a != null ? a.name : string.Empty,
                b != null ? b.name : string.Empty));
            return sprites.Count > 0 ? sprites.ToArray() : Empty;
        }

        /// <summary>
        /// Player / Resources 回退：图集纹理的 Resources 键上 LoadAll 全部 Sprite，按名排序。
        /// </summary>
        public static Sprite[] LoadAtlasFramesFromResources(string atlasAssetPath)
        {
            if (!CardPresentationContentArt.TryGetResourcesRelativeKey(atlasAssetPath, out var key))
            {
                return Empty;
            }

            var loaded = Resources.LoadAll<Sprite>(key);
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

            sprites.Sort((a, b) => CompareFramePaths(
                a != null ? a.name : string.Empty,
                b != null ? b.name : string.Empty));
            return sprites.Count > 0 ? sprites.ToArray() : Empty;
        }

#if UNITY_EDITOR
        private static Sprite[] LoadFolderFramesEditor(string folderAssetPath)
        {
            if (string.IsNullOrEmpty(folderAssetPath))
            {
                return Empty;
            }

            if (!AssetDatabase.IsValidFolder(folderAssetPath))
            {
                return Empty;
            }

            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderAssetPath });
            var paths = new List<string>(guids.Length);
            for (var i = 0; i < guids.Length; i++)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(assetPath))
                {
                    continue;
                }

                // 仅本文件夹一层；子目录留给 atlas 或显式 path。
                var parent = assetPath.Substring(0, assetPath.LastIndexOf('/'));
                if (!string.Equals(parent, folderAssetPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                paths.Add(assetPath);
            }

            paths.Sort(CompareFramePaths);
            var sprites = new List<Sprite>(paths.Count);
            for (var i = 0; i < paths.Count; i++)
            {
                var sprite = CardPresentationSpritePath.LoadSprite(paths[i]);
                if (sprite != null)
                {
                    sprites.Add(sprite);
                }
            }

            return sprites.Count > 0 ? sprites.ToArray() : Empty;
        }

        private static Sprite[] LoadAtlasFramesEditor(string atlasAssetPath)
        {
            if (string.IsNullOrEmpty(atlasAssetPath))
            {
                return Empty;
            }

            var assets = AssetDatabase.LoadAllAssetsAtPath(atlasAssetPath);
            if (assets == null || assets.Length == 0)
            {
                return Empty;
            }

            var sprites = new List<Sprite>(assets.Length);
            for (var i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite sprite)
                {
                    sprites.Add(sprite);
                }
            }

            sprites.Sort((a, b) => CompareFramePaths(a != null ? a.name : string.Empty, b != null ? b.name : string.Empty));
            return sprites.Count > 0 ? sprites.ToArray() : Empty;
        }
#endif

        public static int CompareFramePaths(string a, string b)
        {
            var ia = ExtractFrameIndex(a);
            var ib = ExtractFrameIndex(b);
            var cmp = ia.CompareTo(ib);
            if (cmp != 0)
            {
                return cmp;
            }

            return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        }

        public static int ExtractFrameIndex(string pathOrName)
        {
            var match = FrameIndexRegex.Match(pathOrName ?? string.Empty);
            if (!match.Success)
            {
                // atlas 子图名常无扩展名：再试 _N 后缀。
                var name = pathOrName ?? string.Empty;
                var underscore = name.LastIndexOf('_');
                if (underscore >= 0 && underscore < name.Length - 1)
                {
                    var tail = name.Substring(underscore + 1);
                    if (int.TryParse(tail, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                    {
                        return v;
                    }
                }

                return int.MaxValue;
            }

            return int.TryParse(
                match.Groups[1].Value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var index)
                ? index
                : int.MaxValue;
        }
    }
}
