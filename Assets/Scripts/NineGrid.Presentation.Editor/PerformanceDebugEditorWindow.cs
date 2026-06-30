#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using NineGrid.Presentation.Debugging;
using NineGrid.Presentation.Editor.Ui;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace NineGrid.Presentation.Editor
{
    public sealed class PerformanceDebugEditorWindow : EditorWindow
    {
        private const string TestScenePath = "Assets/Scenes/PerformanceTestScene.unity";
        private const string PrefsSelectedModule = "NineGrid.PerfDebug.SelectedModuleId";
        private const string PrefsSearch = "NineGrid.PerfDebug.Search";
        private const string PrefsCategory = "NineGrid.PerfDebug.Category";
        private const string PrefsFieldPrefix = "NineGrid.PerfDebug.Field.";

        private readonly List<PerformanceDebugWarmConsoleUi.NavEntry> navEntries = new();
        private readonly List<IPerformanceDebugModule> filteredModules = new();

        private PerformanceDebugCatalog catalog;
        private VisualElement contentRoot;
        private VisualElement navListContainer;
        private VisualElement statsContainer;
        private VisualElement anchorListContainer;
        private HelpBox statusHelpBox;
        private Label logLabel;
        private TextField searchField;
        private PopupField<string> categoryPopup;

        private string selectedModuleId = string.Empty;
        private string searchFilter = string.Empty;
        private int categoryFilterIndex;
        private PerformanceDebugPayload workingPayload;
        private IPerformanceDebugModule selectedModule;
        private readonly PerformanceDebugViewRegistry editModeAnchorRegistry = new();

        [MenuItem("TableNine/表演调试面板")]
        public static void ShowWindow()
        {
            var window = GetWindow<PerformanceDebugEditorWindow>();
            window.titleContent = new GUIContent("表演调试");
            window.minSize = new Vector2(1180f, 760f);
            window.Show();
        }

        private void OnEnable()
        {
            catalog = PerformanceDebugCatalog.Discover();
            selectedModuleId = EditorPrefs.GetString(PrefsSelectedModule, string.Empty);
            searchFilter = EditorPrefs.GetString(PrefsSearch, string.Empty);
            categoryFilterIndex = EditorPrefs.GetInt(PrefsCategory, 0);

            BuildShell();
            TryRefreshEditModeAnchors();
            RefreshNavigation();
            SelectModuleById(selectedModuleId);

            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            SavePreferences();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode || state == PlayModeStateChange.EnteredEditMode)
            {
                if (state == PlayModeStateChange.EnteredPlayMode)
                {
                    EditorApplication.delayCall += RefreshPlayModeAnchors;
                }

                RefreshConnectionStatus();
                RefreshStats();
                RefreshAnchorList();
                RefreshLog();
            }
        }

        private void RefreshPlayModeAnchors()
        {
            PerformanceDebugBootstrap bootstrap = PerformanceDebugSession.Current;
            if (bootstrap == null)
            {
                return;
            }

            bootstrap.Harness.ReindexAnchors();
            RefreshConnectionStatus();
            RefreshStats();
            RefreshAnchorList();
            RefreshLog();
        }

        private void OnEditorUpdate()
        {
            if (statsContainer == null)
            {
                return;
            }

            RefreshStats();
            RefreshLog();
        }

        private void PingAnchor(string anchorId)
        {
            PerformanceDebugViewRegistry registry = GetAnchorRegistryForDisplay();
            if (registry == null
                || !registry.TryGetAnchor(anchorId, out Transform anchor)
                || anchor == null)
            {
                return;
            }

            Selection.activeGameObject = anchor.gameObject;
            EditorGUIUtility.PingObject(anchor.gameObject);
        }

        private void OnReindexAnchors()
        {
            PerformanceDebugBootstrap bootstrap = PerformanceDebugSession.Current;
            if (bootstrap != null)
            {
                bootstrap.Harness.ReindexAnchors();
            }
            else
            {
                TryRefreshEditModeAnchors();
            }

            RefreshAnchorList();
            RefreshStats();
            RefreshLog();
        }

        private void OnClearStage()
        {
            GetRunnerOrWarn()?.ClearStage();
            RefreshStats();
            RefreshAnchorList();
            RefreshLog();
        }

        private void BuildShell()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.flexGrow = 1;
            rootVisualElement.style.backgroundColor = PerformanceDebugWarmConsoleUi.Theme.RootBg;

            rootVisualElement.Add(PerformanceDebugWarmConsoleUi.BuildHeader(
                "表演调试控制台",
                "在 Editor 窗口配置与触发表演；Game View 保持无遮挡。需 PerformanceTestScene + Play Mode。"));

            rootVisualElement.Add(BuildMainToolbar());

            var split = new TwoPaneSplitView(0, 250, TwoPaneSplitViewOrientation.Horizontal);
            split.style.flexGrow = 1;
            rootVisualElement.Add(split);

            split.Add(BuildSidebar());
            split.Add(BuildContentPane());
        }

        private Toolbar BuildMainToolbar()
        {
            var toolbar = new Toolbar();
            toolbar.style.height = 34;
            toolbar.style.paddingLeft = 8;
            toolbar.style.paddingRight = 8;
            toolbar.style.backgroundColor = PerformanceDebugWarmConsoleUi.Theme.HeaderBg;
            toolbar.style.borderBottomWidth = 1;
            toolbar.style.borderBottomColor = PerformanceDebugWarmConsoleUi.Theme.Divider;

            AddToolbarButton(toolbar, "运行测试场景", OpenTestSceneAndPlay, "打开 PerformanceTestScene 并进入 Play Mode");
            AddToolbarButton(toolbar, "打开测试场景", OpenTestSceneOnly, "仅打开场景，不自动 Play");
            AddToolbarButton(toolbar, "重扫锚点", OnReindexAnchors, "从场景 Anchors 根节点重新索引");
            AddToolbarButton(toolbar, "刷新连接", RefreshConnectionStatus, "重新检测 PerformanceDebugBootstrap");
            AddToolbarButton(toolbar, "清空日志", ClearLog, "清空运行时日志缓冲");

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            toolbar.Add(spacer);

            searchField = new TextField { value = searchFilter };
            searchField.style.width = 200;
            searchField.style.flexShrink = 0;
            searchField.style.marginRight = 8;
            searchField.RegisterValueChangedCallback(evt =>
            {
                searchFilter = evt.newValue ?? string.Empty;
                EditorPrefs.SetString(PrefsSearch, searchFilter);
                RefreshNavigation();
            });
            toolbar.Add(WrapToolbarField("搜索", searchField));

            var categoryChoices = BuildCategoryChoices();
            categoryPopup = new PopupField<string>(
                categoryChoices,
                Mathf.Clamp(categoryFilterIndex, 0, categoryChoices.Count - 1));
            categoryPopup.style.width = 130;
            categoryPopup.style.flexShrink = 0;
            categoryPopup.RegisterValueChangedCallback(_ =>
            {
                categoryFilterIndex = categoryPopup.index;
                EditorPrefs.SetInt(PrefsCategory, categoryFilterIndex);
                RefreshNavigation();
            });
            toolbar.Add(WrapToolbarField("分类", categoryPopup));

            return toolbar;
        }

        private static void AddToolbarButton(Toolbar toolbar, string text, Action click, string tooltip)
        {
            var btn = new ToolbarButton(click) { text = text };
            btn.tooltip = tooltip;
            toolbar.Add(btn);
        }

        private static VisualElement WrapToolbarField(string label, VisualElement field)
        {
            var wrap = new VisualElement();
            wrap.style.flexDirection = FlexDirection.Row;
            wrap.style.alignItems = Align.Center;
            wrap.style.marginRight = 4;

            var title = PerformanceDebugWarmConsoleUi.CreateTitleLabel(
                label, 11, true, PerformanceDebugWarmConsoleUi.Theme.TextSecondary);
            title.style.marginRight = 6;
            title.style.marginBottom = 0;
            wrap.Add(title);
            wrap.Add(field);
            return wrap;
        }

        private VisualElement BuildSidebar()
        {
            var sidebar = new VisualElement();
            sidebar.style.flexGrow = 1;
            sidebar.style.backgroundColor = PerformanceDebugWarmConsoleUi.Theme.SidebarBg;

            var groupLabel = PerformanceDebugWarmConsoleUi.CreateTitleLabel(
                "MODULES", 10, true, PerformanceDebugWarmConsoleUi.Theme.TextTertiary);
            groupLabel.style.paddingLeft = 12;
            groupLabel.style.paddingTop = 10;
            groupLabel.style.paddingBottom = 4;
            sidebar.Add(groupLabel);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.contentContainer.style.paddingLeft = 10;
            scroll.contentContainer.style.paddingRight = 10;
            scroll.contentContainer.style.paddingTop = 8;
            scroll.contentContainer.style.paddingBottom = 8;

            navListContainer = new VisualElement();
            scroll.Add(navListContainer);
            sidebar.Add(scroll);

            var footer = new VisualElement();
            footer.style.paddingLeft = 10;
            footer.style.paddingRight = 10;
            footer.style.paddingTop = 8;
            footer.style.paddingBottom = 10;
            footer.style.borderTopWidth = 1;
            footer.style.borderTopColor = PerformanceDebugWarmConsoleUi.Theme.Divider;
            footer.Add(PerformanceDebugWarmConsoleUi.CreateTinyPathLabel(
                $"已注册 {catalog.Modules.Count} 个模块"));
            sidebar.Add(footer);

            return sidebar;
        }

        private VisualElement BuildContentPane()
        {
            var pane = new VisualElement();
            pane.style.flexGrow = 1;
            pane.style.flexDirection = FlexDirection.Column;

            var scroll = PerformanceDebugWarmConsoleUi.CreateContentScroll(out contentRoot);
            pane.Add(scroll);
            RebuildDetailPage();
            return pane;
        }

        private void RebuildDetailPage()
        {
            contentRoot.Clear();
            statusHelpBox = PerformanceDebugWarmConsoleUi.CreateStatusHelpBox(string.Empty);
            contentRoot.Add(statusHelpBox);
            RefreshConnectionStatus();

            statsContainer = new VisualElement();
            contentRoot.Add(statsContainer);
            RefreshStats();

            anchorListContainer = new VisualElement();
            contentRoot.Add(anchorListContainer);
            RefreshAnchorList();

            if (selectedModule == null)
            {
                contentRoot.Add(PerformanceDebugWarmConsoleUi.CreatePageHeader(
                    "选择模块",
                    "从左侧列表选择一个表演模块。Edit Mode 可浏览参数；Play Mode 可触发播放。"));
                return;
            }

            contentRoot.Add(PerformanceDebugWarmConsoleUi.CreatePageHeader(
                selectedModule.DisplayName,
                $"{selectedModule.Category} · {selectedModule.Id}"));

            contentRoot.Add(PerformanceDebugWarmConsoleUi.CreateSectionCard(
                "播放参数",
                "写入 EditorPrefs；Play 时应用到运行时 harness。",
                BuildParamFields));

            contentRoot.Add(PerformanceDebugWarmConsoleUi.CreateSectionCard(
                "播放控制",
                "需要已连接 PerformanceTestScene Play 会话。",
                column =>
                {
                    column.Add(PerformanceDebugWarmConsoleUi.CreateButtonRow(
                        new Button(OnPlay) { text = "Play" },
                        new Button(OnReplay) { text = "Replay" },
                        new Button(OnClearStage) { text = "清场" },
                        new Button(OnStopCurrent) { text = "Stop" },
                        new Button(OnStopAll) { text = "Stop All" },
                        new Button(OnResetScene) { text = "重置演员" },
                        new Button(OnRebuildContext) { text = "Rebuild Context" },
                        new Button(OnCopyPayload) { text = "Copy Payload" }));
                }));

            contentRoot.Add(PerformanceDebugWarmConsoleUi.CreateSectionCard(
                "运行日志",
                "来自 PerformanceDebugLogBuffer，最近条目。",
                column =>
                {
                    logLabel = PerformanceDebugWarmConsoleUi.CreateDescriptionLabel("日志为空。");
                    logLabel.style.whiteSpace = WhiteSpace.Normal;
                    column.Add(logLabel);
                }));

            RefreshLog();
        }

        private void BuildParamFields(VisualElement column)
        {
            if (selectedModule == null || workingPayload == null)
            {
                return;
            }

            IReadOnlyList<PerformanceDebugFieldDef> fields = selectedModule.Schema.Fields;
            for (var i = 0; i < fields.Count; i++)
            {
                PerformanceDebugFieldDef field = fields[i];
                string current = workingPayload.GetString(field.Key, field.DefaultValue);
                column.Add(CreateParamRow(field, current));
            }
        }

        private VisualElement CreateParamRow(PerformanceDebugFieldDef field, string currentValue)
        {
            string description = DescribeField(field);

            switch (field.Kind)
            {
                case PerformanceDebugParamKind.Bool:
                {
                    bool isOn = currentValue is "1" or "true" or "True" or "yes" or "Yes";
                    var toggle = new Toggle { value = isOn };
                    toggle.RegisterValueChangedCallback(evt =>
                    {
                        string value = evt.newValue ? "true" : "false";
                        workingPayload.Set(field.Key, value);
                        SaveFieldPref(selectedModule.Id, field.Key, value);
                    });
                    return PerformanceDebugWarmConsoleUi.WrapControl(field.Label, description, toggle);
                }
                case PerformanceDebugParamKind.Enum:
                case PerformanceDebugParamKind.ContextPreset:
                {
                    var options = new List<string>(field.EnumOptions);
                    if (options.Count == 0)
                    {
                        options.Add(currentValue);
                    }

                    int index = Mathf.Max(0, options.IndexOf(currentValue));
                    var popup = new PopupField<string>(options, index);
                    popup.RegisterValueChangedCallback(evt =>
                    {
                        workingPayload.Set(field.Key, evt.newValue);
                        SaveFieldPref(selectedModule.Id, field.Key, evt.newValue);
                    });
                    return PerformanceDebugWarmConsoleUi.WrapControl(field.Label, description, popup);
                }
                default:
                {
                    var textField = new TextField { value = currentValue };
                    textField.RegisterValueChangedCallback(evt =>
                    {
                        string value = evt.newValue ?? string.Empty;
                        workingPayload.Set(field.Key, value);
                        SaveFieldPref(selectedModule.Id, field.Key, value);
                    });
                    return PerformanceDebugWarmConsoleUi.WrapControl(field.Label, description, textField);
                }
            }
        }

        private static string DescribeField(PerformanceDebugFieldDef field)
        {
            return field.Kind switch
            {
                PerformanceDebugParamKind.Int => "整数",
                PerformanceDebugParamKind.Float => "浮点数",
                PerformanceDebugParamKind.Bool => "true / false",
                PerformanceDebugParamKind.ActorId => "演员 ID，如 player / enemy",
                PerformanceDebugParamKind.AnchorId => "锚点 ID：grid.slot3 / deck.slot5 / hand.handcard1",
                PerformanceDebugParamKind.ContextPreset => "重建场景演员布局",
                PerformanceDebugParamKind.Enum => "枚举选项",
                _ => field.Key,
            };
        }

        private void RefreshNavigation()
        {
            navEntries.Clear();
            filteredModules.Clear();
            navListContainer.Clear();

            PerformanceDebugCategory? category = GetCategoryFilter();
            for (var i = 0; i < catalog.Modules.Count; i++)
            {
                IPerformanceDebugModule module = catalog.Modules[i];
                if (category.HasValue && module.Category != category.Value)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(searchFilter)
                    && module.DisplayName.IndexOf(searchFilter, StringComparison.OrdinalIgnoreCase) < 0
                    && module.Id.IndexOf(searchFilter, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                filteredModules.Add(module);
            }

            for (var i = 0; i < filteredModules.Count; i++)
            {
                IPerformanceDebugModule module = filteredModules[i];
                string key = module.Id;
                navListContainer.Add(PerformanceDebugWarmConsoleUi.CreateNavButton(
                    module.DisplayName,
                    $"{module.Category} · {module.Id}",
                    key,
                    () => SelectModuleById(key),
                    navEntries));
            }

            PerformanceDebugWarmConsoleUi.UpdateNavigationStyles(navEntries, selectedModuleId);

            if (filteredModules.Count == 0)
            {
                navListContainer.Add(PerformanceDebugWarmConsoleUi.CreateDescriptionLabel("无匹配模块。"));
            }
            else if (string.IsNullOrEmpty(selectedModuleId)
                     || filteredModules.Find(m => m.Id == selectedModuleId) == null)
            {
                SelectModuleById(filteredModules[0].Id);
            }
        }

        private void SelectModuleById(string moduleId)
        {
            selectedModuleId = moduleId ?? string.Empty;
            EditorPrefs.SetString(PrefsSelectedModule, selectedModuleId);
            selectedModule = catalog.FindById(selectedModuleId);

            if (selectedModule != null)
            {
                workingPayload = selectedModule.Schema.CreateDefaultPayload();
                LoadPayloadPrefs(selectedModule);
            }
            else
            {
                workingPayload = null;
            }

            PerformanceDebugWarmConsoleUi.UpdateNavigationStyles(navEntries, selectedModuleId);
            RebuildDetailPage();
        }

        private void RefreshConnectionStatus()
        {
            if (statusHelpBox == null)
            {
                return;
            }

            if (!EditorApplication.isPlaying)
            {
                statusHelpBox.messageType = HelpBoxMessageType.Info;
                if (SceneManager.GetActiveScene().name == "PerformanceTestScene")
                {
                    statusHelpBox.text =
                        "未进入 Play Mode。已打开测试场景时可预览锚点；点击「运行测试场景」开始播放测试。";
                }
                else
                {
                    statusHelpBox.text =
                        "未进入 Play Mode。可浏览模块与参数；点击工具栏「运行测试场景」开始测试。";
                }

                return;
            }

            string sceneName = SceneManager.GetActiveScene().name;
            if (sceneName != "PerformanceTestScene")
            {
                statusHelpBox.messageType = HelpBoxMessageType.Warning;
                statusHelpBox.text =
                    $"当前场景为 {sceneName}，不是 PerformanceTestScene。请打开测试场景后再 Play。";
                return;
            }

            if (!PerformanceDebugSession.IsConnected)
            {
                statusHelpBox.messageType = HelpBoxMessageType.Warning;
                statusHelpBox.text =
                    "Play Mode 中未找到 PerformanceDebugBootstrap。请确认场景 Directors 下已挂 Bootstrap。";
                return;
            }

            statusHelpBox.messageType = HelpBoxMessageType.Info;
            statusHelpBox.text = "已连接 PerformanceTestScene。可在 Game View 无遮挡观察表演效果。";
        }

        private void RefreshStats()
        {
            if (statsContainer == null)
            {
                return;
            }

            statsContainer.Clear();

            string moduleCount = catalog.Modules.Count.ToString();
            string actorCount = "-";
            string anchorCount = "-";
            string playState = EditorApplication.isPlaying ? "Play Mode" : "Edit Mode";
            string currentModule = "-";
            string lastError = string.Empty;

            PerformanceDebugBootstrap bootstrap = PerformanceDebugSession.Current;
            if (bootstrap != null)
            {
                actorCount = bootstrap.Harness.Registry.Actors.Count.ToString();
                anchorCount = bootstrap.Harness.Registry.Anchors.Count.ToString();
                if (bootstrap.Runner.CurrentModule != null)
                {
                    currentModule = bootstrap.Runner.CurrentModule.DisplayName;
                    PerformanceDebugContext context = bootstrap.Harness.CreateContext();
                    if (bootstrap.Runner.CurrentModule.TryGetIsPlaying(context, out bool isPlaying) && isPlaying)
                    {
                        playState = "Playing";
                    }
                    else
                    {
                        playState = "Idle";
                    }
                }

                if (!string.IsNullOrEmpty(bootstrap.Runner.LastError))
                {
                    lastError = bootstrap.Runner.LastError;
                }
            }
            else if (!EditorApplication.isPlaying && TryRefreshEditModeAnchors())
            {
                actorCount = "0";
                anchorCount = editModeAnchorRegistry.Anchors.Count.ToString();
            }

            statsContainer.Add(PerformanceDebugWarmConsoleUi.CreateStatsGrid(
                ("模块", moduleCount, "Catalog 自动发现"),
                ("演员", actorCount, "Registry 中演员数"),
                ("锚点", anchorCount, "Registry 中锚点数"),
                ("状态", playState, string.IsNullOrEmpty(lastError) ? $"当前：{currentModule}" : $"Err: {lastError}")));
        }

        private void RefreshAnchorList()
        {
            if (anchorListContainer == null)
            {
                return;
            }

            anchorListContainer.Clear();
            anchorListContainer.Add(PerformanceDebugWarmConsoleUi.CreateSectionCard(
                "场景锚点",
                "锚点来自 PerformanceTestScene 的 Anchors 层级。grid.* 九宫格、deck.* 牌组、hand.* 道具手牌。拖动 Transform 后点「重扫锚点」。",
                column =>
                {
                    PerformanceDebugViewRegistry registry = GetAnchorRegistryForDisplay();
                    if (registry == null)
                    {
                        column.Add(PerformanceDebugWarmConsoleUi.CreateDescriptionLabel(
                            "请打开 PerformanceTestScene，或进入 Play Mode 后查看锚点。"));
                        return;
                    }

                    IReadOnlyList<string> keys = registry.GetAnchorKeysSorted();
                    int shown = 0;
                    for (var i = 0; i < keys.Count; i++)
                    {
                        string key = keys[i];
                        if (!PerformanceDebugAnchorIndexing.ShouldDisplayAnchorKey(key))
                        {
                            continue;
                        }

                        shown++;
                        string capture = key;
                        var row = new Button(() => PingAnchor(capture))
                        {
                            text = registry.DescribeAnchor(key),
                        };
                        row.style.height = 24;
                        row.style.marginBottom = 4;
                        row.style.unityTextAlign = TextAnchor.MiddleLeft;
                        column.Add(row);
                    }

                    if (shown == 0)
                    {
                        column.Add(PerformanceDebugWarmConsoleUi.CreateDescriptionLabel("未索引到锚点。请确认场景含 Anchors 根节点后重扫。"));
                    }
                }));
        }

        private void RefreshLog()
        {
            if (logLabel == null)
            {
                return;
            }

            PerformanceDebugBootstrap bootstrap = PerformanceDebugSession.Current;
            if (bootstrap == null)
            {
                logLabel.text = EditorApplication.isPlaying
                    ? "未连接运行时日志。"
                    : "进入 Play Mode 后可查看运行日志。";
                return;
            }

            IReadOnlyList<string> lines = bootstrap.Harness.Log.Lines;
            if (lines.Count == 0)
            {
                logLabel.text = "日志为空。选择模块后点击 Play。";
                return;
            }

            var builder = new StringBuilder();
            int start = Mathf.Max(0, lines.Count - 12);
            for (var i = start; i < lines.Count; i++)
            {
                builder.AppendLine(lines[i]);
            }

            logLabel.text = builder.ToString();
        }

        private PerformanceDebugSequenceRunner GetRunnerOrWarn()
        {
            PerformanceDebugBootstrap bootstrap = PerformanceDebugSession.Current;
            if (bootstrap == null)
            {
                EditorUtility.DisplayDialog(
                    "未连接",
                    "请先打开 PerformanceTestScene 并进入 Play Mode。",
                    "确定");
                return null;
            }

            return bootstrap.Runner;
        }

        private void OnPlay()
        {
            PerformanceDebugSequenceRunner runner = GetRunnerOrWarn();
            if (runner == null || selectedModule == null)
            {
                return;
            }

            runner.Play(selectedModule, workingPayload?.Clone());
            RefreshStats();
            RefreshAnchorList();
            RefreshLog();
        }

        private void OnReplay()
        {
            PerformanceDebugSequenceRunner runner = GetRunnerOrWarn();
            if (runner == null)
            {
                return;
            }

            runner.Replay();
            RefreshStats();
            RefreshAnchorList();
            RefreshLog();
        }

        private void OnStopCurrent()
        {
            GetRunnerOrWarn()?.StopCurrent();
            RefreshStats();
            RefreshLog();
        }

        private void OnStopAll()
        {
            GetRunnerOrWarn()?.StopAll();
            RefreshStats();
            RefreshLog();
        }

        private void OnResetScene()
        {
            GetRunnerOrWarn()?.ResetScene();
            RefreshStats();
            RefreshLog();
        }

        private void OnRebuildContext()
        {
            PerformanceDebugSequenceRunner runner = GetRunnerOrWarn();
            if (runner == null || workingPayload == null)
            {
                return;
            }

            PerformanceDebugContextPreset preset = workingPayload.GetContextPreset("contextPreset");
            if (preset == PerformanceDebugContextPreset.None)
            {
                preset = PerformanceDebugContextPreset.BattlePair;
            }

            runner.RebuildContext(preset);
            RefreshStats();
            RefreshLog();
        }

        private void OnCopyPayload()
        {
            if (workingPayload == null)
            {
                return;
            }

            EditorGUIUtility.systemCopyBuffer = workingPayload.ToJson();
            PerformanceDebugSession.Current?.Harness.Log.Info("Payload copied to clipboard.");
            RefreshLog();
        }

        private void ClearLog()
        {
            PerformanceDebugSession.Current?.Harness.Log.Clear();
            RefreshLog();
        }

        private static void OpenTestSceneOnly()
        {
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
            }

            EditorSceneManager.OpenScene(TestScenePath);
        }

        private static void OpenTestSceneAndPlay()
        {
            OpenTestSceneOnly();
            EditorApplication.isPlaying = true;
        }

        private List<string> BuildCategoryChoices()
        {
            var choices = new List<string> { "All" };
            foreach (PerformanceDebugCategory category in Enum.GetValues(typeof(PerformanceDebugCategory)))
            {
                choices.Add(category.ToString());
            }

            return choices;
        }

        private PerformanceDebugCategory? GetCategoryFilter()
        {
            if (categoryPopup == null || categoryPopup.index <= 0)
            {
                return null;
            }

            string label = categoryPopup.value;
            return Enum.TryParse(label, out PerformanceDebugCategory category) ? category : null;
        }

        private void LoadPayloadPrefs(IPerformanceDebugModule module)
        {
            IReadOnlyList<PerformanceDebugFieldDef> fields = module.Schema.Fields;
            for (var i = 0; i < fields.Count; i++)
            {
                PerformanceDebugFieldDef field = fields[i];
                string saved = EditorPrefs.GetString(FieldPrefKey(module.Id, field.Key), string.Empty);
                if (!string.IsNullOrEmpty(saved))
                {
                    workingPayload.Set(field.Key, saved);
                }
            }
        }

        private static void SaveFieldPref(string moduleId, string fieldKey, string value)
        {
            EditorPrefs.SetString(FieldPrefKey(moduleId, fieldKey), value ?? string.Empty);
        }

        private static string FieldPrefKey(string moduleId, string fieldKey) =>
            $"{PrefsFieldPrefix}{moduleId}.{fieldKey}";

        private bool TryRefreshEditModeAnchors()
        {
            return PerformanceDebugAnchorIndexing.TryReindexActiveScene(
                "PerformanceTestScene",
                editModeAnchorRegistry);
        }

        private PerformanceDebugViewRegistry GetAnchorRegistryForDisplay()
        {
            PerformanceDebugBootstrap bootstrap = PerformanceDebugSession.Current;
            if (bootstrap != null)
            {
                return bootstrap.Harness.Registry;
            }

            return TryRefreshEditModeAnchors() ? editModeAnchorRegistry : null;
        }

        private void SavePreferences()
        {
            EditorPrefs.SetString(PrefsSelectedModule, selectedModuleId ?? string.Empty);
            EditorPrefs.SetString(PrefsSearch, searchFilter ?? string.Empty);
            if (categoryPopup != null)
            {
                EditorPrefs.SetInt(PrefsCategory, categoryPopup.index);
            }
        }
    }
}
#endif
