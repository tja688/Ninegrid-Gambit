#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 批量九宫格拉伸（9-Slice）工具：整图保留为单个 Sprite，只写入 Border，拉伸时四角不变形。
/// </summary>
public sealed class NineGridSpriteBatchWindow : EditorWindow
{
    private const string PrefsKey = "TableNine.NineGridSpriteBatch.Settings.V2";
    private const string MenuPath = "NineGrid/Tools/Nine Grid Batch Slicer";

    private static readonly int[] BorderQuickChoices = { 4, 8, 12, 16, 24, 32, 48, 64 };

    [SerializeField] private BatchSettings settings = BatchSettings.CreateDefault();
    [SerializeField] private Vector2 mainScroll;
    [SerializeField] private Vector2 listScroll;
    [SerializeField] private Vector2 logScroll;
    [SerializeField] private bool showSettings = true;
    [SerializeField] private bool showSelection = true;
    [SerializeField] private bool showLogs = true;

    private readonly List<string> assetPaths = new();
    private readonly List<string> logs = new();
    private readonly Dictionary<string, Texture2D> previewCache = new();

    private bool isProcessing;
    private bool settingsDirty;
    private int highlightIndex = -1;
    private bool followSelection = true;
    private Material previewAlphaMaterial;

    [Serializable]
    private sealed class BatchSettings
    {
        public bool linkBorders = true;
        public int borderLeft = 32;
        public int borderBottom = 32;
        public int borderRight = 32;
        public int borderTop = 32;
        public SpriteAlignment pivotAlignment = SpriteAlignment.Center;
        public Vector2 customPivot = new Vector2(0.5f, 0.5f);
        public float pixelsPerUnit = 32f;
        public bool applyPixelsPerUnit = true;
        public float ghostAlpha = 0.45f;
        public bool nearestFilterPreview = true;
        public bool requireUniformSize = false;
        public bool forceSingleSprite = true;

        public static BatchSettings CreateDefault() => new BatchSettings();

        public Vector4 ToSpriteBorder()
        {
            // TextureImporter.spriteBorder = (L, B, R, T)
            return new Vector4(
                Mathf.Max(0, borderLeft),
                Mathf.Max(0, borderBottom),
                Mathf.Max(0, borderRight),
                Mathf.Max(0, borderTop)
            );
        }
    }

    private struct TextureInfo
    {
        public string Path;
        public int Width;
        public int Height;
        public bool BorderValid;
        public string InvalidReason;
        public Vector4 CurrentBorder;
        public SpriteImportMode ImportMode;
    }

    [MenuItem(MenuPath)]
    public static void Open()
    {
        NineGridSpriteBatchWindow window = GetWindow<NineGridSpriteBatchWindow>();
        window.titleContent = new GUIContent("Nine Grid 9-Slice");
        window.minSize = new Vector2(720f, 560f);
        window.Show();
        window.Focus();
    }

    private void OnEnable()
    {
        titleContent = new GUIContent("Nine Grid 9-Slice");
        LoadSettings();
        Selection.selectionChanged += OnSelectionChanged;
        SyncFromSelection();
    }

    private void OnDisable()
    {
        Selection.selectionChanged -= OnSelectionChanged;
        if (settingsDirty)
            SaveSettings();
        ClearPreviewCache();

        if (previewAlphaMaterial != null)
        {
            DestroyImmediate(previewAlphaMaterial);
            previewAlphaMaterial = null;
        }
    }

    private void OnSelectionChanged()
    {
        if (followSelection || assetPaths.Count == 0)
            SyncFromSelection();
        Repaint();
    }

