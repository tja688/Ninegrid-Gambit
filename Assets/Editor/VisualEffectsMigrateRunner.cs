#if UNITY_EDITOR
using System;
using System.IO;
using NineGrid.Content;
using NineGrid.Content.CardPresentation;
using NineGrid.Content.Editor;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 把 Multiple/Effects 精灵表迁入 Assets/Resources/ContentArt/Multiple/Effects，并扫描生成 visual_effects.json。
/// </summary>
public static class VisualEffectsMigrateRunner
{
    [MenuItem("NineGrid/Tools/Migrate Visual Effects To Resources")]
    public static void Run()
    {
        var report = MigrateAndScan();
        Debug.Log("[VisualEffectsMigrate] " + report);
    }

    public static string MigrateAndScan()
    {
        var migrateReport = MigrateTree();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var existing = VisualEffectCatalogEditorIO.LoadAll(out _);
        var merged = VisualEffectCatalogEditorIO.ScanAndMerge(existing, out var scanReport);
        if (!VisualEffectCatalogEditorIO.TrySaveAll(merged, out var saveError))
        {
            return migrateReport + " | scan failed: " + saveError;
        }

        VisualEffectCatalog.Invalidate();
        return migrateReport + " | " + scanReport + " saved=" + merged.Count;
    }

    public static string MigrateTree()
    {
        var source = VisualEffectContentArt.LegacyArtsEffectsFolder;
        var dest = VisualEffectContentArt.RootAssetFolder;

        if (!AssetDatabase.IsValidFolder(source))
        {
            if (AssetDatabase.IsValidFolder(dest))
            {
                return "already_at_dest skipped_move";
            }

            return "missing_source: " + source;
        }

        if (AssetDatabase.IsValidFolder(dest))
        {
            return "dest_exists skipped_move dest=" + dest;
        }

        EnsureParentFolders(dest);
        var parent = ParentFolder(dest);
        var leaf = Path.GetFileName(dest);
        // MoveAsset 要求目标路径完整（含新名）。
        var moveError = AssetDatabase.MoveAsset(source, dest);
        if (!string.IsNullOrEmpty(moveError))
        {
            // 若父级已有 Effects 空壳冲突，尝试先删空壳再移。
            return "move_failed: " + moveError + " parent=" + parent + " leaf=" + leaf;
        }

        return "moved " + source + " → " + dest;
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

    private static string ParentFolder(string path)
    {
        var normalized = path.Replace('\\', '/');
        var slash = normalized.LastIndexOf('/');
        return slash > 0 ? normalized.Substring(0, slash) : string.Empty;
    }
}
#endif
