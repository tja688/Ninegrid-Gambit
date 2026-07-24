#if UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
/// 像素图导入批处理工具：可配置 PPU/压缩等参数、自定义路径与模式，批量应用 importer 设置。
/// </summary>
public sealed class PixelArtImageProcessorWindow : EditorWindow
{
    private const string PrefsKey = "TableNine.PixelArtImageProcessor.Settings.V2";
    private const string ProcessMarkPrefix = "PIXEL_ART_V2|";
    private const string LegacyProcessMark = "PIXEL_ART_IMAGE_PROCESSED_V1_PPU_32";

    private static readonly int[] MaxTextureSizeChoices =
    {
        32, 64, 128, 256, 512, 1024, 2048, 4096, 8192
    };

    private static readonly int[] PpuQuickChoices = { 8, 16, 24, 32, 48, 64, 96, 128 };

    [SerializeField] private ProcessorSettings settings = ProcessorSettings.CreateDefault();
    [SerializeField] private Vector2 logScroll;
    [SerializeField] private Vector2 mainScroll;
    [SerializeField] private bool showSettings = true;
    [SerializeField] private bool showAsepriteExtras = true;
    [SerializeField] private bool showPaths = true;
    [SerializeField] private bool showLogs = true;

    private readonly List<string> logs = new();
    private ReorderableList pathList;
    private bool isProcessing;
    private bool settingsDirty;

    private enum ProcessMode
    {
        Single = 0,
        Multiple = 1,
        Aseprite = 2
    }

    [Serializable]
    private sealed class PathEntry
    {
        public bool enabled = true;
        public string path = "Assets/";
        public ProcessMode mode = ProcessMode.Single;
    }

    [Serializable]
    private sealed class ProcessorSettings
    {
        public float pixelsPerUnit = 32f;
        public int maxTextureSize = 4096;
        public FilterMode filterMode = FilterMode.Point;
        public TextureImporterCompression textureCompression = TextureImporterCompression.Uncompressed;
        public TextureImporterFormat textureFormat = TextureImporterFormat.RGBA32;
        public bool mipmapEnabled;
        public bool alphaIsTransparency = true;
        public TextureWrapMode wrapMode = TextureWrapMode.Clamp;
        public SpriteMeshType spriteMeshType = SpriteMeshType.FullRect;
        public uint spriteExtrude;
        public uint spritePadding;
        public uint mosaicPadding;
        public bool generatePhysicsShape;
        public bool forceReprocess;
        public bool verboseLogs;
        public bool mirrorToUnityConsole;
        public List<PathEntry> paths = new();

        public static ProcessorSettings CreateDefault()
        {
            return new ProcessorSettings
            {
                paths = new List<PathEntry>
                {
                    new PathEntry
                    {
                        enabled = true,
                        path = "Assets/Arts/Images/Aseprite",
                        mode = ProcessMode.Aseprite
                    },
                    new PathEntry
                    {
                        enabled = true,
                        path = "Assets/Arts/Images/Multiple",
                        mode = ProcessMode.Multiple
                    },
                    new PathEntry
                    {
                        enabled = true,
                        path = "Assets/Arts/Images/Png",
                        mode = ProcessMode.Single
                    }
                }
            };
        }

        public string BuildProcessMark()
        {
            // 指纹变更后会自动重处理；与具体路径无关。
            var sb = new StringBuilder(96);
            sb.Append(ProcessMarkPrefix);
            sb.Append("ppu=").Append(pixelsPerUnit.ToString("0.###"));
            sb.Append("|max=").Append(maxTextureSize);
            sb.Append("|filter=").Append((int)filterMode);
            sb.Append("|comp=").Append((int)textureCompression);
            sb.Append("|fmt=").Append((int)textureFormat);
            sb.Append("|mip=").Append(mipmapEnabled ? 1 : 0);
            sb.Append("|alpha=").Append(alphaIsTransparency ? 1 : 0);
            sb.Append("|wrap=").Append((int)wrapMode);
            sb.Append("|mesh=").Append((int)spriteMeshType);
            sb.Append("|extrude=").Append(spriteExtrude);
            sb.Append("|pad=").Append(spritePadding);
            sb.Append("|mosaic=").Append(mosaicPadding);
            sb.Append("|phys=").Append(generatePhysicsShape ? 1 : 0);
            return sb.ToString();
        }
    }

    private sealed class CollectResult
    {
        public ProcessMode Mode;
        public string Folder;
        public List<string> AssetPaths = new();
        public int WrongFolderCount;
        public List<string> WrongFolderSamples = new();
    }

