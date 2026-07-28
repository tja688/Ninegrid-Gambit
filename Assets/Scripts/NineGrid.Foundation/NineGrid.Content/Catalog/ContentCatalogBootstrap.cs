using System;
using System.IO;
using NineGrid.Core.Content;
using UnityEngine;

namespace NineGrid.Content
{
    public enum ContentCatalogSourceKind
    {
        Hardcoded,
        Luban,
        Auto
    }

    public static class ContentCatalogBootstrap
    {
        public static string ResolveLubanDataDirectory()
        {
            var candidates = new[]
            {
                Path.Combine(Application.streamingAssetsPath, "TableNine", "LubanData"),
                Path.Combine(Environment.CurrentDirectory, TableNineLubanCatalogFactory.DefaultDataRelativePath),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TableNine", "LubanData"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Assets", "StreamingAssets", "TableNine", "LubanData")
            };

            for (var i = 0; i < candidates.Length; i++)
            {
                var fullPath = Path.GetFullPath(candidates[i]);
                if (Directory.Exists(fullPath) && File.Exists(Path.Combine(fullPath, "tablenine_tbeffect.json")))
                {
                    return fullPath;
                }
            }

            return string.Empty;
        }

        public static bool TryLoadLubanCatalog(string dataDirectory, out GameContentCatalog catalog)
        {
            catalog = null;
            if (string.IsNullOrEmpty(dataDirectory) || !Directory.Exists(dataDirectory))
            {
                return false;
            }

            try
            {
                catalog = TableNineLubanCatalogFactory.CreateFromDirectory(dataDirectory);
                return catalog != null;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        public static GameContentCatalog Load(ContentCatalogSourceKind source = ContentCatalogSourceKind.Auto, string lubanDataDirectory = null)
        {
            if (source == ContentCatalogSourceKind.Hardcoded)
            {
                return FinalizeCatalog(TableNineContentCatalog.CreateDefault());
            }

            if (source == ContentCatalogSourceKind.Luban)
            {
                GameContentCatalog lubanCatalog;
                if (TryLoadLubanCatalog(lubanDataDirectory ?? ResolveLubanDataDirectory(), out lubanCatalog))
                {
                    return FinalizeCatalog(lubanCatalog);
                }

                throw new InvalidOperationException("Luban content data is unavailable.");
            }

            GameContentCatalog autoCatalog;
            if (TryLoadLubanCatalog(lubanDataDirectory ?? ResolveLubanDataDirectory(), out autoCatalog))
            {
                return FinalizeCatalog(autoCatalog);
            }

            return FinalizeCatalog(TableNineContentCatalog.CreateDefault());
        }

        /// <summary>
        /// Luban/Hardcoded 底数 → schema≥2 JSON 投影覆盖（帮助卡/怪物/技能/遗物/牌组/房间）
        /// → 未投影卡种 Presentation Overlay。业务仍只消费返回的 <see cref="GameContentCatalog"/>。
        /// </summary>
        private static GameContentCatalog FinalizeCatalog(GameContentCatalog catalog)
        {
            ContentJsonCatalogProjector.ApplyToCatalog(catalog);
            CardPresentationBusinessOverlay.ApplyToCatalog(catalog);
            return catalog;
        }
    }
}
