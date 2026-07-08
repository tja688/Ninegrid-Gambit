#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using NineGrid.DevTest.Editor.Ui;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NineGrid.DevTest.Editor
{
    /// <summary>
    /// 完整测试运行器：从 Layer Profile SO 展示全部测试项；点击即可运行（非 Play Mode 时自动进入 Play Mode）。
    /// </summary>
    public sealed class TestKeyRunnerWindow : EditorWindow
    {
        private const string AllLayersKey = "__all__";

        private readonly List<TestKeyRunnerWarmConsoleUi.NavEntry> _navEntries = new();

        private VisualElement _contentRoot;
        private VisualElement _navContainer;
        private TextField _searchField;
        private string _selectedLayerKey = AllLayersKey;
        private string _searchText = string.Empty;

        [MenuItem("Window/NineGrid/Test Runner")]
        public static void Open()
        {
            var window = GetWindow<TestKeyRunnerWindow>();
            window.titleContent = new GUIContent("Test Runner");
            window.minSize = new Vector2(980f, 640f);
            window.Show();
        }

        private void OnEnable()
        {
            TestKeyManager.Instance.Changed += OnManagerChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            BuildShell();
            RefreshAll();
        }

        private void OnDisable()
        {
            TestKeyManager.Instance.Changed -= OnManagerChanged;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        private void OnManagerChanged()
        {
            RefreshAll();
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            RefreshAll();
        }

        private void BuildShell()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.flexGrow = 1;
            rootVisualElement.style.backgroundColor = TestKeyRunnerWarmConsoleUi.Theme.RootBg;

            rootVisualElement.Add(TestKeyRunnerWarmConsoleUi.BuildHeader(
                "测试运行器",
                "完整 DevTest 面板：展示所有已声明测试项，点击按钮即可运行。非 Play Mode 时会自动进入 Play Mode 后执行。"));

            rootVisualElement.Add(TestKeyRunnerWarmConsoleUi.BuildToolbar(
                ("刷新", RefreshAll, "重新读取 Layer Profile 与运行时挂载状态"),
                ("打开键位监视器", TestKeyMonitorWindow.Open, "查看小键盘级联归属与溢出状态")));

            var split = new TwoPaneSplitView(0, 250, TwoPaneSplitViewOrientation.Horizontal);
            split.style.flexGrow = 1;
            rootVisualElement.Add(split);

            split.Add(BuildSidebar());
            split.Add(BuildContentPane());
        }

        private VisualElement BuildSidebar()
        {
            var sidebar = new VisualElement();
            sidebar.style.flexGrow = 1;
            sidebar.style.backgroundColor = TestKeyRunnerWarmConsoleUi.Theme.SidebarBg;
            sidebar.style.paddingLeft = 10;
            sidebar.style.paddingRight = 10;
            sidebar.style.paddingTop = 10;
            sidebar.style.paddingBottom = 10;

            _searchField = new TextField("搜索");
            _searchField.style.marginBottom = 10;
            _searchField.RegisterValueChangedCallback(evt =>
            {
                _searchText = evt.newValue ?? string.Empty;
                RefreshContent();
            });
            sidebar.Add(_searchField);

            var navScroll = new ScrollView(ScrollViewMode.Vertical);
            navScroll.style.flexGrow = 1;
            sidebar.Add(navScroll);
            _navContainer = navScroll.contentContainer;

            _navEntries.Clear();
            _navContainer.Add(TestKeyRunnerWarmConsoleUi.CreateNavButton(
                "全部测试",
                "显示所有已声明动作",
                AllLayersKey,
                () => SelectLayer(AllLayersKey),
                _navEntries));

            return sidebar;
        }

        private VisualElement BuildContentPane()
        {
            var scroll = TestKeyRunnerWarmConsoleUi.CreateContentScroll(out _contentRoot);
            return scroll;
        }

        private void RefreshAll()
        {
            RebuildNavigation();
            RefreshContent();
        }

        private void RebuildNavigation()
        {
            if (_navContainer == null)
            {
                return;
            }

            while (_navContainer.childCount > 1)
            {
                _navContainer.RemoveAt(1);
            }

            _navEntries.RemoveAll(entry => entry.Key != AllLayersKey);

            var catalog = TestKeyCatalogProvider.GetCatalogEntries();
            var layerSummaries = BuildLayerSummaries(catalog);

            foreach (var summary in layerSummaries)
            {
                _navContainer.Add(TestKeyRunnerWarmConsoleUi.CreateNavButton(
                    summary.DisplayName,
                    $"{summary.ActionCount} 项 · {summary.LayerId}",
                    summary.LayerId,
                    () => SelectLayer(summary.LayerId),
                    _navEntries));
            }

            if (!layerSummaries.Any(summary => summary.LayerId == _selectedLayerKey) && _selectedLayerKey != AllLayersKey)
            {
                _selectedLayerKey = AllLayersKey;
            }

            TestKeyRunnerWarmConsoleUi.UpdateNavigationStyles(_navEntries, _selectedLayerKey);
        }

        private void SelectLayer(string layerKey)
        {
            _selectedLayerKey = layerKey;
            TestKeyRunnerWarmConsoleUi.UpdateNavigationStyles(_navEntries, _selectedLayerKey);
            RefreshContent();
        }

        private void RefreshContent()
        {
            if (_contentRoot == null)
            {
                return;
            }

            _contentRoot.Clear();

            var catalog = TestKeyCatalogProvider.GetCatalogEntries();
            var filteredEntries = FilterEntries(catalog);
            var isPlaying = EditorApplication.isPlaying;
            var liveCount = catalog.Count(entry => entry.HasLiveCallback);

            _contentRoot.Add(TestKeyRunnerWarmConsoleUi.CreateStatusHelpBox(
                isPlaying
                    ? $"Play Mode 中：{liveCount}/{catalog.Count} 项已挂载运行时回调，可直接执行。"
                    : "当前未在 Play Mode。点击任意测试按钮将自动进入 Play Mode 并执行；目录来自 Layer Profile SO。",
                isPlaying ? HelpBoxMessageType.Info : HelpBoxMessageType.None));

            var layerCount = catalog.Select(entry => entry.LayerId).Distinct(StringComparer.Ordinal).Count();
            _contentRoot.Add(TestKeyRunnerWarmConsoleUi.CreateStatsGrid(
                ("测试层", layerCount.ToString(), "Layer Profile SO 中声明的层"),
                ("测试动作", catalog.Count.ToString(), "全部可点击的测试按钮"),
                ("已挂载", isPlaying ? liveCount.ToString() : "—", isPlaying ? "Play Mode 中已有回调" : "进入 Play 后显示")));

            if (filteredEntries.Count == 0)
            {
                _contentRoot.Add(TestKeyRunnerWarmConsoleUi.CreatePageHeader(
                    _selectedLayerKey == AllLayersKey ? "全部测试" : ResolveLayerTitle(_selectedLayerKey, catalog),
                    catalog.Count == 0
                        ? "未找到任何 Layer Profile。请在 Assets/Resources/DevTest 创建 Layer_*.asset 并声明 declaredBindings。"
                        : "当前筛选条件下没有匹配的测试动作。"));
                return;
            }

            if (_selectedLayerKey == AllLayersKey)
            {
                _contentRoot.Add(TestKeyRunnerWarmConsoleUi.CreatePageHeader(
                    "全部测试",
                    $"共 {filteredEntries.Count} 项可点击测试（不受小键盘键位限制）"));

                foreach (var group in GroupEntries(filteredEntries))
                {
                    _contentRoot.Add(BuildLayerSection(group.Key, group.ToList()));
                }

                return;
            }

            var layerEntries = filteredEntries
                .Where(entry => entry.LayerId == _selectedLayerKey)
                .ToList();

            _contentRoot.Add(TestKeyRunnerWarmConsoleUi.CreatePageHeader(
                ResolveLayerTitle(_selectedLayerKey, catalog),
                $"{layerEntries.Count} 项测试动作"));

            _contentRoot.Add(BuildActionList(layerEntries));
        }

        private static VisualElement BuildLayerSection(string layerId, List<TestKeyCatalogEntry> entries)
        {
            var displayName = entries.Count > 0 ? entries[0].LayerDisplayName : layerId;
            return TestKeyRunnerWarmConsoleUi.CreateSectionCard(
                displayName,
                $"layer: {layerId} · {entries.Count} 项",
                column => column.Add(BuildActionList(entries)));
        }

        private static VisualElement BuildActionList(IReadOnlyList<TestKeyCatalogEntry> entries)
        {
            var list = new VisualElement();
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var meta = BuildEntryMeta(entry);
                list.Add(TestKeyRunnerWarmConsoleUi.CreateActionRow(
                    entry.Label,
                    meta,
                    enabled: true,
                    onClick: () => TestKeyRunnerPlayModeLauncher.RequestRun(entry.LayerId, entry.Key)));
            }

            return list;
        }

        private static string BuildEntryMeta(TestKeyCatalogEntry entry)
        {
            var keypadHint = entry.IsKeypadActive
                ? $"小键盘: {entry.Key}"
                : $"小键盘: {entry.Key}（级联溢出，面板仍可运行）";

            var liveHint = entry.HasLiveCallback ? "回调已挂载" : "点击后自动进入 Play Mode 执行";
            return $"{keypadHint} · {liveHint} · layer: {entry.LayerId}";
        }

        private List<TestKeyCatalogEntry> FilterEntries(IReadOnlyList<TestKeyCatalogEntry> entries)
        {
            IEnumerable<TestKeyCatalogEntry> query = entries;

            if (_selectedLayerKey != AllLayersKey)
            {
                query = query.Where(entry => entry.LayerId == _selectedLayerKey);
            }

            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                var keyword = _searchText.Trim();
                query = query.Where(entry =>
                    entry.Label.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0
                    || entry.LayerId.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0
                    || entry.LayerDisplayName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0
                    || entry.Key.ToString().IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            return query.ToList();
        }

        private static IEnumerable<IGrouping<string, TestKeyCatalogEntry>> GroupEntries(
            IReadOnlyList<TestKeyCatalogEntry> entries)
        {
            return entries
                .GroupBy(entry => entry.LayerId, StringComparer.Ordinal)
                .OrderBy(group => group.First().LayerDisplayName, StringComparer.OrdinalIgnoreCase);
        }

        private static string ResolveLayerTitle(string layerId, IReadOnlyList<TestKeyCatalogEntry> entries)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].LayerId == layerId)
                {
                    return entries[i].LayerDisplayName;
                }
            }

            return layerId;
        }

        private static List<LayerSummary> BuildLayerSummaries(IReadOnlyList<TestKeyCatalogEntry> entries)
        {
            return entries
                .GroupBy(entry => entry.LayerId, StringComparer.Ordinal)
                .Select(group => new LayerSummary(
                    group.Key,
                    group.First().LayerDisplayName,
                    group.Count()))
                .OrderBy(summary => summary.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private readonly struct LayerSummary
        {
            public LayerSummary(string layerId, string displayName, int actionCount)
            {
                LayerId = layerId;
                DisplayName = displayName;
                ActionCount = actionCount;
            }

            public string LayerId { get; }

            public string DisplayName { get; }

            public int ActionCount { get; }
        }
    }
}

#endif