    private struct ProcessCounters
    {
        public int Processed;
        public int Skipped;
        public int Warnings;
        public int Errors;
        public int Unchanged;
    }

    [MenuItem("Tools/Pixel Art/Image Import Processor")]
    public static void Open()
    {
        PixelArtImageProcessorWindow window = GetWindow<PixelArtImageProcessorWindow>();
        window.titleContent = new GUIContent("Pixel Art Processor");
        window.minSize = new Vector2(560f, 480f);
        window.Show();
        window.Focus();
    }

    private void OnEnable()
    {
        titleContent = new GUIContent("Pixel Art Processor");
        LoadSettings();
        RebuildPathList();
    }

    private void OnDisable()
    {
        if (settingsDirty)
            SaveSettings();
    }

    private void OnGUI()
    {
        mainScroll = EditorGUILayout.BeginScrollView(mainScroll);

        DrawHeader();
        EditorGUILayout.Space(8);
        DrawSettingsSection();
        EditorGUILayout.Space(8);
        DrawPathsSection();
        EditorGUILayout.Space(8);
        DrawActions();
        EditorGUILayout.Space(8);
        DrawLogsSection();

        EditorGUILayout.EndScrollView();

        if (settingsDirty && GUI.changed)
        {
            // 延迟保存，避免每帧写 Prefs。
            EditorApplication.delayCall -= SaveSettingsDeferred;
            EditorApplication.delayCall += SaveSettingsDeferred;
        }
    }

    private void DrawHeader()
    {
        EditorGUILayout.LabelField("Pixel Art Image Processor", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "按路径批量设置像素图导入参数。已处理且指纹匹配的资源会跳过；改参数后会自动重处理。\n" +
            "AssetDatabase 只能在主线程写 importer；加速靠：磁盘并行扫描、变更检测、批量 StartAssetEditing、减少无意义 Reimport。\n" +
            "新拖入资源后请等 Console 导入完成再点处理，否则易触发 SourceAssetDB mtime 警告（Import Error 4）。",
            MessageType.Info
        );
    }

    private void DrawSettingsSection()
    {
        showSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showSettings, "导入参数");
        if (!showSettings)
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        EditorGUI.BeginChangeCheck();

        EditorGUILayout.LabelField("Pixels Per Unit", EditorStyles.boldLabel);
        settings.pixelsPerUnit = EditorGUILayout.FloatField("PPU", settings.pixelsPerUnit);
        using (new EditorGUILayout.HorizontalScope())
        {
            foreach (int ppu in PpuQuickChoices)
            {
                if (GUILayout.Button(ppu.ToString(), GUILayout.Height(22)))
                    settings.pixelsPerUnit = ppu;
            }
        }

        EditorGUILayout.Space(6);
        int maxIndex = Mathf.Max(0, Array.IndexOf(MaxTextureSizeChoices, settings.maxTextureSize));
        if (Array.IndexOf(MaxTextureSizeChoices, settings.maxTextureSize) < 0)
            maxIndex = MaxTextureSizeChoices.Length - 2; // fallback near 4096

        maxIndex = EditorGUILayout.Popup(
            "Max Texture Size",
            maxIndex,
            Array.ConvertAll(MaxTextureSizeChoices, v => v.ToString())
        );
        settings.maxTextureSize = MaxTextureSizeChoices[maxIndex];

        settings.filterMode = (FilterMode)EditorGUILayout.EnumPopup("Filter Mode", settings.filterMode);
        settings.textureCompression =
            (TextureImporterCompression)EditorGUILayout.EnumPopup("Compression", settings.textureCompression);
        settings.textureFormat =
            (TextureImporterFormat)EditorGUILayout.EnumPopup("Texture Format", settings.textureFormat);
        settings.wrapMode = (TextureWrapMode)EditorGUILayout.EnumPopup("Wrap Mode", settings.wrapMode);
        settings.mipmapEnabled = EditorGUILayout.Toggle("Mipmaps", settings.mipmapEnabled);
        settings.alphaIsTransparency = EditorGUILayout.Toggle("Alpha Is Transparency", settings.alphaIsTransparency);

