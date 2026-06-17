#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public sealed class PixelArtImageProcessorWindow : EditorWindow
{
    private const string AsepriteFolder = "Assets/Arts/Images/Aseprite";
    private const string MultipleFolder = "Assets/Arts/Images/Multiple";
    private const string PngFolder = "Assets/Arts/Images/Png";

    private const int PixelsPerUnit = 32;
    private const int MaxTextureSize = 4096;

    private const string ProcessMark = "PIXEL_ART_IMAGE_PROCESSED_V1_PPU_32";

    private Vector2 scrollPosition;
    private readonly List<string> logs = new();

    [MenuItem("Tools/Pixel Art/Image Import Processor")]
    public static void Open()
    {
        GetWindow<PixelArtImageProcessorWindow>("Pixel Art Processor");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Pixel Art Image Processor", EditorStyles.boldLabel);
        EditorGUILayout.Space(6);

        EditorGUILayout.HelpBox(
            "手动处理 Assets/Arts/Images 下的 Aseprite、Multiple、Png 三个目录。\n" +
            "已处理过的资源会被跳过。发现资源放错目录会告警并跳过。",
            MessageType.Info
        );

        EditorGUILayout.Space(8);

        DrawFolderInfo("Aseprite", AsepriteFolder, ".ase / .aseprite");
        DrawFolderInfo("Multiple", MultipleFolder, ".png, Sprite Mode = Multiple");
        DrawFolderInfo("Png", PngFolder, ".png, Sprite Mode = Single");

        EditorGUILayout.Space(12);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("扫描并处理", GUILayout.Height(36)))
            {
                ProcessAll();
            }

            if (GUILayout.Button("清空日志", GUILayout.Height(36)))
            {
                logs.Clear();
            }
        }

        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField("日志", EditorStyles.boldLabel);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        foreach (string log in logs)
        {
            EditorGUILayout.LabelField(log, EditorStyles.wordWrappedLabel);
        }

        EditorGUILayout.EndScrollView();
    }

    private static void DrawFolderInfo(string label, string folder, string rule)
    {
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        EditorGUILayout.LabelField(folder);
        EditorGUILayout.LabelField("规则：" + rule);
        EditorGUILayout.Space(4);
    }

    private void ProcessAll()
    {
        logs.Clear();

        EnsureFolderExists(AsepriteFolder);
        EnsureFolderExists(MultipleFolder);
        EnsureFolderExists(PngFolder);

        int processed = 0;
        int skipped = 0;
        int warnings = 0;
        int errors = 0;

        AssetDatabase.StartAssetEditing();

        try
        {
            ProcessFolder(
                AsepriteFolder,
                FolderKind.Aseprite,
                ref processed,
                ref skipped,
                ref warnings,
                ref errors
            );

            ProcessFolder(
                MultipleFolder,
                FolderKind.Multiple,
                ref processed,
                ref skipped,
                ref warnings,
                ref errors
            );

            ProcessFolder(
                PngFolder,
                FolderKind.Png,
                ref processed,
                ref skipped,
                ref warnings,
                ref errors
            );
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        AddLog("");
        AddLog($"完成：处理 {processed} 个，跳过 {skipped} 个，告警 {warnings} 个，错误 {errors} 个。");
    }

    private void ProcessFolder(
        string folder,
        FolderKind folderKind,
        ref int processed,
        ref int skipped,
        ref int warnings,
        ref int errors)
    {
        string[] guids = AssetDatabase.FindAssets("", new[] { folder });

        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);

            if (AssetDatabase.IsValidFolder(assetPath))
                continue;

            string extension = System.IO.Path.GetExtension(assetPath).ToLowerInvariant();

            if (!IsSupportedImageExtension(extension))
                continue;

            if (!IsFileAllowedInFolder(extension, folderKind))
            {
                warnings++;
                AddLog($"[告警] 放错目录，已跳过：{assetPath}");
                continue;
            }

            AssetImporter importer = AssetImporter.GetAtPath(assetPath);

            if (importer == null)
            {
                errors++;
                AddLog($"[错误] 找不到 importer：{assetPath}");
                continue;
            }

            if (IsAlreadyProcessed(importer))
            {
                skipped++;
                AddLog($"[跳过] 已处理：{assetPath}");
                continue;
            }

            bool success = false;

            try
            {
                switch (folderKind)
                {
                    case FolderKind.Aseprite:
                        success = ProcessAsepriteAsset(assetPath, importer);
                        break;

                    case FolderKind.Multiple:
                        success = ProcessPngAsset(assetPath, SpriteImportMode.Multiple);
                        break;

                    case FolderKind.Png:
                        success = ProcessPngAsset(assetPath, SpriteImportMode.Single);
                        break;
                }
            }
            catch (Exception exception)
            {
                errors++;
                AddLog($"[错误] 处理失败：{assetPath}");
                AddLog(exception.Message);
                continue;
            }

            if (success)
            {
                importer = AssetImporter.GetAtPath(assetPath);

                if (importer != null)
                {
                    importer.userData = ProcessMark;
                    importer.SaveAndReimport();
                }

                processed++;
                AddLog($"[处理] {assetPath}");
            }
            else
            {
                errors++;
                AddLog($"[错误] 处理失败：{assetPath}");
            }
        }
    }

    private static bool ProcessPngAsset(string assetPath, SpriteImportMode spriteImportMode)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;

        if (importer == null)
            return false;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = spriteImportMode;
        importer.spritePixelsPerUnit = PixelsPerUnit;

        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;

        TextureImporterPlatformSettings defaultSettings = importer.GetDefaultPlatformTextureSettings();
        defaultSettings.maxTextureSize = MaxTextureSize;
        defaultSettings.textureCompression = TextureImporterCompression.Uncompressed;
        defaultSettings.format = TextureImporterFormat.RGBA32;
        importer.SetPlatformTextureSettings(defaultSettings);

        importer.SaveAndReimport();
        return true;
    }

    private bool ProcessAsepriteAsset(string assetPath, AssetImporter importer)
    {
        Type importerType = importer.GetType();

        bool looksLikeAsepriteImporter =
            importerType.FullName != null &&
            importerType.FullName.Contains("Aseprite", StringComparison.OrdinalIgnoreCase);

        if (!looksLikeAsepriteImporter)
        {
            AddLog($"[告警] 当前资源不是 Unity Aseprite Importer 处理的资源，可能没安装 2D Aseprite Importer：{assetPath}");
            return false;
        }

        SetPropertyIfExists(importer, "textureType", TextureImporterType.Sprite);
        SetPropertyIfExists(importer, "spritePixelsPerUnit", (float)PixelsPerUnit);
        SetPropertyIfExists(importer, "filterMode", FilterMode.Point);
        SetPropertyIfExists(importer, "mipmapEnabled", false);
        SetPropertyIfExists(importer, "wrapMode", TextureWrapMode.Clamp);
        SetPropertyIfExists(importer, "spriteMeshType", SpriteMeshType.FullRect);
        SetPropertyIfExists(importer, "spriteExtrude", 0u);
        SetPropertyIfExists(importer, "spritePadding", 0u);
        SetPropertyIfExists(importer, "mosaicPadding", 0u);
        SetPropertyIfExists(importer, "generatePhysicsShape", false);

        TryApplyAsepritePlatformSettings(importer);

        importer.SaveAndReimport();
        return true;
    }

    private static void TryApplyAsepritePlatformSettings(AssetImporter importer)
    {
        Type type = importer.GetType();

        MethodInfo getMethod = type.GetMethod(
            "GetImporterPlatformSettings",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );

        MethodInfo setMethod = type.GetMethod(
            "SetImporterPlatformSettings",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );

        if (getMethod == null || setMethod == null)
            return;

        object settings = getMethod.Invoke(importer, new object[] { EditorUserBuildSettings.activeBuildTarget });

        if (settings == null)
            return;

        SetPropertyIfExists(settings, "maxTextureSize", MaxTextureSize);
        SetPropertyIfExists(settings, "textureCompression", TextureImporterCompression.Uncompressed);
        SetPropertyIfExists(settings, "format", TextureImporterFormat.RGBA32);

        setMethod.Invoke(importer, new[] { settings });
    }

    private static void SetPropertyIfExists(object target, string propertyName, object value)
    {
        PropertyInfo property = target.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );

        if (property == null || !property.CanWrite)
            return;

        Type propertyType = property.PropertyType;

        object finalValue = value;

        if (value != null && propertyType != value.GetType())
        {
            if (propertyType.IsEnum)
            {
                finalValue = Enum.ToObject(propertyType, Convert.ToInt32(value));
            }
            else
            {
                finalValue = Convert.ChangeType(value, propertyType);
            }
        }

        property.SetValue(target, finalValue);
    }

    private static bool IsAlreadyProcessed(AssetImporter importer)
    {
        return importer.userData == ProcessMark;
    }

    private static bool IsSupportedImageExtension(string extension)
    {
        return extension == ".png" ||
               extension == ".ase" ||
               extension == ".aseprite";
    }

    private static bool IsFileAllowedInFolder(string extension, FolderKind folderKind)
    {
        return folderKind switch
        {
            FolderKind.Aseprite => extension == ".ase" || extension == ".aseprite",
            FolderKind.Multiple => extension == ".png",
            FolderKind.Png => extension == ".png",
            _ => false
        };
    }

    private static void EnsureFolderExists(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string[] parts = folderPath.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];

            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private void AddLog(string message)
    {
        logs.Add(message);
        Debug.Log(message);
    }

    private enum FolderKind
    {
        Aseprite,
        Multiple,
        Png
    }
}
#endif