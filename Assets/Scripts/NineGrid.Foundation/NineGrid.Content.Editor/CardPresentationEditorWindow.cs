#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NineGrid.Cards;
using NineGrid.Cards.Anim;
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
        private readonly CardPresentationFlipPreview flipPreview = new CardPresentationFlipPreview();

        private VisualElement rootElement;
        private VisualElement listContainer;
        private VisualElement contentRoot;
        private HelpBox statusHelpBox;
        private IMGUIContainer previewContainer;
        private TextField descriptionField;
        private string previewFingerprint = string.Empty;
        private bool faceUp = true;
        private string previewAnimSlot = CardAnimSlotIds.Idle;
        private CardSpriteAnimPlayer previewAnimPlayer;
        private double lastEditorTickTime;
        private bool editorTickHooked;

        private readonly Dictionary<string, bool> foldoutState =
            new Dictionary<string, bool>(StringComparer.Ordinal);

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
            previewFingerprint = string.Empty;
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

            if (needsRepaint)
            {
                previewContainer?.MarkDirtyRepaint();
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
                "卡牌表现 JSON 权威编辑：所见即所得预览、主视图偏移、帧动画槽与描述。"));

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
            statusHelpBox.style.marginTop = 8;
            pane.Add(statusHelpBox);

            var scroll = ContentVisualWarmConsoleUi.CreateContentScroll(out contentRoot);
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
            var groups = session.GetSidebarGroups();
            for (var i = 0; i < groups.Count; i++)
            {
                listContainer.Add(BuildCategoryFoldout(groups[i]));
            }
        }

        private VisualElement BuildCategoryFoldout(CardPresentationSidebarGroup group)
        {
            var key = "cat:" + group.Category;
            if (!foldoutState.TryGetValue(key, out var expanded))
            {
                expanded = group.ExpandByDefault;
                foldoutState[key] = expanded;
            }

            var foldout = new Foldout
            {
                text = group.Title,
                value = expanded,
            };
            foldout.style.marginBottom = 4;
            foldout.RegisterValueChangedCallback(evt => foldoutState[key] = evt.newValue);

            if (group.Category == CardPresentationSidebarCategory.Monster)
            {
                for (var d = 0; d < group.DeckGroups.Count; d++)
                {
                    foldout.contentContainer.Add(BuildDeckFoldout(group.DeckGroups[d]));
                }

                if (group.DeckGroups.Count == 0)
                {
                    foldout.contentContainer.Add(
                        ContentVisualWarmConsoleUi.CreateDescriptionLabel("（无怪物条目）"));
                }
            }
            else if (group.Category == CardPresentationSidebarCategory.Other)
            {
                var icons = session.GetInsertableIcons();
                if (icons.Count == 0)
                {
                    foldout.contentContainer.Add(
                        ContentVisualWarmConsoleUi.CreateDescriptionLabel("（无 Insertable 槽）"));
                }
                else
                {
                    for (var i = 0; i < icons.Count; i++)
                    {
                        foldout.contentContainer.Add(BuildInsertableRow(icons[i]));
                    }
                }
            }
            else
            {
                if (group.Entries.Count == 0)
                {
                    foldout.contentContainer.Add(
                        ContentVisualWarmConsoleUi.CreateDescriptionLabel("（无条目）"));
                }
                else
                {
                    for (var i = 0; i < group.Entries.Count; i++)
                    {
                        foldout.contentContainer.Add(BuildEntryButton(group.Entries[i]));
                    }
                }
            }

            return foldout;
        }

        private VisualElement BuildDeckFoldout(CardPresentationSidebarDeckGroup deck)
        {
            var key = "deck:" + deck.DeckId;
            if (!foldoutState.TryGetValue(key, out var expanded))
            {
                expanded = true;
                foldoutState[key] = expanded;
            }

            var foldout = new Foldout
            {
                text = deck.Title + " (" + deck.Entries.Count + ")",
                value = expanded,
            };
            foldout.RegisterValueChangedCallback(evt => foldoutState[key] = evt.newValue);
            for (var i = 0; i < deck.Entries.Count; i++)
            {
                foldout.contentContainer.Add(BuildEntryButton(deck.Entries[i]));
            }

            return foldout;
        }

        private VisualElement BuildInsertableRow(CardPresentationInsertableIcon icon)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 4;
            row.style.paddingLeft = 4;
            row.style.paddingRight = 4;
            row.style.paddingTop = 4;
            row.style.paddingBottom = 4;
            row.style.backgroundColor = ContentVisualWarmConsoleUi.Theme.NavNormalBg;
            row.style.borderTopLeftRadius = row.style.borderTopRightRadius = 4;
            row.style.borderBottomLeftRadius = row.style.borderBottomRightRadius = 4;

            var name = ContentVisualWarmConsoleUi.CreateTitleLabel(
                icon.DisplayNameZh, 12, false, ContentVisualWarmConsoleUi.Theme.TextPrimary);
            name.style.flexGrow = 1;
            row.Add(name);

            var tokenField = new TextField { value = icon.Token, isReadOnly = true };
            tokenField.style.width = 120;
            tokenField.RegisterCallback<FocusInEvent>(_ =>
            {
                tokenField.SelectAll();
                EditorGUIUtility.systemCopyBuffer = icon.Token;
            });
            row.Add(tokenField);
            return row;
        }

        private VisualElement BuildEntryButton(CardPresentationEditorEntry entry)
        {
            var selected = string.Equals(entry.ContentId, session.FocusedContentId, StringComparison.Ordinal);
            var btn = new VisualElement();
            btn.style.flexDirection = FlexDirection.Row;
            btn.style.marginBottom = 4;
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
            body.style.paddingTop = 6;
            body.style.paddingBottom = 6;
            body.style.paddingLeft = 8;
            body.style.paddingRight = 6;

            var titleText = entry.DisplayName;
            if (string.IsNullOrWhiteSpace(titleText))
            {
                titleText = entry.ContentId;
            }

            if (entry.IsDirty)
            {
                titleText = "• " + titleText;
            }

            body.Add(ContentVisualWarmConsoleUi.CreateTitleLabel(
                titleText, 12, true, ContentVisualWarmConsoleUi.Theme.TextPrimary));
            body.Add(ContentVisualWarmConsoleUi.CreateTinyPathLabel(entry.ContentId));
            btn.Add(body);

            btn.RegisterCallback<ClickEvent>(_ =>
            {
                session.FocusedContentId = entry.ContentId;
                faceUp = true;
                previewAnimSlot = CardAnimSlotIds.Idle;
                previewFingerprint = string.Empty;
                RefreshSidebar();
                RefreshContent();
            });

            return btn;
        }

        private void RefreshContent()
        {
            contentRoot.Clear();
            var entry = session.GetFocused();
            if (entry?.Dto == null)
            {
                contentRoot.Add(ContentVisualWarmConsoleUi.CreatePageHeader(
                    "未选择",
                    "从左侧树选择一张卡进行编辑。"));
                previewFingerprint = string.Empty;
                return;
            }

            var dto = entry.Dto;
            contentRoot.Add(ContentVisualWarmConsoleUi.CreatePageHeader(
                string.IsNullOrWhiteSpace(dto.displayName) ? dto.contentId : dto.displayName,
                dto.contentId + " · " + dto.kind
                + (entry.IsDirty ? " · 未保存" : string.Empty)));

            var topRow = new VisualElement();
            topRow.style.flexDirection = FlexDirection.Row;
            topRow.style.marginBottom = 8;
            contentRoot.Add(topRow);

            var previewCard = ContentVisualWarmConsoleUi.CreateSectionCard(
                "预览",
                "WYSIWYG（底盘 + L4）。可播放动画槽 / 翻转。",
                column =>
                {
                    previewContainer = new IMGUIContainer(() =>
                    {
                        EnsurePreview(entry);
                        var rect = GUILayoutUtility.GetRect(
                            1f, 1f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                        previewHost.Draw(rect);
                    });
                    previewContainer.style.minHeight = 420;
                    previewContainer.style.minWidth = 280;
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
            previewCard.style.marginRight = 8;
            topRow.Add(previewCard);

            var paramsCard = ContentVisualWarmConsoleUi.CreateSectionCard(
                "即时参数",
                "改动即时写入会话草稿并刷新预览。",
                column => BuildInstantParams(column, entry));
            paramsCard.style.flexGrow = 1;
            paramsCard.style.flexBasis = 0;
            topRow.Add(paramsCard);

            contentRoot.Add(ContentVisualWarmConsoleUi.CreateSectionCard(
                "描述",
                "实时反映到预览；可用「其他」中的 [SlotCode]。",
                column =>
                {
                    descriptionField = new TextField
                    {
                        multiline = true,
                        value = dto.description ?? string.Empty,
                    };
                    descriptionField.style.minHeight = 90;
                    descriptionField.RegisterValueChangedCallback(evt =>
                    {
                        dto.description = evt.newValue ?? string.Empty;
                        session.MarkDirty(dto.contentId);
                        InvalidateAndRefreshPreview(entry);
                        UpdateStatus();
                    });
                    column.Add(descriptionField);
                }));

            contentRoot.Add(ContentVisualWarmConsoleUi.CreateSectionCard(
                "动画",
                "五个主视图槽：none / folder / atlas。",
                column => BuildAnimationSection(column, entry)));

            // 「其他配置」默认折叠（extraSlots）。
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

            column.Add(ContentVisualWarmConsoleUi.WrapControl(
                "主视图 Offset X",
                null,
                BindFloat(dto.mainVisual.offsetX, v =>
                {
                    dto.mainVisual.offsetX = v;
                    OnDtoEdited(entry);
                })));
            column.Add(ContentVisualWarmConsoleUi.WrapControl(
                "主视图 Offset Y",
                null,
                BindFloat(dto.mainVisual.offsetY, v =>
                {
                    dto.mainVisual.offsetY = v;
                    OnDtoEdited(entry);
                })));
            column.Add(ContentVisualWarmConsoleUi.WrapControl(
                "主视图 Scale",
                null,
                BindFloat(dto.mainVisual.uniformScale, v =>
                {
                    dto.mainVisual.uniformScale = v;
                    OnDtoEdited(entry);
                })));

            column.Add(MakeSpriteField("主图标 mainIcon", dto.sprites.mainIcon, path =>
            {
                dto.sprites.mainIcon = path;
                OnDtoEdited(entry);
            }));
            column.Add(MakeSpriteField("卡面背景 faceBackground", dto.sprites.faceBackground, path =>
            {
                dto.sprites.faceBackground = path;
                OnDtoEdited(entry);
            }));
            column.Add(MakeSpriteField("卡背边框 backBorder", dto.sprites.backBorder, path =>
            {
                dto.sprites.backBorder = path;
                OnDtoEdited(entry);
            }));
            column.Add(MakeSpriteField("卡背背纹 backShirt", dto.sprites.backShirt, path =>
            {
                dto.sprites.backShirt = path;
                OnDtoEdited(entry);
            }));
            column.Add(MakeSpriteField("卡背 Logo backLogo", dto.sprites.backLogo, path =>
            {
                dto.sprites.backLogo = path;
                OnDtoEdited(entry);
            }));
            column.Add(MakeSpriteField("卡框 cardFrame", dto.sprites.cardFrame, path =>
            {
                dto.sprites.cardFrame = path;
                OnDtoEdited(entry);
            }));
            column.Add(MakeSpriteField("横幅 banner", dto.sprites.banner, path =>
            {
                dto.sprites.banner = path;
                OnDtoEdited(entry);
            }));

            column.Add(ContentVisualWarmConsoleUi.WrapControl(
                "显示名",
                null,
                BindText(dto.displayName, v =>
                {
                    dto.displayName = v ?? string.Empty;
                    OnDtoEdited(entry);
                })));

            var category = CardPresentationEditorSession.MapSidebarCategory(dto.kind);
            if (category == CardPresentationSidebarCategory.Monster
                || category == CardPresentationSidebarCategory.Avatar)
            {
                column.Add(ContentVisualWarmConsoleUi.WrapControl(
                    "攻击", null, BindInt(dto.stats.attack, v => { dto.stats.attack = v; OnDtoEdited(entry); })));
                column.Add(ContentVisualWarmConsoleUi.WrapControl(
                    "护甲", null, BindInt(dto.stats.armor, v => { dto.stats.armor = v; OnDtoEdited(entry); })));
                column.Add(ContentVisualWarmConsoleUi.WrapControl(
                    "生命", null, BindInt(dto.stats.hp, v => { dto.stats.hp = v; OnDtoEdited(entry); })));
                column.Add(ContentVisualWarmConsoleUi.WrapControl(
                    "行动", null, BindInt(dto.stats.action, v => { dto.stats.action = v; OnDtoEdited(entry); })));
            }

            column.Add(ContentVisualWarmConsoleUi.WrapControl(
                "金币 gold",
                "HelpCard/Relic 对应商店价；Monster 可填击杀金（预览暂不显示）。",
                BindInt(dto.gold, v => { dto.gold = v; OnDtoEdited(entry); })));

            if (category == CardPresentationSidebarCategory.Monster)
            {
                column.Add(ContentVisualWarmConsoleUi.WrapControl(
                    "牌组 deckId",
                    null,
                    BindText(dto.deckId, v =>
                    {
                        dto.deckId = v ?? string.Empty;
                        OnDtoEdited(entry);
                        RefreshSidebar();
                    })));
            }
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

            column.Add(ContentVisualWarmConsoleUi.WrapControl(
                "默认 FPS",
                null,
                BindFloat(dto.animations.defaultFps, v =>
                {
                    dto.animations.defaultFps = Mathf.Max(0.01f, v);
                    OnDtoEdited(entry);
                    RestartPreviewAnim(entry);
                })));

            var slotChoices = new List<string>(CardAnimSlotIds.All);
            var previewSlot = new PopupField<string>("预览槽", slotChoices, previewAnimSlot);
            previewSlot.RegisterValueChangedCallback(evt =>
            {
                previewAnimSlot = CardAnimSlotIds.Normalize(evt.newValue);
                RestartPreviewAnim(entry);
            });
            column.Add(ContentVisualWarmConsoleUi.WrapControl("预览动画槽", null, previewSlot));

            var sourceChoices = new List<string> { "none", "folder", "atlas" };
            for (var i = 0; i < CardAnimSlotIds.All.Length; i++)
            {
                var slotId = CardAnimSlotIds.All[i];
                var slotDto = FindSlot(dto, slotId);
                column.Add(ContentVisualWarmConsoleUi.CreateTitleLabel(
                    slotId, 13, true, ContentVisualWarmConsoleUi.Theme.AccentGoldValue));

                var sourceField = new PopupField<string>(sourceChoices, NormalizeSource(slotDto.sourceType));
                sourceField.RegisterValueChangedCallback(evt =>
                {
                    slotDto.sourceType = evt.newValue;
                    OnDtoEdited(entry);
                    if (string.Equals(previewAnimSlot, slotId, StringComparison.Ordinal))
                    {
                        RestartPreviewAnim(entry);
                    }
                });
                column.Add(ContentVisualWarmConsoleUi.WrapControl(slotId + " sourceType", null, sourceField));

                column.Add(ContentVisualWarmConsoleUi.WrapControl(
                    slotId + " path",
                    null,
                    BindText(slotDto.path, v =>
                    {
                        slotDto.path = v ?? string.Empty;
                        OnDtoEdited(entry);
                        if (string.Equals(previewAnimSlot, slotId, StringComparison.Ordinal))
                        {
                            RestartPreviewAnim(entry);
                        }
                    })));

                column.Add(ContentVisualWarmConsoleUi.WrapControl(
                    slotId + " offsetX",
                    null,
                    BindFloat(slotDto.offsetX, v =>
                    {
                        slotDto.offsetX = v;
                        OnDtoEdited(entry);
                        RestartPreviewAnim(entry);
                    })));
                column.Add(ContentVisualWarmConsoleUi.WrapControl(
                    slotId + " offsetY",
                    null,
                    BindFloat(slotDto.offsetY, v =>
                    {
                        slotDto.offsetY = v;
                        OnDtoEdited(entry);
                        RestartPreviewAnim(entry);
                    })));
            }
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

        private void InvalidateAndRefreshPreview(CardPresentationEditorEntry entry)
        {
            previewFingerprint = string.Empty;
            EnsurePreview(entry, force: true);
            previewContainer?.MarkDirtyRepaint();
            RefreshSidebarSelectionOnly();
        }

        private void RefreshSidebarSelectionOnly()
        {
            // 轻量：仅刷新侧栏脏标记/选中样式
            RefreshSidebar();
        }

        private void EnsurePreview(CardPresentationEditorEntry entry, bool force = false)
        {
            if (entry?.Dto == null)
            {
                return;
            }

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
            // Play() 可能再次打开 VisibleInsideMask；预览末尾统一关掉 Mask 交互。
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

            // mainVisual 由 CardSpriteAnimPlayer.Play 写入；此处再保险应用一次静态偏移。
            if (CardFaceSlotNodeMap.TryFindRenderer(
                    root.transform,
                    CardFaceSlotCodes.MainIcon,
                    out var mainRenderer)
                && dto.mainVisual != null)
            {
                var scale = dto.mainVisual.uniformScale > 0.0001f ? dto.mainVisual.uniformScale : 1f;
                mainRenderer.transform.localScale = new Vector3(scale, scale, 1f);
                var mask = CardMainVisualMaskAnchor.FindOrAdd(root.transform);
                Vector3 local;
                if (mask != null)
                {
                    local = mask.GetSuggestedLocalPosition(mainRenderer.transform);
                    local.x += dto.mainVisual.offsetX;
                    local.y += dto.mainVisual.offsetY;
                }
                else
                {
                    local = mainRenderer.transform.localPosition;
                    local.x = dto.mainVisual.offsetX;
                    local.y = dto.mainVisual.offsetY;
                }

                mainRenderer.transform.localPosition = local;
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
                // 不强制整页重建，避免打断翻牌后的朝向；仅刷新按钮文案与状态。
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

            ContentVisualKind contentKind;
            if (!Enum.TryParse(dto.kind, true, out contentKind))
            {
                contentKind = ContentVisualKind.Unknown;
            }

            var kind = CardFacePreviewBuilder.ToPresentationKind(contentKind, dto.contentId);
            if (kind == CardPresentationKind.Unknown)
            {
                // Skill / 其它道具侧 → HelpCard 预览模板。
                if (CardPresentationEditorSession.MapSidebarCategory(dto.kind)
                    == CardPresentationSidebarCategory.Item)
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
                else if (string.Equals(dto.kind, "Relic", StringComparison.OrdinalIgnoreCase))
                {
                    kind = CardPresentationKind.Relic;
                }
            }

            if (kind == CardPresentationKind.Unknown)
            {
                error = "该 Kind 不挂四套卡面：" + dto.contentId;
                return false;
            }

            var sprites = dto.sprites ?? new CardPresentationSpritesDto();
            var stats = dto.stats ?? new CardPresentationStatsDto();
            request = new CardFacePreviewRequest
            {
                DefId = dto.contentId,
                Kind = kind,
                DisplayName = string.IsNullOrWhiteSpace(dto.displayName)
                    ? session.GetDisplayName(dto.contentId, dto.kind)
                    : dto.displayName,
                BasicDescription = dto.description ?? string.Empty,
                MainIcon = CardPresentationSpritePath.LoadSprite(sprites.mainIcon),
                FaceBackground = CardPresentationSpritePath.LoadSprite(sprites.faceBackground),
                BackBorder = CardPresentationSpritePath.LoadSprite(sprites.backBorder),
                BackShirt = CardPresentationSpritePath.LoadSprite(sprites.backShirt),
                BackLogo = CardPresentationSpritePath.LoadSprite(sprites.backLogo),
                Attack = Mathf.Max(0, stats.attack),
                Armor = Mathf.Max(0, stats.armor),
                Hp = Mathf.Max(0, stats.hp),
                ActionCount = Mathf.Max(0, stats.action),
                FaceUp = faceUp,
            };
            return true;
        }

        private static string BuildPreviewFingerprint(CardPresentationConfigDto dto, bool faceUpFlag)
        {
            var sprites = dto.sprites ?? new CardPresentationSpritesDto();
            var stats = dto.stats ?? new CardPresentationStatsDto();
            var mv = dto.mainVisual ?? new CardPresentationMainVisualDto();
            return string.Join("|",
                dto.contentId,
                dto.kind,
                dto.displayName,
                dto.description,
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
                sprites.cardFrame,
                sprites.banner,
                mv.offsetX,
                mv.offsetY,
                mv.uniformScale,
                faceUpFlag ? "1" : "0");
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
            return ContentVisualWarmConsoleUi.WrapControl(label, path, field);
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

            var focused = session.GetFocused();
            var focusText = focused != null ? focused.ContentId : "（未选）";
            statusHelpBox.text = "条目 " + session.Entries.Count
                                 + " · 脏 " + session.DirtyCount
                                 + " · 焦点 " + focusText
                                 + " · " + previewHost.Status;
        }

        private void SaveAll()
        {
            if (!session.TrySaveAll(out var error))
            {
                EditorUtility.DisplayDialog("保存失败", error ?? "未知错误", "确定");
                return;
            }

            previewFingerprint = string.Empty;
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
