using System;
using System.Collections.Generic;
using System.Linq;
using NineGrid.Content;
using NineGrid.Content.Editor.Ui;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NineGrid.Content.Editor
{
    public sealed class ContentVisualEditorWindow : EditorWindow
    {
        private readonly ContentVisualEditorSession session = new();
        private readonly List<ContentVisualWarmConsoleUi.NavEntry> kindNavEntries = new();
        private readonly ContentVisualCardPreview cardPreview = new();

        private VisualElement rootElement;
        private VisualElement listContainer;
        private VisualElement contentRoot;
        private ToolbarButton saveButton;
        private ToolbarButton regenerateButton;
        private TextField searchField;
        private Toggle missingIconToggle;
        private Toggle missingFaceToggle;
        private Toggle dirtyOnlyToggle;
        private HelpBox statusHelpBox;
        private Toolbar tabToolbar;

        private ObjectField iconField;
        private ObjectField faceField;
        private ObjectField batchIconField;
        private ObjectField batchFaceField;
        private IMGUIContainer previewContainer;

        public static void ShowWindow()
        {
            var window = GetWindow<ContentVisualEditorWindow>();
            window.titleContent = new GUIContent("Content Visual");
            window.minSize = new Vector2(1180f, 760f);
            window.Show();
        }

        private void OnEnable()
        {
            session.LoadPreferences();
            try
            {
                session.ReloadFromXlsx();
            }
            catch (Exception ex)
            {
                Debug.LogError("Content visual editor failed to load xlsx: " + ex.Message);
            }

            BuildShell();
            RefreshAll();
        }

        private void OnDisable()
        {
            session.SavePreferences();
            cardPreview.Dispose();
        }

        private void BuildShell()
        {
            rootVisualElement.Clear();
            rootElement = rootVisualElement;
            rootElement.style.flexGrow = 1;
            rootElement.style.backgroundColor = ContentVisualWarmConsoleUi.Theme.RootBg;

            rootElement.Add(ContentVisualWarmConsoleUi.BuildHeader(
                "表现层配图",
                "content_visual.xlsx：icon/face visual_id；card_frame_style.xlsx：稀有度卡框配色。"));

            tabToolbar = new Toolbar();
            var contentTabButton = new ToolbarButton(() => SetActiveTab(0)) { text = "内容配图" };
            var frameTabButton = new ToolbarButton(() => SetActiveTab(1)) { text = "Card Frame 配色" };
            tabToolbar.Add(contentTabButton);
            tabToolbar.Add(frameTabButton);
            rootElement.Add(tabToolbar);

            saveButton = new ToolbarButton(SaveChanges) { text = "保存" };
            saveButton.tooltip = "PATCH content_visual / visual_asset / card_frame_style";
            regenerateButton = new ToolbarButton(RegenerateLuban) { text = "Regenerate Luban" };
            regenerateButton.tooltip = "运行 gen_table_nine.ps1 刷新 StreamingAssets 与 Generated 代码";

            rootElement.Add(ContentVisualWarmConsoleUi.BuildToolbar(
                ("保存", SaveChanges, saveButton.tooltip),
                ("重新加载", ReloadFromDisk, "丢弃未保存改动并从 xlsx 重读"),
                ("Regenerate Luban", RegenerateLuban, regenerateButton.tooltip),
                ("全选", SelectAllFiltered, "勾选当前筛选结果"),
                ("全部取消", DeselectAll, "取消所有勾选")));

            var split = new TwoPaneSplitView(0, 250, TwoPaneSplitViewOrientation.Horizontal);
            split.style.flexGrow = 1;
            rootElement.Add(split);

            split.Add(BuildSidebar());
            split.Add(BuildContentPane());
        }

        private VisualElement BuildSidebar()
        {
            var sidebar = new VisualElement();
            sidebar.style.flexGrow = 1;
            sidebar.style.backgroundColor = ContentVisualWarmConsoleUi.Theme.SidebarBg;

            var searchWrap = new VisualElement();
            searchWrap.style.paddingLeft = 10;
            searchWrap.style.paddingRight = 10;
            searchWrap.style.paddingTop = 10;
            searchWrap.style.paddingBottom = 8;
            searchWrap.style.borderBottomWidth = 1;
            searchWrap.style.borderBottomColor = ContentVisualWarmConsoleUi.Theme.Divider;

            searchField = new TextField { value = session.SearchText };
            searchField.RegisterValueChangedCallback(evt =>
            {
                session.SearchText = evt.newValue;
                session.SavePreferences();
                RefreshList();
                RefreshContent();
            });
            searchWrap.Add(ContentVisualWarmConsoleUi.WrapControl(
                "搜索",
                "匹配 content_id、display_name、description 或 key",
                searchField));
            sidebar.Add(searchWrap);

            var filterWrap = new VisualElement();
            filterWrap.style.paddingLeft = 10;
            filterWrap.style.paddingRight = 10;
            filterWrap.style.paddingTop = 4;
            filterWrap.style.paddingBottom = 4;
            filterWrap.style.borderBottomWidth = 1;
            filterWrap.style.borderBottomColor = ContentVisualWarmConsoleUi.Theme.Divider;

            missingIconToggle = CreateFilterToggle("仅缺 icon", session.FilterMissingIcon, value =>
            {
                session.FilterMissingIcon = value;
                session.SavePreferences();
                RefreshList();
                RefreshContent();
            });
            missingFaceToggle = CreateFilterToggle("仅缺 face", session.FilterMissingFace, value =>
            {
                session.FilterMissingFace = value;
                session.SavePreferences();
                RefreshList();
                RefreshContent();
            });
            dirtyOnlyToggle = CreateFilterToggle("仅未保存", session.FilterDirtyOnly, value =>
            {
                session.FilterDirtyOnly = value;
                session.SavePreferences();
                RefreshList();
                RefreshContent();
            });

            filterWrap.Add(missingIconToggle);
            filterWrap.Add(missingFaceToggle);
            filterWrap.Add(dirtyOnlyToggle);
            sidebar.Add(filterWrap);

            var kindLabel = ContentVisualWarmConsoleUi.CreateTitleLabel("类型筛选", 10, true, ContentVisualWarmConsoleUi.Theme.TextTertiary);
            kindLabel.style.paddingLeft = 10;
            kindLabel.style.paddingTop = 8;
            kindLabel.style.paddingBottom = 4;
            sidebar.Add(kindLabel);

            var kindNavScroll = new ScrollView();
            kindNavScroll.style.maxHeight = 220;
            kindNavScroll.style.paddingLeft = 10;
            kindNavScroll.style.paddingRight = 10;
            kindNavEntries.Clear();
            kindNavScroll.Add(ContentVisualWarmConsoleUi.CreateNavButton(
                "全部", "所有 content_kind", "All", () => SetKindFilter("All"), kindNavEntries));
            foreach (var kind in Enum.GetNames(typeof(ContentVisualKind)))
            {
                if (kind == nameof(ContentVisualKind.Unknown))
                {
                    continue;
                }

                var captured = kind;
                kindNavScroll.Add(ContentVisualWarmConsoleUi.CreateNavButton(
                    kind, "筛选 " + kind, kind, () => SetKindFilter(captured), kindNavEntries));
            }

            sidebar.Add(kindNavScroll);

            var listLabel = ContentVisualWarmConsoleUi.CreateTitleLabel("内容列表", 10, true, ContentVisualWarmConsoleUi.Theme.TextTertiary);
            listLabel.style.paddingLeft = 10;
            listLabel.style.paddingTop = 8;
            listLabel.style.paddingBottom = 4;
            sidebar.Add(listLabel);

            var listScroll = new ScrollView();
            listScroll.style.flexGrow = 1;
            listScroll.style.paddingLeft = 10;
            listScroll.style.paddingRight = 10;
            listScroll.style.paddingBottom = 10;
            listContainer = new VisualElement();
            listScroll.Add(listContainer);
            sidebar.Add(listScroll);

            return sidebar;
        }

        private VisualElement BuildContentPane()
        {
            var scroll = ContentVisualWarmConsoleUi.CreateContentScroll(out contentRoot);
            statusHelpBox = ContentVisualWarmConsoleUi.CreateStatusHelpBox(
                "description 请在 Excel 修改；配图保存后可用 Regenerate Luban 刷新运行时 JSON。");
            contentRoot.Add(statusHelpBox);
            return scroll;
        }

        private static Toggle CreateFilterToggle(string label, bool initial, Action<bool> onChanged)
        {
            var toggle = new Toggle(label) { value = initial };
            toggle.style.marginBottom = 4;
            toggle.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
            return toggle;
        }

        private void SetActiveTab(int tabIndex)
        {
            session.ActiveTab = tabIndex;
            session.SavePreferences();
            RefreshContent();
        }

        private void SetKindFilter(string kind)
        {
            session.KindFilter = kind;
            session.SavePreferences();
            ContentVisualWarmConsoleUi.UpdateNavigationStyles(kindNavEntries, kind);
            RefreshList();
            RefreshContent();
        }

        private void RefreshAll()
        {
            ContentVisualWarmConsoleUi.UpdateNavigationStyles(kindNavEntries, session.KindFilter ?? "All");
            if (searchField != null)
            {
                searchField.SetValueWithoutNotify(session.SearchText ?? string.Empty);
            }

            RefreshList();
            RefreshContent();
            UpdateToolbarState();
        }

        private void RefreshList()
        {
            if (listContainer == null)
            {
                return;
            }

            listContainer.Clear();
            var filtered = session.GetFilteredRows().ToList();
            for (var i = 0; i < filtered.Count; i++)
            {
                var row = filtered[i];
                listContainer.Add(CreateListRow(row));
            }

            if (filtered.Count == 0)
            {
                listContainer.Add(ContentVisualWarmConsoleUi.CreateDescriptionLabel("无匹配条目。"));
            }
        }

        private VisualElement CreateListRow(ContentVisualEditorRowState row)
        {
            var rowRoot = new VisualElement();
            rowRoot.style.flexDirection = FlexDirection.Row;
            rowRoot.style.alignItems = Align.Center;
            rowRoot.style.backgroundColor = row.ContentId == session.FocusedContentId
                ? ContentVisualWarmConsoleUi.Theme.NavSelectedBg
                : ContentVisualWarmConsoleUi.Theme.NavNormalBg;
            rowRoot.style.borderTopLeftRadius = rowRoot.style.borderTopRightRadius = 6;
            rowRoot.style.borderBottomLeftRadius = rowRoot.style.borderBottomRightRadius = 6;
            rowRoot.style.marginBottom = 6;
            rowRoot.style.overflow = Overflow.Hidden;

            var stripe = new VisualElement();
            stripe.style.width = 4;
            stripe.style.alignSelf = Align.Stretch;
            stripe.style.backgroundColor = row.ContentId == session.FocusedContentId
                ? ContentVisualWarmConsoleUi.Theme.AccentStrong
                : ContentVisualWarmConsoleUi.Theme.NavStripeNormal;
            rowRoot.Add(stripe);

            var checkbox = new Toggle { value = row.IsChecked };
            checkbox.style.marginLeft = 6;
            checkbox.RegisterValueChangedCallback(evt => row.IsChecked = evt.newValue);
            rowRoot.Add(checkbox);

            var body = new VisualElement();
            body.style.flexGrow = 1;
            body.style.paddingTop = 8;
            body.style.paddingBottom = 8;
            body.style.paddingRight = 8;

            var title = ContentVisualWarmConsoleUi.CreateTitleLabel(row.ContentId, 13, true, ContentVisualWarmConsoleUi.Theme.TextPrimary);
            body.Add(title);

            var subtitle = ContentVisualWarmConsoleUi.CreateTinyPathLabel(
                row.ContentKind
                + "  |  I:" + (row.HasIconKey ? "Y" : "-")
                + " F:" + (row.HasFaceKey ? "Y" : "-")
                + (row.IsDirty ? "  *未保存" : string.Empty));
            subtitle.style.marginTop = 2;
            body.Add(subtitle);

            rowRoot.Add(body);
            rowRoot.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target is Toggle)
                {
                    return;
                }

                session.FocusedContentId = row.ContentId;
                RefreshList();
                RefreshContent();
            });

            return rowRoot;
        }

        private void RefreshContent()
        {
            if (contentRoot == null)
            {
                return;
            }

            contentRoot.Clear();
            contentRoot.Add(statusHelpBox);

            if (session.ActiveTab == 1)
            {
                BuildFrameStyleContent();
                UpdateToolbarState();
                return;
            }

            var filteredCount = session.GetFilteredRows().Count();
            var assignedIconCount = session.Rows.Count(row => row.HasIconKey);
            contentRoot.Add(ContentVisualWarmConsoleUi.CreateStatsGrid(
                ("筛选结果", filteredCount.ToString(), "当前列表可见条目"),
                ("已设 icon", assignedIconCount.ToString(), "icon_key 非空"),
                ("缺 icon", session.MissingIconCount.ToString(), "可配合左侧过滤"),
                ("未保存", session.DirtyCount.ToString(), "保存后写入 xlsx")));

            var checkedRows = session.GetCheckedRows();
            var focused = session.GetFocusedRow();
            if (focused == null && checkedRows.Count == 0)
            {
                contentRoot.Add(ContentVisualWarmConsoleUi.CreatePageHeader(
                    "选择内容",
                    "在左侧勾选多条进行批量操作，或点击单条查看详情与预览。"));
                UpdateToolbarState();
                return;
            }

            if (checkedRows.Count > 1)
            {
                BuildMultiSelectionContent(checkedRows);
            }
            else
            {
                var row = focused ?? checkedRows[0];
                if (focused == null)
                {
                    session.FocusedContentId = row.ContentId;
                }

                BuildSingleSelectionContent(row);
            }

            UpdateToolbarState();
        }

        private void BuildMultiSelectionContent(List<ContentVisualEditorRowState> checkedRows)
        {
            contentRoot.Add(ContentVisualWarmConsoleUi.CreatePageHeader(
                "已选 " + checkedRows.Count + " 条",
                "批量指派将写入会话；点击保存后 PATCH 回权威 xlsx。"));

            contentRoot.Add(ContentVisualWarmConsoleUi.CreateSectionCard(
                "批量操作",
                "对当前勾选条目统一设置或清空视觉 key。",
                column =>
                {
                    batchIconField = new ObjectField { objectType = typeof(Sprite), allowSceneObjects = false };
                    batchIconField.RegisterValueChangedCallback(evt =>
                        ApplyBatchSprite(checkedRows, ContentVisualKeySlot.Icon, evt.newValue as Sprite));
                    column.Add(ContentVisualWarmConsoleUi.WrapControl(
                        "批量 Icon",
                        "拖入 Sprite → 写入 visual.icon.{content_id}",
                        batchIconField));

                    batchFaceField = new ObjectField { objectType = typeof(Sprite), allowSceneObjects = false };
                    batchFaceField.RegisterValueChangedCallback(evt =>
                        ApplyBatchSprite(checkedRows, ContentVisualKeySlot.Face, evt.newValue as Sprite));
                    column.Add(ContentVisualWarmConsoleUi.WrapControl(
                        "批量 Face",
                        "卡面/阵营底图 visual_id",
                        batchFaceField));

                    column.Add(ContentVisualWarmConsoleUi.CreateButtonRow(
                        new Button(() => session.ClearKeyOnRows(checkedRows, ContentVisualKeySlot.Icon)) { text = "清除 Icon" },
                        new Button(() => session.ClearKeyOnRows(checkedRows, ContentVisualKeySlot.Face)) { text = "清除 Face" }));
                }));

            contentRoot.Add(ContentVisualWarmConsoleUi.CreateSectionCard(
                "选中条目",
                null,
                column =>
                {
                    foreach (var row in checkedRows.Take(12))
                    {
                        column.Add(ContentVisualWarmConsoleUi.CreateChecklistLabel(row.ContentId));
                    }

                    if (checkedRows.Count > 12)
                    {
                        column.Add(ContentVisualWarmConsoleUi.CreateDescriptionLabel(
                            "... 另有 " + (checkedRows.Count - 12) + " 条"));
                    }
                }));
        }

        private void BuildSingleSelectionContent(ContentVisualEditorRowState row)
        {
            ContentVisualResolvedView resolved;
            TryBuildPreviewView(row, out resolved);
            var displayName = session.GetDisplayName(row.ContentId, row.ContentKind);

            contentRoot.Add(ContentVisualWarmConsoleUi.CreatePageHeader(
                displayName,
                row.ContentId + "  ·  " + row.ContentKind + (row.IsDirty ? "  ·  未保存" : string.Empty)));

            contentRoot.Add(ContentVisualWarmConsoleUi.CreateSectionCard(
                "标准卡预览",
                "基于当前会话中的 key（含未保存）与 Resolver 推导。",
                column =>
                {
                    previewContainer = new IMGUIContainer(() =>
                    {
                        var rect = GUILayoutUtility.GetRect(280f, 360f, GUILayout.ExpandWidth(true));
                        cardPreview.Draw(rect, resolved);
                    });
                    previewContainer.style.minHeight = 360;
                    column.Add(previewContainer);
                }));

            contentRoot.Add(ContentVisualWarmConsoleUi.CreateSectionCard(
                "视觉 Key",
                "保存时 PATCH icon/face visual_id，并 upsert visual_asset.xlsx。",
                column =>
                {
                    ContentVisualKind kind;
                    Enum.TryParse(row.ContentKind, true, out kind);

                    Sprite iconSprite;
                    Sprite faceSprite;
                    ContentVisualSpriteKeyCodec.TryDecode(row.IconKey, out iconSprite);
                    ContentVisualSpriteKeyCodec.TryDecode(row.FaceKey, out faceSprite);

                    iconField = new ObjectField { objectType = typeof(Sprite), allowSceneObjects = false, value = iconSprite };
                    iconField.RegisterValueChangedCallback(evt =>
                    {
                        session.ApplySpriteToRows(new[] { row }, ContentVisualKeySlot.Icon, evt.newValue as Sprite);
                        RefreshAfterRowEdit();
                    });
                    column.Add(ContentVisualWarmConsoleUi.WrapControl(
                        "icon_key",
                        "业务 visual_id；空则走约定路径。",
                        iconField));
                    column.Add(ContentVisualWarmConsoleUi.CreateTinyPathLabel("当前：" + (string.IsNullOrEmpty(row.IconKey) ? "(空)" : row.IconKey)));

                    faceField = new ObjectField { objectType = typeof(Sprite), allowSceneObjects = false, value = faceSprite };
                    faceField.RegisterValueChangedCallback(evt =>
                    {
                        session.ApplySpriteToRows(new[] { row }, ContentVisualKeySlot.Face, evt.newValue as Sprite);
                        RefreshAfterRowEdit();
                    });
                    column.Add(ContentVisualWarmConsoleUi.WrapControl(
                        "face_key",
                        "卡面/阵营底图 visual_id。",
                        faceField));
                    column.Add(ContentVisualWarmConsoleUi.CreateTinyPathLabel("当前：" + (string.IsNullOrEmpty(row.FaceKey) ? "(空)" : row.FaceKey)));

                    column.Add(ContentVisualWarmConsoleUi.CreateButtonRow(
                        new Button(() => ClearRowKey(row, ContentVisualKeySlot.Icon)) { text = "清空 Icon" },
                        new Button(() => ClearRowKey(row, ContentVisualKeySlot.Face)) { text = "清空 Face" }));
                }));

            contentRoot.Add(ContentVisualWarmConsoleUi.CreateSectionCard(
                "只读信息",
                "文案与身份列请在 Excel / bootstrap 维护。",
                column =>
                {
                    column.Add(ContentVisualWarmConsoleUi.WrapControl(
                        "display_name",
                        "来自内核 Catalog",
                        ContentVisualWarmConsoleUi.CreateTinyPathLabel(displayName)));
                    column.Add(ContentVisualWarmConsoleUi.WrapControl(
                        "description",
                        "权威列：Excel",
                        ContentVisualWarmConsoleUi.CreateDescriptionLabel(
                            string.IsNullOrEmpty(row.Description) ? "(空)" : row.Description)));

                    if (resolved != null)
                    {
                        column.Add(ContentVisualWarmConsoleUi.WrapControl(
                            "推导 icon",
                            "icon visual_id",
                            ContentVisualWarmConsoleUi.CreateTinyPathLabel(resolved.IconVisualId)));
                        column.Add(ContentVisualWarmConsoleUi.WrapControl(
                            "推导 face",
                            "face visual_id",
                            ContentVisualWarmConsoleUi.CreateTinyPathLabel(
                                string.IsNullOrEmpty(resolved.FaceVisualId) ? "(空)" : resolved.FaceVisualId)));
                        column.Add(ContentVisualWarmConsoleUi.WrapControl(
                            "推导 frame",
                            "稀有度/tier → frame_style_id",
                            ContentVisualWarmConsoleUi.CreateTinyPathLabel(resolved.FrameStyleId)));
                    }
                }));
        }

        private void BuildFrameStyleContent()
        {
            contentRoot.Add(ContentVisualWarmConsoleUi.CreatePageHeader(
                "Card Frame 配色",
                "卡框为纯色矩形；稀有度/tier 自动映射到 frame_style_id，运行时只改 SpriteRenderer.color。"));

            contentRoot.Add(ContentVisualWarmConsoleUi.CreateStatsGrid(
                ("样式数", session.FrameStyleRows.Count.ToString(), "card_frame_style.xlsx"),
                ("未保存", session.FrameStyleRows.Count(row => row.IsDirty).ToString(), "保存后 PATCH RGBA"),
                ("", "", ""),
                ("", "", "")));

            ContentVisualResolvedView previewView = null;
            var sampleRow = session.GetFocusedRow() ?? session.GetFilteredRows().FirstOrDefault();
            if (sampleRow != null)
            {
                session.TryResolveRow(sampleRow, out previewView);
            }

            contentRoot.Add(ContentVisualWarmConsoleUi.CreateSectionCard(
                "预览样本",
                sampleRow != null ? sampleRow.ContentId : "在「内容配图」页选择一条内容作为框色预览样本",
                column =>
                {
                    previewContainer = new IMGUIContainer(() =>
                    {
                        var rect = GUILayoutUtility.GetRect(280f, 360f, GUILayout.ExpandWidth(true));
                        cardPreview.Draw(rect, previewView);
                    });
                    previewContainer.style.minHeight = 360;
                    column.Add(previewContainer);
                }));

            contentRoot.Add(ContentVisualWarmConsoleUi.CreateSectionCard(
                "稀有度 / Tier 配色",
                "保存后写入 card_frame_style.xlsx。",
                column =>
                {
                    for (var i = 0; i < session.FrameStyleRows.Count; i++)
                    {
                        var frameRow = session.FrameStyleRows[i];
                        var field = new ColorField { value = frameRow.Color, showAlpha = true };
                        var captured = frameRow;
                        field.RegisterValueChangedCallback(evt =>
                        {
                            captured.Color = evt.newValue;
                            if (previewView != null && sampleRow != null)
                            {
                                session.TryResolveRow(sampleRow, out previewView);
                            }

                            RefreshContent();
                        });
                        column.Add(ContentVisualWarmConsoleUi.WrapControl(
                            frameRow.StyleId,
                            "frame_style_id",
                            field));
                    }
                }));
        }

        private void ApplyBatchSprite(List<ContentVisualEditorRowState> rows, ContentVisualKeySlot slot, Sprite sprite)
        {
            session.ApplySpriteToRows(rows, slot, sprite);
            RefreshAfterRowEdit();
        }

        private void ClearRowKey(ContentVisualEditorRowState row, ContentVisualKeySlot slot)
        {
            session.ClearKeyOnRows(new[] { row }, slot);
            RefreshAfterRowEdit();
        }

        private void RefreshAfterRowEdit()
        {
            RefreshList();
            RefreshContent();
        }

        private bool TryBuildPreviewView(ContentVisualEditorRowState row, out ContentVisualResolvedView view)
        {
            return session.TryResolveRow(row, out view);
        }

        private void UpdateToolbarState()
        {
            if (regenerateButton != null)
            {
                regenerateButton.SetEnabled(session.DirtyCount == 0);
            }
        }

        private void SelectAllFiltered()
        {
            session.SelectAllFiltered();
            RefreshList();
            RefreshContent();
        }

        private void DeselectAll()
        {
            session.DeselectAll();
            RefreshList();
            RefreshContent();
        }

        private void ReloadFromDisk()
        {
            if (session.DirtyCount > 0
                && !EditorUtility.DisplayDialog(
                    "重新加载",
                    "有 " + session.DirtyCount + " 处未保存改动，确定丢弃？",
                    "丢弃并重载",
                    "取消"))
            {
                return;
            }

            try
            {
                session.ReloadFromXlsx();
                RefreshAll();
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("加载失败", ex.Message, "确定");
            }
        }

        private void SaveChanges()
        {
            string error;
            if (!session.TrySave(out error))
            {
                EditorUtility.DisplayDialog("保存失败", error ?? "未知错误", "确定");
                return;
            }

            session.ReloadCatalogs();
            RefreshAll();
            ShowNotification(new GUIContent("已保存至 xlsx"));
        }

        private void RegenerateLuban()
        {
            if (session.DirtyCount > 0)
            {
                EditorUtility.DisplayDialog(
                    "请先保存",
                    "存在未保存的视觉 key 改动，请先保存后再运行 Luban。",
                    "确定");
                return;
            }

            ContentVisualLubanMenu.RegenerateLuban();
            session.ReloadCatalogs();
            RefreshAll();
        }
    }
}
