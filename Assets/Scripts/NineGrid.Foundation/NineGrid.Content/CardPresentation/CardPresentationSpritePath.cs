using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NineGrid.Content.CardPresentation
{
    /// <summary>
    /// 按 Unity 资产路径加载 Sprite。
    /// Multiple 图集子切片使用 <c>Assets/.../sheet.png#spriteName</c>；
    /// 无 <c>#</c> 时保持旧行为（Single 主资产，或 Multiple 回退首切片）。
    /// Editor / Play Mode in Editor 走 AssetDatabase；
    /// 路径含 /Resources/ 时走 Resources.Load（Player 包体同源；ADR-0008，不用 Addressables）。
    /// 卡牌内容美术约定根见 <see cref="CardPresentationContentArt"/>。
    /// </summary>
    public static class CardPresentationSpritePath
    {
        public const char SubSpriteSeparator = '#';

        /// <summary>
        /// 拆分 <c>path</c> 或 <c>path#spriteName</c>。spriteName 无则 null。
        /// </summary>
        public static void SplitPath(string assetPath, out string path, out string spriteName)
        {
            path = null;
            spriteName = null;
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return;
            }

            var normalized = assetPath.Replace('\\', '/').Trim();
            var separatorIndex = normalized.IndexOf(SubSpriteSeparator);
            if (separatorIndex > 0 && separatorIndex < normalized.Length - 1)
            {
                path = normalized.Substring(0, separatorIndex);
                spriteName = normalized.Substring(separatorIndex + 1);
                return;
            }

            path = normalized;
        }

        /// <summary>
        /// 组装 Multiple 子切片键；spriteName 空则返回纯 path。
        /// </summary>
        public static string ComposePath(string assetPath, string spriteName)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return string.Empty;
            }

            var path = assetPath.Replace('\\', '/').Trim();
            if (string.IsNullOrWhiteSpace(spriteName))
            {
                return path;
            }

            return path + SubSpriteSeparator + spriteName.Trim();
        }

        public static Sprite LoadSprite(string assetPath)
        {
            SplitPath(assetPath, out var path, out var spriteName);
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(spriteName))
            {
                var named = FindSubSpriteAtPath(path, spriteName);
                if (named != null)
                {
                    return named;
                }

                return null;
            }

            // Single 模式：主资产就是 Sprite。
            var editorSprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (editorSprite != null)
            {
                return editorSprite;
            }

            // Multiple / 图集：仅有纹理路径时无切片名，回退首切片（兼容旧 JSON）。
            var subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            if (subAssets != null && subAssets.Length > 0)
            {
                Sprite first = null;
                var texName = System.IO.Path.GetFileNameWithoutExtension(path);
                for (var i = 0; i < subAssets.Length; i++)
                {
                    if (subAssets[i] is Sprite sprite)
                    {
                        if (first == null)
                        {
                            first = sprite;
                        }

                        // 优先同名切片（去掉扩展名）。
                        if (string.Equals(sprite.name, texName, StringComparison.Ordinal))
                        {
                            return sprite;
                        }
                    }
                }

                if (first != null)
                {
                    return first;
                }
            }
#endif

            if (TryLoadFromResources(path, spriteName, out var resourcesSprite))
            {
                return resourcesSprite;
            }

            return null;
        }

#if UNITY_EDITOR
        private static Sprite FindSubSpriteAtPath(string assetPath, string spriteName)
        {
            var subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            if (subAssets == null)
            {
                return null;
            }

            for (var i = 0; i < subAssets.Length; i++)
            {
                if (subAssets[i] is Sprite sprite
                    && string.Equals(sprite.name, spriteName, StringComparison.Ordinal))
                {
                    return sprite;
                }
            }

            return null;
        }

        /// <summary>
        /// Multiple 图集写入 <c>path#spriteName</c>；Single / 非 Sprite 仅写 path。
        /// </summary>
        public static string EncodeAssetReference(UnityEngine.Object asset)
        {
            if (asset == null)
            {
                return string.Empty;
            }

            var path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            path = path.Replace('\\', '/');
            if (!(asset is Sprite sprite))
            {
                return path;
            }

            if (CountSpritesAtPath(path) > 1)
            {
                return ComposePath(path, sprite.name);
            }

            return path;
        }

        private static int CountSpritesAtPath(string assetPath)
        {
            var subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            if (subAssets == null)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < subAssets.Length; i++)
            {
                if (subAssets[i] is Sprite)
                {
                    count++;
                }
            }

            return count;
        }
#endif

        private static bool TryLoadFromResources(string assetPath, string spriteName, out Sprite sprite)
        {
            sprite = null;
            const string marker = "/Resources/";
            var index = assetPath.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return false;
            }

            var relative = assetPath.Substring(index + marker.Length);
            if (relative.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                || relative.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                || relative.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                || relative.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            {
                relative = relative.Substring(0, relative.LastIndexOf('.'));
            }

            if (!string.IsNullOrEmpty(spriteName))
            {
                var all = Resources.LoadAll<Sprite>(relative);
                if (all == null)
                {
                    return false;
                }

                for (var i = 0; i < all.Length; i++)
                {
                    if (all[i] != null
                        && string.Equals(all[i].name, spriteName, StringComparison.Ordinal))
                    {
                        sprite = all[i];
                        return true;
                    }
                }

                return false;
            }

            sprite = Resources.Load<Sprite>(relative);
            return sprite != null;
        }
    }
}