        EditorGUILayout.Space(4);
        showAsepriteExtras = EditorGUILayout.Foldout(showAsepriteExtras, "Sprite / Aseprite 额外项", true);
        if (showAsepriteExtras)
        {
            EditorGUI.indentLevel++;
            settings.spriteMeshType =
                (SpriteMeshType)EditorGUILayout.EnumPopup("Sprite Mesh Type", settings.spriteMeshType);
            settings.spriteExtrude = (uint)Mathf.Max(0, EditorGUILayout.IntField("Sprite Extrude", (int)settings.spriteExtrude));
            settings.spritePadding = (uint)Mathf.Max(0, EditorGUILayout.IntField("Sprite Padding", (int)settings.spritePadding));
            settings.mosaicPadding = (uint)Mathf.Max(0, EditorGUILayout.IntField("Mosaic Padding", (int)settings.mosaicPadding));
            settings.generatePhysicsShape = EditorGUILayout.Toggle("Generate Physics Shape", settings.generatePhysicsShape);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(4);
        settings.forceReprocess = EditorGUILayout.Toggle(
            new GUIContent("强制重处理", "忽略已处理指纹，全部重新写入并 Reimport"),
            settings.forceReprocess
        );
        settings.verboseLogs = EditorGUILayout.Toggle(
            new GUIContent("详细日志", "输出每个跳过/未变更资源；关闭时只保留摘要与告警/错误"),
            settings.verboseLogs
        );
        settings.mirrorToUnityConsole = EditorGUILayout.Toggle(
            new GUIContent("同步到 Console", "窗口日志同时 Debug.Log（大量资源时会明显变慢）"),
            settings.mirrorToUnityConsole
        );

        if (EditorGUI.EndChangeCheck())
            settingsDirty = true;

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("当前指纹", EditorStyles.miniBoldLabel);
        EditorGUILayout.SelectableLabel(settings.BuildProcessMark(), EditorStyles.textField, GUILayout.Height(18));

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawPathsSection()
    {
        showPaths = EditorGUILayout.BeginFoldoutHeaderGroup(showPaths, "处理路径");
        if (!showPaths)
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        EditorGUILayout.HelpBox(
            "每个路径独立选择模式：Single（整图精灵）/ Multiple（图集切片）/ Aseprite（.ase/.aseprite）。",
            MessageType.None
        );

        pathList?.DoLayoutList();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("恢复默认路径", GUILayout.Height(24)))
            {
                settings.paths = ProcessorSettings.CreateDefault().paths;
                RebuildPathList();
                settingsDirty = true;
            }

            if (GUILayout.Button("添加当前选中文件夹", GUILayout.Height(24)))
            {
                TryAddSelectedFolder();
            }
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawActions()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(isProcessing))
            {
                if (GUILayout.Button(isProcessing ? "处理中…" : "扫描并处理", GUILayout.Height(36)))
                {
                    EditorApplication.delayCall -= ProcessAllDeferred;
                    EditorApplication.delayCall += ProcessAllDeferred;
                }
            }

            if (GUILayout.Button("清空日志", GUILayout.Height(36)))
                logs.Clear();

