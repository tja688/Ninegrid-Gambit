#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NineGrid.Content.CardPresentation;
using NineGrid.Content.Editor.Ui;
using NineGrid.Core.Content;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NineGrid.Content.Editor
{
    public sealed partial class CardPresentationEditorWindow
    {
        private VisualElement BuildDungeonFictionSectionFoldout()
        {
            const string key = "section:dungeon-fiction";
            if (!foldoutState.ContainsKey(key))
            {
                foldoutState[key] = true;
            }

            return BuildSectionFoldout(key, "地下城虚构", container =>
            {
                container.Add(BuildNavButton(
                    session.IsDungeonFictionRulesFocused,
                    "切换规则",
                    "层内切分 · 困难血色",
                    () =>
                    {
                        session.FocusDungeonFiction(CardPresentationEditorSession.DungeonFictionRulesId);
                        RefreshAll();
                    }));

                var document = session.DungeonEnvironments;
                var variants = document?.Table?.variants;
                if (variants == null || variants.Length == 0)
                {
                    container.Add(ContentVisualWarmConsoleUi.CreateDescriptionLabel("（无环境变体）"));
                    return;
                }

                var groups = new List<(string Layer, List<DungeonEnvironmentVariantDto> Rows)>();
                for (var i = 0; i < variants.Length; i++)
                {
                    var row = variants[i];
                    if (row == null || string.IsNullOrWhiteSpace(row.id))
                    {
                        continue;
                    }

                    var layer = string.IsNullOrWhiteSpace(row.layerName) ? "未分层" : row.layerName;
                    var found = false;
                    for (var g = 0; g < groups.Count; g++)
                    {
                        if (!string.Equals(groups[g].Layer, layer, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        groups[g].Rows.Add(row);
                        found = true;
                        break;
                    }

                    if (!found)
                    {
                        groups.Add((layer, new List<DungeonEnvironmentVariantDto> { row }));
                    }
                }

                for (var g = 0; g < groups.Count; g++)
                {
                    var group = groups[g];
                    var key = "dungeon-layer:" + group.Layer;
                    if (!foldoutState.TryGetValue(key, out var expanded))
                    {
                        expanded = true;
                        foldoutState[key] = expanded;
                    }

                    var foldout = new Foldout
                    {
                        text = group.Layer + " · " + group.Rows.Count,
                        value = expanded,
                    };
                    foldout.style.marginLeft = 4;
                    foldout.style.marginBottom = 2;
                    foldout.RegisterValueChangedCallback(evt => foldoutState[key] = evt.newValue);
                    for (var i = 0; i < group.Rows.Count; i++)
                    {
                        foldout.contentContainer.Add(BuildDungeonVariantNavButton(group.Rows[i]));
                    }

                    container.Add(foldout);
                }
            });
        }

        private VisualElement BuildDungeonVariantNavButton(DungeonEnvironmentVariantDto row)
        {
            var selected = session.FocusKind == CardPresentationEditorFocusKind.DungeonFiction
                           && string.Equals(row.id, session.FocusedDungeonVariantId, StringComparison.Ordinal);
            var title = string.IsNullOrWhiteSpace(row.displayName)
                ? row.variantName
                : row.displayName;
            if (session.DungeonEnvironments != null && session.DungeonEnvironments.IsDirty)
            {
                title = "• " + title;
            }

            var subtitle = row.isBlood
                ? "困难 · 第" + row.floor + "层整层"
                : "第" + row.floor + "层 · 房间 " + row.roomMin + "–" + row.roomMax;
            return BuildNavButton(
                selected,
                title,
                subtitle,
                () =>
                {
                    session.FocusDungeonFiction(row.id);
                    RefreshAll();
                });
        }

        private void BuildDungeonFictionContent()
        {
            var document = session.DungeonEnvironments;
            if (document?.Table == null)
            {
                contentRoot.Add(ContentVisualWarmConsoleUi.CreatePageHeader(
                    "地下城虚构",
                    "未能加载 dungeon_environments.json。保存一次会按默认表写出。"));
                return;
            }

            if (session.IsDungeonFictionRulesFocused)
            {
                BuildDungeonFictionRulesContent(document);
                return;
            }

            var row = session.GetFocusedDungeonVariant();
            if (row == null)
            {
                contentRoot.Add(ContentVisualWarmConsoleUi.CreatePageHeader(
                    "未选择",
                    "从左侧「地下城虚构」选择一个环境变体。"));
                return;
            }

            BuildDungeonFictionVariantContent(document, row);
        }

        private void BuildDungeonFictionRulesContent(DungeonEnvironmentEditorDocument document)
        {
            var table = document.Table;
            contentRoot.Add(ContentVisualWarmConsoleUi.CreatePageHeader(
                "地下城虚构 · 切换规则",
                "层内房间切分与困难血色。具体卡面/场地图在各变体详情里替换。表："
                + "Assets/Arts/ContentVisual/tables/" + DungeonEnvironmentCatalog.FileName));

            contentRoot.Add(ContentVisualWarmConsoleUi.CreateStatusHelpBox(
                "怪物卡 JSON 的 faceBackground 不是运行时权威。保存后 Catalog 立即重载，进 Play 即可看到场地与卡面背景切换。"));

            contentRoot.Add(ContentVisualWarmConsoleUi.CreateStatsGrid(
                ("变体", table.variants != null ? table.variants.Length.ToString() : "0", "含三层血色"),
                ("切分", table.roomSplit.ToString(), "层内前/后段默认房间数"),
                ("状态", document.IsDirty ? "未保存" : "已保存", "Authoring + Streaming 双写")));

            contentRoot.Add(ContentVisualWarmConsoleUi.CreateSectionCard(
                "进度规则",
                "困难档整层走 isBlood 变体；普通/进阶按各变体自己的房间范围命中。",
                column =>
                {
                    var splitField = new IntegerField { value = table.roomSplit };
                    splitField.RegisterValueChangedCallback(evt =>
                    {
                        var value = evt.newValue < 1 ? 1 : evt.newValue;
                        table.roomSplit = value;
                        if (value != evt.newValue)
                        {
                            splitField.SetValueWithoutNotify(value);
                        }

                        MarkDungeonFictionDirty();
                    });
                    column.Add(ContentVisualWarmConsoleUi.WrapControl(
                        "层内切分节点",
                        "默认每层前 N 房用前段环境、之后用后段。改这里只记规则，不会自动改已有变体的房间范围。",
                        splitField));

                    column.Add(ContentVisualWarmConsoleUi.CreateChecklistLabel(
                        "普通：第 1 层密林 → 第 2 层岩层 → 第 3 层溶洞"));
                    column.Add(ContentVisualWarmConsoleUi.CreateChecklistLabel(
                        "每层前四房 / 后四房切内部变体（可在变体页改房间范围）"));
                    column.Add(ContentVisualWarmConsoleUi.CreateChecklistLabel(
                        "选人「困难」整层强制该层血色变体，数值 +2/+4"));
                }));
        }

        private void BuildDungeonFictionVariantContent(
            DungeonEnvironmentEditorDocument document,
            DungeonEnvironmentVariantDto row)
        {
            var dirty = document.IsDirty ? " · 未保存" : string.Empty;
            contentRoot.Add(ContentVisualWarmConsoleUi.CreatePageHeaderCompact(
                string.IsNullOrWhiteSpace(row.displayName) ? row.id : row.displayName,
                row.id + " · 第" + row.floor + "层" + dirty));

            contentRoot.Add(ContentVisualWarmConsoleUi.CreateStatsGrid(
                ("楼层", row.floor.ToString(), row.layerName),
                ("房间", row.roomMin + "–" + row.roomMax, row.isBlood ? "困难整层" : "普通切分"),
                ("MainBG", row.mainBackgroundHex, "滑动底色罩"),
                ("格面", row.slotHex, "九宫 slots")));

            contentRoot.Add(ContentVisualWarmConsoleUi.CreateSectionCard(
                "文案",
                "显示名会进大楼层提示和战前 {floor}。改名不会自动改资源路径，方便只换图。",
                column =>
                {
                    var layerField = new TextField { value = row.layerName };
                    layerField.RegisterValueChangedCallback(evt =>
                    {
                        row.layerName = evt.newValue ?? string.Empty;
                        MarkDungeonFictionDirty();
                    });
                    column.Add(ContentVisualWarmConsoleUi.WrapControl(
                        "层名",
                        "密林 / 岩层 / 溶洞，侧栏分组用。",
                        layerField));

                    var variantField = new TextField { value = row.variantName };
                    variantField.RegisterValueChangedCallback(evt =>
                    {
                        row.variantName = evt.newValue ?? string.Empty;
                        MarkDungeonFictionDirty();
                    });
                    column.Add(ContentVisualWarmConsoleUi.WrapControl(
                        "变体名",
                        "翡翠迷雾、藏骨堂、血色等。",
                        variantField));

                    var displayField = new TextField { value = row.displayName };
                    displayField.RegisterValueChangedCallback(evt =>
                    {
                        row.displayName = evt.newValue ?? string.Empty;
                        MarkDungeonFictionDirty();
                    });
                    column.Add(ContentVisualWarmConsoleUi.WrapControl(
                        "显示名",
                        "玩家看到的完整名，例如 溶洞_黄昏礼堂。",
                        displayField));

                    var notesField = new TextField { value = row.notes, multiline = true };
                    notesField.style.minHeight = 48;
                    notesField.RegisterValueChangedCallback(evt =>
                    {
                        row.notes = evt.newValue ?? string.Empty;
                        MarkDungeonFictionDirty();
                    });
                    column.Add(ContentVisualWarmConsoleUi.WrapControl(
                        "备注",
                        "给自己看的替换备忘，不进游戏。",
                        notesField));
                }));

            contentRoot.Add(ContentVisualWarmConsoleUi.CreateSectionCard(
                "进度绑定",
                "Resolve(floor, room, isHard) 用这些字段命中本行。",
                column =>
                {
                    var floorField = new IntegerField { value = row.floor };
                    floorField.RegisterValueChangedCallback(evt =>
                    {
                        row.floor = evt.newValue < 1 ? 1 : evt.newValue;
                        MarkDungeonFictionDirty();
                    });
                    column.Add(ContentVisualWarmConsoleUi.WrapControl(
                        "楼层",
                        "1 密林 / 2 岩层 / 3 溶洞。",
                        floorField));

                    var minField = new IntegerField { value = row.roomMin };
                    var maxField = new IntegerField { value = row.roomMax };
                    minField.RegisterValueChangedCallback(evt =>
                    {
                        row.roomMin = evt.newValue < 1 ? 1 : evt.newValue;
                        MarkDungeonFictionDirty();
                    });
                    maxField.RegisterValueChangedCallback(evt =>
                    {
                        row.roomMax = evt.newValue < 1 ? 1 : evt.newValue;
                        MarkDungeonFictionDirty();
                    });
                    column.Add(ContentVisualWarmConsoleUi.WrapControl(
                        "房间起",
                        "层内 1–8。普通变体通常 1–4 或 5–8。",
                        minField));
                    column.Add(ContentVisualWarmConsoleUi.WrapControl(
                        "房间止",
                        "含端点。困难血色一般为 1–8。",
                        maxField));

                    var bloodToggle = new Toggle { value = row.isBlood };
                    bloodToggle.RegisterValueChangedCallback(evt =>
                    {
                        row.isBlood = evt.newValue;
                        MarkDungeonFictionDirty();
                    });
                    column.Add(ContentVisualWarmConsoleUi.WrapControl(
                        "困难血色",
                        "勾选后，选人困难档整层命中本行，不再走普通前/后段。",
                        bloodToggle));
                }));

            Image facePreview = null;
            Image panelPreview = null;
            contentRoot.Add(ContentVisualWarmConsoleUi.CreateSectionCard(
                "卡面背景",
                "怪物卡运行时覆盖此图。路径需落在 Resources 下（ADR-0008）。",
                column =>
                {
                    facePreview = CreateSpritePreview(row.faceBackground, 94f, 136f);
                    column.Add(facePreview);
                    var faceField = CreateSpriteObjectField(row.faceBackground, sprite =>
                    {
                        row.faceBackground = ToSpriteAssetPath(sprite);
                        if (facePreview != null)
                        {
                            facePreview.sprite = sprite;
                        }

                        MarkDungeonFictionDirty();
                    });
                    var facePathLabel = ContentVisualWarmConsoleUi.CreateTinyPathLabel(row.faceBackground);
                    column.Add(ContentVisualWarmConsoleUi.WrapControl(
                        "卡面背景 Sprite",
                        "建议 47×68、PPU 16。换图即可，不必改显示名。",
                        faceField));
                    column.Add(facePathLabel);
                    faceField.RegisterValueChangedCallback(_ =>
                    {
                        facePathLabel.text = row.faceBackground ?? string.Empty;
                    });
                }));

            contentRoot.Add(ContentVisualWarmConsoleUi.CreateSectionCard(
                "场地与底色",
                "GroundPanel 是 9-slice 场地框；MainBG 是画面氛围底；格面是 GroundAnchors 下白色 slot 框的 tint。三色须同属 Apollo，明度分层：底最暗、格面中间、框上已画亮轨。",
                column =>
                {
                    panelPreview = CreateSpritePreview(row.groundPanel, 96f, 96f);
                    column.Add(panelPreview);
                    var panelField = CreateSpriteObjectField(row.groundPanel, sprite =>
                    {
                        row.groundPanel = ToSpriteAssetPath(sprite);
                        if (panelPreview != null)
                        {
                            panelPreview.sprite = sprite;
                        }

                        MarkDungeonFictionDirty();
                    });
                    var panelPathLabel = ContentVisualWarmConsoleUi.CreateTinyPathLabel(row.groundPanel);
                    column.Add(ContentVisualWarmConsoleUi.WrapControl(
                        "GroundPanel Sprite",
                        "建议 96×96、PPU 32、border 32。血色三层可共用一张。",
                        panelField));
                    column.Add(panelPathLabel);
                    panelField.RegisterValueChangedCallback(_ =>
                    {
                        panelPathLabel.text = row.groundPanel ?? string.Empty;
                    });

                    Image mainBgPreview = CreateSpritePreview(row.mainBackground, 96f, 96f);
                    column.Add(mainBgPreview);
                    var mainBgField = CreateSpriteObjectField(row.mainBackground, sprite =>
                    {
                        row.mainBackground = ToSpriteAssetPath(sprite);
                        if (mainBgPreview != null)
                        {
                            mainBgPreview.sprite = sprite;
                        }

                        MarkDungeonFictionDirty();
                    });
                    var mainBgPathLabel = ContentVisualWarmConsoleUi.CreateTinyPathLabel(row.mainBackground);
                    column.Add(ContentVisualWarmConsoleUi.WrapControl(
                        "MainBG 滑动贴图",
                        "32×32 Repeat wrap。密林/岩层/溶洞各一张，血色三层共用。",
                        mainBgField));
                    column.Add(mainBgPathLabel);
                    mainBgField.RegisterValueChangedCallback(_ =>
                    {
                        mainBgPathLabel.text = row.mainBackground ?? string.Empty;
                    });

                    AddApolloColorFields(
                        column,
                        "MainBG 色",
                        "滑动底色罩。普通层 #b09173，困难血色 #7a0305。",
                        "MainBG hex",
                        () => row.mainBackgroundHex,
                        hex => row.mainBackgroundHex = hex,
                        DungeonEnvironmentCatalog.MainBackgroundDefaultColorHex);
                    AddApolloColorFields(
                        column,
                        "格面色",
                        "GroundAnchors/slotN 白色框 tint。与场地框填充同族、比 MainBG 亮，让九宫格读得出来。",
                        "格面 hex",
                        () => row.slotHex,
                        hex => row.slotHex = hex,
                        "#a53030");
                }));
        }

        private void AddApolloColorFields(
            VisualElement column,
            string colorLabel,
            string colorHint,
            string hexLabel,
            Func<string> getHex,
            Action<string> setHex,
            string fallbackHex)
        {
            var parsed = ParseHexColor(getHex());
            var colorField = new ColorField { value = parsed, showAlpha = false };
            var hexField = new TextField { value = getHex() };
            colorField.RegisterValueChangedCallback(evt =>
            {
                var hex = "#" + ColorUtility.ToHtmlStringRGB(evt.newValue).ToLowerInvariant();
                setHex(hex);
                hexField.SetValueWithoutNotify(hex);
                MarkDungeonFictionDirty();
            });
            hexField.RegisterValueChangedCallback(evt =>
            {
                var hex = string.IsNullOrWhiteSpace(evt.newValue)
                    ? fallbackHex
                    : evt.newValue.Trim();
                setHex(hex);
                if (ColorUtility.TryParseHtmlString(hex, out var color))
                {
                    color.a = 1f;
                    colorField.SetValueWithoutNotify(color);
                }

                MarkDungeonFictionDirty();
            });
            column.Add(ContentVisualWarmConsoleUi.WrapControl(colorLabel, colorHint, colorField));
            column.Add(ContentVisualWarmConsoleUi.WrapControl(
                hexLabel,
                "须是 Apollo 色板内的 hex。",
                hexField));
        }

        private void MarkDungeonFictionDirty()
        {
            UpdateStatus();
        }

        private static Image CreateSpritePreview(string assetPath, float width, float height)
        {
            var image = new Image
            {
                sprite = CardPresentationSpritePath.LoadSprite(assetPath),
                scaleMode = ScaleMode.ScaleToFit,
            };
            image.style.width = width;
            image.style.height = height;
            image.style.marginTop = 8;
            image.style.marginBottom = 8;
            image.style.alignSelf = Align.FlexStart;
            return image;
        }

        private static ObjectField CreateSpriteObjectField(string assetPath, Action<Sprite> onChanged)
        {
            var field = new ObjectField
            {
                objectType = typeof(Sprite),
                allowSceneObjects = false,
                value = CardPresentationSpritePath.LoadSprite(assetPath),
            };
            field.RegisterValueChangedCallback(evt => onChanged?.Invoke(evt.newValue as Sprite));
            return field;
        }

        private static string ToSpriteAssetPath(Sprite sprite)
        {
            return CardPresentationMigration.AssetPathOrEmpty(sprite);
        }

        private static Color ParseHexColor(string hex)
        {
            if (!string.IsNullOrWhiteSpace(hex) && ColorUtility.TryParseHtmlString(hex, out var color))
            {
                color.a = 1f;
                return color;
            }

            return new Color(0.141f, 0.082f, 0.153f, 1f);
        }
    }
}
#endif
