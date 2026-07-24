#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using NineGrid.Cards;
using NineGrid.Cards.Presentation;
using NineGrid.Presentation.Editor;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 像素怪物合集帧动画预览：体型对比 + 卡面适配（联合包围盒居中 / 比对框 / catalog）。
/// </summary>
public sealed class PixelMonsterSpriteAnimPreviewWindow : EditorWindow
{
    private const string MenuPath = "Tools/Pixel Art/Monster Sprite Anim Preview";
    private const string DefaultSpritesRoot = "Assets/Arts/Images/Png/像素怪物合集/sprites";
    private const string PrefsRootKey = "TableNine.MonsterSpriteAnimPreview.Root";
    private const string PrefsFpsKey = "TableNine.MonsterSpriteAnimPreview.Fps";
    private const string PrefsZoomKey = "TableNine.MonsterSpriteAnimPreview.Zoom";
    private const string PrefsSearchKey = "TableNine.MonsterSpriteAnimPreview.Search";
    private const string PrefsModeKey = "TableNine.MonsterSpriteAnimPreview.Mode";
    private const string PrefsSlotWKey = "TableNine.MonsterSpriteAnimPreview.SlotW";
    private const string PrefsSlotHKey = "TableNine.MonsterSpriteAnimPreview.SlotH";
    private const string PrefsSlackKey = "TableNine.MonsterSpriteAnimPreview.Slack";
    private const string PrefsAlphaKey = "TableNine.MonsterSpriteAnimPreview.Alpha";
    private const float DefaultSpritePpu = 32f;

    private static readonly Color CanvasBg = new(0.14f, 0.15f, 0.17f, 1f);
    private static readonly Color CheckerA = new(0.18f, 0.19f, 0.21f, 1f);
    private static readonly Color CheckerB = new(0.22f, 0.23f, 0.26f, 1f);
    private static readonly Color GroundLine = new(0.95f, 0.55f, 0.25f, 0.95f);
    private static readonly Color GuideLine = new(0.45f, 0.75f, 1f, 0.55f);
    private static readonly Color CenterLine = new(1f, 1f, 1f, 0.18f);
    private static readonly Color SelectionBg = new(0.24f, 0.42f, 0.72f, 0.55f);
    private static readonly Color UsableBg = new(0.15f, 0.38f, 0.22f, 0.45f);
    private static readonly Color CriticalBg = new(0.42f, 0.36f, 0.12f, 0.5f);
    private static readonly Color UnusableBg = new(0.42f, 0.16f, 0.16f, 0.5f);
    private static readonly Color SlotFill = new(0.2f, 0.55f, 0.95f, 0.12f);
    private static readonly Color SlotBorder = new(0.35f, 0.75f, 1f, 0.95f);
    private static readonly Color SlackBorder = new(0.95f, 0.85f, 0.35f, 0.7f);
    private static readonly Color UnionOutline = new(0.95f, 0.45f, 0.85f, 0.9f);
    private static readonly int[] HeightGuides = { 32, 48, 64, 96, 128 };

    private enum PreviewMode
    {
        BodyCompare = 0,
        CardFit = 1,
    }

    private enum FitListFilter
    {
        All = 0,
        Usable = 1,
        Unusable = 2,
        Missing = 3,
    }

    [SerializeField] private string spritesRoot = DefaultSpritesRoot;
    [SerializeField] private string searchQuery = string.Empty;
    [SerializeField] private int selectedIndex;
    [SerializeField] private float fps = 8f;
    [SerializeField] private float zoom = 3f;
    [SerializeField] private bool playing = true;
    [SerializeField] private bool loop = true;
    [SerializeField] private bool showGuides = true;
    [SerializeField] private bool showChecker = true;
    [SerializeField] private bool pingOnSelect;
    [SerializeField] private Vector2 listScroll;
    [SerializeField] private int scrubFrame;
    [SerializeField] private PreviewMode previewMode = PreviewMode.CardFit;
    [SerializeField] private int slotW = SpriteClipCardFitCatalog.DefaultSlotW;
    [SerializeField] private int slotH = SpriteClipCardFitCatalog.DefaultSlotH;
    [SerializeField] private float slackPx = SpriteClipCardFitCatalog.DefaultSlackPx;
    [SerializeField] private int alphaThreshold = SpriteClipCardFitCatalog.DefaultAlphaThreshold;
    [SerializeField] private bool writePerClipFiles;
    [SerializeField] private bool showUnionOutline = true;
    [SerializeField] private FitListFilter fitListFilter = FitListFilter.All;

    private readonly List<ClipInfo> allClips = new();
    private readonly List<int> filteredIndices = new();
    private readonly List<Texture2D> currentFrames = new();
    private readonly List<string> currentFramePaths = new();
    private readonly List<Sprite> currentSprites = new();

    private string loadedClipFolder = string.Empty;
    private double lastFrameTime;
    private int playFrame;
    private string statusMessage = string.Empty;
    private bool needsRescan = true;
    private GUIStyle clipNameStyle;
    private GUIStyle metaStyle;
    private int listFocusIndex = -1;
    private bool wantFocusSearch;
    private bool isScanning;
    private SpriteClipCardFitCatalog.CatalogRoot catalog;
    private CardFacePreviewHost cardFaceHost;
    private SpriteRenderer cardIconRenderer;
    private Transform cardIconTransform;
    private bool cardFaceLive;

    private sealed class ClipInfo
    {
        public string FolderName;
        public string FolderPath;
        public int FrameCount;
        public int Width;
        public int Height;
    }

