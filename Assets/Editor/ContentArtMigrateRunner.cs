#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using NineGrid.Content.CardPresentation;
using NineGrid.Content.Editor;
using UnityEditor;
using UnityEngine;

/// <summary>
/// #66：把内容 JSON 引用的 Arts/Images 美术迁入 Assets/Resources/ContentArt（保 GUID），并重写 JSON 路径。
/// </summary>
public static class ContentArtMigrateRunner
{
    [MenuItem("NineGrid/Tools/Migrate Content Art To Resources")]
    public static void Run()
    {
        var report = Migrate();
        Debug.Log("[ContentArtMigrate] " + report);
    }

    public static string Migrate()
    {
        EnsureFolder(CardPresentationContentArt.RootAssetFolder);

        var sources = CollectReferencedLegacyPaths();
        var moved = 0;
        var skipped = 0;
        var failed = 0;
        var errors = new List<string>();

        foreach (var source in sources)
        {
            var dest = CardPresentationContentArt.RewriteLegacyArtsImagesPath(source);
            CardPresentationSpritePath.SplitPath(dest, out var destPath, out _);
            CardPresentationSpritePath.SplitPath(source, out var sourcePath, out _);

            if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(destPath))
            {
                skipped++;
                continue;
            }

            if (string.Equals(sourcePath, destPath, StringComparison.OrdinalIgnoreCase))
            {
                skipped++;
                continue;
            }

            if (!AssetExists(sourcePath))
            {
                failed++;
                errors.Add("missing source: " + sourcePath);
                continue;
            }

            if (AssetExists(destPath))
            {
                skipped++;
                continue;
            }

            EnsureParentFolders(destPath);
            var moveError = AssetDatabase.MoveAsset(sourcePath, destPath);
            if (!string.IsNullOrEmpty(moveError))
            {
                failed++;
                errors.Add(sourcePath + " → " + destPath + ": " + moveError);
                continue;
            }

            moved++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // SaveAuthoring 同步写 Arts + Streaming；只扫 Authoring 即可。
        var rewritten = RewriteFolder(CardPresentationJsonIO.AuthoringFolder);
        return "moved=" + moved
               + " skipped=" + skipped
               + " failed=" + failed
               + " jsonRewritten=" + rewritten
               + (errors.Count == 0
                   ? string.Empty
                   : " errors=[" + string.Join("; ", errors.GetRange(0, Math.Min(errors.Count, 8))) + "]");
    }

    private static HashSet<string> CollectReferencedLegacyPaths()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectFromFolder(CardPresentationJsonIO.AuthoringFolder, set);
        CollectFromFolder(CardPresentationJsonIO.StreamingFolder, set);
        return set;
    }

    private static void CollectFromFolder(string folder, HashSet<string> set)
    {
        var absolute = ToAbsolute(folder);
        if (!Directory.Exists(absolute))
        {
            return;
        }

        var files = Directory.GetFiles(absolute, "*.json", SearchOption.TopDirectoryOnly);
        var entries = new List<CardPresentationAssetPathEntry>(32);
        for (var i = 0; i < files.Length; i++)
        {
            var name = Path.GetFileName(files[i]);
            if (string.Equals(name, CardPresentationJsonIO.IndexFileName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!CardPresentationJsonIO.TryLoad(files[i], out var dto, out _) || dto == null)
            {
                continue;
            }

            entries.Clear();
            CardPresentationContentArt.CollectAssetPathEntries(dto, entries);
            for (var e = 0; e < entries.Count; e++)
            {
                CardPresentationSpritePath.SplitPath(entries[e].Path, out var path, out _);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                if (path.StartsWith(
                        CardPresentationContentArt.LegacyArtsImagesPrefix,
                        StringComparison.OrdinalIgnoreCase))
                {
                    set.Add(path.Replace('\\', '/'));
                }
            }
        }
    }

    private static int RewriteFolder(string folder)
    {
        var absolute = ToAbsolute(folder);
        if (!Directory.Exists(absolute))
        {
            return 0;
        }

        var count = 0;
        var files = Directory.GetFiles(absolute, "*.json", SearchOption.TopDirectoryOnly);
        for (var i = 0; i < files.Length; i++)
        {
            var name = Path.GetFileName(files[i]);
            if (string.Equals(name, CardPresentationJsonIO.IndexFileName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!CardPresentationJsonIO.TryLoad(files[i], out var dto, out _) || dto == null)
            {
                continue;
            }

            if (!RewriteDtoPaths(dto))
            {
                continue;
            }

            CardPresentationJsonIO.SaveAuthoring(dto);
            count++;
        }

        return count;
    }

    private static bool RewriteDtoPaths(CardPresentationConfigDto dto)
    {
        var changed = false;
        if (dto.sprites != null)
        {
            changed |= RewriteField(ref dto.sprites.mainIcon);
            changed |= RewriteField(ref dto.sprites.faceBackground);
            changed |= RewriteField(ref dto.sprites.cardFrame);
            changed |= RewriteField(ref dto.sprites.banner);
            changed |= RewriteField(ref dto.sprites.backBorder);
            changed |= RewriteField(ref dto.sprites.backShirt);
            changed |= RewriteField(ref dto.sprites.backLogo);
        }

        if (dto.animations?.slots != null)
        {
            for (var i = 0; i < dto.animations.slots.Length; i++)
            {
                var slot = dto.animations.slots[i];
                if (slot == null)
                {
                    continue;
                }

                changed |= RewriteField(ref slot.path);
            }
        }

        if (dto.extraSlots != null)
        {
            for (var i = 0; i < dto.extraSlots.Length; i++)
            {
                var extra = dto.extraSlots[i];
                if (extra == null)
                {
                    continue;
                }

                changed |= RewriteField(ref extra.path);
            }
        }

        return changed;
    }

    private static bool RewriteField(ref string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var rewritten = CardPresentationContentArt.RewriteLegacyArtsImagesPath(path);
        if (string.Equals(path, rewritten, StringComparison.Ordinal))
        {
            return false;
        }

        path = rewritten;
        return true;
    }

    private static bool AssetExists(string assetPath)
    {
        if (AssetDatabase.IsValidFolder(assetPath))
        {
            return true;
        }

        return !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(assetPath));
    }

    private static void EnsureParentFolders(string assetFileOrFolderPath)
    {
        var normalized = assetFileOrFolderPath.Replace('\\', '/');
        var lastSlash = normalized.LastIndexOf('/');
        if (lastSlash <= 0)
        {
            return;
        }

        EnsureFolder(normalized.Substring(0, lastSlash));
    }

    private static void EnsureFolder(string folderAssetPath)
    {
        var normalized = folderAssetPath.Replace('\\', '/');
        if (AssetDatabase.IsValidFolder(normalized))
        {
            return;
        }

        var parts = normalized.Split('/');
        if (parts.Length == 0 || !string.Equals(parts[0], "Assets", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Folder must be under Assets/: " + folderAssetPath);
        }

        var current = "Assets";
        for (var i = 1; i < parts.Length; i++)
        {
            if (string.IsNullOrEmpty(parts[i]))
            {
                continue;
            }

            var next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private static string ToAbsolute(string assetPath)
    {
        var normalized = assetPath.Replace('\\', '/');
        var project = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
        return Path.GetFullPath(Path.Combine(project, normalized));
    }
}
#endif