    private void OnGUI()
    {
        mainScroll = EditorGUILayout.BeginScrollView(mainScroll);

        DrawHeader();
        EditorGUILayout.Space(6);

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(320f)))
            {
                DrawSettings();
                EditorGUILayout.Space(6);
                DrawSelectionList();
                EditorGUILayout.Space(6);
                DrawActions();
            }

            EditorGUILayout.Space(8);
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
            {
                DrawPreview();
                EditorGUILayout.Space(6);
                DrawConflictSummary();
            }
        }

        EditorGUILayout.Space(8);
        DrawLogs();

        EditorGUILayout.EndScrollView();

        if (settingsDirty && GUI.changed)
        {
            EditorApplication.delayCall -= SaveSettingsDeferred;
            EditorApplication.delayCall += SaveSettingsDeferred;
        }
    }

    private void DrawHeader()
    {
        EditorGUILayout.LabelField("Nine Grid 9-Slice Batch", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "这是 UI 九宫格拉伸（Sprite Border），不是把图切成 9 张。\n" +
            "整图仍是 1 个 Sprite；只写入 L/B/R/T 边框。配合 Image Type = Sliced 时，四角不变形、中间拉伸。\n" +
            "若之前被误切成 Multiple，本工具会恢复为 Single 并写 Border。",
            MessageType.Info
        );
    }

    private void DrawSettings()
    {
        showSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showSettings, "九宫格边框（像素）");
        if (!showSettings)
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        EditorGUI.BeginChangeCheck();

        settings.linkBorders = EditorGUILayout.Toggle(
            new GUIContent("四边联动", "改一边时同步到四边（适合对称面板）"),
            settings.linkBorders
        );

        if (settings.linkBorders)
        {
            int uniform = Mathf.Max(0, settings.borderLeft);
            int newUniform = EditorGUILayout.IntField("边框（四边）", uniform);
            if (newUniform != uniform)
                SetUniformBorder(newUniform);
            DrawQuickRow(SetUniformBorder);
        }
        else
        {
            settings.borderLeft = Mathf.Max(0, EditorGUILayout.IntField("Left", settings.borderLeft));
            settings.borderRight = Mathf.Max(0, EditorGUILayout.IntField("Right", settings.borderRight));
            settings.borderTop = Mathf.Max(0, EditorGUILayout.IntField("Top", settings.borderTop));
            settings.borderBottom = Mathf.Max(0, EditorGUILayout.IntField("Bottom", settings.borderBottom));

            EditorGUILayout.LabelField("Left/Right 快捷", EditorStyles.miniLabel);
            DrawQuickRow(v =>
            {
                settings.borderLeft = v;
                settings.borderRight = v;
            });
            EditorGUILayout.LabelField("Top/Bottom 快捷", EditorStyles.miniLabel);
            DrawQuickRow(v =>
            {
                settings.borderTop = v;
                settings.borderBottom = v;
            });
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("按首图三等分", GUILayout.Height(24)))
                TryInferEqualThirdsFromFirst();
            if (GUILayout.Button("从首图当前 Border 读取", GUILayout.Height(24)))
                TryLoadBorderFromFirst();
        }

        EditorGUILayout.Space(4);
        settings.pivotAlignment =
            (SpriteAlignment)EditorGUILayout.EnumPopup("Pivot", settings.pivotAlignment);
        if (settings.pivotAlignment == SpriteAlignment.Custom)
            settings.customPivot = EditorGUILayout.Vector2Field("Custom Pivot", settings.customPivot);

        settings.applyPixelsPerUnit = EditorGUILayout.Toggle("同时写 PPU", settings.applyPixelsPerUnit);
        using (new EditorGUI.DisabledScope(!settings.applyPixelsPerUnit))
            settings.pixelsPerUnit = EditorGUILayout.FloatField("Pixels Per Unit", settings.pixelsPerUnit);

        settings.forceSingleSprite = EditorGUILayout.Toggle(
            new GUIContent("强制 Single", "从误切的 Multiple 恢复为整图 Sprite"),
            settings.forceSingleSprite
        );
        settings.ghostAlpha = EditorGUILayout.Slider("虚像透明度", settings.ghostAlpha, 0.05f, 1f);
        settings.nearestFilterPreview = EditorGUILayout.Toggle("预览最近点采样", settings.nearestFilterPreview);
        settings.requireUniformSize = EditorGUILayout.Toggle("要求批次尺寸一致", settings.requireUniformSize);

        Vector4 border = settings.ToSpriteBorder();
        EditorGUILayout.LabelField(
            $"将写入 Border = (L={border.x}, B={border.y}, R={border.z}, T={border.w})",
            EditorStyles.miniLabel
        );

        if (EditorGUI.EndChangeCheck())
            settingsDirty = true;

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void SetUniformBorder(int value)
    {
        value = Mathf.Max(0, value);
        settings.borderLeft = value;
        settings.borderRight = value;
        settings.borderTop = value;
        settings.borderBottom = value;
    }

    private void DrawQuickRow(Action<int> apply)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            foreach (int value in BorderQuickChoices)
            {
                if (GUILayout.Button(value.ToString(), GUILayout.Height(20)))
                {
                    apply(value);
                    settingsDirty = true;
                }
            }
        }
    }

    private void DrawSelectionList()
    {
        showSelection = EditorGUILayout.BeginFoldoutHeaderGroup(
            showSelection,
            $"批次列表（{assetPaths.Count}）"
        );
        if (!showSelection)
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        EditorGUI.BeginChangeCheck();
        followSelection = EditorGUILayout.ToggleLeft(
            new GUIContent("跟随 Project 多选", "开启后列表始终等于当前勾选"),
            followSelection
        );
        if (EditorGUI.EndChangeCheck() && followSelection)
            SyncFromSelection();

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(followSelection))
            {
                if (GUILayout.Button("从选中同步", GUILayout.Height(24)))
                    SyncFromSelection();
                if (GUILayout.Button("追加选中", GUILayout.Height(24)))
                    AppendFromSelection();
            }

            if (GUILayout.Button("清空", GUILayout.Height(24)))
            {
                followSelection = false;
                assetPaths.Clear();
                highlightIndex = -1;
                ClearPreviewCache();
            }
        }

        listScroll = EditorGUILayout.BeginScrollView(listScroll, GUILayout.MinHeight(140f));
        for (int i = 0; i < assetPaths.Count; i++)
        {
            string path = assetPaths[i];
            TextureInfo info = BuildTextureInfo(path);

            using (new EditorGUILayout.HorizontalScope())
            {
                bool selected = highlightIndex == i;
                if (GUILayout.Toggle(selected, GUIContent.none, GUILayout.Width(18f)) != selected)
                    highlightIndex = selected ? -1 : i;

                string mode = info.ImportMode == SpriteImportMode.Multiple ? "Multiple!" : "Single";
                string label = $"{Path.GetFileName(path)}  {info.Width}×{info.Height}  [{mode}]";
                if (!info.BorderValid)
                    label += "  !";

                Color old = GUI.color;
                if (!info.BorderValid)
                    GUI.color = new Color(1f, 0.75f, 0.45f);
                else if (info.ImportMode == SpriteImportMode.Multiple)
                    GUI.color = new Color(1f, 0.85f, 0.55f);
                EditorGUILayout.LabelField(label);
                GUI.color = old;

                if (GUILayout.Button("×", GUILayout.Width(22f)))
                {
                    assetPaths.RemoveAt(i);
                    if (highlightIndex == i)
                        highlightIndex = -1;
                    else if (highlightIndex > i)
                        highlightIndex--;
                    break;
                }
            }
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawActions()
    {
        using (new EditorGUI.DisabledScope(isProcessing || assetPaths.Count == 0))
        {
            if (GUILayout.Button(isProcessing ? "处理中…" : "批量写入 9-Slice Border", GUILayout.Height(36)))
            {
                EditorApplication.delayCall -= ProcessBatchDeferred;
                EditorApplication.delayCall += ProcessBatchDeferred;
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("清空日志", GUILayout.Height(24)))
                logs.Clear();
            if (GUILayout.Button("保存设置", GUILayout.Height(24)))
                SaveSettings();
        }
    }

    private void DrawPreview()
    {
        EditorGUILayout.LabelField("叠虚像 + 九宫格边框预览（整图）", EditorStyles.boldLabel);

        Rect previewRect = GUILayoutUtility.GetRect(
            16f, 16f, 360f, 360f,
            GUILayout.ExpandWidth(true),
            GUILayout.MinHeight(360f)
        );

        EditorGUI.DrawRect(previewRect, new Color(0.12f, 0.12f, 0.12f, 1f));
        Handles.DrawSolidRectangleWithOutline(previewRect, Color.clear, new Color(0.35f, 0.35f, 0.35f, 1f));

        if (assetPaths.Count == 0)
        {
            GUI.Label(previewRect, "在 Project 勾选 Texture/Sprite", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        List<TextureInfo> infos = assetPaths.Select(BuildTextureInfo).ToList();
        int maxW = Mathf.Max(1, infos.Max(i => i.Width));
        int maxH = Mathf.Max(1, infos.Max(i => i.Height));

        float pad = 8f;
        float scale = Mathf.Min(
            (previewRect.width - pad * 2f) / maxW,
            (previewRect.height - pad * 2f) / maxH
        );
        scale = Mathf.Max(0.01f, scale);

        var canvas = new Rect(
            previewRect.x + (previewRect.width - maxW * scale) * 0.5f,
            previewRect.y + (previewRect.height - maxH * scale) * 0.5f,
            maxW * scale,
            maxH * scale
        );

        EditorGUI.DrawRect(canvas, new Color(0.18f, 0.18f, 0.18f, 1f));

        int count = assetPaths.Count;
        float perLayerAlpha = count <= 1
            ? settings.ghostAlpha
            : Mathf.Clamp(settings.ghostAlpha / Mathf.Sqrt(count), 0.08f, settings.ghostAlpha);

        for (int i = 0; i < count; i++)
        {
            Texture2D tex = GetPreviewTexture(assetPaths[i]);
            if (tex == null)
                continue;

            float alpha = highlightIndex == i
                ? Mathf.Clamp01(Mathf.Max(perLayerAlpha * 1.8f, settings.ghostAlpha))
                : perLayerAlpha;
            Color tint = count <= 1 ? Color.white : Color.HSVToRGB((i * 0.13f) % 1f, 0.22f, 1f);
            DrawTextureAlphaBlended(canvas, tex, maxW, maxH, alpha, tint);
        }

        DrawNineSliceGuides(canvas, maxW, maxH, scale);

        EditorGUILayout.LabelField(
            $"叠画 {count} 张（整图）  |  画布 {maxW}×{maxH}  |  缩放 {scale:0.###}  |  " +
            $"Border L{settings.borderLeft} B{settings.borderBottom} R{settings.borderRight} T{settings.borderTop}",
            EditorStyles.miniLabel
        );
    }

    private void DrawNineSliceGuides(Rect canvas, int textureW, int textureH, float scale)
    {
        int l = settings.borderLeft;
        int r = settings.borderRight;
        int t = settings.borderTop;
        int b = settings.borderBottom;

        Handles.BeginGUI();

        // 纹理原点在左下：guiY = canvas.yMax - texY * scale
        float guiBottom = canvas.y + canvas.height;
        float lineLeft = canvas.x + l * scale;
        float lineRight = canvas.x + (textureW - r) * scale;
        float lineBottom = guiBottom - b * scale;
        float lineTop = guiBottom - (textureH - t) * scale;

        var center = Rect.MinMaxRect(
            Mathf.Min(lineLeft, lineRight),
            Mathf.Min(lineTop, lineBottom),
            Mathf.Max(lineLeft, lineRight),
            Mathf.Max(lineTop, lineBottom)
        );

        Handles.DrawSolidRectangleWithOutline(
            center,
            new Color(1f, 0.82f, 0.25f, 0.08f),
            new Color(1f, 0.82f, 0.25f, 0.95f)
        );

        Handles.color = new Color(0.55f, 0.85f, 1f, 0.85f);
        Handles.DrawLine(new Vector3(lineLeft, guiBottom), new Vector3(lineLeft, canvas.y));
        Handles.DrawLine(new Vector3(lineRight, guiBottom), new Vector3(lineRight, canvas.y));
        Handles.DrawLine(new Vector3(canvas.x, lineBottom), new Vector3(canvas.xMax, lineBottom));
        Handles.DrawLine(new Vector3(canvas.x, lineTop), new Vector3(canvas.xMax, lineTop));

        // 左上角：不拉伸角块提示
        if (l > 0 && t > 0)
        {
            Handles.DrawSolidRectangleWithOutline(
                new Rect(canvas.x, canvas.y, l * scale, t * scale),
                new Color(1f, 0.35f, 0.25f, 0.12f),
                new Color(1f, 0.45f, 0.35f, 0.9f)
            );
        }

        Handles.EndGUI();
    }

    private void DrawTextureAlphaBlended(
        Rect canvas,
        Texture2D tex,
        int maxW,
        int maxH,
        float alpha,
        Color tint)
    {
        if (tex == null || Event.current.type != EventType.Repaint)
            return;

        FilterMode oldFilter = tex.filterMode;
        if (settings.nearestFilterPreview)
            tex.filterMode = FilterMode.Point;

        float scaleX = canvas.width / Mathf.Max(1, maxW);
        float scaleY = canvas.height / Mathf.Max(1, maxH);
        var dest = new Rect(
            canvas.x,
            canvas.y + canvas.height - tex.height * scaleY,
            tex.width * scaleX,
            tex.height * scaleY
        );

        Color drawColor = new Color(tint.r, tint.g, tint.b, Mathf.Clamp01(alpha));
        Graphics.DrawTexture(dest, tex, new Rect(0f, 0f, 1f, 1f), 0, 0, 0, 0, drawColor, GetPreviewAlphaMaterial());

        tex.filterMode = oldFilter;
    }

    private Material GetPreviewAlphaMaterial()
    {
        if (previewAlphaMaterial != null)
            return previewAlphaMaterial;

        Shader shader =
            Shader.Find("Hidden/Internal-GUITexture") ??
            Shader.Find("Unlit/Transparent") ??
            Shader.Find("Sprites/Default");

        previewAlphaMaterial = shader != null
            ? new Material(shader) { hideFlags = HideFlags.HideAndDontSave }
            : null;
        return previewAlphaMaterial;
    }

    private void DrawConflictSummary()
    {
        EditorGUILayout.LabelField("边框检查", EditorStyles.boldLabel);

        if (assetPaths.Count == 0)
        {
            EditorGUILayout.HelpBox("尚无批次资源。", MessageType.None);
            return;
        }

        List<TextureInfo> infos = assetPaths.Select(BuildTextureInfo).ToList();
        int invalid = infos.Count(i => !i.BorderValid);
        int multiple = infos.Count(i => i.ImportMode == SpriteImportMode.Multiple);
        bool uniform = infos.All(i => i.Width == infos[0].Width && i.Height == infos[0].Height);

        var sb = new StringBuilder();
        sb.AppendLine($"资源 {infos.Count}  |  Border 合法 {infos.Count - invalid}  |  当前 Multiple {multiple}");
        sb.AppendLine(uniform ? "尺寸：一致" : "尺寸：不一致（可分别检查边框是否越界）");
        sb.Append("效果：整图保留，写入 Sprite Border，供 Sliced 拉伸。");

        MessageType type = MessageType.Info;
        if (invalid > 0)
            type = MessageType.Error;
        else if (multiple > 0)
            type = MessageType.Warning;
        else if (!uniform && settings.requireUniformSize)
            type = MessageType.Error;

        EditorGUILayout.HelpBox(sb.ToString(), type);

        foreach (TextureInfo info in infos.Where(i => !i.BorderValid).Take(8))
            EditorGUILayout.LabelField($"· {Path.GetFileName(info.Path)}：{info.InvalidReason}", EditorStyles.miniLabel);
    }

    private void DrawLogs()
    {
        showLogs = EditorGUILayout.BeginFoldoutHeaderGroup(showLogs, $"日志（{logs.Count}）");
        if (!showLogs)
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        logScroll = EditorGUILayout.BeginScrollView(logScroll, GUILayout.MinHeight(120f));
        foreach (string log in logs)
            EditorGUILayout.LabelField(log, EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void SyncFromSelection()
    {
        assetPaths.Clear();
        highlightIndex = -1;
        ClearPreviewCache();
        foreach (string path in CollectTexturePathsFromSelection())
            assetPaths.Add(path);
        Repaint();
    }

    private void AppendFromSelection()
    {
        foreach (string path in CollectTexturePathsFromSelection())
        {
            if (!assetPaths.Contains(path, StringComparer.OrdinalIgnoreCase))
                assetPaths.Add(path);
        }

        Repaint();
    }

    private static List<string> CollectTexturePathsFromSelection()
    {
        var paths = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string guid in Selection.assetGUIDs)
            TryAddTexturePath(AssetDatabase.GUIDToAssetPath(guid), paths, seen);

        foreach (UnityEngine.Object obj in Selection.objects)
            TryAddTexturePath(ResolveTextureAssetPath(obj), paths, seen);

        foreach (Texture2D tex in Selection.GetFiltered<Texture2D>(SelectionMode.Assets))
            TryAddTexturePath(AssetDatabase.GetAssetPath(tex), paths, seen);

        foreach (Sprite sprite in Selection.GetFiltered<Sprite>(SelectionMode.Assets))
            TryAddTexturePath(ResolveTextureAssetPath(sprite), paths, seen);

        paths.Sort(StringComparer.OrdinalIgnoreCase);
        return paths;
    }

    private static void TryAddTexturePath(string path, List<string> paths, HashSet<string> seen)
    {
        if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path))
            return;
        if (AssetImporter.GetAtPath(path) is not TextureImporter)
            return;
        if (!seen.Add(path))
            return;
        paths.Add(path);
    }

    private void TryInferEqualThirdsFromFirst()
    {
        if (assetPaths.Count == 0)
        {
            EditorUtility.DisplayDialog("Nine Grid 9-Slice", "请先选中至少一张图。", "确定");
            return;
        }

        TextureInfo info = BuildTextureInfo(assetPaths[0]);
        if (info.Width < 3 || info.Height < 3)
        {
            EditorUtility.DisplayDialog("Nine Grid 9-Slice", "首图太小。", "确定");
            return;
        }

        settings.linkBorders = false;
        settings.borderLeft = info.Width / 3;
        settings.borderRight = info.Width / 3;
        settings.borderTop = info.Height / 3;
        settings.borderBottom = info.Height / 3;
        settingsDirty = true;
        Repaint();
    }

    private void TryLoadBorderFromFirst()
    {
        if (assetPaths.Count == 0)
            return;

        TextureImporter importer = AssetImporter.GetAtPath(assetPaths[0]) as TextureImporter;
        if (importer == null)
            return;

        Vector4 b = importer.spriteBorder;
        settings.borderLeft = Mathf.RoundToInt(b.x);
        settings.borderBottom = Mathf.RoundToInt(b.y);
        settings.borderRight = Mathf.RoundToInt(b.z);
        settings.borderTop = Mathf.RoundToInt(b.w);
        settings.linkBorders =
            settings.borderLeft == settings.borderRight &&
            settings.borderLeft == settings.borderTop &&
            settings.borderLeft == settings.borderBottom;
        settingsDirty = true;
        Repaint();
    }

    private void ProcessBatchDeferred()
    {
        if (this == null || isProcessing)
            return;

        isProcessing = true;
        SaveSettings();

        try
        {
            ProcessBatch();
        }
        finally
        {
            isProcessing = false;
            EditorUtility.ClearProgressBar();
            if (this != null)
            {
                ClearPreviewCache();
                Repaint();
            }
        }
    }

    private void ProcessBatch()
    {
        logs.Clear();
        if (assetPaths.Count == 0)
        {
            AddLog("[错误] 批次为空。");
            return;
        }

        List<TextureInfo> infos = assetPaths.Select(BuildTextureInfo).ToList();
        if (settings.requireUniformSize &&
            !infos.All(i => i.Width == infos[0].Width && i.Height == infos[0].Height))
        {
            AddLog("[错误] 批次尺寸不一致，已中止。");
            return;
        }

        int processed = 0;
        int skipped = 0;
        int errors = 0;
        Vector4 border = settings.ToSpriteBorder();

        AssetDatabase.StartAssetEditing();
        try
        {
            for (int i = 0; i < assetPaths.Count; i++)
            {
                string path = assetPaths[i];
                if (EditorUtility.DisplayCancelableProgressBar(
                        "Nine Grid 9-Slice",
                        $"写入 Border {i + 1}/{assetPaths.Count}\n{path}",
                        (i + 1f) / assetPaths.Count))
                {
                    AddLog("[取消] 用户中止。");
                    break;
                }

                TextureInfo info = BuildTextureInfo(path);
                if (!info.BorderValid)
                {
                    errors++;
                    AddLog($"[错误] {path}：{info.InvalidReason}");
                    continue;
                }

                try
                {
                    if (ApplyNineSliceBorder(path, border))
                    {
                        processed++;
                        AddLog($"[处理] {path} → Border (L{border.x}, B{border.y}, R{border.z}, T{border.w})  Single");
                    }
                    else
                    {
                        skipped++;
                        AddLog($"[跳过] 已是目标状态：{path}");
                    }
                }
                catch (Exception ex)
                {
                    errors++;
                    AddLog($"[错误] {path}");
                    AddLog(ex.Message);
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
        }

        AddLog("");
        AddLog($"完成：处理 {processed}，跳过 {skipped}，错误 {errors}。");
        AddLog("提示：UGUI Image 请设 Type = Sliced 才会按九宫格拉伸。");
    }

    private bool ApplyNineSliceBorder(string assetPath, Vector4 border)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
            return false;

        bool changed = false;

        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            changed = true;
        }

        if (settings.forceSingleSprite && importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.spriteImportMode = SpriteImportMode.Single;
            // 清掉误切的 spritesheet，恢复整图。
            if (importer.spritesheet != null && importer.spritesheet.Length > 0)
                importer.spritesheet = Array.Empty<SpriteMetaData>();
            changed = true;
        }

        if (importer.spriteBorder != border)
        {
            importer.spriteBorder = border;
            changed = true;
        }

        if (settings.applyPixelsPerUnit &&
            !Mathf.Approximately(importer.spritePixelsPerUnit, settings.pixelsPerUnit))
        {
            importer.spritePixelsPerUnit = settings.pixelsPerUnit;
            changed = true;
        }

        // Pivot：Single 模式下用 textureSettings / spritePivot
        TextureImporterSettings texSettings = new TextureImporterSettings();
        importer.ReadTextureSettings(texSettings);
        if (texSettings.spriteAlignment != (int)settings.pivotAlignment)
        {
            texSettings.spriteAlignment = (int)settings.pivotAlignment;
            changed = true;
        }

        Vector2 pivot = settings.pivotAlignment == SpriteAlignment.Custom
            ? settings.customPivot
            : GetPivotForAlignment(settings.pivotAlignment);
        if (texSettings.spritePivot != pivot)
        {
            texSettings.spritePivot = pivot;
            changed = true;
        }

        if (changed)
        {
            importer.SetTextureSettings(texSettings);
            // 再写一次 border：部分 Unity 版本 SetTextureSettings 会覆盖
            importer.spriteBorder = border;
            if (settings.forceSingleSprite)
                importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
        }

        return changed;
    }

    private static Vector2 GetPivotForAlignment(SpriteAlignment alignment)
    {
        return alignment switch
        {
            SpriteAlignment.Center => new Vector2(0.5f, 0.5f),
            SpriteAlignment.TopLeft => new Vector2(0f, 1f),
            SpriteAlignment.TopCenter => new Vector2(0.5f, 1f),
            SpriteAlignment.TopRight => new Vector2(1f, 1f),
            SpriteAlignment.LeftCenter => new Vector2(0f, 0.5f),
            SpriteAlignment.RightCenter => new Vector2(1f, 0.5f),
            SpriteAlignment.BottomLeft => new Vector2(0f, 0f),
            SpriteAlignment.BottomCenter => new Vector2(0.5f, 0f),
            SpriteAlignment.BottomRight => new Vector2(1f, 0f),
            _ => new Vector2(0.5f, 0.5f)
        };
    }

    private TextureInfo BuildTextureInfo(string assetPath)
    {
        var info = new TextureInfo
        {
            Path = assetPath,
            ImportMode = SpriteImportMode.Single,
            BorderValid = false,
            InvalidReason = "无法读取"
        };

        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            importer.GetSourceTextureWidthAndHeight(out info.Width, out info.Height);
            info.ImportMode = importer.spriteImportMode;
            info.CurrentBorder = importer.spriteBorder;
        }
        else
        {
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (tex == null)
                return info;
            info.Width = tex.width;
            info.Height = tex.height;
        }

        int l = settings.borderLeft;
        int r = settings.borderRight;
        int t = settings.borderTop;
        int b = settings.borderBottom;

        if (l + r >= info.Width)
        {
            info.InvalidReason = $"Left+Right ({l}+{r}) >= 宽 {info.Width}";
            return info;
        }

        if (t + b >= info.Height)
        {
            info.InvalidReason = $"Top+Bottom ({t}+{b}) >= 高 {info.Height}";
            return info;
        }

        info.BorderValid = true;
        info.InvalidReason = null;
        return info;
    }

    private Texture2D GetPreviewTexture(string assetPath)
    {
        if (previewCache.TryGetValue(assetPath, out Texture2D cached) && cached != null)
            return cached;

        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        if (tex != null)
            previewCache[assetPath] = tex;
        return tex;
    }

    private void ClearPreviewCache() => previewCache.Clear();

    private static string ResolveTextureAssetPath(UnityEngine.Object obj)
    {
        if (obj == null)
            return null;

        string path = AssetDatabase.GetAssetPath(obj);
        if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path))
            return null;

        if (AssetImporter.GetAtPath(path) is TextureImporter)
            return path;

        if (obj is Sprite sprite && sprite.texture != null)
        {
            string texPath = AssetDatabase.GetAssetPath(sprite.texture);
            if (!string.IsNullOrEmpty(texPath) && AssetImporter.GetAtPath(texPath) is TextureImporter)
                return texPath;
        }

        return null;
    }

    private void AddLog(string message)
    {
        if (this == null)
            return;
        logs.Add(message);
    }

    private void LoadSettings()
    {
        string json = EditorPrefs.GetString(PrefsKey, string.Empty);
        if (string.IsNullOrEmpty(json))
        {
            settings = BatchSettings.CreateDefault();
            return;
        }

        try
        {
            settings = JsonUtility.FromJson<BatchSettings>(json) ?? BatchSettings.CreateDefault();
        }
        catch
        {
            settings = BatchSettings.CreateDefault();
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