    [MenuItem(MenuPath)]
    public static void Open()
    {
        var window = GetWindow<PixelMonsterSpriteAnimPreviewWindow>();
        window.titleContent = new GUIContent("Monster Anim Preview");
        window.minSize = new Vector2(920f, 600f);
        window.Show();
        window.Focus();
    }

    private void OnEnable()
    {
        spritesRoot = EditorPrefs.GetString(PrefsRootKey, DefaultSpritesRoot);
        fps = EditorPrefs.GetFloat(PrefsFpsKey, 8f);
        zoom = EditorPrefs.GetFloat(PrefsZoomKey, 3f);
        searchQuery = EditorPrefs.GetString(PrefsSearchKey, string.Empty);
        previewMode = (PreviewMode)EditorPrefs.GetInt(PrefsModeKey, (int)PreviewMode.CardFit);
        slotW = EditorPrefs.GetInt(PrefsSlotWKey, SpriteClipCardFitCatalog.DefaultSlotW);
        slotH = EditorPrefs.GetInt(PrefsSlotHKey, SpriteClipCardFitCatalog.DefaultSlotH);
        slackPx = EditorPrefs.GetFloat(PrefsSlackKey, SpriteClipCardFitCatalog.DefaultSlackPx);
        alphaThreshold = EditorPrefs.GetInt(PrefsAlphaKey, SpriteClipCardFitCatalog.DefaultAlphaThreshold);
        needsRescan = true;
        ReloadCatalog();
        EditorApplication.update += OnEditorUpdate;
        wantFocusSearch = true;
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorPrefs.SetString(PrefsRootKey, spritesRoot ?? DefaultSpritesRoot);
        EditorPrefs.SetFloat(PrefsFpsKey, fps);
        EditorPrefs.SetFloat(PrefsZoomKey, zoom);
        EditorPrefs.SetString(PrefsSearchKey, searchQuery ?? string.Empty);
        EditorPrefs.SetInt(PrefsModeKey, (int)previewMode);
        EditorPrefs.SetInt(PrefsSlotWKey, slotW);
        EditorPrefs.SetInt(PrefsSlotHKey, slotH);
        EditorPrefs.SetFloat(PrefsSlackKey, slackPx);
        EditorPrefs.SetInt(PrefsAlphaKey, alphaThreshold);
        DisposeCardFacePreview();
        UnloadCurrentFrames();
    }

    private void OnEditorUpdate()
    {
        if (playing && currentFrames.Count > 0)
        {
            double now = EditorApplication.timeSinceStartup;
            double interval = 1.0 / Math.Max(1.0, fps);
            if (now - lastFrameTime >= interval)
            {
                lastFrameTime = now;
                playFrame++;
                if (playFrame >= currentFrames.Count)
                    playFrame = loop ? 0 : currentFrames.Count - 1;
                scrubFrame = playFrame;
                PushCardFaceFrame();
                Repaint();
            }
        }
        else if (cardFaceLive)
        {
            PushCardFaceFrame();
        }
    }

    private void OnGUI()
    {
        EnsureStyles();
        HandleKeyboard();

        if (needsRescan)
        {
            RescanClips();
            needsRescan = false;
        }

        DrawToolbar();
        if (previewMode == PreviewMode.CardFit)
            DrawCardFitToolbar();

        EditorGUILayout.Space(4f);

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawClipList(GUILayout.Width(Mathf.Clamp(position.width * 0.34f, 280f, 440f)));
            DrawPreviewPane();
        }

