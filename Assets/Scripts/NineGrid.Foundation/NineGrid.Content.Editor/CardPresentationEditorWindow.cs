#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NineGrid.Cards;
using NineGrid.Cards.Anim;
using NineGrid.Cards.Presentation;
using NineGrid.Cards.Slots;
using NineGrid.Content;
using NineGrid.Content.CardPresentation;
using NineGrid.Content.Editor.Ui;
using NineGrid.Presentation.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NineGrid.Content.Editor
{
    public sealed class CardPresentationEditorWindow : EditorWindow
    {
        private readonly CardPresentationEditorSession session = new CardPresentationEditorSession();
        private readonly CardFacePreviewHost previewHost = new CardFacePreviewHost();
        private readonly VisualEffectPreviewHost vfxPreviewHost = new VisualEffectPreviewHost();
        private readonly CardPresentationFlipPreview flipPreview = new CardPresentationFlipPreview();

        private VisualElement rootElement;
        private VisualElement listContainer;
        private VisualElement contentRoot;
        private HelpBox statusHelpBox;
        private IMGUIContainer previewContainer;
        private IMGUIContainer vfxPreviewContainer;
        private TextField descriptionField;
        private Label descriptionModeLabel;
        private bool suppressDescriptionCallback;
        private string previewFingerprint = string.Empty;
        private string vfxPreviewFingerprint = string.Empty;
        private bool faceUp = true;
        private string previewAnimSlot = CardAnimSlotIds.Idle;
        private CardSpriteAnimPlayer previewAnimPlayer;
        private double lastEditorTickTime;
        private bool editorTickHooked;

        private readonly Dictionary<string, bool> foldoutState =
            new Dictionary<string, bool>(StringComparer.Ordinal);

        private CardPresentationKind richTextPreviewKind = CardPresentationKind.Monster;
        private string richTextSelectedCode = CardFaceSlotCodes.ActionIcon;
        private string richTextSampleDescription = "在[Action_Icon]后攻击玩家；施加[Poison]";
        private CardFaceDescriptionInlineIconStyleSO inlineIconStyle;
        private CardFaceDescriptionIconCatalogSO iconCatalog;
        private bool inlineIconStyleDirty;
        private bool iconCatalogDirty;

        private string newDeckIdDraft = string.Empty;
        private string newDeckNameDraft = string.Empty;

        [MenuItem("NineGrid/表现层配置")]
        public static void ShowWindow()
        {
            var window = GetWindow<CardPresentationEditorWindow>();
            window.titleContent = new GUIContent("表现层配置");
            window.minSize = new Vector2(1280f, 780f);
            window.Show();
        }

        private void OnEnable()
        {
            try
            {
                session.Reload();
            }
            catch (Exception ex)
            {
                Debug.LogError("[CardPresentation] 加载失败：" + ex.Message);
            }

            HookEditorTick(true);
            BuildShell();
            RefreshAll();
        }

        private void OnDisable()
        {
            HookEditorTick(false);
            flipPreview.Stop();
            previewAnimPlayer = null;
            previewHost.Dispose();
            vfxPreviewHost.Dispose();
            previewFingerprint = string.Empty;
            vfxPreviewFingerprint = string.Empty;
            CardFacePresentationBinder.SetInlineIconStyleOverride(null);
            CardFacePresentationBinder.SetDescriptionIconCatalogOverride(null);
            TryPersistInlineIconStyle();
            TryPersistIconCatalog();
        }

        private void HookEditorTick(bool enable)
        {
            if (enable == editorTickHooked)
            {
                return;
            }

            if (enable)
            {
                EditorApplication.update += OnEditorUpdate;
                lastEditorTickTime = EditorApplication.timeSinceStartup;
            }
            else
            {
                EditorApplication.update -= OnEditorUpdate;
            }

            editorTickHooked = enable;
        }

        private void OnEditorUpdate()
        {
            var now = EditorApplication.timeSinceStartup;
            var delta = (float)(now - lastEditorTickTime);
            lastEditorTickTime = now;
            if (delta < 0f || delta > 0.25f)
            {
                delta = 0.016f;
            }

            var needsRepaint = false;
            if (flipPreview.IsPlaying)
            {
                flipPreview.Tick(delta);
                needsRepaint = true;
            }

            if (previewAnimPlayer != null && previewAnimPlayer.IsPlaying)
            {
                previewAnimPlayer.EditorTick(delta);
                needsRepaint = true;
            }

            if (session.FocusKind == CardPresentationEditorFocusKind.VisualEffect)
            {
                vfxPreviewHost.EditorTick(delta);
                if (vfxPreviewHost.Player != null && vfxPreviewHost.Player.IsPlaying)
                {
                    needsRepaint = true;
                }
            }

            if (needsRepaint)
            {
                previewContainer?.MarkDirtyRepaint();
                vfxPreviewContainer?.MarkDirtyRepaint();
            }
        }

        private void BuildShell()
        {
            rootVisualElement.Clear();
            rootElement = rootVisualElement;
            rootElement.style.flexGrow = 1;
            rootElement.style.backgroundColor = ContentVisualWarmConsoleUi.Theme.RootBg;

            rootElement.Add(ContentVisualWarmConsoleUi.BuildHeader(
                "表现层配置",
                "卡面 / 效果池 / 特效库 / 卡组·卡背；特效库本阶段纯预览，时机装配后续接线。"));

            rootElement.Add(ContentVisualWarmConsoleUi.BuildToolbar(
                ("保存", SaveAll, "保存全部脏 JSON（Authoring + StreamingAssets）"),
                ("重新加载", ReloadFromDisk, "丢弃未保存改动并重读 xlsx / JSON / Catalog"),
                ("从旧 Catalog 迁移", MigrateFromLegacy, "对缺失 JSON 的条目从 Catalog SO + Core 填充并落盘"),
                ("导出索引", ExportIndex, "写出 cards/_index.json")));

            var split = new TwoPaneSplitView(0, 280, TwoPaneSplitViewOrientation.Horizontal);
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
            sidebar.style.paddingTop = 8;
            sidebar.style.paddingBottom = 8;
            sidebar.style.paddingLeft = 8;
            sidebar.style.paddingRight = 8;

            var label = ContentVisualWarmConsoleUi.CreateTitleLabel(
                "内容树", 10, true, ContentVisualWarmConsoleUi.Theme.TextTertiary);
            label.style.marginBottom = 6;
            sidebar.Add(label);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            listContainer = scroll.contentContainer;
            sidebar.Add(scroll);
            return sidebar;
        }

        private VisualElement BuildContentPane()
        {
            var pane = new VisualElement();
            pane.style.flexGrow = 1;
            pane.style.backgroundColor = ContentVisualWarmConsoleUi.Theme.ContentBg;
            pane.style.flexDirection = FlexDirection.Column;

            statusHelpBox = ContentVisualWarmConsoleUi.CreateStatusHelpBox("就绪");
            statusHelpBox.style.marginLeft = 12;
            statusHelpBox.style.marginRight = 12;
            statusHelpBox.style.marginTop = 6;
            statusHelpBox.style.marginBottom = 4;
            pane.Add(statusHelpBox);

            var scroll = ContentVisualWarmConsoleUi.CreateContentScroll(out contentRoot);
            scroll.contentContainer.style.paddingTop = 8;
            scroll.contentContainer.style.paddingBottom = 10;
            scroll.contentContainer.style.paddingLeft = 12;
            scroll.contentContainer.style.paddingRight = 12;
            pane.Add(scroll);
            return pane;
        }

        private void RefreshAll()
        {
            RefreshSidebar();
            RefreshContent();
            UpdateStatus();
        }

        private void RefreshSidebar()
        {
            listContainer.Clear();
            listContainer.Add(BuildFacesSectionFoldout());
            listContainer.Add(BuildEffectPoolSectionFoldout());
            listContainer.Add(BuildVfxLibrarySectionFoldout());
            listContainer.Add(BuildDecksSectionFoldout());
        }

        private VisualElement BuildSectionFoldout(string key, string title, Action<VisualElement> buildContent)
        {
            if (!foldoutState.TryGetValue(key, out var expanded))
            {
                expanded = false;
                foldoutState[key] = expanded;
            }

            var foldout = new Foldout
            {
                text = title,
                value = expanded,
            };
            foldout.style.marginBottom = 4;
            foldout.RegisterValueChangedCallback(evt => foldoutState[key] = evt.newValue);
            buildContent(foldout.contentContainer);
            return foldout;
        }

        private VisualElement BuildFacesSectionFoldout()
        {
            return BuildSectionFoldout("section:faces", "卡面", container =>
            {
                var groups = session.GetFaceDeckGroups();
                if (groups.Count == 0)
                {
                    container.Add(ContentVisualWarmConsoleUi.CreateDescriptionLabel("（无卡面条目）"));
                    return;
                }

                for (var i = 0; i < groups.Count; i++)
                {
                    container.Add(BuildFaceDeckGroupFoldout(groups[i]));
                }
            });
        }

        private VisualElement BuildFaceDeckGroupFoldout(CardPresentationSidebarDeckGroup deck)
        {
            var key = "faceDeck:" + deck.DeckId;
            if (!foldoutState.TryGetValue(key, out var expanded))
            {
                expanded = false;
                foldoutState[key] = expanded;
            }

            var foldout = new Foldout
            {
                text = deck.Title + " (" + deck.Entries.Count + ")",
                value = expanded,
            };
            foldout.style.marginLeft = 8;
            foldout.style.marginBottom = 2;
            foldout.RegisterValueChangedCallback(evt => foldoutState[key] = evt.newValue);

            for (var i = 0; i < deck.Entries.Count; i++)
            {
                foldout.contentContainer.Add(BuildFaceEntryButton(deck.Entries[i]));
            }

            return foldout;
        }

        private VisualElement BuildEffectPoolSectionFoldout()
        {
            return BuildSectionFoldout("section:effects", "效果池", container =>
            {
                container.Add(BuildDescriptionGlossaryNavButton());

                var insertables = session.GetInsertableIcons();
                if (insertables.Count > 0)
                {
                    var insertFoldout = new Foldout
                    {
                        text = "可插入装配槽编码",
                        value = false,
                    };
                    insertFoldout.style.marginLeft = 4;
                    insertFoldout.style.marginBottom = 4;
                    for (var i = 0; i < insertables.Count; i++)
                    {
                        insertFoldout.contentContainer.Add(BuildInsertableTokenRow(insertables[i]));
                    }

                    container.Add(insertFoldout);
                }

                var catalogCodes = ListCatalogIconCodes();
                if (catalogCodes.Count > 0)
                {
                    var catalogFoldout = new Foldout
                    {
                        text = "描述词条编码（点复制）",
                        value = false,
                    };
                    catalogFoldout.style.marginLeft = 4;
                    catalogFoldout.style.marginBottom = 4;
                    for (var i = 0; i < catalogCodes.Count; i++)
                    {
                        catalogFoldout.contentContainer.Add(BuildCatalogTokenRow(catalogCodes[i]));
                    }

                    container.Add(catalogFoldout);
                }

                var templates = session.EffectTemplates;
                if (templates.Count == 0)
                {
                    container.Add(ContentVisualWarmConsoleUi.CreateDescriptionLabel("（无效果模板）"));
                    return;
                }

                var buckets = new Dictionary<EffectTemplateOriginCategory, List<EffectTemplateEditorIO.TemplateRow>>();
                void AddToBucket(EffectTemplateOriginCategory category, EffectTemplateEditorIO.TemplateRow row)
                {
                    if (!buckets.TryGetValue(category, out var list))
                    {
                        list = new List<EffectTemplateEditorIO.TemplateRow>();
                        buckets[category] = list;
                    }

                    list.Add(row);
                }

                for (var i = 0; i < templates.Count; i++)
                {
                    var row = templates[i];
                    if (row == null || string.IsNullOrWhiteSpace(row.id))
                    {
                        continue;
                    }

                    AddToBucket(CardPresentationEditorSession.ResolveEffectTemplateCategory(row.id), row);
                }

                var order = new[]
                {
                    EffectTemplateOriginCategory.Item,
                    EffectTemplateOriginCategory.Relic,
                    EffectTemplateOriginCategory.MonsterSkill,
                    EffectTemplateOriginCategory.TrapSkill,
                    EffectTemplateOriginCategory.Other,
                };
                for (var o = 0; o < order.Length; o++)
                {
                    var category = order[o];
                    if (!buckets.TryGetValue(category, out var list) || list.Count == 0)
                    {
                        continue;
                    }

                    list.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
                    var catKey = "effect-cat:" + category;
                    if (!foldoutState.TryGetValue(catKey, out var catExpanded))
                    {
                        catExpanded = category != EffectTemplateOriginCategory.Other;
                        foldoutState[catKey] = catExpanded;
                    }

                    var catFoldout = new Foldout
                    {
                        text = CardPresentationEditorSession.ResolveEffectCategoryTitle(category)
                               + " · " + list.Count,
                        value = catExpanded,
                    };
                    catFoldout.style.marginLeft = 4;
                    catFoldout.style.marginBottom = 2;
                    catFoldout.RegisterValueChangedCallback(evt => foldoutState[catKey] = evt.newValue);
                    for (var i = 0; i < list.Count; i++)
                    {
                        catFoldout.contentContainer.Add(BuildTemplateEntryButton(list[i], includeCategorySuffix: false));
                    }

                    container.Add(catFoldout);
                }
            });
        }

        private VisualElement BuildVfxLibrarySectionFoldout()
        {
            return BuildSectionFoldout("section:vfx", "特效库", container =>
            {
                var groups = session.GetVisualEffectSidebarGroups();
                if (groups.Count == 0)
                {
                    container.Add(ContentVisualWarmConsoleUi.CreateDescriptionLabel(
                        "（无特效条目 — 请先跑 NineGrid/Tools/Migrate Visual Effects To Resources）"));
                    return;
                }

                for (var i = 0; i < groups.Count; i++)
                {
                    var cat = groups[i];
                    var catKey = "vfx-cat:" + cat.Category;
                    if (!foldoutState.TryGetValue(catKey, out var catExpanded))
                    {
                        catExpanded = false;
                        foldoutState[catKey] = catExpanded;
                    }

                    var catFoldout = new Foldout
                    {
                        text = cat.Title + " · " + cat.Variants.Count,
                        value = catExpanded,
                    };
                    catFoldout.style.marginLeft = 4;
                    catFoldout.style.marginBottom = 2;
                    catFoldout.RegisterValueChangedCallback(evt => foldoutState[catKey] = evt.newValue);

                    foreach (var variant in cat.Variants.Values.OrderBy(v => v.VariantId, StringComparer.OrdinalIgnoreCase))
                    {
                        catFoldout.contentContainer.Add(BuildVfxVariantButton(cat.Category, variant));
                    }

                    container.Add(catFoldout);
                }
            });
        }

        private VisualElement BuildVfxVariantButton(string category, VisualEffectSidebarVariantGroup variant)
        {
            var focused = session.GetFocusedVisualEffect();
            var selected = focused?.Dto != null
                && string.Equals(focused.Dto.category, category, StringComparison.OrdinalIgnoreCase)
                && string.Equals(focused.Dto.variantId, variant.VariantId, StringComparison.Ordinal);

            var button = new Button(() => SelectVisualEffectVariant(category, variant.VariantId))
            {
                text = variant.VariantId,
            };
            button.style.unityTextAlign = TextAnchor.MiddleLeft;
            button.style.marginLeft = 8;
            button.style.marginBottom = 1;
            button.style.height = 22;
            if (selected)
            {
                button.style.backgroundColor = ContentVisualWarmConsoleUi.Theme.NavSelectedBg;
            }

            return button;
        }

        private void SelectVisualEffectVariant(string category, string variantId)
        {
            var leaf = session.PreferDefaultLeafForVariant(category, variantId);
            if (leaf == null)
            {
                return;
            }

            session.FocusVisualEffect(leaf.Id);
            vfxPreviewFingerprint = string.Empty;
            RefreshAll();
        }

        private void SelectVisualEffectLeaf(string visualEffectId)
        {
            session.FocusVisualEffect(visualEffectId);
            vfxPreviewFingerprint = string.Empty;
            RefreshAll();
        }

        private VisualElement BuildDecksSectionFoldout()
        {
            return BuildSectionFoldout("section:decks", "卡组·卡背", container =>
            {
                var decks = session.DeckEntries;
                if (decks.Count == 0)
                {
                    container.Add(ContentVisualWarmConsoleUi.CreateDescriptionLabel("（无卡组条目）"));
                }
                else
                {
                    for (var i = 0; i < decks.Count; i++)
                    {
                        container.Add(BuildDeckEntryButton(decks[i]));
                    }
                }

                container.Add(BuildCreateDeckInlineForm());
            });
        }

        private VisualElement BuildCreateDeckInlineForm()
        {
            var box = new VisualElement();
            box.style.marginTop = 8;
            box.style.paddingTop = 6;
            box.style.paddingBottom = 4;
            box.style.paddingLeft = 4;
            box.style.paddingRight = 4;
            box.style.backgroundColor = ContentVisualWarmConsoleUi.Theme.NavNormalBg;
            box.style.borderTopLeftRadius = box.style.borderTopRightRadius = 4;
            box.style.borderBottomLeftRadius = box.style.borderBottomRightRadius = 4;

            box.Add(ContentVisualWarmConsoleUi.CreateTitleLabel(
                "新建卡组", 11, true, ContentVisualWarmConsoleUi.Theme.TextPrimary));

            var idField = new TextField { value = newDeckIdDraft ?? string.Empty };
            idField.RegisterValueChangedCallback(evt => newDeckIdDraft = evt.newValue ?? string.Empty);
            box.Add(ContentVisualWarmConsoleUi.WrapControlRow("deckId", idField, 56f));

            var nameField = new TextField { value = newDeckNameDraft ?? string.Empty };
            nameField.RegisterValueChangedCallback(evt => newDeckNameDraft = evt.newValue ?? string.Empty);
            box.Add(ContentVisualWarmConsoleUi.WrapControlRow("显示名", nameField, 56f));

            var createBtn = new Button(() =>
            {
                if (!session.TryCreateDeck(newDeckIdDraft, newDeckNameDraft, out var error))
                {
                    EditorUtility.DisplayDialog("新建卡组失败", error ?? "未知错误", "确定");
                    return;
                }

                newDeckIdDraft = string.Empty;
                newDeckNameDraft = string.Empty;
                previewFingerprint = string.Empty;
                RefreshAll();
            })
            {
                text = "新建卡组",
            };
            createBtn.style.marginTop = 4;
            box.Add(createBtn);
            return box;
        }

        private VisualElement BuildDescriptionGlossaryNavButton()
        {
            var selected = session.FocusKind == CardPresentationEditorFocusKind.DescriptionGlossary;
            return BuildNavButton(
                selected,
                "描述词条",
                "内联图标 · 基础预制体预览",
                () => OpenDescriptionRichTextPage());
        }

        private VisualElement BuildFaceEntryButton(CardPresentationEditorEntry entry)
        {
            var selected = session.FocusKind == CardPresentationEditorFocusKind.Face
                           && string.Equals(entry.ContentId, session.FocusedContentId, StringComparison.Ordinal);
            var title = FormatEntryTitle(entry);
            return BuildNavButton(
                selected,
                title,
                entry.ContentId,
                () =>
                {
                    EnsureDescriptionIconPipeline();
                    session.FocusFace(entry.ContentId);
                    faceUp = true;
                    previewAnimSlot = CardAnimSlotIds.Idle;
                    previewFingerprint = string.Empty;
                    RefreshAll();
                });
        }

        private VisualElement BuildDeckEntryButton(CardPresentationEditorEntry entry)
        {
            var selected = session.FocusKind == CardPresentationEditorFocusKind.Deck
                           && string.Equals(entry.ContentId, session.FocusedContentId, StringComparison.Ordinal);
            var title = FormatEntryTitle(entry);
            return BuildNavButton(
                selected,
                title,
                entry.ContentId,
                () =>
                {
                    EnsureDescriptionIconPipeline();
                    session.FocusDeck(entry.ContentId);
                    faceUp = false;
                    previewFingerprint = string.Empty;
                    RefreshAll();
                });
        }

        private VisualElement BuildTemplateEntryButton(
            EffectTemplateEditorIO.TemplateRow row,
            bool includeCategorySuffix = true)
        {
            var selected = session.FocusKind == CardPresentationEditorFocusKind.EffectTemplate
                           && string.Equals(row.id, session.FocusedTemplateId, StringComparison.Ordinal);
            var design = CardPresentationEditorSession.FormatEffectTemplateChoiceLabel(
                row.id,
                row.design_text,
                includeCategorySuffix);
            var title = design;
            if (title.Length > 36)
            {
                title = title.Substring(0, 36) + "…";
            }

            if (row.IsDirty)
            {
                title = "• " + title;
            }

            return BuildNavButton(
                selected,
                title,
                row.id,
                () =>
                {
                    EnsureDescriptionIconPipeline();
                    session.FocusEffectTemplate(row.id);
                    previewFingerprint = string.Empty;
                    RefreshAll();
                });
        }

        private static string FormatEntryTitle(CardPresentationEditorEntry entry)
        {
            var titleText = entry.DisplayName;
            if (string.IsNullOrWhiteSpace(titleText))
            {
                titleText = entry.ContentId;
            }

            if (entry.IsDirty)
            {
                titleText = "• " + titleText;
            }

            return titleText;
        }

        private static VisualElement BuildNavButton(
            bool selected,
            string title,
            string subtitle,
            Action onClick)
        {
            var btn = new VisualElement();
            btn.style.flexDirection = FlexDirection.Row;
            btn.style.marginBottom = 2;
            btn.style.overflow = Overflow.Hidden;
            btn.style.borderTopLeftRadius = btn.style.borderTopRightRadius = 4;
            btn.style.borderBottomLeftRadius = btn.style.borderBottomRightRadius = 4;
            btn.style.backgroundColor = selected
                ? ContentVisualWarmConsoleUi.Theme.NavSelectedBg
                : ContentVisualWarmConsoleUi.Theme.NavNormalBg;

            var stripe = new VisualElement();
            stripe.style.width = 3;
            stripe.style.backgroundColor = selected
                ? ContentVisualWarmConsoleUi.Theme.AccentStrong
                : ContentVisualWarmConsoleUi.Theme.NavStripeNormal;
            btn.Add(stripe);

            var body = new VisualElement();
            body.style.flexGrow = 1;
            body.style.paddingTop = 4;
            body.style.paddingBottom = 4;
            body.style.paddingLeft = 6;
            body.style.paddingRight = 4;
            body.Add(ContentVisualWarmConsoleUi.CreateTitleLabel(
                title, 11, true, ContentVisualWarmConsoleUi.Theme.TextPrimary));
            body.Add(ContentVisualWarmConsoleUi.CreateTinyPathLabel(subtitle));
            btn.Add(body);

            btn.RegisterCallback<ClickEvent>(_ => onClick?.Invoke());
            return btn;
        }

        private void OpenDescriptionRichTextPage(string selectCode = null)
        {
            session.FocusDescriptionGlossary();
            if (!string.IsNullOrEmpty(selectCode))
            {
                richTextSelectedCode = selectCode;
            }

            EnsureInlineIconStyleLoaded();
            EnsureIconCatalogLoaded();
            CardFacePresentationBinder.SetInlineIconStyleOverride(inlineIconStyle);
            CardFacePresentationBinder.SetDescriptionIconCatalogOverride(iconCatalog);
            previewFingerprint = string.Empty;
            RefreshAll();
        }

        private void RefreshContent()
        {
            contentRoot.Clear();

            switch (session.FocusKind)
            {
                case CardPresentationEditorFocusKind.DescriptionGlossary:
                    BuildDescriptionRichTextContent();
                    return;
                case CardPresentationEditorFocusKind.Face:
                    BuildFaceContent();
                    return;
                case CardPresentationEditorFocusKind.EffectTemplate:
                    BuildEffectTemplateContent();
                    return;
                case CardPresentationEditorFocusKind.Deck:
                    BuildDeckContent();
                    return;
                case CardPresentationEditorFocusKind.VisualEffect:
                    BuildVisualEffectContent();
                    return;
                default:
                    contentRoot.Add(ContentVisualWarmConsoleUi.CreatePageHeader(
                        "未选择",
                        "从左侧选择卡面、效果模板、特效、卡组，或打开「描述词条」。"));
                    previewFingerprint = string.Empty;
                    vfxPreviewFingerprint = string.Empty;
                    return;
            }
        }

        private void BuildVisualEffectContent()
        {
            var row = session.GetFocusedVisualEffect();
            var dto = row?.Dto;
            if (dto == null)
            {
                contentRoot.Add(ContentVisualWarmConsoleUi.CreatePageHeader(
                    "未选择",
                    "从左侧特效库选择一个变体。"));
                vfxPreviewFingerprint = string.Empty;
                return;
            }

            var title = string.IsNullOrWhiteSpace(dto.displayName) ? dto.variantId : dto.displayName;
            contentRoot.Add(ContentVisualWarmConsoleUi.CreatePageHeaderCompact(
                title,
                dto.id + " · " + VisualEffectCatalogEditorIO.GetCategoryLabel(dto.category)
                + (row.IsDirty ? " · 未保存" : string.Empty)));

            var leaves = session.GetLeavesForVariant(dto.category, dto.variantId);

            var mainRow = new VisualElement();
            mainRow.style.flexDirection = FlexDirection.Row;
            mainRow.style.alignItems = Align.Stretch;
            mainRow.style.flexGrow = 1;
            mainRow.style.minHeight = 440;
            contentRoot.Add(mainRow);

            var previewCard = ContentVisualWarmConsoleUi.CreateSectionCard(
                "预览",
                "左卡参照 · 右特效（自动取景）",
                column =>
                {
                    vfxPreviewContainer = new IMGUIContainer(() =>
                    {
                        EnsureVfxPreview(row);
                        var rect = GUILayoutUtility.GetRect(
                            1f, 1f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                        vfxPreviewHost.Draw(rect);
                    });
                    vfxPreviewContainer.style.minHeight = 420;
                    vfxPreviewContainer.style.flexGrow = 1;
                    column.Add(vfxPreviewContainer);
                });
            previewCard.style.flexGrow = 1.15f;
            previewCard.style.flexBasis = 0;
            previewCard.style.flexShrink = 1;
            previewCard.style.minWidth = 280;
            previewCard.style.marginRight = 8;
            previewCard.style.minHeight = 460;
            mainRow.Add(previewCard);

            var sideColumn = new VisualElement();
            sideColumn.style.flexGrow = 0.85f;
            sideColumn.style.flexBasis = 0;
            sideColumn.style.flexShrink = 1;
            sideColumn.style.minWidth = 260;
            sideColumn.style.maxWidth = 420;
            mainRow.Add(sideColumn);

            sideColumn.Add(ContentVisualWarmConsoleUi.CreateSectionCard(
                "尺寸 / 颜色",
                "同一变体下的 large/small 与色板",
                column =>
                {
                    var chipRow = new VisualElement();
                    chipRow.style.flexDirection = FlexDirection.Row;
                    chipRow.style.flexWrap = Wrap.Wrap;
                    for (var i = 0; i < leaves.Count; i++)
                    {
                        var leaf = leaves[i];
                        if (leaf?.Dto == null)
                        {
                            continue;
                        }

                        var id = leaf.Dto.id;
                        var selected = string.Equals(id, dto.id, StringComparison.Ordinal);
                        var chip = new Button(() => SelectVisualEffectLeaf(id))
                        {
                            text = leaf.Dto.size + " · " + leaf.Dto.color,
                        };
                        chip.style.marginRight = 4;
                        chip.style.marginBottom = 4;
                        chip.style.height = 24;
                        if (selected)
                        {
                            chip.style.backgroundColor = ContentVisualWarmConsoleUi.Theme.NavSelectedBg;
                        }

                        chipRow.Add(chip);
                    }

                    column.Add(chipRow);
                }));

            sideColumn.Add(ContentVisualWarmConsoleUi.CreateSectionCard(
                "播放控制",
                "所见即所得：速度与大小写回 visual_effects.json",
                column =>
                {
                    var playing = vfxPreviewHost.Player != null && vfxPreviewHost.Player.IsPlaying;
                    var transport = new VisualElement();
                    transport.style.flexDirection = FlexDirection.Row;
                    transport.style.flexWrap = Wrap.Wrap;
                    transport.style.marginBottom = 8;
                    transport.Add(new Button(() =>
                    {
                        EnsureVfxPreview(row, force: true);
                        vfxPreviewHost.Player?.Play();
                        RefreshContent();
                        UpdateStatus();
                    })
                    {
                        text = "继续",
                    });
                    transport.Add(new Button(() =>
                    {
                        vfxPreviewHost.Player?.Pause();
                        RefreshContent();
                        UpdateStatus();
                    })
                    {
                        text = "暂停",
                    });
                    transport.Add(new Button(() =>
                    {
                        vfxPreviewHost.Player?.Stop();
                        vfxPreviewHost.Player?.Play();
                        UpdateStatus();
                    })
                    {
                        text = "重播",
                    });
                    var stateLabel = ContentVisualWarmConsoleUi.CreateDescriptionLabel(
                        playing ? "状态：播放中（循环）" : "状态：已暂停");
                    stateLabel.style.marginLeft = 8;
                    transport.Add(stateLabel);
                    column.Add(transport);

                    var fpsField = new FloatField("播放速度 (FPS)")
                    {
                        value = dto.defaultFps,
                    };
                    fpsField.RegisterValueChangedCallback(evt =>
                    {
                        dto.defaultFps = Mathf.Max(0.01f, evt.newValue);
                        vfxPreviewHost.ApplyFps(dto.defaultFps);
                        UpdateStatus();
                    });
                    column.Add(fpsField);

                    var scaleField = new FloatField("大小 (Scale)")
                    {
                        value = dto.defaultScale,
                    };
                    scaleField.RegisterValueChangedCallback(evt =>
                    {
                        dto.defaultScale = Mathf.Max(0.01f, evt.newValue);
                        vfxPreviewHost.ApplyScale(dto.defaultScale);
                        vfxPreviewContainer?.MarkDirtyRepaint();
                        UpdateStatus();
                    });
                    column.Add(scaleField);

                    var nameField = new TextField("显示名")
                    {
                        value = dto.displayName ?? string.Empty,
                    };
                    nameField.RegisterValueChangedCallback(evt =>
                    {
                        dto.displayName = evt.newValue ?? string.Empty;
                        UpdateStatus();
                    });
                    column.Add(nameField);

                    var pathLabel = ContentVisualWarmConsoleUi.CreateDescriptionLabel(
                        "sheetPath: " + dto.sheetPath);
                    pathLabel.style.whiteSpace = WhiteSpace.Normal;
                    pathLabel.style.marginTop = 6;
                    column.Add(pathLabel);
                }));
        }

        private void EnsureVfxPreview(VisualEffectCatalogEditorIO.EditorRow row, bool force = false)
        {
            var dto = row?.Dto;
            if (dto == null)
            {
                return;
            }

            var fingerprint = dto.id + "|" + dto.sheetPath;
            if (!force && fingerprint == vfxPreviewFingerprint && vfxPreviewHost.SceneRoot != null)
            {
                return;
            }

            vfxPreviewFingerprint = fingerprint;
            var request = VisualEffectPreviewHost.BuildFallbackMonsterRequest();
            vfxPreviewHost.Rebuild(dto, request);
            if (vfxPreviewHost.Player != null)
            {
                vfxPreviewHost.ApplyFps(dto.defaultFps);
                vfxPreviewHost.ApplyScale(dto.defaultScale);
            }
        }

        private void BuildFaceContent()
        {
            var entry = session.GetFocusedFace();
            if (entry?.Dto == null)
            {
                contentRoot.Add(ContentVisualWarmConsoleUi.CreatePageHeader(
                    "未选择",
                    "从左侧卡面树选择一张卡。"));
                previewFingerprint = string.Empty;
                return;
            }

            var dto = entry.Dto;
            if (session.TryExpandSkillIdsIntoAssemblies(dto))
            {
                OnDtoEdited(entry);
            }

            contentRoot.Add(ContentVisualWarmConsoleUi.CreatePageHeaderCompact(
                string.IsNullOrWhiteSpace(dto.displayName) ? dto.contentId : dto.displayName,
                dto.contentId + " · " + dto.kind
                + (entry.IsDirty ? " · 未保存" : string.Empty)));

            var topRow = new VisualElement();
            topRow.style.flexDirection = FlexDirection.Row;
            topRow.style.marginBottom = 6;
            topRow.style.alignItems = Align.Stretch;
            contentRoot.Add(topRow);

            var previewCard = ContentVisualWarmConsoleUi.CreateSectionCard(
                "预览",
                null,
                column =>
                {
                    previewContainer = new IMGUIContainer(() =>
                    {
                        EnsurePreview(entry);
                        var rect = GUILayoutUtility.GetRect(
                            1f, 1f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                        previewHost.Draw(rect);
                    });
                    previewContainer.style.minHeight = 340;
                    previewContainer.style.minWidth = 260;
                    column.Add(previewContainer);

                    column.Add(ContentVisualWarmConsoleUi.CreateButtonRow(
                        new Button(() =>
                        {
                            previewFingerprint = string.Empty;
                            EnsurePreview(entry, force: true);
                            previewContainer?.MarkDirtyRepaint();
                        }) { text = "刷新预览" },
                        new Button(() => ToggleFlip(entry)) { text = faceUp ? "翻到背面" : "翻到正面" }));
                });
            previewCard.style.flexGrow = 1;
            previewCard.style.flexBasis = 0;
            previewCard.style.marginRight = 6;
            topRow.Add(previewCard);

            var paramsCard = ContentVisualWarmConsoleUi.CreateSectionCard(
                "即时参数",
                null,
                column => BuildInstantParams(column, entry));
            paramsCard.style.flexGrow = 1;
            paramsCard.style.flexBasis = 0;
            topRow.Add(paramsCard);

            var bottomRow = new VisualElement();
            bottomRow.style.flexDirection = FlexDirection.Row;
            bottomRow.style.alignItems = Align.Stretch;
            bottomRow.style.marginBottom = 6;
            contentRoot.Add(bottomRow);

            var descriptionCard = ContentVisualWarmConsoleUi.CreateSectionCard(
                "描述",
                "[Code] 词条图标见左侧「效果池 → 描述词条 / 可插入编码」；装配效果时自动写入参数化简要描述；手改后锁定自定义，清空后恢复自动",
                column =>
                {
                    EnsureDescriptionIconPipeline();
                    entry.DescriptionCustomLocked = session.InferDescriptionCustomLocked(dto);
                    if (!entry.DescriptionCustomLocked)
                    {
                        session.TrySyncAutoDescription(dto, customLocked: false);
                    }

                    descriptionModeLabel = ContentVisualWarmConsoleUi.CreateDescriptionLabel(string.Empty);
                    descriptionModeLabel.style.marginBottom = 4;
                    column.Add(descriptionModeLabel);
                    RefreshDescriptionModeLabel(entry);

                    descriptionField = new TextField
                    {
                        multiline = true,
                        value = dto.description ?? string.Empty,
                    };
                    descriptionField.style.minHeight = 64;
                    descriptionField.style.maxHeight = 96;
                    descriptionField.RegisterValueChangedCallback(evt =>
                    {
                        if (suppressDescriptionCallback)
                        {
                            return;
                        }

                        var text = evt.newValue ?? string.Empty;
                        dto.description = text;
                        if (string.IsNullOrWhiteSpace(text))
                        {
                            entry.DescriptionCustomLocked = false;
                            if (session.TrySyncAutoDescription(dto, customLocked: false))
                            {
                                SetDescriptionFieldValue(dto.description);
                            }
                        }
                        else
                        {
                            var auto = session.BuildAutoCardDescription(dto.effectAssemblies);
                            entry.DescriptionCustomLocked = !string.Equals(
                                text.Trim(),
                                auto.Trim(),
                                StringComparison.Ordinal);
                        }

                        RefreshDescriptionModeLabel(entry);
                        session.MarkDirty(dto.contentId);
                        InvalidateAndRefreshPreview(entry);
                        UpdateStatus();
                    });
                    column.Add(descriptionField);
                });
            descriptionCard.style.flexGrow = 1;
            descriptionCard.style.flexBasis = 0;
            descriptionCard.style.marginRight = 6;
            bottomRow.Add(descriptionCard);

            var animationCard = ContentVisualWarmConsoleUi.CreateSectionCard(
                "动画",
                "none / folder / atlas",
                column => BuildAnimationSection(column, entry));
            animationCard.style.flexGrow = 2;
            animationCard.style.flexBasis = 0;
            bottomRow.Add(animationCard);

            var effectAssemblyCard = ContentVisualWarmConsoleUi.CreateSectionCard(
                "效果装配",
                "按卡种只挂同类效果（道具/遗物/怪物技能）；可搜索选模板多载。下拉文案用 {amount} 等参数位，数值只看下方 argsJson。清空后白板。旧 skillIds 打开时会展开进本列表",
                column => BuildEffectAssemblySection(column, entry));
            effectAssemblyCard.style.marginBottom = 6;
            contentRoot.Add(effectAssemblyCard);

            var extraOuter = new VisualElement();
            extraOuter.style.flexDirection = FlexDirection.Row;
            extraOuter.style.marginBottom = 12;
            extraOuter.style.backgroundColor = ContentVisualWarmConsoleUi.Theme.SectionCardBg;
            extraOuter.style.borderTopLeftRadius = extraOuter.style.borderTopRightRadius = 8;
            extraOuter.style.borderBottomLeftRadius = extraOuter.style.borderBottomRightRadius = 8;
            extraOuter.style.overflow = Overflow.Hidden;
            var extraStripe = new VisualElement();
            extraStripe.style.width = 3;
            extraStripe.style.backgroundColor = ContentVisualWarmConsoleUi.Theme.AccentWeak;
            extraOuter.Add(extraStripe);
            var extraInner = new VisualElement();
            extraInner.style.flexGrow = 1;
            extraInner.style.paddingTop = 8;
            extraInner.style.paddingBottom = 10;
            extraInner.style.paddingLeft = 8;
            extraInner.style.paddingRight = 10;
            var extraFoldout = new Foldout { text = "其他配置", value = false };
            extraFoldout.style.unityFontStyleAndWeight = FontStyle.Bold;
            extraFoldout.style.fontSize = 13;
            extraFoldout.style.color = ContentVisualWarmConsoleUi.Theme.TextPrimary;
            var extraDesc = ContentVisualWarmConsoleUi.CreateDescriptionLabel("extraSlots");
            extraDesc.style.marginLeft = 4;
            extraDesc.style.marginTop = 6;
            extraDesc.style.marginBottom = 8;
            extraFoldout.contentContainer.Add(extraDesc);
            var extraColumn = new VisualElement { style = { flexDirection = FlexDirection.Column } };
            BuildExtraSlotsSection(extraColumn, entry);
            extraFoldout.contentContainer.Add(extraColumn);
            extraInner.Add(extraFoldout);
            extraOuter.Add(extraInner);
            contentRoot.Add(extraOuter);

            EnsurePreview(entry);
        }

        private void BuildEffectTemplateContent()
        {
            var row = session.GetFocusedTemplate();
            if (row == null)
            {
                contentRoot.Add(ContentVisualWarmConsoleUi.CreatePageHeader(
                    "未选择",
                    "从左侧效果池选择一条模板。"));
                previewFingerprint = string.Empty;
                return;
            }

            contentRoot.Add(ContentVisualWarmConsoleUi.CreatePageHeaderCompact(
                row.id,
                "效果模板" + (row.IsDirty ? " · 未保存" : string.Empty)));

            var topRow = new VisualElement();
            topRow.style.flexDirection = FlexDirection.Row;
            topRow.style.marginBottom = 6;
            topRow.style.alignItems = Align.Stretch;
            contentRoot.Add(topRow);

            var previewCard = ContentVisualWarmConsoleUi.CreateSectionCard(
                "描述预览",
                "只预览 design_text 内容框；[Code] 走描述词条图标表",
                column =>
                {
                    previewContainer = new IMGUIContainer(() =>
                    {
                        EnsureTemplatePreview(row);
                        var rect = GUILayoutUtility.GetRect(
                            1f, 1f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                        previewHost.Draw(rect);
                    });
                    previewContainer.style.minHeight = 340;
                    previewContainer.style.minWidth = 260;
                    column.Add(previewContainer);

                    column.Add(ContentVisualWarmConsoleUi.CreateButtonRow(
                        new Button(() =>
                        {
                            previewFingerprint = string.Empty;
                            EnsureTemplatePreview(row, force: true);
                            previewContainer?.MarkDirtyRepaint();
                        }) { text = "刷新预览" }));
                });
            previewCard.style.flexGrow = 1;
            previewCard.style.flexBasis = 0;
            previewCard.style.marginRight = 6;
            topRow.Add(previewCard);

            var metaCard = ContentVisualWarmConsoleUi.CreateSectionCard(
                "模板元数据",
                null,
                column =>
                {
                    var idField = new TextField { value = row.id, isReadOnly = true };
                    column.Add(ContentVisualWarmConsoleUi.WrapControlRow("id", idField, 72f));

                    var stateField = new TextField { value = row.state ?? string.Empty };
                    stateField.RegisterValueChangedCallback(evt =>
                    {
                        row.state = evt.newValue ?? string.Empty;
                        previewFingerprint = string.Empty;
                        UpdateStatus();
                        RefreshSidebar();
                    });
                    column.Add(ContentVisualWarmConsoleUi.WrapControlRow("state", stateField, 72f));

                    var designField = new TextField
                    {
                        multiline = true,
                        value = row.design_text ?? string.Empty,
                    };
                    designField.style.minHeight = 72;
                    designField.RegisterValueChangedCallback(evt =>
                    {
                        row.design_text = evt.newValue ?? string.Empty;
                        previewFingerprint = string.Empty;
                        EnsureTemplatePreview(row, force: true);
                        previewContainer?.MarkDirtyRepaint();
                        UpdateStatus();
                        RefreshSidebar();
                    });
                    column.Add(ContentVisualWarmConsoleUi.WrapControlRow("design_text", designField, 72f));

                    var requiresLabel = new Label(row.requires_json ?? "[]");
                    requiresLabel.style.whiteSpace = WhiteSpace.Normal;
                    column.Add(ContentVisualWarmConsoleUi.WrapControlRow("requires", requiresLabel, 72f));

                    var conditionsLabel = new Label(row.conditions_json ?? "[]");
                    conditionsLabel.style.whiteSpace = WhiteSpace.Normal;
                    column.Add(ContentVisualWarmConsoleUi.WrapControlRow("conditions", conditionsLabel, 72f));

                    var bodyFoldout = new Foldout { text = "body（只读）", value = false };
                    var bodyField = new TextField
                    {
                        multiline = true,
                        value = row.body ?? string.Empty,
                        isReadOnly = true,
                    };
                    bodyField.style.minHeight = 120;
                    bodyFoldout.contentContainer.Add(bodyField);
                    column.Add(bodyFoldout);
                });
            metaCard.style.flexGrow = 1;
            metaCard.style.flexBasis = 0;
            topRow.Add(metaCard);

            EnsureTemplatePreview(row);
        }

        private void BuildDeckContent()
        {
            var entry = session.GetFocusedDeck();
            if (entry?.Dto == null)
            {
                contentRoot.Add(ContentVisualWarmConsoleUi.CreatePageHeader(
                    "未选择",
                    "从左侧卡组·卡背选择一条。"));
                previewFingerprint = string.Empty;
                return;
            }

            var dto = entry.Dto;
            if (dto.sprites == null)
            {
                dto.sprites = new CardPresentationSpritesDto();
            }

            contentRoot.Add(ContentVisualWarmConsoleUi.CreatePageHeaderCompact(
                string.IsNullOrWhiteSpace(dto.displayName) ? dto.contentId : dto.displayName,
                dto.contentId + " · Deck"
                + (entry.IsDirty ? " · 未保存" : string.Empty)));

            var topRow = new VisualElement();
            topRow.style.flexDirection = FlexDirection.Row;
            topRow.style.marginBottom = 6;
            topRow.style.alignItems = Align.Stretch;
            contentRoot.Add(topRow);

            var previewKind = ResolveDeckPreviewKind(dto.contentId);
            var previewCard = ContentVisualWarmConsoleUi.CreateSectionCard(
                "卡背预览",
                "默认看背面；空槽保留「" + previewKind + "」模板兜底卡背。卡面所属本组后，预览与运行时跟随本组卡背。",
                column =>
                {
                    previewContainer = new IMGUIContainer(() =>
                    {
                        EnsurePreview(entry);
                        var rect = GUILayoutUtility.GetRect(
                            1f, 1f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                        previewHost.Draw(rect);
                    });
                    previewContainer.style.minHeight = 340;
                    previewContainer.style.minWidth = 260;
                    column.Add(previewContainer);

                    column.Add(ContentVisualWarmConsoleUi.CreateButtonRow(
                        new Button(() =>
                        {
                            previewFingerprint = string.Empty;
                            EnsurePreview(entry, force: true);
                            previewContainer?.MarkDirtyRepaint();
                        }) { text = "刷新预览" },
                        new Button(() => ToggleFlip(entry)) { text = faceUp ? "翻到背面" : "翻到正面" }));
                });
            previewCard.style.flexGrow = 1;
            previewCard.style.flexBasis = 0;
            previewCard.style.marginRight = 6;
            topRow.Add(previewCard);

            var card = ContentVisualWarmConsoleUi.CreateSectionCard(
                "卡组配置",
                "卡背三槽；卡面 deckId 归属本组后预览/运行时跟随本组卡背",
                column =>
                {
                    column.Add(ContentVisualWarmConsoleUi.WrapControlRow(
                        "显示名",
                        BindText(dto.displayName, v =>
                        {
                            dto.displayName = v ?? string.Empty;
                            OnDeckEdited(entry);
                        })));

                    BuildDeckUsageReadOnlySection(column, dto.contentId);

                    column.Add(MakeSpriteField("卡背边框", dto.sprites.backBorder, path =>
                    {
                        dto.sprites.backBorder = path;
                        OnDeckEdited(entry);
                    }));
                    column.Add(MakeSpriteField("卡背背纹", dto.sprites.backShirt, path =>
                    {
                        dto.sprites.backShirt = path;
                        OnDeckEdited(entry);
                    }));
                    column.Add(MakeSpriteField("卡背 Logo", dto.sprites.backLogo, path =>
                    {
                        dto.sprites.backLogo = path;
                        OnDeckEdited(entry);
                    }));

                    var status = ContentVisualWarmConsoleUi.CreateDescriptionLabel(
                        "状态：" + (entry.IsDirty ? "有未保存改动" : "已同步磁盘")
                        + " · 预览模板 " + previewKind);
                    status.style.marginTop = 8;
                    column.Add(status);
                });
            card.style.flexGrow = 1;
            card.style.flexBasis = 0;
            topRow.Add(card);

            EnsurePreview(entry);
        }

        private void BuildDeckUsageReadOnlySection(VisualElement column, string deckContentId)
        {
            var usageBox = new VisualElement();
            usageBox.style.marginTop = 6;
            usageBox.style.marginBottom = 6;
            usageBox.style.paddingTop = 8;
            usageBox.style.paddingBottom = 8;
            usageBox.style.paddingLeft = 10;
            usageBox.style.paddingRight = 10;
            usageBox.style.backgroundColor = ContentVisualWarmConsoleUi.Theme.NavNormalBg;
            usageBox.style.borderTopLeftRadius = usageBox.style.borderTopRightRadius = 4;
            usageBox.style.borderBottomLeftRadius = usageBox.style.borderBottomRightRadius = 4;

            usageBox.Add(ContentVisualWarmConsoleUi.CreateTitleLabel(
                "遭遇编排（只读）",
                11,
                true,
                ContentVisualWarmConsoleUi.Theme.TextPrimary));

            var tableHint = ContentVisualWarmConsoleUi.CreateDescriptionLabel(
                "玩法登记：Assets/Arts/ContentVisual/tables/monster_decks.json");
            tableHint.style.marginTop = 2;
            tableHint.style.marginBottom = 6;
            usageBox.Add(tableHint);

            var summary = MonsterDeckUsageInspector.InspectDeck(deckContentId, session);
            var headlineColor = summary.Status == MonsterDeckWiringStatus.Wired
                ? ContentVisualWarmConsoleUi.Theme.AccentGoldValue
                : summary.Status == MonsterDeckWiringStatus.PresentationOnly
                    ? ContentVisualWarmConsoleUi.Theme.TextSecondary
                    : ContentVisualWarmConsoleUi.Theme.AccentStrong;

            var headline = ContentVisualWarmConsoleUi.CreateDescriptionLabel(summary.StatusHeadline ?? string.Empty);
            headline.style.unityFontStyleAndWeight = FontStyle.Bold;
            headline.style.color = headlineColor;
            headline.style.marginBottom = 4;
            usageBox.Add(headline);

            if (!string.IsNullOrWhiteSpace(summary.DetailLines))
            {
                var detail = ContentVisualWarmConsoleUi.CreateDescriptionLabel(summary.DetailLines);
                detail.style.whiteSpace = WhiteSpace.Normal;
                usageBox.Add(detail);
            }

            var unwired = MonsterDeckUsageInspector.ListUnwiredDecks(session);
            if (unwired.Count > 0)
            {
                var builder = new StringBuilder(256);
                builder.AppendLine("全局未接线卡组：");
                for (var i = 0; i < unwired.Count; i++)
                {
                    var item = unwired[i];
                    builder.Append("· ").Append(item.DeckId).Append(" — ").Append(item.StatusHeadline);
                    if (i < unwired.Count - 1)
                    {
                        builder.AppendLine();
                    }
                }

                var global = ContentVisualWarmConsoleUi.CreateDescriptionLabel(builder.ToString());
                global.style.marginTop = 8;
                global.style.whiteSpace = WhiteSpace.Normal;
                global.style.color = ContentVisualWarmConsoleUi.Theme.AccentStrong;
                usageBox.Add(global);
            }

            column.Add(usageBox);
        }

        private void BuildEffectAssemblySection(VisualElement column, CardPresentationEditorEntry entry)
        {
            var dto = entry.Dto;
            if (dto.effectAssemblies == null)
            {
                dto.effectAssemblies = Array.Empty<EffectAssemblyDto>();
            }

            var list = new List<EffectAssemblyDto>(dto.effectAssemblies);
            var containerType = InferDefaultContainerType(dto.kind);
            var templateChoices = session.GetEffectTemplateChoices(
                containerType,
                includeCategorySuffix: false);
            var searchableChoices = new List<SearchableChoiceField.Choice>(templateChoices.Count);
            for (var i = 0; i < templateChoices.Count; i++)
            {
                var choice = templateChoices[i];
                if (choice == null || string.IsNullOrWhiteSpace(choice.TemplateId))
                {
                    continue;
                }

                searchableChoices.Add(new SearchableChoiceField.Choice
                {
                    Label = choice.Label,
                    Value = choice.TemplateId,
                    SearchHaystack = (choice.Label ?? string.Empty) + " " + choice.TemplateId,
                });
            }

            if (searchableChoices.Count == 0)
            {
                searchableChoices.Add(new SearchableChoiceField.Choice
                {
                    Label = "（无本类效果模板）",
                    Value = "__none__",
                    SearchHaystack = "无本类效果模板",
                });
            }

            var listRoot = new VisualElement();
            column.Add(listRoot);

            void CommitAssemblies(bool rebuildUi)
            {
                dto.effectAssemblies = list.ToArray();
                dto.skillIds = Array.Empty<string>();
                if (dto.effectAssemblies.Length == 0)
                {
                    dto.effectIds = Array.Empty<string>();
                }

                if (session.TrySyncAutoDescription(dto, entry.DescriptionCustomLocked))
                {
                    SetDescriptionFieldValue(dto.description);
                    RefreshDescriptionModeLabel(entry);
                }

                OnDtoEdited(entry);
                if (rebuildUi)
                {
                    RebuildList();
                }
            }

            void RebuildList()
            {
                listRoot.Clear();
                for (var i = 0; i < list.Count; i++)
                {
                    var index = i;
                    var assembly = list[i];
                    if (assembly == null)
                    {
                        assembly = new EffectAssemblyDto();
                        list[i] = assembly;
                    }

                    if (string.IsNullOrWhiteSpace(assembly.containerType))
                    {
                        assembly.containerType = containerType;
                    }

                    var row = new VisualElement();
                    row.style.flexDirection = FlexDirection.Column;
                    row.style.marginBottom = 8;
                    row.style.paddingLeft = 4;
                    row.style.paddingRight = 4;
                    row.style.paddingTop = 4;
                    row.style.paddingBottom = 4;
                    row.style.backgroundColor = ContentVisualWarmConsoleUi.Theme.NavNormalBg;
                    row.style.borderTopLeftRadius = row.style.borderTopRightRadius = 4;
                    row.style.borderBottomLeftRadius = row.style.borderBottomRightRadius = 4;

                    var idField = new TextField { value = assembly.id ?? string.Empty };
                    idField.RegisterValueChangedCallback(evt =>
                    {
                        assembly.id = evt.newValue ?? string.Empty;
                        CommitAssemblies(rebuildUi: false);
                    });
                    row.Add(ContentVisualWarmConsoleUi.WrapControlRow("挂载 id", idField, 56f));

                    var currentTpl = assembly.templateId ?? string.Empty;
                    var rowChoices = new List<SearchableChoiceField.Choice>(searchableChoices);
                    var hasCurrent = false;
                    for (var c = 0; c < rowChoices.Count; c++)
                    {
                        if (string.Equals(rowChoices[c].Value, currentTpl, StringComparison.Ordinal))
                        {
                            hasCurrent = true;
                            break;
                        }
                    }

                    if (!hasCurrent && !string.IsNullOrEmpty(currentTpl))
                    {
                        // 跨类遗留挂载：保留可选以便查看/改回同类，标注真实分类。
                        var orphanLabel = session.GetParameterizedDesignText(currentTpl, assembly.argsJson);
                        rowChoices.Insert(0, new SearchableChoiceField.Choice
                        {
                            Label = CardPresentationEditorSession.FormatEffectTemplateChoiceLabel(
                                currentTpl,
                                orphanLabel,
                                includeCategorySuffix: true),
                            Value = currentTpl,
                            SearchHaystack = orphanLabel + " " + currentTpl,
                        });
                    }

                    var tplField = new SearchableChoiceField();
                    tplField.SetChoices(rowChoices, string.IsNullOrEmpty(currentTpl) ? rowChoices[0].Value : currentTpl);
                    tplField.ValueChanged += picked =>
                    {
                        assembly.templateId = string.Equals(picked, "__none__", StringComparison.Ordinal)
                            ? string.Empty
                            : (picked ?? string.Empty);
                        assembly.containerType = containerType;
                        assembly.argsJson = session.SuggestArgsJsonForTemplate(assembly.templateId);
                        CommitAssemblies(rebuildUi: true);
                    };
                    row.Add(ContentVisualWarmConsoleUi.WrapControlRow("效果", tplField, 96f));

                    if (!string.IsNullOrEmpty(currentTpl))
                    {
                        row.Add(ContentVisualWarmConsoleUi.CreateTinyPathLabel(currentTpl));
                    }

                    var argsField = new TextField { value = assembly.argsJson ?? string.Empty };
                    argsField.RegisterValueChangedCallback(evt =>
                    {
                        assembly.argsJson = evt.newValue ?? string.Empty;
                        // args 只影响预览插值；自动描述存 {param}，无需因改数重写描述。
                        CommitAssemblies(rebuildUi: false);
                    });
                    row.Add(ContentVisualWarmConsoleUi.WrapControlRow("argsJson", argsField, 72f));

                    var removeBtn = new Button(() =>
                    {
                        list.RemoveAt(index);
                        CommitAssemblies(rebuildUi: true);
                    })
                    {
                        text = "删除",
                    };
                    row.Add(removeBtn);
                    listRoot.Add(row);
                }
            }

            RebuildList();

            column.Add(ContentVisualWarmConsoleUi.CreateButtonRow(
                new Button(() =>
                {
                    var templateId = searchableChoices.Count > 0
                        && !string.Equals(searchableChoices[0].Value, "__none__", StringComparison.Ordinal)
                        ? searchableChoices[0].Value
                        : string.Empty;
                    list.Add(new EffectAssemblyDto
                    {
                        id = "fx." + Guid.NewGuid().ToString("N").Substring(0, 8),
                        templateId = templateId,
                        containerType = containerType,
                        argsJson = session.SuggestArgsJsonForTemplate(templateId),
                    });
                    CommitAssemblies(rebuildUi: true);
                }) { text = "添加效果" },
                new Button(() =>
                {
                    if (list.Count == 0 && (dto.effectIds == null || dto.effectIds.Length == 0))
                    {
                        return;
                    }

                    list.Clear();
                    dto.effectIds = Array.Empty<string>();
                    CommitAssemblies(rebuildUi: true);
                }) { text = "清空全部效果" }));
        }

        private void SetDescriptionFieldValue(string value)
        {
            if (descriptionField == null)
            {
                return;
            }

            suppressDescriptionCallback = true;
            try
            {
                descriptionField.SetValueWithoutNotify(value ?? string.Empty);
            }
            finally
            {
                suppressDescriptionCallback = false;
            }
        }

        private void RefreshDescriptionModeLabel(CardPresentationEditorEntry entry)
        {
            if (descriptionModeLabel == null)
            {
                return;
            }

            descriptionModeLabel.text = entry != null && entry.DescriptionCustomLocked
                ? "模式：自定义（手改已锁定；清空描述框后恢复自动同步）"
                : "模式：自动同步（跟随效果装配增删；文案为 {param} 参数位）";
            descriptionModeLabel.style.color = entry != null && entry.DescriptionCustomLocked
                ? ContentVisualWarmConsoleUi.Theme.AccentGoldValue
                : ContentVisualWarmConsoleUi.Theme.TextSecondary;
        }

        private static string InferDefaultContainerType(string kind)
        {
            if (CardPresentationEditorSession.IsMonsterKind(kind))
            {
                return "MonsterSkill";
            }

            if (string.Equals(kind, "Trap", StringComparison.OrdinalIgnoreCase))
            {
                return "Trap";
            }

            if (string.Equals(kind, "Relic", StringComparison.OrdinalIgnoreCase))
            {
                return "Relic";
            }

            if (CardPresentationEditorSession.IsItemLikeKind(kind))
            {
                return "HelpCard";
            }

            return string.Empty;
        }

        private void BuildDescriptionRichTextContent()
        {
            EnsureInlineIconStyleLoaded();
            EnsureIconCatalogLoaded();
            CardFacePresentationBinder.SetInlineIconStyleOverride(inlineIconStyle);
            CardFacePresentationBinder.SetDescriptionIconCatalogOverride(iconCatalog);

            var dirtyHint = (inlineIconStyleDirty || iconCatalogDirty) ? " · 未保存" : string.Empty;
            contentRoot.Add(ContentVisualWarmConsoleUi.CreatePageHeaderCompact(
                "描述词条",
                "全局描述图标表 · 装配槽只读下行 · 四套基础卡面预览" + dirtyHint));

            var topRow = new VisualElement();
            topRow.style.flexDirection = FlexDirection.Row;
            topRow.style.marginBottom = 6;
            topRow.style.alignItems = Align.Stretch;
            contentRoot.Add(topRow);

            var previewCard = ContentVisualWarmConsoleUi.CreateSectionCard(
                "基础预制体预览",
                "不读已有卡牌 JSON，仅看描述内嵌图标效果",
                column =>
                {
                    var kindChoices = new List<string> { "玩家卡", "怪物卡", "道具卡", "遗物卡" };
                    var kindIndex = RichTextKindToIndex(richTextPreviewKind);
                    var kindField = new PopupField<string>(kindChoices, kindIndex);
                    kindField.RegisterValueChangedCallback(evt =>
                    {
                        richTextPreviewKind = RichTextIndexToKind(kindChoices.IndexOf(evt.newValue));
                        previewFingerprint = string.Empty;
                        RefreshContent();
                    });
                    column.Add(ContentVisualWarmConsoleUi.WrapControlRow("卡种", kindField, 72f));

                    previewContainer = new IMGUIContainer(() =>
                    {
                        EnsureRichTextPreview();
                        var rect = GUILayoutUtility.GetRect(
                            1f, 1f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                        previewHost.Draw(rect);
                    });
                    previewContainer.style.minHeight = 340;
                    previewContainer.style.minWidth = 260;
                    column.Add(previewContainer);

                    column.Add(ContentVisualWarmConsoleUi.CreateButtonRow(
                        new Button(() =>
                        {
                            previewFingerprint = string.Empty;
                            EnsureRichTextPreview(force: true);
                            previewContainer?.MarkDirtyRepaint();
                        }) { text = "刷新预览" },
                        new Button(() =>
                        {
                            TryPersistInlineIconStyle();
                            TryPersistIconCatalog();
                            RefreshContent();
                        }) { text = "保存样式与图标表" }));
                });
            previewCard.style.flexGrow = 1;
            previewCard.style.flexBasis = 0;
            previewCard.style.marginRight = 6;
            topRow.Add(previewCard);

            var paramsCard = ContentVisualWarmConsoleUi.CreateSectionCard(
                "图标映射",
                "描述专用可改 Sprite；装配下行只读，布局仍用 em",
                column => BuildRichTextParams(column));
            paramsCard.style.flexGrow = 1.35f;
            paramsCard.style.flexBasis = 0;
            topRow.Add(paramsCard);

            var sampleCard = ContentVisualWarmConsoleUi.CreateSectionCard(
                "样例描述",
                "输入框为 [Code]；装配槽走模板，自定义码走描述图标表",
                column =>
                {
                    var sampleField = new TextField
                    {
                        multiline = true,
                        value = richTextSampleDescription ?? string.Empty,
                    };
                    sampleField.style.minHeight = 72;
                    sampleField.RegisterValueChangedCallback(evt =>
                    {
                        richTextSampleDescription = evt.newValue ?? string.Empty;
                        previewFingerprint = string.Empty;
                        EnsureRichTextPreview(force: true);
                        previewContainer?.MarkDirtyRepaint();
                    });
                    column.Add(sampleField);
                });
            sampleCard.style.marginBottom = 12;
            contentRoot.Add(sampleCard);
        }

        private void BuildRichTextParams(VisualElement column)
        {
            EnsureInlineIconStyleLoaded();
            EnsureIconCatalogLoaded();
            var templateSprites = CaptureTemplateSpritesForKind(richTextPreviewKind);

            column.Add(ContentVisualWarmConsoleUi.CreateDescriptionLabel(
                "装配下行（只读 Sprite）：改图请走预制体 / 单卡装配，描述侧不可反向覆盖。"));
            BuildAssemblyDownlinkRows(column, templateSprites);

            column.Add(ContentVisualWarmConsoleUi.CreateDescriptionLabel(
                "描述专用图标表（全局）：可新建代号并选任意 Sprite，不写回预制体。"));

            var listHost = new VisualElement();
            listHost.style.flexDirection = FlexDirection.Column;
            column.Add(listHost);

            var entries = iconCatalog.Entries;
            if (entries == null || entries.Count == 0)
            {
                listHost.Add(ContentVisualWarmConsoleUi.CreateDescriptionLabel("（表空 · 点下方添加）"));
            }
            else
            {
                for (var i = 0; i < entries.Count; i++)
                {
                    var catalogEntry = entries[i];
                    if (catalogEntry == null)
                    {
                        continue;
                    }

                    listHost.Add(BuildCatalogEntryRow(catalogEntry, i));
                }
            }

            column.Add(ContentVisualWarmConsoleUi.CreateButtonRow(
                new Button(() =>
                {
                    EnsureIconCatalogLoaded();
                    var created = iconCatalog.AddBlankEntry();
                    created.code = AllocateNewCatalogCode();
                    iconCatalog.InvalidateLookup();
                    iconCatalogDirty = true;
                    CardFacePresentationBinder.SetDescriptionIconCatalogOverride(iconCatalog);
                    CardFacePresentationBinder.InvalidateDescriptionIconCatalogCache();
                    RefreshContent();
                }) { text = "添加图标" }));
        }

        private void BuildAssemblyDownlinkRows(
            VisualElement column,
            Dictionary<string, Sprite> templateSprites)
        {
            var icons = session.GetInsertableIcons();
            if (icons.Count == 0)
            {
                column.Add(ContentVisualWarmConsoleUi.CreateDescriptionLabel("（无 Insertable 装配槽）"));
                return;
            }

            for (var i = 0; i < icons.Count; i++)
            {
                var icon = icons[i];
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Column;
                row.style.marginBottom = 8;
                row.style.paddingLeft = 4;
                row.style.paddingRight = 4;
                row.style.paddingTop = 4;
                row.style.paddingBottom = 4;
                row.style.backgroundColor = ContentVisualWarmConsoleUi.Theme.NavNormalBg;
                row.style.borderTopLeftRadius = row.style.borderTopRightRadius = 4;
                row.style.borderBottomLeftRadius = row.style.borderBottomRightRadius = 4;

                Sprite previewSprite = null;
                templateSprites.TryGetValue(icon.Code, out previewSprite);
                var title = icon.DisplayNameZh + "  " + icon.Token;
                row.Add(ContentVisualWarmConsoleUi.CreateTitleLabel(
                    title, 12, true, ContentVisualWarmConsoleUi.Theme.TextPrimary));
                row.Add(ContentVisualWarmConsoleUi.CreateDescriptionLabel(
                    previewSprite != null
                        ? "模板默认 Sprite（只读）：" + previewSprite.name
                        : "当前卡种模板无此图标（描述中将保留字面量）"));

                var spriteField = new ObjectField
                {
                    objectType = typeof(Sprite),
                    allowSceneObjects = false,
                    value = previewSprite,
                };
                spriteField.SetEnabled(false);
                row.Add(ContentVisualWarmConsoleUi.WrapControlRow("Sprite", spriteField, 72f));

                inlineIconStyle.Resolve(icon.Code, out var bx, out var by, out var scale);
                var code = icon.Code;
                row.Add(BuildLayoutFields(code, bx, by, scale));
                column.Add(row);
            }
        }

        private VisualElement BuildCatalogEntryRow(
            CardFaceDescriptionIconCatalogSO.Entry entry,
            int index)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Column;
            row.style.marginBottom = 8;
            row.style.paddingLeft = 4;
            row.style.paddingRight = 4;
            row.style.paddingTop = 4;
            row.style.paddingBottom = 4;
            row.style.backgroundColor = ContentVisualWarmConsoleUi.Theme.NavNormalBg;
            row.style.borderTopLeftRadius = row.style.borderTopRightRadius = 4;
            row.style.borderBottomLeftRadius = row.style.borderBottomRightRadius = 4;

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.Add(ContentVisualWarmConsoleUi.CreateTitleLabel(
                "条目 #" + (index + 1), 12, true, ContentVisualWarmConsoleUi.Theme.TextPrimary));
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            header.Add(spacer);
            var removeBtn = new Button(() =>
            {
                iconCatalog.TryRemoveEntry(entry);
                iconCatalogDirty = true;
                CardFacePresentationBinder.SetDescriptionIconCatalogOverride(iconCatalog);
                CardFacePresentationBinder.InvalidateDescriptionIconCatalogCache();
                previewFingerprint = string.Empty;
                RefreshContent();
            })
            {
                text = "删除",
            };
            header.Add(removeBtn);
            row.Add(header);

            var codeField = new TextField { value = entry.code ?? string.Empty };
            codeField.RegisterValueChangedCallback(evt =>
            {
                var next = (evt.newValue ?? string.Empty).Trim();
                if (CardFaceDescriptionIconCatalogSO.IsReservedAssemblySlotCode(next))
                {
                    EditorUtility.DisplayDialog(
                        "保留代号",
                        "不能占用装配槽代号：" + next,
                        "确定");
                    codeField.SetValueWithoutNotify(entry.code ?? string.Empty);
                    return;
                }

                entry.code = next;
                iconCatalog.InvalidateLookup();
                iconCatalogDirty = true;
                MarkCatalogDraftAndRefreshPreview();
            });
            row.Add(ContentVisualWarmConsoleUi.WrapControlRow("代号", codeField, 72f));

            var nameField = new TextField { value = entry.displayNameZh ?? string.Empty };
            nameField.RegisterValueChangedCallback(evt =>
            {
                entry.displayNameZh = evt.newValue ?? string.Empty;
                iconCatalogDirty = true;
                UpdateStatus();
            });
            row.Add(ContentVisualWarmConsoleUi.WrapControlRow("中文名", nameField, 72f));

            var token = string.IsNullOrEmpty(entry.code) ? "[?]" : "[" + entry.code + "]";
            var tokenField = new TextField { value = token, isReadOnly = true };
            row.Add(ContentVisualWarmConsoleUi.WrapControlRow("Token", tokenField, 72f));

            var spriteField = new ObjectField
            {
                objectType = typeof(Sprite),
                allowSceneObjects = false,
                value = entry.sprite,
            };
            spriteField.RegisterValueChangedCallback(evt =>
            {
                entry.sprite = evt.newValue as Sprite;
                iconCatalog.InvalidateLookup();
                iconCatalogDirty = true;
                MarkCatalogDraftAndRefreshPreview();
            });
            row.Add(ContentVisualWarmConsoleUi.WrapControlRow("Sprite", spriteField, 72f));

            var layoutCode = string.IsNullOrEmpty(entry.code) ? string.Empty : entry.code;
            if (!string.IsNullOrEmpty(layoutCode))
            {
                inlineIconStyle.Resolve(layoutCode, out var bx, out var by, out var scale);
                row.Add(BuildLayoutFields(layoutCode, bx, by, scale));
            }

            return row;
        }

        private VisualElement BuildLayoutFields(string code, float bx, float by, float scale)
        {
            var box = new VisualElement();
            box.style.flexDirection = FlexDirection.Column;

            var bearingX = new FloatField("Offset X (em)") { value = bx };
            bearingX.RegisterValueChangedCallback(evt =>
            {
                inlineIconStyle.Resolve(code, out _, out var curBy, out var curScale);
                ApplyRichTextLayout(code, evt.newValue, curBy, curScale);
            });
            box.Add(bearingX);

            var bearingY = new FloatField("Offset Y (em)") { value = by };
            bearingY.RegisterValueChangedCallback(evt =>
            {
                inlineIconStyle.Resolve(code, out var curBx, out _, out var curScale);
                ApplyRichTextLayout(code, curBx, evt.newValue, curScale);
            });
            box.Add(bearingY);

            var baseScale = new FloatField("Base Scale") { value = scale };
            baseScale.RegisterValueChangedCallback(evt =>
            {
                inlineIconStyle.Resolve(code, out var curBx, out var curBy, out _);
                ApplyRichTextLayout(code, curBx, curBy, evt.newValue);
            });
            box.Add(baseScale);
            return box;
        }

        private void MarkCatalogDraftAndRefreshPreview()
        {
            CardFacePresentationBinder.SetDescriptionIconCatalogOverride(iconCatalog);
            CardFacePresentationBinder.InvalidateDescriptionIconCatalogCache();
            previewFingerprint = string.Empty;
            EnsureRichTextPreview(force: true);
            previewContainer?.MarkDirtyRepaint();
            UpdateStatus();
        }

        private void ApplyRichTextLayout(string code, float bearingX, float bearingY, float baseScale)
        {
            if (string.IsNullOrEmpty(code))
            {
                return;
            }

            EnsureInlineIconStyleLoaded();
            inlineIconStyle.SetEntry(code, bearingX, bearingY, baseScale);
            inlineIconStyleDirty = true;
            CardFacePresentationBinder.SetInlineIconStyleOverride(inlineIconStyle);
            CardFacePresentationBinder.InvalidateInlineIconStyleCache();
            previewFingerprint = string.Empty;
            EnsureRichTextPreview(force: true);
            previewContainer?.MarkDirtyRepaint();
            UpdateStatus();
        }

        private void EnsureRichTextPreview(bool force = false)
        {
            EnsureDescriptionIconPipeline();
            var fingerprint = "richtext|" + richTextPreviewKind + "|"
                              + (richTextSampleDescription ?? string.Empty) + "|"
                              + (inlineIconStyle != null ? inlineIconStyle.GetInstanceID() : 0) + "|"
                              + (iconCatalog != null ? iconCatalog.GetInstanceID() : 0);
            if (inlineIconStyle != null)
            {
                fingerprint += "|styleEntries=" + inlineIconStyle.Entries.Count;
            }

            if (iconCatalog != null)
            {
                fingerprint += "|catalogEntries=" + iconCatalog.Entries.Count;
                for (var i = 0; i < iconCatalog.Entries.Count; i++)
                {
                    var catalogEntry = iconCatalog.Entries[i];
                    if (catalogEntry == null)
                    {
                        continue;
                    }

                    fingerprint += ";" + (catalogEntry.code ?? string.Empty) + "="
                                   + (catalogEntry.sprite != null ? catalogEntry.sprite.GetInstanceID() : 0);
                }
            }

            if (!force && fingerprint == previewFingerprint && previewHost.PreviewRoot != null)
            {
                return;
            }

            previewFingerprint = fingerprint;
            flipPreview.Stop(resetRotation: true);
            previewAnimPlayer = null;

            var request = new CardFacePreviewRequest
            {
                DefId = "preview.richtext." + richTextPreviewKind,
                Kind = richTextPreviewKind == CardPresentationKind.Unknown
                    ? CardPresentationKind.Monster
                    : richTextPreviewKind,
                DisplayName = "描述富文本预览",
                BasicDescription = richTextSampleDescription ?? string.Empty,
                FaceUp = true,
            };

            previewHost.Rebuild(request);
            if (previewHost.PreviewRoot != null)
            {
                CardMainVisualMaskAnchor.DisableMaskingForEditorPreview(previewHost.PreviewRoot.transform);
                ApplyFaceVisibility(previewHost.PreviewRoot.transform, true);
            }
        }

        private void EnsureTemplatePreview(EffectTemplateEditorIO.TemplateRow row, bool force = false)
        {
            EnsureDescriptionIconPipeline();
            var fingerprint = "template|" + (row?.id ?? string.Empty) + "|"
                              + (row?.design_text ?? string.Empty) + "|"
                              + (iconCatalog != null ? iconCatalog.GetInstanceID() : 0) + "|"
                              + (inlineIconStyle != null ? inlineIconStyle.GetInstanceID() : 0);
            if (iconCatalog != null)
            {
                fingerprint += "|catalogEntries=" + iconCatalog.Entries.Count;
            }

            if (!force && fingerprint == previewFingerprint && previewHost.PreviewRoot != null)
            {
                return;
            }

            previewFingerprint = fingerprint;
            flipPreview.Stop(resetRotation: true);
            previewAnimPlayer = null;

            // 不显示模板 id：名字槽留空，只预览描述内容框（含 [Code] 词条图标）。
            var request = new CardFacePreviewRequest
            {
                DefId = "preview.template",
                Kind = CardPresentationKind.HelpCard,
                DisplayName = string.Empty,
                BasicDescription = row?.design_text ?? string.Empty,
                FaceUp = true,
            };

            previewHost.Rebuild(request);
            if (previewHost.PreviewRoot != null)
            {
                CardMainVisualMaskAnchor.DisableMaskingForEditorPreview(previewHost.PreviewRoot.transform);
                ApplyFaceVisibility(previewHost.PreviewRoot.transform, true);
            }
        }

        private void EnsureDescriptionIconPipeline()
        {
            EnsureInlineIconStyleLoaded();
            EnsureIconCatalogLoaded();
            CardFacePresentationBinder.SetInlineIconStyleOverride(inlineIconStyle);
            CardFacePresentationBinder.SetDescriptionIconCatalogOverride(iconCatalog);
        }

        private List<string> ListCatalogIconCodes()
        {
            EnsureIconCatalogLoaded();
            var codes = new List<string>();
            if (iconCatalog?.Entries == null)
            {
                return codes;
            }

            for (var i = 0; i < iconCatalog.Entries.Count; i++)
            {
                var entry = iconCatalog.Entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.code))
                {
                    continue;
                }

                codes.Add(entry.code.Trim());
            }

            codes.Sort(StringComparer.OrdinalIgnoreCase);
            return codes;
        }

        private VisualElement BuildInsertableTokenRow(CardPresentationInsertableIcon icon)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 2;
            row.style.paddingLeft = 4;
            row.style.paddingRight = 4;
            row.style.paddingTop = 2;
            row.style.paddingBottom = 2;

            var name = ContentVisualWarmConsoleUi.CreateTitleLabel(
                icon.DisplayNameZh, 11, false, ContentVisualWarmConsoleUi.Theme.TextPrimary);
            name.style.flexGrow = 1;
            row.Add(name);

            var tokenField = new TextField { value = icon.Token, isReadOnly = true };
            tokenField.style.width = 110;
            tokenField.RegisterCallback<FocusInEvent>(_ =>
            {
                tokenField.SelectAll();
                EditorGUIUtility.systemCopyBuffer = icon.Token;
            });
            row.Add(tokenField);

            row.RegisterCallback<ClickEvent>(_ =>
            {
                EditorGUIUtility.systemCopyBuffer = icon.Token;
                OpenDescriptionRichTextPage(icon.Code);
            });
            return row;
        }

        private VisualElement BuildCatalogTokenRow(string code)
        {
            var token = "[" + code + "]";
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 2;
            row.style.paddingLeft = 4;
            row.style.paddingRight = 4;

            var name = ContentVisualWarmConsoleUi.CreateTitleLabel(
                code, 11, false, ContentVisualWarmConsoleUi.Theme.TextPrimary);
            name.style.flexGrow = 1;
            row.Add(name);

            var tokenField = new TextField { value = token, isReadOnly = true };
            tokenField.style.width = 110;
            tokenField.RegisterCallback<FocusInEvent>(_ =>
            {
                tokenField.SelectAll();
                EditorGUIUtility.systemCopyBuffer = token;
            });
            row.Add(tokenField);

            row.RegisterCallback<ClickEvent>(_ =>
            {
                EditorGUIUtility.systemCopyBuffer = token;
                OpenDescriptionRichTextPage(code);
            });
            return row;
        }

        private void EnsureInlineIconStyleLoaded()
        {
            if (inlineIconStyle != null)
            {
                return;
            }

            inlineIconStyle = AssetDatabase.LoadAssetAtPath<CardFaceDescriptionInlineIconStyleSO>(
                CardChassisPaths.DescriptionInlineIconStyleAsset);
            if (inlineIconStyle != null)
            {
                return;
            }

            inlineIconStyle = ScriptableObject.CreateInstance<CardFaceDescriptionInlineIconStyleSO>();
            inlineIconStyle.name = "CardFaceDescriptionInlineIconStyle";
            AssetDatabase.CreateAsset(inlineIconStyle, CardChassisPaths.DescriptionInlineIconStyleAsset);
            AssetDatabase.SaveAssets();
            CardFacePresentationBinder.InvalidateInlineIconStyleCache();
        }

        private void EnsureIconCatalogLoaded()
        {
            if (iconCatalog != null)
            {
                return;
            }

            iconCatalog = AssetDatabase.LoadAssetAtPath<CardFaceDescriptionIconCatalogSO>(
                CardChassisPaths.DescriptionIconCatalogAsset);
            if (iconCatalog != null)
            {
                return;
            }

            iconCatalog = ScriptableObject.CreateInstance<CardFaceDescriptionIconCatalogSO>();
            iconCatalog.name = "CardFaceDescriptionIconCatalog";
            AssetDatabase.CreateAsset(iconCatalog, CardChassisPaths.DescriptionIconCatalogAsset);
            AssetDatabase.SaveAssets();
            CardFacePresentationBinder.InvalidateDescriptionIconCatalogCache();
        }

        private void TryPersistInlineIconStyle()
        {
            if (!inlineIconStyleDirty || inlineIconStyle == null)
            {
                return;
            }

            EditorUtility.SetDirty(inlineIconStyle);
            AssetDatabase.SaveAssets();
            inlineIconStyleDirty = false;
            CardFacePresentationBinder.InvalidateInlineIconStyleCache();
        }

        private void TryPersistIconCatalog()
        {
            if (!iconCatalogDirty || iconCatalog == null)
            {
                return;
            }

            var keep = new List<CardFaceDescriptionIconCatalogSO.Entry>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < iconCatalog.Entries.Count; i++)
            {
                var entry = iconCatalog.Entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.code))
                {
                    continue;
                }

                var code = entry.code.Trim();
                if (CardFaceDescriptionIconCatalogSO.IsReservedAssemblySlotCode(code)
                    || !seen.Add(code))
                {
                    continue;
                }

                entry.code = code;
                keep.Add(entry);
            }

            iconCatalog.ReplaceEntries(keep);
            EditorUtility.SetDirty(iconCatalog);
            AssetDatabase.SaveAssets();
            iconCatalogDirty = false;
            CardFacePresentationBinder.InvalidateDescriptionIconCatalogCache();
        }

        private string AllocateNewCatalogCode()
        {
            EnsureIconCatalogLoaded();
            const string prefix = "New_Icon";
            if (!iconCatalog.TryGetEntry(prefix, out _))
            {
                return prefix;
            }

            for (var i = 2; i < 1000; i++)
            {
                var candidate = prefix + "_" + i;
                if (!iconCatalog.TryGetEntry(candidate, out _))
                {
                    return candidate;
                }
            }

            return prefix + "_" + Guid.NewGuid().ToString("N").Substring(0, 6);
        }

        private static Dictionary<string, Sprite> CaptureTemplateSpritesForKind(CardPresentationKind kind)
        {
            var path = kind switch
            {
                CardPresentationKind.Avatar => CardChassisPaths.AvatarFacePrefab,
                CardPresentationKind.Monster => CardChassisPaths.MonsterFacePrefab,
                CardPresentationKind.Trap => CardChassisPaths.TrapFacePrefab,
                CardPresentationKind.HelpCard => CardChassisPaths.ItemFacePrefab,
                CardPresentationKind.Relic => CardChassisPaths.RelicFacePrefab,
                CardPresentationKind.ChoiceOption => CardChassisPaths.RoomOptionFacePrefab,
                _ => CardChassisPaths.MonsterFacePrefab,
            };

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                return new Dictionary<string, Sprite>();
            }

            return CardFaceSlotNodeMap.CaptureTemplateDefaults(prefab.transform);
        }

        private static int RichTextKindToIndex(CardPresentationKind kind)
        {
            return kind switch
            {
                CardPresentationKind.Avatar => 0,
                CardPresentationKind.Monster => 1,
                CardPresentationKind.Trap => 1, // 最小改动：RichText 四项里 Trap 复用怪物索引；Load 仍走机关模版
                CardPresentationKind.HelpCard => 2,
                CardPresentationKind.Relic => 3,
                _ => 1,
            };
        }

        private static CardPresentationKind RichTextIndexToKind(int index)
        {
            return index switch
            {
                0 => CardPresentationKind.Avatar,
                1 => CardPresentationKind.Monster,
                2 => CardPresentationKind.HelpCard,
                3 => CardPresentationKind.Relic,
                _ => CardPresentationKind.Monster,
            };
        }

        private void BuildInstantParams(VisualElement column, CardPresentationEditorEntry entry)
        {
            var dto = entry.Dto;
            if (dto.mainVisual == null)
            {
                dto.mainVisual = new CardPresentationMainVisualDto { uniformScale = 1f };
            }

            if (dto.sprites == null)
            {
                dto.sprites = new CardPresentationSpritesDto();
            }

            if (dto.stats == null)
            {
                dto.stats = new CardPresentationStatsDto();
            }

            column.Add(ContentVisualWarmConsoleUi.CreateInlineFieldGroup(
                ContentVisualWarmConsoleUi.WrapControlRow(
                    "Offset X",
                    BindFloat(dto.mainVisual.offsetX, v =>
                    {
                        dto.mainVisual.offsetX = v;
                        OnDtoEdited(entry);
                    }),
                    64f),
                ContentVisualWarmConsoleUi.WrapControlRow(
                    "Offset Y",
                    BindFloat(dto.mainVisual.offsetY, v =>
                    {
                        dto.mainVisual.offsetY = v;
                        OnDtoEdited(entry);
                    }),
                    64f),
                ContentVisualWarmConsoleUi.WrapControlRow(
                    "Scale",
                    BindFloat(dto.mainVisual.uniformScale, v =>
                    {
                        dto.mainVisual.uniformScale = v;
                        OnDtoEdited(entry);
                    }),
                    52f)));

            var spriteFields = new List<VisualElement>
            {
                MakeSpriteField("主图标", dto.sprites.mainIcon, path =>
                {
                    dto.sprites.mainIcon = path;
                    OnDtoEdited(entry);
                }),
                MakeSpriteField("卡面背景", dto.sprites.faceBackground, path =>
                {
                    dto.sprites.faceBackground = path;
                    OnDtoEdited(entry);
                }),
                MakeSpriteField("卡背边框", dto.sprites.backBorder, path =>
                {
                    dto.sprites.backBorder = path;
                    OnDtoEdited(entry);
                }),
                MakeSpriteField("卡背背纹", dto.sprites.backShirt, path =>
                {
                    dto.sprites.backShirt = path;
                    OnDtoEdited(entry);
                }),
                MakeSpriteField("卡背 Logo", dto.sprites.backLogo, path =>
                {
                    dto.sprites.backLogo = path;
                    OnDtoEdited(entry);
                }),
                MakeSpriteField("卡框", dto.sprites.cardFrame, path =>
                {
                    dto.sprites.cardFrame = path;
                    OnDtoEdited(entry);
                }),
                MakeSpriteField("横幅", dto.sprites.banner, path =>
                {
                    dto.sprites.banner = path;
                    OnDtoEdited(entry);
                }),
            };
            ContentVisualWarmConsoleUi.AddTwoColumnGrid(column, spriteFields);

            column.Add(BuildDeckBackFollowHint(dto));

            column.Add(ContentVisualWarmConsoleUi.WrapControlRow(
                "显示名",
                BindText(dto.displayName, v =>
                {
                    dto.displayName = v ?? string.Empty;
                    OnDtoEdited(entry);
                })));

            if (CardPresentationEditorSession.IsRoomKind(dto.kind))
            {
                column.Add(ContentVisualWarmConsoleUi.WrapControlRow(
                    "图标预制体",
                    BindText(dto.iconPrefab, v =>
                    {
                        dto.iconPrefab = v ?? string.Empty;
                        OnDtoEdited(entry);
                    }),
                    tooltip: "Assets/Prefabs/xxx图标.prefab；空则按 contentId 默认映射。"));
                column.Add(ContentVisualWarmConsoleUi.CreateInlineFieldGroup(
                    ContentVisualWarmConsoleUi.WrapControlRow(
                        "权重",
                        BindInt(dto.weight, v => { dto.weight = v; OnDtoEdited(entry); }),
                        40f),
                    ContentVisualWarmConsoleUi.WrapControlRow(
                        "金币Δ",
                        BindInt(dto.goldDelta, v => { dto.goldDelta = v; OnDtoEdited(entry); }),
                        40f),
                    ContentVisualWarmConsoleUi.WrapControlRow(
                        "血上限Δ",
                        BindInt(dto.maxHpDelta, v => { dto.maxHpDelta = v; OnDtoEdited(entry); }),
                        40f),
                    ContentVisualWarmConsoleUi.WrapControlRow(
                        "商店货数",
                        BindInt(dto.shopOfferCount, v => { dto.shopOfferCount = v; OnDtoEdited(entry); }),
                        48f)));
                column.Add(ContentVisualWarmConsoleUi.WrapControlRow(
                    "回满血",
                    BindToggle(dto.healToFull, v => { dto.healToFull = v; OnDtoEdited(entry); })));
                column.Add(ContentVisualWarmConsoleUi.WrapControlRow(
                    "奖励池",
                    BindText(dto.rewardPoolId, v =>
                    {
                        dto.rewardPoolId = v ?? string.Empty;
                        OnDtoEdited(entry);
                    })));
            }

            if (CardPresentationEditorSession.IsCombatStatsKind(dto.kind))
            {
                column.Add(ContentVisualWarmConsoleUi.CreateInlineFieldGroup(
                    ContentVisualWarmConsoleUi.WrapControlRow(
                        "攻击",
                        BindInt(dto.stats.attack, v => { dto.stats.attack = v; OnDtoEdited(entry); }),
                        40f),
                    ContentVisualWarmConsoleUi.WrapControlRow(
                        "护甲",
                        BindInt(dto.stats.armor, v => { dto.stats.armor = v; OnDtoEdited(entry); }),
                        40f),
                    ContentVisualWarmConsoleUi.WrapControlRow(
                        "生命",
                        BindInt(dto.stats.hp, v => { dto.stats.hp = v; OnDtoEdited(entry); }),
                        40f),
                    ContentVisualWarmConsoleUi.WrapControlRow(
                        "行动",
                        BindInt(dto.stats.action, v => { dto.stats.action = v; OnDtoEdited(entry); }),
                        40f)));
            }

            if (!CardPresentationEditorSession.IsRoomKind(dto.kind)
                && !CardPresentationEditorSession.IsChoiceOptionKind(dto.kind))
            {
                column.Add(ContentVisualWarmConsoleUi.WrapControlRow(
                    "金币",
                    BindInt(dto.gold, v => { dto.gold = v; OnDtoEdited(entry); }),
                    tooltip: "HelpCard/Relic 商店价；Monster 击杀金。"));
            }
            else if (CardPresentationEditorSession.IsChoiceOptionKind(dto.kind))
            {
                column.Add(ContentVisualWarmConsoleUi.WrapControlRow(
                    "金币",
                    BindInt(dto.gold, v => { dto.gold = v; OnDtoEdited(entry); }),
                    tooltip: "选项卡展示价（如卡店服务花费）；不进 Catalog。"));
            }

            if (CardPresentationEditorSession.IsRoomKind(dto.kind)
                || CardPresentationEditorSession.IsChoiceOptionKind(dto.kind))
            {
                var faceIntroFieldRoom = new TextField
                {
                    multiline = true,
                    value = dto.faceIntro ?? string.Empty,
                };
                faceIntroFieldRoom.style.minHeight = 56;
                faceIntroFieldRoom.style.maxHeight = 96;
                faceIntroFieldRoom.RegisterValueChangedCallback(evt =>
                {
                    dto.faceIntro = evt.newValue ?? string.Empty;
                    OnDtoEdited(entry);
                });
                column.Add(ContentVisualWarmConsoleUi.WrapControl(
                    "卡面介绍",
                    "房间/选项说明；可不填",
                    faceIntroFieldRoom));
                return;
            }

            var deckLabels = new List<string> { CardPresentationEditorSession.UngroupedDeckId };
            var deckValues = new List<string> { string.Empty };
            var deckChoices = session.GetDeckChoices();
            for (var i = 0; i < deckChoices.Count; i++)
            {
                var choice = deckChoices[i];
                deckLabels.Add(choice.Label);
                deckValues.Add(choice.DeckId);
            }

            var currentDeckId = dto.deckId ?? string.Empty;
            if (!string.IsNullOrEmpty(currentDeckId) && !deckValues.Contains(currentDeckId))
            {
                deckLabels.Insert(1, session.FormatDeckChoiceLabel(currentDeckId) + " (缺失卡组)");
                deckValues.Insert(1, currentDeckId);
            }

            var deckIndex = Math.Max(0, deckValues.IndexOf(currentDeckId));
            var deckField = new PopupField<string>(deckLabels, deckIndex);
            deckField.RegisterValueChangedCallback(evt =>
            {
                var pickedIndex = deckLabels.IndexOf(evt.newValue);
                dto.deckId = pickedIndex >= 0 && pickedIndex < deckValues.Count
                    ? deckValues[pickedIndex]
                    : string.Empty;
                OnDtoEdited(entry);
                // 重建即时参数区（卡背跟随提示）；延迟避免 Popup 回调中销毁自身。
                rootVisualElement.schedule.Execute(() =>
                {
                    RefreshContent();
                    UpdateStatus();
                });
            });
            column.Add(ContentVisualWarmConsoleUi.WrapControlRow("所属卡组", deckField, 72f));

            var faceIntroField = new TextField
            {
                multiline = true,
                value = dto.faceIntro ?? string.Empty,
            };
            faceIntroField.style.minHeight = 56;
            faceIntroField.style.maxHeight = 96;
            faceIntroField.tooltip = "纯跟卡面绑定的介绍文案；右键详述面板会用。可含 {param} 与 [词条]。";
            faceIntroField.RegisterValueChangedCallback(evt =>
            {
                dto.faceIntro = evt.newValue ?? string.Empty;
                OnDtoEdited(entry);
            });
            column.Add(ContentVisualWarmConsoleUi.WrapControl(
                "卡面介绍",
                "右键详述用人手写介绍；空则回退卡面基础描述",
                faceIntroField));
        }

        private void BuildAnimationSection(VisualElement column, CardPresentationEditorEntry entry)
        {
            var dto = entry.Dto;
            if (dto.animations == null)
            {
                dto.animations = new CardPresentationAnimationsDto
                {
                    defaultFps = 8f,
                    slots = CreateDefaultSlotsArray(),
                };
            }

            EnsureAllAnimSlots(dto);

            var slotChoices = new List<string>(CardAnimSlotIds.All);
            var previewSlot = new PopupField<string>(slotChoices, previewAnimSlot);
            previewSlot.RegisterValueChangedCallback(evt =>
            {
                previewAnimSlot = CardAnimSlotIds.Normalize(evt.newValue);
                RestartPreviewAnim(entry);
            });

            column.Add(ContentVisualWarmConsoleUi.CreateInlineFieldGroup(
                ContentVisualWarmConsoleUi.WrapControlRow(
                    "默认 FPS",
                    BindFloat(dto.animations.defaultFps, v =>
                    {
                        dto.animations.defaultFps = Mathf.Max(0.01f, v);
                        OnDtoEdited(entry);
                        RestartPreviewAnim(entry);
                    }),
                    72f),
                ContentVisualWarmConsoleUi.WrapControlRow("预览槽", previewSlot, 72f)));

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginTop = 4;
            header.style.marginBottom = 2;
            header.style.paddingBottom = 2;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = ContentVisualWarmConsoleUi.Theme.Divider;

            void AddHeaderCell(string text, float width, bool grow = false)
            {
                var lbl = ContentVisualWarmConsoleUi.CreateTitleLabel(
                    text, 10, true, ContentVisualWarmConsoleUi.Theme.TextTertiary);
                if (grow)
                {
                    lbl.style.flexGrow = 1;
                    lbl.style.minWidth = 60;
                }
                else
                {
                    lbl.style.width = width;
                    lbl.style.flexShrink = 0;
                }

                header.Add(lbl);
            }

            AddHeaderCell("槽", 52f);
            AddHeaderCell("来源", 72f);
            AddHeaderCell("路径", 0f, grow: true);
            AddHeaderCell("ΔX", 44f);
            AddHeaderCell("ΔY", 44f);
            column.Add(header);

            var sourceChoices = new List<string> { "none", "folder", "atlas" };
            for (var i = 0; i < CardAnimSlotIds.All.Length; i++)
            {
                var slotId = CardAnimSlotIds.All[i];
                var slotDto = FindSlot(dto, slotId);
                column.Add(BuildAnimSlotRow(entry, slotId, slotDto, sourceChoices));
            }
        }

        private VisualElement BuildAnimSlotRow(
            CardPresentationEditorEntry entry,
            string slotId,
            CardPresentationAnimSlotDto slotDto,
            List<string> sourceChoices)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 2;
            row.style.minHeight = 22;

            var idLabel = ContentVisualWarmConsoleUi.CreateTitleLabel(
                slotId, 11, true, ContentVisualWarmConsoleUi.Theme.AccentGoldValue);
            idLabel.style.width = 52;
            idLabel.style.flexShrink = 0;
            row.Add(idLabel);

            var sourceField = new PopupField<string>(sourceChoices, NormalizeSource(slotDto.sourceType));
            sourceField.style.width = 72;
            sourceField.style.flexShrink = 0;
            sourceField.RegisterValueChangedCallback(evt =>
            {
                slotDto.sourceType = evt.newValue;
                OnDtoEdited(entry);
                if (string.Equals(previewAnimSlot, slotId, StringComparison.Ordinal))
                {
                    RestartPreviewAnim(entry);
                }
            });
            row.Add(sourceField);

            var pathField = BindText(slotDto.path, v =>
            {
                slotDto.path = v ?? string.Empty;
                OnDtoEdited(entry);
                if (string.Equals(previewAnimSlot, slotId, StringComparison.Ordinal))
                {
                    RestartPreviewAnim(entry);
                }
            });
            pathField.style.flexGrow = 1;
            pathField.style.minWidth = 40;
            pathField.style.marginRight = 4;
            row.Add(pathField);

            var ox = BindFloat(slotDto.offsetX, v =>
            {
                slotDto.offsetX = v;
                OnDtoEdited(entry);
                RestartPreviewAnim(entry);
            });
            ox.style.width = 44;
            ox.style.flexShrink = 0;
            ox.style.marginRight = 4;
            row.Add(ox);

            var oy = BindFloat(slotDto.offsetY, v =>
            {
                slotDto.offsetY = v;
                OnDtoEdited(entry);
                RestartPreviewAnim(entry);
            });
            oy.style.width = 44;
            oy.style.flexShrink = 0;
            row.Add(oy);

            return row;
        }

        private void BuildExtraSlotsSection(VisualElement column, CardPresentationEditorEntry entry)
        {
            var dto = entry.Dto;
            if (dto.extraSlots == null)
            {
                dto.extraSlots = Array.Empty<CardPresentationExtraSlotDto>();
            }

            var list = new List<CardPresentationExtraSlotDto>(dto.extraSlots);
            var listRoot = new VisualElement();
            column.Add(listRoot);

            void RebuildList()
            {
                listRoot.Clear();
                for (var i = 0; i < list.Count; i++)
                {
                    var index = i;
                    var item = list[i];
                    var row = new VisualElement();
                    row.style.flexDirection = FlexDirection.Row;
                    row.style.marginBottom = 6;

                    var code = new TextField { value = item.code ?? string.Empty };
                    code.style.flexGrow = 1;
                    code.RegisterValueChangedCallback(evt =>
                    {
                        item.code = evt.newValue ?? string.Empty;
                        CommitExtra();
                    });
                    row.Add(code);

                    var path = new TextField { value = item.path ?? string.Empty };
                    path.style.flexGrow = 2;
                    path.RegisterValueChangedCallback(evt =>
                    {
                        item.path = evt.newValue ?? string.Empty;
                        CommitExtra();
                    });
                    row.Add(path);

                    var remove = new Button(() =>
                    {
                        list.RemoveAt(index);
                        CommitExtra();
                        RebuildList();
                    }) { text = "删" };
                    row.Add(remove);
                    listRoot.Add(row);
                }
            }

            void CommitExtra()
            {
                dto.extraSlots = list.ToArray();
                OnDtoEdited(entry);
            }

            RebuildList();

            column.Add(new Button(() =>
            {
                list.Add(new CardPresentationExtraSlotDto { code = string.Empty, path = string.Empty });
                CommitExtra();
                RebuildList();
            })
            {
                text = "添加 extraSlot",
            });
        }

        private void OnDtoEdited(CardPresentationEditorEntry entry)
        {
            session.MarkDirty(entry.ContentId);
            InvalidateAndRefreshPreview(entry);
            UpdateStatus();
        }

        private void OnDeckEdited(CardPresentationEditorEntry entry)
        {
            session.MarkDirty(entry.ContentId);
            InvalidateAndRefreshPreview(entry);
            // 显示名变更时侧栏卡面分组标题与卡组列表需同步（Invalidate 已 RefreshSidebar）。
            UpdateStatus();
        }

        private VisualElement BuildDeckBackFollowHint(CardPresentationConfigDto dto)
        {
            var sprites = dto?.sprites;
            var hasOwnBack = sprites != null
                && (!string.IsNullOrWhiteSpace(sprites.backBorder)
                    || !string.IsNullOrWhiteSpace(sprites.backShirt)
                    || !string.IsNullOrWhiteSpace(sprites.backLogo));
            var deckId = dto?.deckId?.Trim() ?? string.Empty;
            string text;
            if (string.IsNullOrEmpty(deckId))
            {
                text = hasOwnBack
                    ? "卡背：自有背图（未选所属卡组）"
                    : "卡背：未选所属卡组，预览用模板兜底";
            }
            else
            {
                var label = session.FormatDeckChoiceLabel(deckId);
                if (session.DeckHasAnyBackSprite(deckId))
                {
                    text = hasOwnBack
                        ? "卡背：跟随 " + label + "（卡组背图覆盖自有槽；运行时同规则）"
                        : "卡背：跟随 " + label;
                }
                else
                {
                    text = hasOwnBack
                        ? "卡背：自有背图（" + label + " 尚未配置卡背）"
                        : "卡背：跟随 " + label + " — 本组尚未配置卡背，预览用模板兜底";
                }
            }

            var hint = ContentVisualWarmConsoleUi.CreateDescriptionLabel(text);
            hint.style.marginTop = 4;
            hint.style.marginBottom = 6;
            return hint;
        }

        private void InvalidateAndRefreshPreview(CardPresentationEditorEntry entry)
        {
            previewFingerprint = string.Empty;
            EnsurePreview(entry, force: true);
            previewContainer?.MarkDirtyRepaint();
            RefreshSidebar();
        }

        private void EnsurePreview(CardPresentationEditorEntry entry, bool force = false)
        {
            if (entry?.Dto == null)
            {
                return;
            }

            EnsureDescriptionIconPipeline();
            var fingerprint = BuildPreviewFingerprint(entry.Dto, faceUp);
            if (!force && fingerprint == previewFingerprint && previewHost.PreviewRoot != null)
            {
                return;
            }

            previewFingerprint = fingerprint;
            if (!TryBuildPreviewRequest(entry, out var request, out var error))
            {
                previewHost.Rebuild(null);
                previewAnimPlayer = null;
                Debug.LogWarning("[CardPresentation] 预览跳过：" + error);
                return;
            }

            flipPreview.Stop(resetRotation: true);
            previewHost.Rebuild(request);
            ApplyPostRebuildExtras(entry.Dto);
            ApplyFaceVisibility(previewHost.PreviewRoot != null ? previewHost.PreviewRoot.transform : null, faceUp);
            RestartPreviewAnim(entry);
            if (previewHost.PreviewRoot != null)
            {
                CardMainVisualMaskAnchor.DisableMaskingForEditorPreview(previewHost.PreviewRoot.transform);
            }
        }

        private static void ApplyFaceVisibility(Transform root, bool showFront)
        {
            if (root == null)
            {
                return;
            }

            var front = CardPresentationFlipPreview.FindFrontRoot(root);
            var back = CardPresentationFlipPreview.FindBackRoot(root);
            if (front != null)
            {
                front.gameObject.SetActive(showFront);
            }

            if (back != null)
            {
                back.gameObject.SetActive(!showFront);
            }
        }

        private void ApplyPostRebuildExtras(CardPresentationConfigDto dto)
        {
            var root = previewHost.PreviewRoot;
            if (root == null || dto == null)
            {
                return;
            }

            if (dto.sprites != null)
            {
                TryApplySpritePath(root.transform, CardFaceSlotCodes.CardFrame, dto.sprites.cardFrame);
                TryApplySpritePath(root.transform, CardFaceSlotCodes.Banner, dto.sprites.banner);
            }
        }

        private static void TryApplySpritePath(Transform root, string slotCode, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var sprite = CardPresentationSpritePath.LoadSprite(path);
            if (sprite == null)
            {
                return;
            }

            if (CardFaceSlotNodeMap.TryFindRenderer(root, slotCode, out var renderer))
            {
                renderer.sprite = sprite;
                renderer.enabled = true;
            }
        }

        private void RestartPreviewAnim(CardPresentationEditorEntry entry)
        {
            var root = previewHost.PreviewRoot;
            if (root == null || entry?.Dto == null)
            {
                previewAnimPlayer = null;
                return;
            }

            if (string.Equals(entry.Dto.kind, "Deck", StringComparison.OrdinalIgnoreCase))
            {
                previewAnimPlayer = null;
                return;
            }

            previewAnimPlayer = root.GetComponent<CardSpriteAnimPlayer>();
            if (previewAnimPlayer == null)
            {
                previewAnimPlayer = root.AddComponent<CardSpriteAnimPlayer>();
            }

            previewAnimPlayer.BindConfig(entry.Dto);
            previewAnimPlayer.Play(previewAnimSlot);
            previewContainer?.MarkDirtyRepaint();
        }

        private void ToggleFlip(CardPresentationEditorEntry entry)
        {
            if (previewHost.PreviewRoot == null)
            {
                EnsurePreview(entry, force: true);
            }

            var root = previewHost.PreviewRoot;
            if (root == null)
            {
                return;
            }

            var target = !faceUp;
            flipPreview.Begin(root.transform, target, () =>
            {
                faceUp = target;
                UpdateStatus();
                RefreshContent();
            });
        }

        private bool TryBuildPreviewRequest(
            CardPresentationEditorEntry entry,
            out CardFacePreviewRequest request,
            out string error)
        {
            request = null;
            error = null;
            var dto = entry?.Dto;
            if (dto == null)
            {
                error = "无选中条目。";
                return false;
            }

            CardPresentationKind kind;
            if (string.Equals(dto.kind, "Deck", StringComparison.OrdinalIgnoreCase))
            {
                kind = ResolveDeckPreviewKind(dto.contentId);
            }
            else
            {
                ContentVisualKind contentKind;
                if (!Enum.TryParse(dto.kind, true, out contentKind))
                {
                    contentKind = ContentVisualKind.Unknown;
                }

                kind = CardFacePreviewBuilder.ToPresentationKind(contentKind, dto.contentId);
                if (kind == CardPresentationKind.Unknown)
                {
                    if (CardPresentationEditorSession.IsItemLikeKind(dto.kind))
                    {
                        kind = CardPresentationKind.HelpCard;
                    }
                    else if (string.Equals(dto.kind, "Avatar", StringComparison.OrdinalIgnoreCase))
                    {
                        kind = CardPresentationKind.Avatar;
                    }
                    else if (string.Equals(dto.kind, "Monster", StringComparison.OrdinalIgnoreCase))
                    {
                        kind = CardPresentationKind.Monster;
                    }
                    else if (string.Equals(dto.kind, "Trap", StringComparison.OrdinalIgnoreCase))
                    {
                        kind = CardPresentationKind.Trap;
                    }
                    else if (string.Equals(dto.kind, "Relic", StringComparison.OrdinalIgnoreCase))
                    {
                        kind = CardPresentationKind.Relic;
                    }
                    else if (string.Equals(dto.kind, "Room", StringComparison.OrdinalIgnoreCase))
                    {
                        kind = CardPresentationKind.Room;
                    }
                    else if (string.Equals(dto.kind, "ChoiceOption", StringComparison.OrdinalIgnoreCase))
                    {
                        kind = CardPresentationKind.ChoiceOption;
                    }
                }
            }

            if (kind == CardPresentationKind.Unknown)
            {
                error = "该 Kind 不挂卡面预览：" + dto.contentId;
                return false;
            }

            var sprites = dto.sprites ?? new CardPresentationSpritesDto();
            var stats = dto.stats ?? new CardPresentationStatsDto();

            var backBorderPath = sprites.backBorder;
            var backShirtPath = sprites.backShirt;
            var backLogoPath = sprites.backLogo;
            var isDeckEntry = string.Equals(dto.kind, "Deck", StringComparison.OrdinalIgnoreCase);
            // 对齐运行时 ApplyDeckBackSprites：所属卡组背槽有图则覆盖卡面同槽。
            if (!isDeckEntry
                && session.TryGetDeckDto(dto.deckId, out var deckDto)
                && deckDto?.sprites != null)
            {
                if (!string.IsNullOrWhiteSpace(deckDto.sprites.backBorder))
                {
                    backBorderPath = deckDto.sprites.backBorder;
                }

                if (!string.IsNullOrWhiteSpace(deckDto.sprites.backShirt))
                {
                    backShirtPath = deckDto.sprites.backShirt;
                }

                if (!string.IsNullOrWhiteSpace(deckDto.sprites.backLogo))
                {
                    backLogoPath = deckDto.sprites.backLogo;
                }
            }

            request = new CardFacePreviewRequest
            {
                DefId = dto.contentId,
                Kind = kind,
                DisplayName = string.IsNullOrWhiteSpace(dto.displayName)
                    ? session.GetDisplayName(dto.contentId, dto.kind)
                    : dto.displayName,
                BasicDescription = isDeckEntry
                    ? string.Empty
                    : CardFaceDescriptionParamFiller.FillFromAssemblies(
                        dto.description ?? string.Empty,
                        dto.effectAssemblies),
                RoomIconPrefabPath = dto.iconPrefab ?? string.Empty,
                MainIcon = isDeckEntry
                    ? null
                    : CardPresentationSpritePath.LoadSprite(sprites.mainIcon),
                FaceBackground = isDeckEntry
                    ? null
                    : CardPresentationSpritePath.LoadSprite(sprites.faceBackground),
                BackBorder = CardPresentationSpritePath.LoadSprite(backBorderPath),
                BackShirt = CardPresentationSpritePath.LoadSprite(backShirtPath),
                BackLogo = CardPresentationSpritePath.LoadSprite(backLogoPath),
                Attack = isDeckEntry ? 0 : Mathf.Max(0, stats.attack),
                Armor = isDeckEntry ? 0 : Mathf.Max(0, stats.armor),
                Hp = isDeckEntry ? 0 : Mathf.Max(0, stats.hp),
                ActionCount = isDeckEntry ? 0 : Mathf.Max(0, stats.action),
                FaceUp = faceUp,
            };
            return true;
        }

        /// <summary>
        /// 卡组页预览需要挂一套带 back 子树的卡面模板；按 deckId 约定选壳，空背槽保留该模板兜底图。
        /// </summary>
        private static CardPresentationKind ResolveDeckPreviewKind(string deckContentId)
        {
            if (string.IsNullOrWhiteSpace(deckContentId))
            {
                return CardPresentationKind.Monster;
            }

            var id = deckContentId.Trim();
            if (string.Equals(id, CardPresentationEditorSession.PlayerDeckId, StringComparison.OrdinalIgnoreCase)
                || id.IndexOf("player", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return CardPresentationKind.Avatar;
            }

            if (string.Equals(id, "deck.help", StringComparison.OrdinalIgnoreCase)
                || id.IndexOf("help", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return CardPresentationKind.HelpCard;
            }

            if (string.Equals(id, "deck.trap", StringComparison.OrdinalIgnoreCase)
                || id.IndexOf("trap", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return CardPresentationKind.Trap;
            }

            if (string.Equals(id, "deck.relic", StringComparison.OrdinalIgnoreCase)
                || id.IndexOf("relic", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return CardPresentationKind.Relic;
            }

            return CardPresentationKind.Monster;
        }

        private string BuildPreviewFingerprint(CardPresentationConfigDto dto, bool faceUpFlag)
        {
            var sprites = dto.sprites ?? new CardPresentationSpritesDto();
            var stats = dto.stats ?? new CardPresentationStatsDto();
            var mv = dto.mainVisual ?? new CardPresentationMainVisualDto();
            var assemblies = dto.effectAssemblies;
            var assemblyFp = string.Empty;
            if (assemblies != null)
            {
                for (var i = 0; i < assemblies.Length; i++)
                {
                    var a = assemblies[i];
                    if (a == null)
                    {
                        continue;
                    }

                    assemblyFp += a.templateId + "#" + a.argsJson + ";";
                }
            }

            return string.Join("|",
                dto.contentId,
                dto.kind,
                dto.deckId,
                dto.displayName,
                dto.description,
                assemblyFp,
                dto.gold,
                stats.hp,
                stats.armor,
                stats.attack,
                stats.action,
                sprites.mainIcon,
                sprites.faceBackground,
                sprites.backBorder,
                sprites.backShirt,
                sprites.backLogo,
                ResolveDeckBackFingerprint(dto),
                sprites.cardFrame,
                sprites.banner,
                mv.offsetX,
                mv.offsetY,
                mv.uniformScale,
                faceUpFlag ? "1" : "0");
        }

        /// <summary>卡面预览指纹纳入所属卡组背图，改组或改组背图后强制重建。</summary>
        private string ResolveDeckBackFingerprint(CardPresentationConfigDto dto)
        {
            if (dto == null
                || string.Equals(dto.kind, "Deck", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(dto.deckId)
                || !session.TryGetDeckDto(dto.deckId, out var deckDto)
                || deckDto?.sprites == null)
            {
                return string.Empty;
            }

            return string.Join(";",
                deckDto.sprites.backBorder ?? string.Empty,
                deckDto.sprites.backShirt ?? string.Empty,
                deckDto.sprites.backLogo ?? string.Empty);
        }

        private VisualElement MakeSpriteField(string label, string path, Action<string> onPath)
        {
            var field = new ObjectField
            {
                objectType = typeof(Sprite),
                allowSceneObjects = false,
                value = string.IsNullOrWhiteSpace(path)
                    ? null
                    : CardPresentationSpritePath.LoadSprite(path),
            };
            field.RegisterValueChangedCallback(evt =>
            {
                var sprite = evt.newValue as Sprite;
                onPath?.Invoke(CardPresentationMigration.AssetPathOrEmpty(sprite));
            });
            return ContentVisualWarmConsoleUi.WrapControlRow(
                label,
                field,
                72f,
                string.IsNullOrWhiteSpace(path) ? null : path);
        }

        private static FloatField BindFloat(float value, Action<float> onChange)
        {
            var field = new FloatField { value = value };
            field.RegisterValueChangedCallback(evt => onChange?.Invoke(evt.newValue));
            return field;
        }

        private static IntegerField BindInt(int value, Action<int> onChange)
        {
            var field = new IntegerField { value = value };
            field.RegisterValueChangedCallback(evt => onChange?.Invoke(evt.newValue));
            return field;
        }

        private static TextField BindText(string value, Action<string> onChange)
        {
            var field = new TextField { value = value ?? string.Empty };
            field.RegisterValueChangedCallback(evt => onChange?.Invoke(evt.newValue));
            return field;
        }

        private static Toggle BindToggle(bool value, Action<bool> onChange)
        {
            var field = new Toggle { value = value };
            field.RegisterValueChangedCallback(evt => onChange?.Invoke(evt.newValue));
            return field;
        }

        private static CardPresentationAnimSlotDto FindSlot(CardPresentationConfigDto dto, string slotId)
        {
            EnsureAllAnimSlots(dto);
            for (var i = 0; i < dto.animations.slots.Length; i++)
            {
                var slot = dto.animations.slots[i];
                if (slot != null
                    && string.Equals(slot.id, slotId, StringComparison.OrdinalIgnoreCase))
                {
                    return slot;
                }
            }

            var created = new CardPresentationAnimSlotDto
            {
                id = slotId,
                sourceType = "none",
                path = string.Empty,
            };
            var list = new List<CardPresentationAnimSlotDto>(dto.animations.slots) { created };
            dto.animations.slots = list.ToArray();
            return created;
        }

        private static void EnsureAllAnimSlots(CardPresentationConfigDto dto)
        {
            if (dto.animations == null)
            {
                dto.animations = new CardPresentationAnimationsDto
                {
                    defaultFps = 8f,
                    slots = CreateDefaultSlotsArray(),
                };
                return;
            }

            if (dto.animations.slots == null || dto.animations.slots.Length == 0)
            {
                dto.animations.slots = CreateDefaultSlotsArray();
                return;
            }

            var map = new Dictionary<string, CardPresentationAnimSlotDto>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < dto.animations.slots.Length; i++)
            {
                var slot = dto.animations.slots[i];
                if (slot == null || string.IsNullOrWhiteSpace(slot.id))
                {
                    continue;
                }

                map[CardAnimSlotIds.Normalize(slot.id)] = slot;
            }

            var ordered = new CardPresentationAnimSlotDto[CardAnimSlotIds.All.Length];
            for (var i = 0; i < CardAnimSlotIds.All.Length; i++)
            {
                var id = CardAnimSlotIds.All[i];
                if (!map.TryGetValue(id, out var slot) || slot == null)
                {
                    slot = new CardPresentationAnimSlotDto
                    {
                        id = id,
                        sourceType = "none",
                        path = string.Empty,
                    };
                }
                else
                {
                    slot.id = id;
                }

                ordered[i] = slot;
            }

            dto.animations.slots = ordered;
        }

        private static CardPresentationAnimSlotDto[] CreateDefaultSlotsArray()
        {
            var slots = new CardPresentationAnimSlotDto[CardAnimSlotIds.All.Length];
            for (var i = 0; i < slots.Length; i++)
            {
                slots[i] = new CardPresentationAnimSlotDto
                {
                    id = CardAnimSlotIds.All[i],
                    sourceType = "none",
                    path = string.Empty,
                };
            }

            return slots;
        }

        private static string NormalizeSource(string sourceType)
        {
            if (string.Equals(sourceType, "folder", StringComparison.OrdinalIgnoreCase))
            {
                return "folder";
            }

            if (string.Equals(sourceType, "atlas", StringComparison.OrdinalIgnoreCase))
            {
                return "atlas";
            }

            return "none";
        }

        private void UpdateStatus()
        {
            if (statusHelpBox == null)
            {
                return;
            }

            string focusText;
            switch (session.FocusKind)
            {
                case CardPresentationEditorFocusKind.DescriptionGlossary:
                    focusText = "描述词条 · " + richTextPreviewKind
                                + (inlineIconStyleDirty || iconCatalogDirty ? " · 脏" : string.Empty);
                    break;
                case CardPresentationEditorFocusKind.Face:
                    var face = session.GetFocusedFace();
                    focusText = "卡面 · " + (face != null ? face.ContentId : "（未选）");
                    break;
                case CardPresentationEditorFocusKind.Deck:
                    var deck = session.GetFocusedDeck();
                    focusText = "卡组 · " + (deck != null ? deck.ContentId : "（未选）");
                    break;
                case CardPresentationEditorFocusKind.EffectTemplate:
                    var template = session.GetFocusedTemplate();
                    focusText = "效果模板 · " + (template != null ? template.id : "（未选）");
                    break;
                case CardPresentationEditorFocusKind.VisualEffect:
                    var vfx = session.GetFocusedVisualEffect();
                    focusText = "特效 · " + (vfx != null ? vfx.Id : "（未选）");
                    break;
                default:
                    focusText = "（未选）";
                    break;
            }

            var previewStatus = session.FocusKind == CardPresentationEditorFocusKind.VisualEffect
                ? vfxPreviewHost.Status
                : previewHost.Status;
            statusHelpBox.text = "焦点 " + focusText
                                 + " · 脏 " + session.DirtyCount
                                 + " · " + previewStatus;
        }

        private void SaveAll()
        {
            TryPersistInlineIconStyle();
            TryPersistIconCatalog();
            if (!session.TrySaveAll(out var error))
            {
                EditorUtility.DisplayDialog("保存失败", error ?? "未知错误", "确定");
                return;
            }

            previewFingerprint = string.Empty;
            vfxPreviewFingerprint = string.Empty;
            RefreshAll();
            ShowNotification(new GUIContent("已保存卡牌表现 JSON"));
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
                session.Reload();
                previewFingerprint = string.Empty;
                vfxPreviewFingerprint = string.Empty;
                faceUp = true;
                previewAnimSlot = CardAnimSlotIds.Idle;
                RefreshAll();
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("加载失败", ex.Message, "确定");
            }
        }

        private void MigrateFromLegacy()
        {
            var created = session.MigrateFromLegacyCatalogs(out var message);
            previewFingerprint = string.Empty;
            RefreshAll();
            EditorUtility.DisplayDialog("从旧 Catalog 迁移", message + "\n（新建 " + created + "）", "确定");
        }

        private void ExportIndex()
        {
            if (!session.TryExportIndex(out var message))
            {
                EditorUtility.DisplayDialog("导出索引失败", message, "确定");
                return;
            }

            ShowNotification(new GUIContent(message));
            UpdateStatus();
        }
    }
}
#endif