            if (GUILayout.Button("保存设置", GUILayout.Height(36)))
                SaveSettings();
        }
    }

    private void DrawLogsSection()
    {
        showLogs = EditorGUILayout.BeginFoldoutHeaderGroup(showLogs, $"日志（{logs.Count}）");
        if (!showLogs)
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        logScroll = EditorGUILayout.BeginScrollView(logScroll, GUILayout.MinHeight(180f));
        foreach (string log in logs)
            EditorGUILayout.LabelField(log, EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndScrollView();

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void RebuildPathList()
    {
        pathList = new ReorderableList(settings.paths, typeof(PathEntry), true, true, true, true)
        {
            drawHeaderCallback = rect => EditorGUI.LabelField(rect, "路径列表（可拖拽排序）"),
            elementHeight = EditorGUIUtility.singleLineHeight + 6f,
            onAddCallback = list =>
            {
                settings.paths.Add(new PathEntry
                {
                    enabled = true,
                    path = "Assets/",
                    mode = ProcessMode.Single
                });
                settingsDirty = true;
            },
            onRemoveCallback = list =>
            {
                if (list.index >= 0 && list.index < settings.paths.Count)
                    settings.paths.RemoveAt(list.index);
                settingsDirty = true;
            },
            drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                if (index < 0 || index >= settings.paths.Count)
                    return;

                PathEntry entry = settings.paths[index];
                rect.y += 2f;
                float h = EditorGUIUtility.singleLineHeight;

                Rect enabledRect = new Rect(rect.x, rect.y, 18f, h);
                Rect modeRect = new Rect(rect.x + 22f, rect.y, 96f, h);
                Rect browseRect = new Rect(rect.xMax - 56f, rect.y, 56f, h);
                Rect pathRect = new Rect(modeRect.xMax + 4f, rect.y, browseRect.x - modeRect.xMax - 8f, h);

                EditorGUI.BeginChangeCheck();
                entry.enabled = EditorGUI.Toggle(enabledRect, entry.enabled);
                entry.mode = (ProcessMode)EditorGUI.EnumPopup(modeRect, entry.mode);
                entry.path = EditorGUI.TextField(pathRect, entry.path);

                if (GUI.Button(browseRect, "浏览"))
                {
                    string start = string.IsNullOrEmpty(entry.path) ? "Assets" : entry.path;
                    string absStart = Path.GetFullPath(start);
                    if (!Directory.Exists(absStart))
                        absStart = Application.dataPath;

                    string picked = EditorUtility.OpenFolderPanel("选择处理目录", absStart, string.Empty);
                    if (!string.IsNullOrEmpty(picked))
                    {
                        string assetPath = AbsolutePathToAssetPath(picked);
                        if (string.IsNullOrEmpty(assetPath))
                            EditorUtility.DisplayDialog("无效路径", "请选择项目 Assets 下的文件夹。", "确定");
                        else
                            entry.path = assetPath;
                    }
                }

                if (EditorGUI.EndChangeCheck())
                    settingsDirty = true;
            }
        };
    }

    private void TryAddSelectedFolder()
    {
        UnityEngine.Object[] selected = Selection.objects;
        foreach (UnityEngine.Object obj in selected)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path) || !AssetDatabase.IsValidFolder(path))
                continue;

            if (settings.paths.Any(p => string.Equals(p.path, path, StringComparison.OrdinalIgnoreCase)))
                continue;

            settings.paths.Add(new PathEntry
            {
                enabled = true,
                path = path,
                mode = GuessModeFromFolderName(path)
            });
            settingsDirty = true;
        }

        RebuildPathList();
    }

    private static ProcessMode GuessModeFromFolderName(string path)
    {
        string name = Path.GetFileName(path.TrimEnd('/', '\\'));
        if (name.IndexOf("aseprite", StringComparison.OrdinalIgnoreCase) >= 0)
            return ProcessMode.Aseprite;
        if (name.IndexOf("multiple", StringComparison.OrdinalIgnoreCase) >= 0)
            return ProcessMode.Multiple;
        return ProcessMode.Single;
    }

    private void ProcessAllDeferred()
    {
        if (this == null || isProcessing)
            return;

        isProcessing = true;
        SaveSettings();

        try
        {
            ProcessAll();
        }
        finally
        {
            isProcessing = false;
            EditorUtility.ClearProgressBar();

            if (this != null)
                Repaint();
        }
    }

    private void ProcessAll()
    {
        logs.Clear();
        string mark = settings.BuildProcessMark();
        AddLog($"指纹：{mark}");

        List<PathEntry> enabledPaths = settings.paths
            .Where(p => p != null && p.enabled && !string.IsNullOrWhiteSpace(p.path))
            .ToList();

        if (enabledPaths.Count == 0)
        {
            AddLog("[错误] 没有启用的处理路径。");
            return;
        }

        // 新导入资源若仍在并行 Worker 中，立刻 WriteImportSettingsIfDirty 会改写 .meta mtime，
        // 与 SourceAssetDB 登记时间错位 → Import Error Code (4) Build asset version error。
        if (!WaitForAssetDatabaseIdle(120f, "等待资源导入空闲…"))
        {
            AddLog("[错误] 等待 AssetDatabase 空闲超时。请等 Console 导入结束后再重试。");
            return;
        }

        foreach (PathEntry entry in enabledPaths)
        {
            string normalized = NormalizeAssetFolder(entry.path);
            entry.path = normalized;
            EnsureFolderExists(normalized);
        }

        settingsDirty = true;

        // 1) 磁盘并行扫描（不碰 AssetDatabase）
        EditorUtility.DisplayProgressBar("Pixel Art Processor", "并行扫描文件…", 0.02f);
        List<CollectResult> collections = CollectAssetPathsParallel(enabledPaths);

        int totalCandidates = collections.Sum(c => c.AssetPaths.Count);
        AddLog($"扫描完成：{totalCandidates} 个候选资源，覆盖 {collections.Count} 个路径。");

        foreach (CollectResult collection in collections)
        {
            if (collection.WrongFolderCount <= 0)
                continue;

            AddLog($"[告警] {collection.Folder}（{collection.Mode}）扩展名不匹配：{collection.WrongFolderCount} 个，已跳过");
            foreach (string sample in collection.WrongFolderSamples)
                AddLog($"  · {sample}");
        }

        // 2) 主线程批量写 importer
        var counters = new ProcessCounters();
        var dirtyPaths = new List<string>(Math.Min(totalCandidates, 256));

        AssetDatabase.StartAssetEditing();
        try
        {
            int done = 0;
            foreach (CollectResult collection in collections)
            {
                foreach (string assetPath in collection.AssetPaths)
                {
                    done++;
                    if (done == 1 || done % 8 == 0 || done == totalCandidates)
                    {
                        float progress = totalCandidates <= 0 ? 1f : done / (float)totalCandidates;
                        if (EditorUtility.DisplayCancelableProgressBar(
                                "Pixel Art Processor",
                                $"写入导入设置 {done}/{totalCandidates}\n{assetPath}",
                                progress))
                        {
                            AddLog("[取消] 用户中止处理。");
                            goto FinishBatch;
                        }
                    }

                    try
                    {
                        ProcessOneAsset(assetPath, collection.Mode, mark, ref counters, dirtyPaths);
                    }
                    catch (Exception exception)
                    {
                        counters.Errors++;
                        AddLog($"[错误] 处理失败：{assetPath}");
                        AddLog(exception.Message);
                    }
                }
            }

            FinishBatch: ;
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
        }

        // 3) 等本轮批量导入落地，再显式 ForceUpdate 脏资源，把 SourceAssetDB mtime 对齐到磁盘。
        WaitForAssetDatabaseIdle(120f, "等待批量导入完成…");
        if (dirtyPaths.Count > 0)
            ForceReimportPaths(dirtyPaths);

        AddLog("");
        AddLog(
            $"完成：处理 {counters.Processed}，未变更跳过 {counters.Unchanged}，已处理跳过 {counters.Skipped}，" +
            $"告警 {counters.Warnings}，错误 {counters.Errors}；实际标记脏资源 {dirtyPaths.Count}。"
        );
    }

    private bool WaitForAssetDatabaseIdle(float timeoutSeconds, string progressLabel)
    {
        double start = EditorApplication.timeSinceStartup;
        while (EditorApplication.isUpdating)
        {
            float elapsed = (float)(EditorApplication.timeSinceStartup - start);
            if (elapsed > timeoutSeconds)
                return false;

            float progress = timeoutSeconds <= 0f ? 1f : Mathf.Clamp01(elapsed / timeoutSeconds);
            if (EditorUtility.DisplayCancelableProgressBar(
                    "Pixel Art Processor",
                    progressLabel,
                    progress))
            {
                AddLog("[取消] 等待导入时空闲中止。");
                return false;
            }

            // 批量工具本身阻塞主线程；短睡让导入 Worker / 主线程队列有机会推进。
            System.Threading.Thread.Sleep(50);
        }

        return true;
    }

    private void ForceReimportPaths(List<string> assetPaths)
    {
        if (assetPaths == null || assetPaths.Count == 0)
            return;

        AddLog($"对齐 SourceAssetDB：强制重导 {assetPaths.Count} 个已变更资源…");
        AssetDatabase.StartAssetEditing();
        try
        {
            for (int i = 0; i < assetPaths.Count; i++)
            {
                string assetPath = assetPaths[i];
                if (i == 0 || (i + 1) % 8 == 0 || i + 1 == assetPaths.Count)
                {
                    float progress = (i + 1f) / assetPaths.Count;
                    if (EditorUtility.DisplayCancelableProgressBar(
                            "Pixel Art Processor",
                            $"强制重导 {i + 1}/{assetPaths.Count}\n{assetPath}",
                            progress))
                    {
                        AddLog("[取消] 强制重导中止。");
                        break;
                    }
                }

                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        WaitForAssetDatabaseIdle(120f, "等待强制重导完成…");
    }

    private void ProcessOneAsset(
        string assetPath,
        ProcessMode mode,
        string mark,
        ref ProcessCounters counters,
        List<string> dirtyPaths)
    {
        AssetImporter importer = AssetImporter.GetAtPath(assetPath);
        if (importer == null)
        {
            counters.Errors++;
            AddLog($"[错误] 找不到 importer：{assetPath}");
            return;
        }

        if (!settings.forceReprocess && IsAlreadyProcessed(importer, mark))
        {
            counters.Skipped++;
            if (settings.verboseLogs)
                AddLog($"[跳过] 已处理：{assetPath}");
            return;
        }

        bool changed;
        switch (mode)
        {
            case ProcessMode.Aseprite:
                if (!TryProcessAsepriteAsset(assetPath, importer, mark, out changed))
                {
                    counters.Errors++;
                    AddLog($"[错误] Aseprite 处理失败：{assetPath}");
                    return;
                }

                break;

            case ProcessMode.Multiple:
                if (!TryProcessPngAsset(assetPath, SpriteImportMode.Multiple, mark, out changed))
                {
                    counters.Errors++;
                    AddLog($"[错误] PNG Multiple 处理失败：{assetPath}");
                    return;
                }

                break;

            case ProcessMode.Single:
                if (!TryProcessPngAsset(assetPath, SpriteImportMode.Single, mark, out changed))
                {
                    counters.Errors++;
                    AddLog($"[错误] PNG Single 处理失败：{assetPath}");
                    return;
                }

                break;

            default:
                counters.Errors++;
                AddLog($"[错误] 未知模式：{mode} @ {assetPath}");
                return;
        }

        if (!changed)
        {
            counters.Unchanged++;
            if (settings.verboseLogs)
                AddLog($"[未变更] {assetPath}");
            return;
        }

        // 批量编辑期间只对变更项排队；StopAssetEditing 后统一触发导入。
        EditorUtility.SetDirty(importer);
        AssetDatabase.WriteImportSettingsIfDirty(assetPath);
        dirtyPaths.Add(assetPath);
        counters.Processed++;
        AddLog($"[处理] {assetPath}");
    }

    private bool TryProcessPngAsset(string assetPath, SpriteImportMode spriteImportMode, string mark, out bool changed)
    {
        changed = false;
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
            return false;

        // 先读再比，避免无意义 dirty + reimport。
        TextureImporterPlatformSettings platform = importer.GetDefaultPlatformTextureSettings();
        bool needs =
            settings.forceReprocess ||
            importer.textureType != TextureImporterType.Sprite ||
            importer.spriteImportMode != spriteImportMode ||
            !Mathf.Approximately(importer.spritePixelsPerUnit, settings.pixelsPerUnit) ||
            importer.mipmapEnabled != settings.mipmapEnabled ||
            importer.filterMode != settings.filterMode ||
            importer.textureCompression != settings.textureCompression ||
            importer.alphaIsTransparency != settings.alphaIsTransparency ||
            importer.wrapMode != settings.wrapMode ||
            platform.maxTextureSize != settings.maxTextureSize ||
            platform.textureCompression != settings.textureCompression ||
            platform.format != settings.textureFormat ||
            importer.userData != mark;

        if (!needs)
            return true;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = spriteImportMode;
        importer.spritePixelsPerUnit = settings.pixelsPerUnit;
        importer.mipmapEnabled = settings.mipmapEnabled;
        importer.filterMode = settings.filterMode;
        importer.textureCompression = settings.textureCompression;
        importer.alphaIsTransparency = settings.alphaIsTransparency;
        importer.wrapMode = settings.wrapMode;

        platform.maxTextureSize = settings.maxTextureSize;
        platform.textureCompression = settings.textureCompression;
        platform.format = settings.textureFormat;
        importer.SetPlatformTextureSettings(platform);

        importer.userData = mark;
        changed = true;
        return true;
    }

    private bool TryProcessAsepriteAsset(string assetPath, AssetImporter importer, string mark, out bool changed)
    {
        changed = false;
        Type importerType = importer.GetType();
        bool looksLikeAsepriteImporter =
            importerType.FullName != null &&
            importerType.FullName.Contains("Aseprite", StringComparison.OrdinalIgnoreCase);

        if (!looksLikeAsepriteImporter)
        {
            AddLog($"[告警] 不是 Unity Aseprite Importer 资源（可能未安装 2D Aseprite Importer）：{assetPath}");
            return false;
        }

        bool needs =
            settings.forceReprocess ||
            importer.userData != mark ||
            NeedsAsepritePropertyUpdate(importer);

        if (!needs)
            return true;

        SetPropertyIfExists(importer, "textureType", TextureImporterType.Sprite);
        SetPropertyIfExists(importer, "spritePixelsPerUnit", settings.pixelsPerUnit);
        SetPropertyIfExists(importer, "filterMode", settings.filterMode);
        SetPropertyIfExists(importer, "mipmapEnabled", settings.mipmapEnabled);
        SetPropertyIfExists(importer, "wrapMode", settings.wrapMode);
        SetPropertyIfExists(importer, "spriteMeshType", settings.spriteMeshType);
        SetPropertyIfExists(importer, "spriteExtrude", settings.spriteExtrude);
        SetPropertyIfExists(importer, "spritePadding", settings.spritePadding);
        SetPropertyIfExists(importer, "mosaicPadding", settings.mosaicPadding);
        SetPropertyIfExists(importer, "generatePhysicsShape", settings.generatePhysicsShape);
        SetPropertyIfExists(importer, "alphaIsTransparency", settings.alphaIsTransparency);

        TryApplyAsepritePlatformSettings(importer);

        importer.userData = mark;
        changed = true;
        return true;
    }

    private bool NeedsAsepritePropertyUpdate(AssetImporter importer)
    {
        if (!PropertyEquals(importer, "spritePixelsPerUnit", settings.pixelsPerUnit))
            return true;
        if (!PropertyEquals(importer, "filterMode", settings.filterMode))
            return true;
        if (!PropertyEquals(importer, "mipmapEnabled", settings.mipmapEnabled))
            return true;
        if (!PropertyEquals(importer, "wrapMode", settings.wrapMode))
            return true;
        if (!PropertyEquals(importer, "spriteMeshType", settings.spriteMeshType))
            return true;
        if (!PropertyEquals(importer, "spriteExtrude", settings.spriteExtrude))
            return true;
        if (!PropertyEquals(importer, "spritePadding", settings.spritePadding))
            return true;
        if (!PropertyEquals(importer, "mosaicPadding", settings.mosaicPadding))
            return true;
        if (!PropertyEquals(importer, "generatePhysicsShape", settings.generatePhysicsShape))
            return true;
        return false;
    }

    private static bool PropertyEquals(object target, string propertyName, object expected)
    {
        PropertyInfo property = target.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );

        if (property == null || !property.CanRead)
            return true; // 属性不存在则视为无需因它而更新

        object current = property.GetValue(target);
        if (current == null)
            return expected == null;

        if (expected != null && current.GetType() != expected.GetType())
        {
            try
            {
                expected = Convert.ChangeType(expected, current.GetType());
            }
            catch
            {
                return false;
            }
        }

        return Equals(current, expected);
    }

    private void TryApplyAsepritePlatformSettings(AssetImporter importer)
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

        object platformSettings = getMethod.Invoke(
            importer,
            new object[] { EditorUserBuildSettings.activeBuildTarget }
        );

        if (platformSettings == null)
            return;

        SetPropertyIfExists(platformSettings, "maxTextureSize", settings.maxTextureSize);
        SetPropertyIfExists(platformSettings, "textureCompression", settings.textureCompression);
        SetPropertyIfExists(platformSettings, "format", settings.textureFormat);
        setMethod.Invoke(importer, new[] { platformSettings });
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
                finalValue = Enum.ToObject(propertyType, Convert.ToInt32(value));
            else
                finalValue = Convert.ChangeType(value, propertyType);
        }

        property.SetValue(target, finalValue);
    }

    private List<CollectResult> CollectAssetPathsParallel(List<PathEntry> enabledPaths)
    {
        // 主线程预取路径，避免工作线程碰 Unity API。
        string dataPath = Application.dataPath.Replace('\\', '/');
        string projectRoot = Path.GetDirectoryName(Application.dataPath)?.Replace('\\', '/') ?? string.Empty;
        var results = new ConcurrentBag<(int Order, CollectResult Result)>();

        Parallel.ForEach(
            Enumerable.Range(0, enabledPaths.Count),
            index =>
            {
                PathEntry entry = enabledPaths[index];
                var collect = new CollectResult
                {
                    Mode = entry.mode,
                    Folder = NormalizeAssetFolder(entry.path)
                };

                string absFolder = AssetPathToAbsolute(collect.Folder, projectRoot);
                if (!Directory.Exists(absFolder))
                {
                    results.Add((index, collect));
                    return;
                }

                IEnumerable<string> files;
                try
                {
                    files = Directory.EnumerateFiles(absFolder, "*.*", SearchOption.AllDirectories);
                }
                catch
                {
                    results.Add((index, collect));
                    return;
                }

                foreach (string file in files)
                {
                    string extension = Path.GetExtension(file).ToLowerInvariant();
                    if (!IsSupportedImageExtension(extension))
                        continue;

                    if (!IsFileAllowedForMode(extension, entry.mode))
                    {
                        collect.WrongFolderCount++;
                        if (collect.WrongFolderSamples.Count < 5)
                        {
                            string sample = AbsoluteFileToAssetPath(file, dataPath) ?? file.Replace('\\', '/');
                            collect.WrongFolderSamples.Add(sample);
                        }

                        continue;
                    }

                    string assetPath = AbsoluteFileToAssetPath(file, dataPath);
                    if (string.IsNullOrEmpty(assetPath))
                        continue;

                    collect.AssetPaths.Add(assetPath);
                }

                collect.AssetPaths.Sort(StringComparer.OrdinalIgnoreCase);
                results.Add((index, collect));
            });

        return results
            .OrderBy(tuple => tuple.Order)
            .Select(tuple => tuple.Result)
            .ToList();
    }

    private static string AbsoluteFileToAssetPath(string absolutePath, string dataPath)
    {
        if (string.IsNullOrEmpty(absolutePath))
            return null;

        string full = Path.GetFullPath(absolutePath).Replace('\\', '/');
        if (!full.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
            return null;

        return ("Assets" + full.Substring(dataPath.Length)).Replace('\\', '/');
    }

    private bool IsAlreadyProcessed(AssetImporter importer, string mark)
    {
        string userData = importer.userData;
        if (userData == mark)
            return true;

        // 旧版标记：仅当当前参数等价于旧硬编码默认值时跳过，避免改参后永久漏处理。
        return userData == LegacyProcessMark && IsLegacyCompatibleSettings();
    }

    private bool IsLegacyCompatibleSettings()
    {
        return Mathf.Approximately(settings.pixelsPerUnit, 32f)
               && settings.maxTextureSize == 4096
               && settings.filterMode == FilterMode.Point
               && settings.textureCompression == TextureImporterCompression.Uncompressed
               && settings.textureFormat == TextureImporterFormat.RGBA32
               && !settings.mipmapEnabled
               && settings.alphaIsTransparency
               && settings.wrapMode == TextureWrapMode.Clamp
               && settings.spriteMeshType == SpriteMeshType.FullRect
               && settings.spriteExtrude == 0
               && settings.spritePadding == 0
               && settings.mosaicPadding == 0
               && !settings.generatePhysicsShape;
    }

    private static bool IsSupportedImageExtension(string extension)
    {
        return extension is ".png" or ".ase" or ".aseprite";
    }

    private static bool IsFileAllowedForMode(string extension, ProcessMode mode)
    {
        return mode switch
        {
            ProcessMode.Aseprite => extension is ".ase" or ".aseprite",
            ProcessMode.Multiple => extension == ".png",
            ProcessMode.Single => extension == ".png",
            _ => false
        };
    }

    private static string NormalizeAssetFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "Assets";

        string normalized = path.Replace('\\', '/').Trim();
        while (normalized.EndsWith("/", StringComparison.Ordinal) && normalized.Length > 1)
            normalized = normalized.TrimEnd('/');

        if (!normalized.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
        {
            string asAsset = AbsolutePathToAssetPath(normalized);
            if (!string.IsNullOrEmpty(asAsset))
                normalized = asAsset;
        }

        return normalized;
    }

    private static string AssetPathToAbsolute(string assetPath, string projectRoot)
    {
        string relative = assetPath.Replace('\\', '/');
        return Path.GetFullPath(Path.Combine(projectRoot, relative));
    }

    private static string AbsolutePathToAssetPath(string absolutePath)
    {
        if (string.IsNullOrEmpty(absolutePath))
            return null;

        string full = Path.GetFullPath(absolutePath).Replace('\\', '/');
        string dataPath = Application.dataPath.Replace('\\', '/');

        if (!full.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
            return null;

        string relative = "Assets" + full.Substring(dataPath.Length);
        return relative.Replace('\\', '/');
    }

    private static void EnsureFolderExists(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string[] parts = folderPath.Split('/');
        if (parts.Length == 0 || !string.Equals(parts[0], "Assets", StringComparison.OrdinalIgnoreCase))
            return;

        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private void AddLog(string message)
    {
        if (this == null)
            return;

        logs.Add(message);
        if (settings.mirrorToUnityConsole)
            Debug.Log(message);
    }

    private void LoadSettings()
    {
        string json = EditorPrefs.GetString(PrefsKey, string.Empty);
        if (string.IsNullOrEmpty(json))
        {
            settings = ProcessorSettings.CreateDefault();
            return;
        }

        try
        {
            var loaded = JsonUtility.FromJson<ProcessorSettings>(json);
            if (loaded == null || loaded.paths == null || loaded.paths.Count == 0)
                settings = ProcessorSettings.CreateDefault();
            else
                settings = loaded;
        }
        catch
        {
            settings = ProcessorSettings.CreateDefault();
        }
    }

    private void SaveSettingsDeferred()
    {
        if (this == null)
            return;

        SaveSettings();
    }

    private void SaveSettings()
    {
        EditorPrefs.SetString(PrefsKey, JsonUtility.ToJson(settings));
        settingsDirty = false;
    }
}
#endif