        DrawStatusBar();
    }

    private void EnsureStyles()
    {
        if (clipNameStyle != null)
            return;

        clipNameStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 14,
            wordWrap = true,
            alignment = TextAnchor.MiddleLeft,
        };
        metaStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            richText = true,
            wordWrap = true,
        };
    }

    private void HandleKeyboard()
    {
        Event e = Event.current;
        if (e.type != EventType.KeyDown)
            return;

        if (EditorGUIUtility.editingTextField && e.keyCode != KeyCode.Escape)
            return;

        bool used = false;
        switch (e.keyCode)
        {
            case KeyCode.LeftArrow:
            case KeyCode.A:
            case KeyCode.UpArrow:
            case KeyCode.W:
                SelectRelative(-1);
                used = true;
                break;
            case KeyCode.RightArrow:
            case KeyCode.D:
            case KeyCode.DownArrow:
            case KeyCode.S:
                SelectRelative(1);
                used = true;
                break;
            case KeyCode.Space:
                playing = !playing;
                if (playing)
                    lastFrameTime = EditorApplication.timeSinceStartup;
                used = true;
                break;
            case KeyCode.Home:
                if (filteredIndices.Count > 0)
                    SelectFilteredIndex(0);
                used = true;
                break;
            case KeyCode.End:
                if (filteredIndices.Count > 0)
                    SelectFilteredIndex(filteredIndices.Count - 1);
                used = true;
                break;
            case KeyCode.PageUp:
                SelectRelative(-10);
                used = true;
                break;
            case KeyCode.PageDown:
                SelectRelative(10);
                used = true;
                break;
            case KeyCode.F:
                if (e.control || e.command)
                {
                    wantFocusSearch = true;
                    used = true;
                }
                break;
            case KeyCode.Escape:
                if (!string.IsNullOrEmpty(searchQuery))
                {
                    searchQuery = string.Empty;
                    RebuildFilter();
                    used = true;
                }
                break;
            case KeyCode.Tab:
                previewMode = previewMode == PreviewMode.CardFit ? PreviewMode.BodyCompare : PreviewMode.CardFit;
                used = true;
                break;
        }

        if (used)
        {
            e.Use();
            GUI.FocusControl(null);
            Repaint();
        }
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("Rescan", EditorStyles.toolbarButton, GUILayout.Width(56f)))
            {
                needsRescan = true;
                GUI.FocusControl(null);
            }

            if (GUILayout.Button("Root…", EditorStyles.toolbarButton, GUILayout.Width(48f)))
            {
                string picked = EditorUtility.OpenFolderPanel("Sprites Root", AbsoluteFromAssetPath(spritesRoot), string.Empty);
                if (!string.IsNullOrEmpty(picked))
                {
                    string assetPath = AbsoluteToAssetPath(picked);
                    if (!string.IsNullOrEmpty(assetPath))
                    {
                        spritesRoot = assetPath.Replace('\\', '/');
                        needsRescan = true;
                        ReloadCatalog();
                    }
                    else
                    {
                        statusMessage = "Root must be inside the project Assets folder.";
                    }
                }
            }

            EditorGUI.BeginChangeCheck();
            previewMode = (PreviewMode)EditorGUILayout.EnumPopup(previewMode, EditorStyles.toolbarPopup, GUILayout.Width(110f));
            if (EditorGUI.EndChangeCheck())
                GUI.FocusControl(null);

            GUILayout.Space(6f);
            GUILayout.Label("Search", GUILayout.Width(44f));

            GUI.SetNextControlName("MonsterAnimSearch");
            string nextSearch = GUILayout.TextField(searchQuery ?? string.Empty, EditorStyles.toolbarSearchField, GUILayout.MinWidth(140f));
            if (!string.Equals(nextSearch, searchQuery, StringComparison.Ordinal))
            {
                searchQuery = nextSearch;
                RebuildFilter();
            }

            if (wantFocusSearch && Event.current.type == EventType.Repaint)
            {
                GUI.FocusControl("MonsterAnimSearch");
                wantFocusSearch = false;
            }

            if (GUILayout.Button("×", EditorStyles.toolbarButton, GUILayout.Width(22f)) && !string.IsNullOrEmpty(searchQuery))
            {
                searchQuery = string.Empty;
                RebuildFilter();
                GUI.FocusControl(null);
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label($"{filteredIndices.Count} / {allClips.Count} clips", EditorStyles.miniLabel);
            if (catalog != null)
                GUILayout.Label($"  · catalog {catalog.usableCount}/{catalog.clipCount} usable", EditorStyles.miniLabel);
        }
    }

    private void DrawCardFitToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUILayout.Label("Slot", GUILayout.Width(28f));
            slotW = EditorGUILayout.IntField(slotW, GUILayout.Width(36f));
            GUILayout.Label("×", GUILayout.Width(12f));
            slotH = EditorGUILayout.IntField(slotH, GUILayout.Width(36f));
            GUILayout.Label("Slack", GUILayout.Width(36f));
            slackPx = EditorGUILayout.FloatField(slackPx, GUILayout.Width(40f));
            GUILayout.Label("α≥", GUILayout.Width(22f));
            alphaThreshold = EditorGUILayout.IntField(alphaThreshold, GUILayout.Width(32f));
            writePerClipFiles = GUILayout.Toggle(writePerClipFiles, "Per-clip JSON", EditorStyles.toolbarButton, GUILayout.Width(92f));

            EditorGUI.BeginChangeCheck();
            fitListFilter = (FitListFilter)EditorGUILayout.EnumPopup(fitListFilter, EditorStyles.toolbarPopup, GUILayout.Width(88f));
            if (EditorGUI.EndChangeCheck())
                RebuildFilter();

            GUILayout.FlexibleSpace();

            EditorGUI.BeginDisabledGroup(isScanning);
            if (GUILayout.Button("Scan All", EditorStyles.toolbarButton, GUILayout.Width(64f)))
                RunCatalogScan(incremental: false);
            if (GUILayout.Button("Scan Dirty", EditorStyles.toolbarButton, GUILayout.Width(72f)))
                RunCatalogScan(incremental: true);
            if (GUILayout.Button("Reload JSON", EditorStyles.toolbarButton, GUILayout.Width(80f)))
            {
                ReloadCatalog();
                RebuildFilter();
            }
            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button("Ping Face Prefab", EditorStyles.toolbarButton, GUILayout.Width(108f)))
                PingMonsterFacePrefab();

            bool live = GUILayout.Toggle(cardFaceLive, "Live Card Face", EditorStyles.toolbarButton, GUILayout.Width(100f));
            if (live != cardFaceLive)
            {
                if (live)
                    EnsureCardFacePreview();
                else
                    DisposeCardFacePreview();
            }
        }
    }

    private void DrawClipList(params GUILayoutOption[] options)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, options))
        {
            GUILayout.Label(
                previewMode == PreviewMode.CardFit ? "Clips · Card Fit" : "Clips (directory order)",
                EditorStyles.boldLabel);

            listScroll = EditorGUILayout.BeginScrollView(listScroll);
            const float rowHeight = 20f;
            int filteredCount = filteredIndices.Count;

            for (int i = 0; i < filteredCount; i++)
            {
                int clipIndex = filteredIndices[i];
                ClipInfo clip = allClips[clipIndex];
                bool selected = clipIndex == selectedIndex;
                SpriteClipCardFitCatalog.ClipEntry entry = FindCatalogEntry(clip.FolderName);

                Rect row = GUILayoutUtility.GetRect(0f, rowHeight, GUILayout.ExpandWidth(true));
                if (selected)
                    EditorGUI.DrawRect(row, SelectionBg);
                else if (previewMode == PreviewMode.CardFit)
                    EditorGUI.DrawRect(row, FitRowColor(entry));
                else if (i % 2 == 1)
                    EditorGUI.DrawRect(row, new Color(0f, 0f, 0f, 0.08f));

                string label = clip.FolderName + "  ·  " + clip.FrameCount + "f";
                if (previewMode == PreviewMode.CardFit)
                {
                    if (entry != null)
                        label += $"  ov={entry.maxOverflow:0.#}  sc={entry.score:0.00}" + (entry.usable ? "  OK" : "  NO");
                    else
                        label += "  (no catalog)";
                }
                else if (clip.Width > 0)
                {
                    label += $"  {clip.Width}×{clip.Height}";
                }

                if (GUI.Button(row, label, EditorStyles.label))
                {
                    SelectClipIndex(clipIndex);
                    GUI.FocusControl(null);
                }

                if (selected && listFocusIndex != i && Event.current.type == EventType.Repaint)
                {
                    float viewTop = listScroll.y;
                    float viewBottom = listScroll.y + position.height - 180f;
                    float rowTop = i * rowHeight;
                    if (rowTop < viewTop)
                        listScroll.y = rowTop;
                    else if (rowTop + rowHeight > viewBottom)
                        listScroll.y = rowTop + rowHeight - Math.Max(40f, position.height - 180f);
                    listFocusIndex = i;
                }
            }

            EditorGUILayout.EndScrollView();
        }
    }

    private static Color FitRowColor(SpriteClipCardFitCatalog.ClipEntry entry)
    {
        if (entry == null)
            return new Color(0.2f, 0.2f, 0.25f, 0.35f);
        if (entry.usable)
            return UsableBg;
        if (entry.maxOverflow <= entry.slotW * 0.25f)
            return CriticalBg;
        return UnusableBg;
    }

    private void DrawPreviewPane()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            if (allClips.Count == 0 || selectedIndex < 0 || selectedIndex >= allClips.Count)
            {
                EditorGUILayout.HelpBox("No clips found under the sprites root.", MessageType.Info);
                return;
            }

            ClipInfo clip = allClips[selectedIndex];
            EnsureFramesLoaded(clip);
            SpriteClipCardFitCatalog.ClipEntry entry = FindCatalogEntry(clip.FolderName);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(clip.FolderName, clipNameStyle);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("◀ Prev", GUILayout.Width(64f)))
                    SelectRelative(-1);
                if (GUILayout.Button("Next ▶", GUILayout.Width(64f)))
                    SelectRelative(1);
                if (GUILayout.Button("Ping", GUILayout.Width(44f)))
                    PingCurrentClip();
            }

            GUILayout.Label(
                $"Frame <b>{DisplayFrame + 1}</b> / {currentFrames.Count}   ·   " +
                $"Size <b>{clip.Width}×{clip.Height}</b>   ·   " +
                $"Index <b>{FilteredOrdinal + 1}</b> / {filteredIndices.Count}",
                metaStyle);

            if (previewMode == PreviewMode.CardFit)
                DrawCardFitMeta(entry);

            DrawPlaybackControls();
            if (previewMode == PreviewMode.CardFit)
                DrawPreviewCanvasCardFit(entry);
            else
                DrawPreviewCanvasBody();

            if (cardFaceLive && cardFaceHost != null)
            {
                EditorGUILayout.Space(4f);
                GUILayout.Label("Live Monster Card Face", EditorStyles.boldLabel);
                Rect faceRect = GUILayoutUtility.GetRect(220f, 280f, GUILayout.ExpandWidth(true));
                cardFaceHost.Draw(faceRect);
            }

            DrawFooterHints();
        }
    }

    private void DrawCardFitMeta(SpriteClipCardFitCatalog.ClipEntry entry)
    {
        if (entry == null)
        {
            EditorGUILayout.HelpBox("No catalog entry. Run Scan All / Scan Dirty.", MessageType.Warning);
            return;
        }

        string reasons = entry.reasons != null && entry.reasons.Length > 0
            ? string.Join(", ", entry.reasons)
            : "-";
        GUILayout.Label(
            $"Union <b>{entry.opaqueUnion?.width:0.#}×{entry.opaqueUnion?.height:0.#}</b>   ·   " +
            $"Offset <b>({entry.offsetPx?.x:0.#}, {entry.offsetPx?.y:0.#})</b>   ·   " +
            $"Overflow LRTB <b>{entry.overflowPx?.l:0.#}/{entry.overflowPx?.r:0.#}/{entry.overflowPx?.t:0.#}/{entry.overflowPx?.b:0.#}</b>   ·   " +
            $"max <b>{entry.maxOverflow:0.#}</b>   ·   fill <b>{entry.fillRatio:0.00}</b>   ·   " +
            (entry.usable ? "<color=#7dffa0>usable</color>" : "<color=#ff8a8a>unusable</color>") +
            $"   ·   score <b>{entry.score:0.00}</b>   ·   suggestScale <b>{entry.suggestedUniformScale:0.###}</b>\n" +
            $"reasons: {reasons}",
            metaStyle);
    }

    private void DrawPlaybackControls()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(playing ? "❚❚" : "▶", GUILayout.Width(36f)))
            {
                playing = !playing;
                if (playing)
                    lastFrameTime = EditorApplication.timeSinceStartup;
            }

            loop = GUILayout.Toggle(loop, "Loop", GUILayout.Width(48f));
            showChecker = GUILayout.Toggle(showChecker, "Checker", GUILayout.Width(64f));
            if (previewMode == PreviewMode.BodyCompare)
                showGuides = GUILayout.Toggle(showGuides, "Guides", GUILayout.Width(58f));
            else
                showUnionOutline = GUILayout.Toggle(showUnionOutline, "Union", GUILayout.Width(52f));
            pingOnSelect = GUILayout.Toggle(pingOnSelect, "Auto Ping", GUILayout.Width(72f));

            GUILayout.FlexibleSpace();
            GUILayout.Label("FPS", GUILayout.Width(28f));
            fps = EditorGUILayout.Slider(fps, 1f, 30f, GUILayout.Width(140f));
            GUILayout.Label("Zoom", GUILayout.Width(36f));
            zoom = EditorGUILayout.Slider(zoom, 1f, 8f, GUILayout.Width(120f));
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            int max = Math.Max(0, currentFrames.Count - 1);
            EditorGUI.BeginChangeCheck();
            int next = EditorGUILayout.IntSlider("Scrub", scrubFrame, 0, max);
            if (EditorGUI.EndChangeCheck())
            {
                scrubFrame = next;
                playFrame = next;
                playing = false;
                PushCardFaceFrame();
            }
        }
    }

    private void DrawPreviewCanvasBody()
    {
        float stageWidth = Mathf.Max(280f, position.width * 0.58f);
        float stageHeight = Mathf.Clamp(position.height - 240f, 280f, 520f);
        Rect stage = GUILayoutUtility.GetRect(stageWidth, stageHeight, GUILayout.ExpandWidth(true));
        if (Event.current.type != EventType.Repaint)
            return;

        EditorGUI.DrawRect(stage, CanvasBg);
        if (showChecker)
            DrawChecker(stage, 12f);

        float z = Mathf.Max(1f, zoom);
        float groundY = stage.yMax - 36f;
        float centerX = stage.center.x;

        Handles.BeginGUI();
        Handles.color = CenterLine;
        Handles.DrawLine(new Vector3(centerX, stage.yMin + 8f), new Vector3(centerX, groundY));
        if (showGuides)
        {
            Handles.color = GuideLine;
            foreach (int h in HeightGuides)
            {
                float y = groundY - h * z;
                if (y < stage.yMin + 4f)
                    continue;
                Handles.DrawLine(new Vector3(stage.xMin + 10f, y), new Vector3(stage.xMax - 10f, y));
                GUI.Label(new Rect(stage.xMin + 12f, y - 12f, 48f, 14f), $"{h}px", EditorStyles.miniLabel);
            }
        }

        Handles.color = GroundLine;
        Handles.DrawLine(new Vector3(stage.xMin + 8f, groundY), new Vector3(stage.xMax - 8f, groundY));
        Handles.EndGUI();

        Texture2D tex = CurrentTexture;
        if (tex == null)
            return;

        float drawW = tex.width * z;
        float drawH = tex.height * z;
        var dest = new Rect(centerX - drawW * 0.5f, groundY - drawH, drawW, drawH);
        DrawTexturePoint(stage, dest, tex);
        GUI.Label(new Rect(stage.xMin + 8f, stage.yMin + 6f, 180f, 16f), $"{tex.width}×{tex.height} @ ×{z:0.#}", EditorStyles.miniLabel);
    }

    private void DrawPreviewCanvasCardFit(SpriteClipCardFitCatalog.ClipEntry entry)
    {
        float stageWidth = Mathf.Max(280f, position.width * 0.58f);
        float stageHeight = Mathf.Clamp(position.height - 280f, 280f, 520f);
        Rect stage = GUILayoutUtility.GetRect(stageWidth, stageHeight, GUILayout.ExpandWidth(true));
        if (Event.current.type != EventType.Repaint)
            return;

        EditorGUI.DrawRect(stage, CanvasBg);
        if (showChecker)
            DrawChecker(stage, 12f);

        float z = Mathf.Max(1f, zoom);
        float cx = stage.center.x;
        float cy = stage.center.y;

        int sw = entry?.slotW > 0 ? entry.slotW : Math.Max(1, slotW);
        int sh = entry?.slotH > 0 ? entry.slotH : Math.Max(1, slotH);
        float ox = entry?.offsetPx?.x ?? 0f;
        float oy = entry?.offsetPx?.y ?? 0f;

        // Card slot centered on stage (GUI Y down).
        var slotRect = new Rect(cx - sw * z * 0.5f, cy - sh * z * 0.5f, sw * z, sh * z);
        EditorGUI.DrawRect(slotRect, SlotFill);
        DrawRectOutline(slotRect, SlotBorder, 2f);

        if (slackPx > 0.01f)
        {
            var slackRect = new Rect(
                cx - (sw * 0.5f + slackPx) * z,
                cy - (sh * 0.5f + slackPx) * z,
                (sw + slackPx * 2f) * z,
                (sh + slackPx * 2f) * z);
            DrawRectOutline(slackRect, SlackBorder, 1f);
        }

        Handles.BeginGUI();
        Handles.color = CenterLine;
        Handles.DrawLine(new Vector3(cx, stage.yMin + 6f), new Vector3(cx, stage.yMax - 6f));
        Handles.DrawLine(new Vector3(stage.xMin + 6f, cy), new Vector3(stage.xMax - 6f, cy));
        Handles.color = SlotBorder;
        Handles.DrawSolidDisc(new Vector3(cx, cy), Vector3.forward, 3f);
        Handles.EndGUI();

        Texture2D tex = CurrentTexture;
        if (tex != null)
        {
            // Texture center sits at card center + clip offset (Y flipped for GUI).
            float texCx = cx + ox * z;
            float texCy = cy - oy * z;
            float drawW = tex.width * z;
            float drawH = tex.height * z;
            var dest = new Rect(texCx - drawW * 0.5f, texCy - drawH * 0.5f, drawW, drawH);
            DrawTexturePoint(stage, dest, tex);
        }

        if (showUnionOutline && entry?.opaqueUnion != null && entry.opaqueUnion.width > 0f)
        {
            float uminX = entry.opaqueUnion.minX + ox;
            float umaxX = entry.opaqueUnion.maxX + ox;
            float uminY = entry.opaqueUnion.minY + oy;
            float umaxY = entry.opaqueUnion.maxY + oy;
            // Clip Y-up → GUI Y-down relative to stage center.
            var unionGui = Rect.MinMaxRect(
                cx + uminX * z,
                cy - umaxY * z,
                cx + umaxX * z,
                cy - uminY * z);
            DrawRectOutline(unionGui, UnionOutline, 1f);
        }

        GUI.Label(
            new Rect(stage.xMin + 8f, stage.yMin + 6f, 280f, 32f),
            $"slot {sw}×{sh}  slack {slackPx:0.#}  zoom ×{z:0.#}\ncenter = card frame center",
            EditorStyles.miniLabel);
    }

    private static void DrawTexturePoint(Rect stage, Rect dest, Texture2D tex)
    {
        GUI.BeginClip(stage);
        var localDest = new Rect(dest.x - stage.x, dest.y - stage.y, dest.width, dest.height);
        var prev = tex.filterMode;
        tex.filterMode = FilterMode.Point;
        GUI.DrawTexture(localDest, tex, ScaleMode.StretchToFill, true);
        tex.filterMode = prev;
        GUI.EndClip();
    }

    private static void DrawRectOutline(Rect r, Color color, float thickness)
    {
        EditorGUI.DrawRect(new Rect(r.xMin, r.yMin, r.width, thickness), color);
        EditorGUI.DrawRect(new Rect(r.xMin, r.yMax - thickness, r.width, thickness), color);
        EditorGUI.DrawRect(new Rect(r.xMin, r.yMin, thickness, r.height), color);
        EditorGUI.DrawRect(new Rect(r.xMax - thickness, r.yMin, thickness, r.height), color);
    }

    private void DrawFooterHints()
    {
        EditorGUILayout.Space(2f);
        string modeHint = previewMode == PreviewMode.CardFit
            ? "Card Fit：整段 clip 用联合不透明包围盒中心对齐卡框中心；蓝框=比对窗，黄框=slack，粉框=union。不改源素材。"
            : "Body Compare：固定底边中心落脚，便于横向体型对比。";
        EditorGUILayout.HelpBox(
            "← →：切换动画  ·  Space：播放/暂停  ·  Tab：切换模式  ·  Ctrl+F：搜索\n" + modeHint,
            MessageType.None);
    }

    private void DrawStatusBar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUILayout.Label(spritesRoot, EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            if (isScanning)
                GUILayout.Label("Scanning…", EditorStyles.miniLabel);
            else if (!string.IsNullOrEmpty(statusMessage))
                GUILayout.Label(statusMessage, EditorStyles.miniLabel);
        }
    }

    private static void DrawChecker(Rect rect, float cell)
    {
        int cols = Mathf.CeilToInt(rect.width / cell);
        int rows = Mathf.CeilToInt(rect.height / cell);
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                Color c = ((x + y) & 1) == 0 ? CheckerA : CheckerB;
                var r = new Rect(
                    rect.x + x * cell,
                    rect.y + y * cell,
                    Math.Min(cell, rect.xMax - (rect.x + x * cell)),
                    Math.Min(cell, rect.yMax - (rect.y + y * cell)));
                EditorGUI.DrawRect(r, c);
            }
        }
    }

    private int DisplayFrame =>
        currentFrames.Count == 0 ? 0 : Mathf.Clamp(playing ? playFrame : scrubFrame, 0, currentFrames.Count - 1);

    private Texture2D CurrentTexture =>
        currentFrames.Count == 0 ? null : currentFrames[DisplayFrame];

    private int FilteredOrdinal
    {
        get
        {
            int ord = filteredIndices.IndexOf(selectedIndex);
            return ord < 0 ? 0 : ord;
        }
    }

    private void SelectRelative(int delta)
    {
        if (filteredIndices.Count == 0)
            return;

        int ord = filteredIndices.IndexOf(selectedIndex);
        if (ord < 0)
            ord = 0;
        else
            ord = Mathf.Clamp(ord + delta, 0, filteredIndices.Count - 1);

        SelectFilteredIndex(ord);
    }

    private void SelectFilteredIndex(int filteredOrd)
    {
        if (filteredOrd < 0 || filteredOrd >= filteredIndices.Count)
            return;
        SelectClipIndex(filteredIndices[filteredOrd]);
    }

    private void SelectClipIndex(int clipIndex)
    {
        if (clipIndex < 0 || clipIndex >= allClips.Count)
            return;

        selectedIndex = clipIndex;
        listFocusIndex = -1;
        playFrame = 0;
        scrubFrame = 0;
        lastFrameTime = EditorApplication.timeSinceStartup;
        EnsureFramesLoaded(allClips[clipIndex]);
        PushCardFaceFrame();
        if (pingOnSelect)
            PingCurrentClip();
        statusMessage = allClips[clipIndex].FolderPath;
    }

    private void PingCurrentClip()
    {
        if (selectedIndex < 0 || selectedIndex >= allClips.Count)
            return;

        UnityEngine.Object obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(allClips[selectedIndex].FolderPath);
        if (obj != null)
            EditorGUIUtility.PingObject(obj);
    }

    private static void PingMonsterFacePrefab()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(CardChassisPaths.MonsterFacePrefab);
        if (prefab != null)
        {
            EditorGUIUtility.PingObject(prefab);
            Selection.activeObject = prefab;
        }
    }

    private void EnsureFramesLoaded(ClipInfo clip)
    {
        if (clip == null)
            return;
        if (string.Equals(loadedClipFolder, clip.FolderPath, StringComparison.Ordinal) && currentFrames.Count > 0)
            return;

        UnloadCurrentFrames();
        loadedClipFolder = clip.FolderPath;

        if (!AssetDatabase.IsValidFolder(clip.FolderPath))
        {
            statusMessage = "Missing folder: " + clip.FolderPath;
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { clip.FolderPath });
        var paths = new List<string>(guids.Length);
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                continue;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.Equals(parent, clip.FolderPath, StringComparison.OrdinalIgnoreCase))
                continue;
            paths.Add(path);
        }

        paths.Sort(CompareFramePaths);

        int maxW = 0;
        int maxH = 0;
        foreach (string path in paths)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
                continue;
            currentFrames.Add(tex);
            currentFramePaths.Add(path);
            currentSprites.Add(AssetDatabase.LoadAssetAtPath<Sprite>(path));
            maxW = Math.Max(maxW, tex.width);
            maxH = Math.Max(maxH, tex.height);
        }

        clip.FrameCount = currentFrames.Count;
        if (maxW > 0)
        {
            clip.Width = maxW;
            clip.Height = maxH;
        }

        playFrame = 0;
        scrubFrame = 0;
    }

    private void UnloadCurrentFrames()
    {
        currentFrames.Clear();
        currentFramePaths.Clear();
        currentSprites.Clear();
        loadedClipFolder = string.Empty;
    }

    private void RescanClips()
    {
        string keepName = selectedIndex >= 0 && selectedIndex < allClips.Count
            ? allClips[selectedIndex].FolderName
            : null;

        allClips.Clear();
        UnloadCurrentFrames();

        string root = string.IsNullOrWhiteSpace(spritesRoot) ? DefaultSpritesRoot : spritesRoot.Replace('\\', '/');
        spritesRoot = root;

        if (!AssetDatabase.IsValidFolder(root))
        {
            statusMessage = "Sprites root not found: " + root;
            RebuildFilter();
            return;
        }

        string[] subFolders = AssetDatabase.GetSubFolders(root);
        Array.Sort(subFolders, StringComparer.OrdinalIgnoreCase);

        foreach (string folder in subFolders)
        {
            string folderName = Path.GetFileName(folder);
            if (string.Equals(folderName, SpriteClipCardFitCatalog.DefaultRelativeOutputDir, StringComparison.OrdinalIgnoreCase))
                continue;

            int frameCount = CountPngsInFolder(folder);
            if (frameCount <= 0)
                continue;

            allClips.Add(new ClipInfo
            {
                FolderName = folderName,
                FolderPath = folder.Replace('\\', '/'),
                FrameCount = frameCount,
            });
        }

        RebuildFilter(keepName);
        statusMessage = $"Scanned {allClips.Count} clips under {root}";
    }

    private void RebuildFilter(string preferName = null)
    {
        filteredIndices.Clear();
        string q = (searchQuery ?? string.Empty).Trim();
        bool hasQuery = q.Length > 0;
        string[] tokens = hasQuery
            ? q.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
            : Array.Empty<string>();

        for (int i = 0; i < allClips.Count; i++)
        {
            string name = allClips[i].FolderName;
            if (hasQuery)
            {
                bool ok = true;
                foreach (string token in tokens)
                {
                    if (name.IndexOf(token, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        ok = false;
                        break;
                    }
                }

                if (!ok)
                    continue;
            }

            if (previewMode == PreviewMode.CardFit && fitListFilter != FitListFilter.All)
            {
                SpriteClipCardFitCatalog.ClipEntry entry = FindCatalogEntry(name);
                switch (fitListFilter)
                {
                    case FitListFilter.Usable:
                        if (entry == null || !entry.usable)
                            continue;
                        break;
                    case FitListFilter.Unusable:
                        if (entry == null || entry.usable)
                            continue;
                        break;
                    case FitListFilter.Missing:
                        if (entry != null)
                            continue;
                        break;
                }
            }

            filteredIndices.Add(i);
        }

        if (filteredIndices.Count == 0)
        {
            selectedIndex = -1;
            UnloadCurrentFrames();
            return;
        }

        int prefer = -1;
        if (!string.IsNullOrEmpty(preferName))
            prefer = allClips.FindIndex(c => string.Equals(c.FolderName, preferName, StringComparison.OrdinalIgnoreCase));

        if (prefer >= 0 && filteredIndices.Contains(prefer))
            SelectClipIndex(prefer);
        else if (filteredIndices.Contains(selectedIndex))
            SelectClipIndex(selectedIndex);
        else
            SelectClipIndex(filteredIndices[0]);
    }

    private SpriteClipCardFitCatalog.ClipEntry FindCatalogEntry(string folderName)
    {
        return SpriteClipCardFitCatalog.FindClip(catalog, folderName);
    }

    private void ReloadCatalog()
    {
        catalog = SpriteClipCardFitCatalog.Load(spritesRoot);
        if (catalog != null)
            statusMessage = $"Loaded catalog: {catalog.usableCount}/{catalog.clipCount} usable @ {catalog.analyzedAt}";
        else
            statusMessage = "No catalog.json yet — run Scan All.";
    }

    private void RunCatalogScan(bool incremental)
    {
        if (isScanning)
            return;

        isScanning = true;
        try
        {
            var settings = new SpriteClipCardFitCatalog.ScanSettings
            {
                SpritesRootAssetPath = spritesRoot,
                SlotW = Math.Max(1, slotW),
                SlotH = Math.Max(1, slotH),
                AlphaThreshold = (byte)Mathf.Clamp(alphaThreshold, 0, 255),
                SlackPx = Math.Max(0f, slackPx),
                WritePerClipFiles = writePerClipFiles,
                Incremental = incremental,
            };

            SpriteClipCardFitCatalog.CatalogRoot previous = incremental ? catalog : null;
            catalog = SpriteClipCardFitCatalog.Scan(
                settings,
                previous,
                progress =>
                {
                    if (progress.Total <= 0)
                        return;
                    if (progress.Done % 25 == 0 || progress.Done >= progress.Total)
                    {
                        EditorUtility.DisplayProgressBar(
                            incremental ? "Scan Dirty (Card Fit)" : "Scan All (Card Fit)",
                            $"{progress.Current} ({progress.Done}/{progress.Total})",
                            (float)progress.Done / progress.Total);
                    }
                });

            SpriteClipCardFitCatalog.Save(catalog, spritesRoot, writePerClipFiles);
            RebuildFilter();
            statusMessage =
                $"Catalog saved: {catalog.usableCount}/{catalog.clipCount} usable → " +
                SpriteClipCardFitCatalog.GetCatalogAssetPath(spritesRoot);
        }
        catch (Exception ex)
        {
            statusMessage = "Scan failed: " + ex.Message;
            Debug.LogException(ex);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            isScanning = false;
            Repaint();
        }
    }

    private void EnsureCardFacePreview()
    {
        DisposeCardFacePreview();

        var request = new CardFacePreviewRequest
        {
            Kind = CardPresentationKind.Monster,
            DefId = "sprite-anim-preview",
            DisplayName = selectedIndex >= 0 && selectedIndex < allClips.Count
                ? allClips[selectedIndex].FolderName
                : "Preview",
            BasicDescription = string.Empty,
            Attack = 1,
            Armor = 0,
            Hp = 3,
            ActionCount = 1,
            FaceUp = true,
            MainIcon = CurrentSprite,
        };

        cardFaceHost = new CardFacePreviewHost();
        if (!cardFaceHost.Rebuild(request))
        {
            statusMessage = "Card face preview failed: " + cardFaceHost.Status;
            DisposeCardFacePreview();
            return;
        }

        cardIconTransform = FindChildByName(cardFaceHost.Build.Root.transform, "核心图标");
        cardIconRenderer = cardIconTransform != null
            ? cardIconTransform.GetComponent<SpriteRenderer>()
            : null;

        if (cardIconRenderer == null)
        {
            statusMessage = "Card face preview missing 核心图标 SpriteRenderer.";
            DisposeCardFacePreview();
            return;
        }

        cardFaceLive = true;
        PushCardFaceFrame();
        statusMessage = "Live card face preview active. Toggle off to dispose. " + cardFaceHost.Status;
    }

    private void DisposeCardFacePreview()
    {
        cardFaceLive = false;
        cardIconRenderer = null;
        cardIconTransform = null;
        cardFaceHost?.Dispose();
        cardFaceHost = null;
    }

    private Sprite CurrentSprite =>
        currentSprites.Count == 0 ? null : currentSprites[Mathf.Clamp(DisplayFrame, 0, currentSprites.Count - 1)];

    private void PushCardFaceFrame()
    {
        if (!cardFaceLive || cardIconRenderer == null)
            return;

        Sprite sprite = CurrentSprite;
        if (sprite != null)
            cardIconRenderer.sprite = sprite;

        if (cardIconTransform == null)
            return;

        SpriteClipCardFitCatalog.ClipEntry entry = null;
        if (selectedIndex >= 0 && selectedIndex < allClips.Count)
            entry = FindCatalogEntry(allClips[selectedIndex].FolderName);

        float ox = entry?.offsetPx?.x ?? 0f;
        float oy = entry?.offsetPx?.y ?? 0f;
        // World units: sprite PPU (pack default 32). Offset is in texture pixels (Y-up).
        cardIconTransform.localPosition = new Vector3(ox / DefaultSpritePpu, oy / DefaultSpritePpu, 0f);
    }

    private static Transform FindChildByName(Transform root, string name)
    {
        if (root == null)
            return null;
        if (root.name == name)
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildByName(root.GetChild(i), name);
            if (found != null)
                return found;
        }

        return null;
    }

    private static int CountPngsInFolder(string folder)
    {
        string abs = AbsoluteFromAssetPath(folder);
        if (string.IsNullOrEmpty(abs) || !Directory.Exists(abs))
            return 0;

        try
        {
            return Directory.GetFiles(abs, "*.png", SearchOption.TopDirectoryOnly).Length;
        }
        catch
        {
            return 0;
        }
    }

    private static int CompareFramePaths(string a, string b)
    {
        return SpriteOpaqueBoundsAnalyzer.CompareFramePaths(a, b);
    }

    private static string AbsoluteFromAssetPath(string assetPath)
    {
        return SpriteClipCardFitCatalog.AbsoluteFromAssetPath(assetPath);
    }

    private static string AbsoluteToAssetPath(string absolutePath)
    {
        return SpriteClipCardFitCatalog.AbsoluteToAssetPath(absolutePath);
    }
}
#endif
