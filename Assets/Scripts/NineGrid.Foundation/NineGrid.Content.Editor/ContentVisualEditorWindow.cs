using System;
using System.Collections.Generic;
using System.Linq;
using NineGrid.Cards;
using NineGrid.Presentation.Editor;
using NineGrid.Cards.Slots;
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
        private readonly CardFacePreviewHost previewHost = new();

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
        private ObjectField backBorderField;
        private ObjectField backShirtField;
        private ObjectField backLogoField;
        private ObjectField batchIconField;
        private ObjectField batchFaceField;
        private ObjectField kindDefaultFaceField;
        private ObjectField kindFallbackIconField;
        private IMGUIContainer previewContainer;
        private TextField descriptionField;
        private string previewFingerprint = string.Empty;

        private const string CardSpriteExportFolder = "Assets/Notes/CardSprite";

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
            previewHost.Dispose();
            previewFingerprint = string.Empty;
        }

        private void BuildShell()
        {
            rootVisualElement.Clear();
            rootElement = rootVisualElement;
            rootElement.style.flexGrow = 1;
            rootElement.style.backgroundColor = ContentVisualWarmConsoleUi.Theme.RootBg;

            rootElement.Add(ContentVisualWarmConsoleUi.BuildHeader(
                "表现层配图",
                "本窗为卡面权威预览（底盘+L4+ApplyPresentation）。description 可编辑写回 xlsx；图标写入 Catalog SO。"));

            tabToolbar = new Toolbar();
            var contentTabButton = new ToolbarButton(() => SetActiveTab(0)) { text = "内容配图" };
            var frameTabButton = new ToolbarButton(() => SetActiveTab(1)) { text = "Card Frame 配色" };
            tabToolbar.Add(contentTabButton);
            tabToolbar.Add(frameTabButton);
            rootElement.Add(tabToolbar);

            saveButton = new ToolbarButton(SaveChanges) { text = "保存" };
            saveButton.tooltip = "保存 Catalog SO 图引用 + PATCH description / card_frame_style";
            regenerateButton = new ToolbarButton(RegenerateLuban) { text = "Regenerate Luban" };
            regenerateButton.tooltip = "运行 gen_table_nine.ps1 刷新 StreamingAssets 与 Generated 代码（描述进 Play 前须执行）";

            rootElement.Add(ContentVisualWarmConsoleUi.BuildToolbar(
                ("保存", SaveChanges, saveButton.tooltip),
                ("重新加载", ReloadFromDisk, "丢弃未保存改动并从 xlsx 重读"),
                ("Regenerate Luban", RegenerateLuban, regenerateButton.tooltip),
                ("导出卡图关联", ExportCardSpriteManifest, "一键保存到 Assets/Notes/CardSprite（语义化 JSON）"),
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
                "匹配 content_id、display_name、description",
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
                "description 在 Excel 维护；图标/卡面写入 Assets/Arts/ContentVisual/*.asset；框色仍走 card_frame_style.xlsx。");
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
                + "  |  I:" + (row.HasIcon ? "Y" : "-")
                + " F:" + (row.HasFace ? "Y" : "-")
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
            var assignedIconCount = session.Rows.Count(row => row.HasIcon);
            contentRoot.Add(ContentVisualWarmConsoleUi.CreateStatsGrid(
                ("筛选结果", filteredCount.ToString(), "当前列表可见条目"),
                ("已设 icon", assignedIconCount.ToString(), "Catalog SO 已挂 icon"),
                ("缺 icon", session.MissingIconCount.ToString(), "可配合左侧过滤"),
                ("未保存", session.DirtyCount.ToString(), "保存后写入 Catalog SO / 框色 xlsx")));

            BuildKindDefaultsSection();

            var checkedRows = session.GetCheckedRows();
            var focused = session.GetFocusedRow();
            if (focused == null && checkedRows.Count == 0)
            {
                contentRoot.Add(ContentVisualWarmConsoleUi.CreatePageHeader(
                    "选择内容",
                    "在左侧勾选多条进行批量操作，或点击单条查看详情与预览。左侧点类型可编辑该类型默认卡面/图标回退。"));
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

        private void BuildKindDefaultsSection()
        {
            var kind = session.KindFilter;
            if (string.IsNullOrEmpty(kind) || kind == "All")
            {
                return;
            }

            var catalog = session.ResolveCatalogForKindFilter(kind);
            if (catalog == null)
            {
                return;
            }

            contentRoot.Add(ContentVisualWarmConsoleUi.CreateSectionCard(
                kind + " · 类型默认设置",
                "点击左侧类型时显示。默认卡面仅作「覆盖应用」源；缺失主图标时运行时回退 Fallback Icon。",
                column =>
                {
                    kindDefaultFaceField = new ObjectField
                    {
                        objectType = typeof(Sprite),
                        allowSceneObjects = false,
                        value = catalog.DefaultFace
                    };
                    column.Add(ContentVisualWarmConsoleUi.WrapControl(
                        "默认卡面 DefaultFace",
                        "不持续覆盖条目；点下方按钮可一次性写入本类型全部 face",
                        kindDefaultFaceField));

                    kindFallbackIconField = new ObjectField
                    {
                        objectType = typeof(Sprite),
                        allowSceneObjects = false,
                        value = catalog.FallbackIcon
                    };
                    column.Add(ContentVisualWarmConsoleUi.WrapControl(
                        "缺失主图标回退 FallbackIcon",
                        "条目 icon 为空时运行时使用此 Sprite",
                        kindFallbackIconField));

                    column.Add(ContentVisualWarmConsoleUi.CreateButtonRow(
                        new Button(() => PersistKindDefaults(kind))
                        {
                            text = "保存类型默认"
                        },
                        new Button(() => ApplyKindDefaultFaceOnce(kind))
                        {
                            text = "覆盖应用默认卡面"
                        }));
                    column.Add(ContentVisualWarmConsoleUi.CreateDescriptionLabel(
                        "「覆盖应用」= 一次性把 DefaultFace 写入本类型全部条目 face（初始化），不是持续绑定。"));
                }));
        }

        private void PersistKindDefaults(string kind)
        {
            var defaultFace = kindDefaultFaceField != null ? kindDefaultFaceField.value as Sprite : null;
            var fallbackIcon = kindFallbackIconField != null ? kindFallbackIconField.value as Sprite : null;
            string error;
            if (!session.TryPersistKindDefaults(kind, defaultFace, fallbackIcon, out error))
            {
                EditorUtility.DisplayDialog("保存失败", error ?? "未知错误", "确定");
                return;
            }

            ShowNotification(new GUIContent("已保存 " + kind + " 类型默认"));
            RefreshContent();
        }

        private void ApplyKindDefaultFaceOnce(string kind)
        {
            // 先把当前 ObjectField 写回 SO，再一次性覆盖会话 face。
            PersistKindDefaults(kind);
            var catalog = session.ResolveCatalogForKindFilter(kind);
            if (catalog == null || catalog.DefaultFace == null)
            {
                EditorUtility.DisplayDialog("无法覆盖", "请先指定默认卡面 DefaultFace。", "确定");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "覆盖应用默认卡面",
                    "将把 " + kind + " 的 DefaultFace 一次性写入该类型全部条目的 face（可再单独改）。继续？",
                    "覆盖应用",
                    "取消"))
            {
                return;
            }

            var count = session.ApplyKindDefaultFaceToSession(kind);
            RefreshAfterRowEdit();
            ShowNotification(new GUIContent("已覆盖 " + count + " 条 face（未点保存前仅在会话）"));
        }

        private void BuildMultiSelectionContent(List<ContentVisualEditorRowState> checkedRows)
        {
            contentRoot.Add(ContentVisualWarmConsoleUi.CreatePageHeader(
                "已选 " + checkedRows.Count + " 条",
                "批量指派写入会话；点击保存后写入对应 Kind 的 Catalog SO。"));

            contentRoot.Add(ContentVisualWarmConsoleUi.CreateSectionCard(
                "批量操作",
                "对当前勾选条目统一设置或清空 Sprite。",
                column =>
                {
                    batchIconField = new ObjectField { objectType = typeof(Sprite), allowSceneObjects = false };
                    batchIconField.RegisterValueChangedCallback(evt =>
                        ApplyBatchSprite(checkedRows, ContentVisualKeySlot.Icon, evt.newValue as Sprite));
                    column.Add(ContentVisualWarmConsoleUi.WrapControl(
                        "批量 Icon",
                        "拖入 Sprite → 按 contentId 写入 Catalog SO",
                        batchIconField));

                    batchFaceField = new ObjectField { objectType = typeof(Sprite), allowSceneObjects = false };
                    batchFaceField.RegisterValueChangedCallback(evt =>
                        ApplyBatchSprite(checkedRows, ContentVisualKeySlot.Face, evt.newValue as Sprite));
                    column.Add(ContentVisualWarmConsoleUi.WrapControl(
                        "批量 Face",
                        "卡面/立绘 Sprite",
                        batchFaceField));

                    column.Add(ContentVisualWarmConsoleUi.CreateButtonRow(
                        new Button(() =>
                        {
                            session.ClearKeyOnRows(checkedRows, ContentVisualKeySlot.Icon);
                            RefreshAfterRowEdit();
                        })
                        { text = "清除 Icon" },
                        new Button(() =>
                        {
                            session.ClearKeyOnRows(checkedRows, ContentVisualKeySlot.Face);
                            RefreshAfterRowEdit();
                        })
                        { text = "清除 Face" }));
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
                "卡面终态预览",
                "底盘 + L4 + ApplyPresentation；含未保存 Sprite / 描述草稿。本窗为权威预览。",
                column =>
                {
                    previewContainer = new IMGUIContainer(() =>
                    {
                        EnsureHiFiPreview(row);
                        var rect = GUILayoutUtility.GetRect(280f, 420f, GUILayout.ExpandWidth(true));
                        previewHost.Draw(rect);
                    });
                    previewContainer.style.minHeight = 420;
                    column.Add(previewContainer);

                    column.Add(ContentVisualWarmConsoleUi.CreateButtonRow(
                        new Button(() =>
                        {
                            previewFingerprint = string.Empty;
                            EnsureHiFiPreview(row);
                            previewContainer?.MarkDirtyRepaint();
                        }) { text = "重建预览" },
                        new Button(() =>
                        {
                            EnsureHiFiPreview(row);
                            previewHost.ValidateSortingOrders();
                            EditorUtility.DisplayDialog(
                                "通层 sortingOrder",
                                previewHost.Status
                                + (previewHost.Warnings.Count > 0
                                    ? "\n\n" + string.Join("\n", previewHost.Warnings)
                                    : string.Empty),
                                "确定");
                        }) { text = "校验 sortingOrder" }));

                    column.Add(ContentVisualWarmConsoleUi.CreateDescriptionLabel(previewHost.Status));
                }));

            contentRoot.Add(ContentVisualWarmConsoleUi.CreateSectionCard(
                "直接暴露装配项",
                "Main_Icon / Face_Background / 卡背三件套写入 Catalog SO；空则回退卡面模板（无全局卡背）。",
                column =>
                {
                    iconField = CreateSpriteField(row.Icon, sprite =>
                    {
                        session.ApplySpriteToRows(new[] { row }, ContentVisualKeySlot.Icon, sprite);
                        InvalidateAndRefreshPreview(row);
                    });
                    column.Add(ContentVisualWarmConsoleUi.WrapControl(
                        "Main_Icon",
                        "主图标；留空回退卡面模板",
                        iconField));

                    faceField = CreateSpriteField(row.Face, sprite =>
                    {
                        session.ApplySpriteToRows(new[] { row }, ContentVisualKeySlot.Face, sprite);
                        InvalidateAndRefreshPreview(row);
                    });
                    column.Add(ContentVisualWarmConsoleUi.WrapControl(
                        "Face_Background",
                        "卡面背景；留空回退卡面模板",
                        faceField));

                    backBorderField = CreateSpriteField(row.BackBorder, sprite =>
                    {
                        session.ApplySpriteToRows(new[] { row }, ContentVisualKeySlot.BackBorder, sprite);
                        InvalidateAndRefreshPreview(row);
                    });
                    column.Add(ContentVisualWarmConsoleUi.WrapControl(
                        "Back_Border",
                        "卡背·背框；留空回退该卡面模板兜底",
                        backBorderField));

                    backShirtField = CreateSpriteField(row.BackShirt, sprite =>
                    {
                        session.ApplySpriteToRows(new[] { row }, ContentVisualKeySlot.BackShirt, sprite);
                        InvalidateAndRefreshPreview(row);
                    });
                    column.Add(ContentVisualWarmConsoleUi.WrapControl(
                        "Back_Shirt",
                        "卡背·背纹；留空回退该卡面模板兜底",
                        backShirtField));

                    backLogoField = CreateSpriteField(row.BackLogo, sprite =>
                    {
                        session.ApplySpriteToRows(new[] { row }, ContentVisualKeySlot.BackLogo, sprite);
                        InvalidateAndRefreshPreview(row);
                    });
                    column.Add(ContentVisualWarmConsoleUi.WrapControl(
                        "Back_Logo",
                        "卡背·Logo；留空回退该卡面模板兜底",
                        backLogoField));

                    column.Add(ContentVisualWarmConsoleUi.CreateButtonRow(
                        new Button(() => ClearRowKey(row, ContentVisualKeySlot.Icon)) { text = "清空主图标" },
                        new Button(() => ClearRowKey(row, ContentVisualKeySlot.Face)) { text = "清空背景" },
                        new Button(() =>
                        {
                            ClearRowKey(row, ContentVisualKeySlot.BackBorder);
                            ClearRowKey(row, ContentVisualKeySlot.BackShirt);
                            ClearRowKey(row, ContentVisualKeySlot.BackLogo);
                        }) { text = "清空卡背" }));
                }));

            contentRoot.Add(ContentVisualWarmConsoleUi.CreateSectionCard(
                "静态基础描述",
                "写入 content_visual.xlsx description；可用 [SlotCode] 插入装配图标。保存后须 Regenerate Luban 才进 Play。",
                column =>
                {
                    descriptionField = new TextField
                    {
                        multiline = true,
                        value = row.DraftDescription ?? string.Empty
                    };
                    descriptionField.style.minHeight = 88;
                    descriptionField.RegisterValueChangedCallback(evt =>
                    {
                        row.DraftDescription = evt.newValue ?? string.Empty;
                        InvalidateAndRefreshPreview(row);
                    });
                    column.Add(ContentVisualWarmConsoleUi.WrapControl(
                        "Basic_Description",
                        "静态卡面描述（不进数值 Commit）",
                        descriptionField));

                    var insertables = CardFacePreviewBuilder.ListInsertableSlots();
                    if (insertables.Count > 0)
                    {
                        var buttonRow = new VisualElement();
                        buttonRow.style.flexDirection = FlexDirection.Row;
                        buttonRow.style.flexWrap = Wrap.Wrap;
                        for (var i = 0; i < insertables.Count; i++)
                        {
                            var slot = insertables[i];
                            var code = slot.Code;
                            var label = string.IsNullOrEmpty(slot.DisplayNameZh)
                                ? code
                                : slot.DisplayNameZh + " [" + code + "]";
                            var button = new Button(() => InsertSlotCode(row, code))
                            {
                                text = "插入 " + label,
                                tooltip = "插入 [" + code + "] 到描述框，并复制到剪贴板"
                            };
                            button.style.marginRight = 4;
                            button.style.marginBottom = 4;
                            buttonRow.Add(button);
                        }

                        column.Add(buttonRow);
                    }

                    column.Add(ContentVisualWarmConsoleUi.WrapControl(
                        "display_name",
                        "来自内核 Catalog",
                        ContentVisualWarmConsoleUi.CreateTinyPathLabel(displayName)));

                    if (resolved != null)
                    {
                        column.Add(ContentVisualWarmConsoleUi.WrapControl(
                            "frame_style",
                            "稀有度/tier → frame_style_id（首波预览以卡面模板为准）",
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

            var sampleRow = session.GetFocusedRow() ?? session.GetFilteredRows().FirstOrDefault();

            contentRoot.Add(ContentVisualWarmConsoleUi.CreateSectionCard(
                "预览样本",
                sampleRow != null
                    ? sampleRow.ContentId + "（高保真卡面；框色首波以卡面模板为准）"
                    : "在「内容配图」页选择一条内容作为预览样本",
                column =>
                {
                    previewContainer = new IMGUIContainer(() =>
                    {
                        if (sampleRow != null)
                        {
                            EnsureHiFiPreview(sampleRow);
                        }

                        var rect = GUILayoutUtility.GetRect(280f, 420f, GUILayout.ExpandWidth(true));
                        previewHost.Draw(rect);
                    });
                    previewContainer.style.minHeight = 420;
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
                            previewFingerprint = string.Empty;
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

        private static ObjectField CreateSpriteField(Sprite value, Action<Sprite> onChanged)
        {
            var field = new ObjectField
            {
                objectType = typeof(Sprite),
                allowSceneObjects = false,
                value = value
            };
            field.RegisterValueChangedCallback(evt => onChanged?.Invoke(evt.newValue as Sprite));
            return field;
        }

        private void ClearRowKey(ContentVisualEditorRowState row, ContentVisualKeySlot slot)
        {
            session.ClearKeyOnRows(new[] { row }, slot);
            InvalidateAndRefreshPreview(row);
        }

        private void RefreshAfterRowEdit()
        {
            previewFingerprint = string.Empty;
            RefreshList();
            RefreshContent();
        }

        private void InvalidateAndRefreshPreview(ContentVisualEditorRowState row)
        {
            previewFingerprint = string.Empty;
            RefreshList();
            // 避免整页重建打断描述框输入：仅重绘预览与列表脏标记
            if (descriptionField != null && row != null
                && descriptionField.value != row.DraftDescription)
            {
                descriptionField.SetValueWithoutNotify(row.DraftDescription ?? string.Empty);
            }

            EnsureHiFiPreview(row);
            previewContainer?.MarkDirtyRepaint();
            UpdateToolbarState();
        }

        private void EnsureHiFiPreview(ContentVisualEditorRowState row)
        {
            if (row == null)
            {
                return;
            }

            var fingerprint = BuildPreviewFingerprint(row);
            if (fingerprint == previewFingerprint && previewHost.PreviewRoot != null)
            {
                return;
            }

            previewFingerprint = fingerprint;
            if (!TryBuildPreviewRequest(row, out var request, out var error))
            {
                previewHost.Rebuild(null);
                Debug.LogWarning("[ContentVisual] 预览跳过：" + error);
                return;
            }

            previewHost.Rebuild(request);
        }

        private static string BuildPreviewFingerprint(ContentVisualEditorRowState row)
        {
            return string.Join("|",
                row.ContentId,
                row.ContentKind,
                row.DraftDescription ?? string.Empty,
                SpriteId(row.Icon),
                SpriteId(row.Face),
                SpriteId(row.BackBorder),
                SpriteId(row.BackShirt),
                SpriteId(row.BackLogo));
        }

        private static string SpriteId(Sprite sprite)
        {
            return sprite == null ? "-" : sprite.GetInstanceID().ToString();
        }

        private bool TryBuildPreviewRequest(
            ContentVisualEditorRowState row,
            out CardFacePreviewRequest request,
            out string error)
        {
            request = null;
            error = null;
            if (row == null)
            {
                error = "无选中行。";
                return false;
            }

            ContentVisualKind contentKind;
            if (!Enum.TryParse(row.ContentKind, true, out contentKind))
            {
                contentKind = ContentVisualKind.Unknown;
            }

            var kind = CardFacePreviewBuilder.ToPresentationKind(contentKind, row.ContentId);
            if (kind == CardPresentationKind.Unknown)
            {
                error = "该 Kind 不挂四套卡面：" + row.ContentId;
                return false;
            }

            request = new CardFacePreviewRequest
            {
                DefId = row.ContentId,
                Kind = kind,
                DisplayName = session.GetDisplayName(row.ContentId, row.ContentKind),
                BasicDescription = row.DraftDescription ?? string.Empty,
                MainIcon = row.Icon,
                FaceBackground = row.Face,
                BackBorder = row.BackBorder,
                BackShirt = row.BackShirt,
                BackLogo = row.BackLogo,
                FaceUp = true,
            };
            CardFacePreviewBuilder.TryFillSampleStats(request, session.CoreCatalog);
            return true;
        }

        private void InsertSlotCode(ContentVisualEditorRowState row, string slotCode)
        {
            if (row == null || string.IsNullOrEmpty(slotCode))
            {
                return;
            }

            var token = "[" + slotCode + "]";
            EditorGUIUtility.systemCopyBuffer = token;

            var text = row.DraftDescription ?? string.Empty;
            var index = text.Length;
            if (descriptionField != null)
            {
                try
                {
                    index = Mathf.Clamp(descriptionField.cursorIndex, 0, text.Length);
                }
                catch
                {
                    index = text.Length;
                }
            }

            row.DraftDescription = text.Insert(index, token);
            if (descriptionField != null)
            {
                descriptionField.SetValueWithoutNotify(row.DraftDescription);
                try
                {
                    var caret = index + token.Length;
                    descriptionField.SelectRange(caret, caret);
                }
                catch
                {
                    // UI Toolkit 版本差异：无光标 API 时已写入全文。
                }
            }

            InvalidateAndRefreshPreview(row);
            ShowNotification(new GUIContent("已插入并复制 " + token));
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

        private new void SaveChanges()
        {
            string error;
            if (!session.TrySave(out error))
            {
                EditorUtility.DisplayDialog("保存失败", error ?? "未知错误", "确定");
                return;
            }

            session.ReloadCatalogs();
            previewFingerprint = string.Empty;
            RefreshAll();
            ShowNotification(new GUIContent("已保存 Catalog SO / description / 框色表"));
        }

        private void RegenerateLuban()
        {
            if (session.DirtyCount > 0)
            {
                EditorUtility.DisplayDialog(
                    "请先保存",
                    "存在未保存的配图/框色改动，请先保存后再运行 Luban。",
                    "确定");
                return;
            }

            ContentVisualLubanMenu.RegenerateLuban();
            session.ReloadCatalogs();
            RefreshAll();
        }

        private void ExportCardSpriteManifest()
        {
            string absolutePath;
            string error;
            if (!session.TryExportCardSpriteManifest(CardSpriteExportFolder, out absolutePath, out error))
            {
                EditorUtility.DisplayDialog("导出失败", error ?? "未知错误", "确定");
                return;
            }

            ShowNotification(new GUIContent("已导出卡图关联"));
            Debug.Log("[ContentVisual] Card sprite manifest → " + absolutePath);
            EditorUtility.RevealInFinder(absolutePath);
        }
    }
}
