#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NineGrid.Content.Editor.Ui
{
    public static class ContentVisualWarmConsoleUi
    {
        public static class Theme
        {
            public static readonly Color RootBg = new Color(0.10f, 0.085f, 0.07f);
            public static readonly Color ContentBg = new Color(0.09f, 0.075f, 0.06f);
            public static readonly Color SidebarBg = new Color(0.12f, 0.095f, 0.08f);
            public static readonly Color HeaderBg = new Color(0.13f, 0.10f, 0.08f);
            public static readonly Color StatCardBg = new Color(0.16f, 0.12f, 0.09f);
            public static readonly Color SectionCardBg = new Color(0.15f, 0.12f, 0.095f);

            public static readonly Color AccentStrong = new Color(0.86f, 0.64f, 0.28f);
            public static readonly Color AccentGoldValue = new Color(0.94f, 0.75f, 0.40f);
            public static readonly Color AccentMid = new Color(0.83f, 0.62f, 0.26f);
            public static readonly Color AccentWeak = new Color(0.56f, 0.40f, 0.18f);

            public static readonly Color TextPrimary = new Color(0.95f, 0.89f, 0.79f);
            public static readonly Color TextSecondary = new Color(0.79f, 0.73f, 0.67f);
            public static readonly Color TextTertiary = new Color(0.78f, 0.72f, 0.68f);
            public static readonly Color TextPath = new Color(0.73f, 0.70f, 0.66f);
            public static readonly Color TextChecklist = new Color(0.83f, 0.78f, 0.72f);

            public static readonly Color Divider = new Color(0.22f, 0.18f, 0.14f);

            public static readonly Color NavNormalBg = new Color(0.18f, 0.14f, 0.11f);
            public static readonly Color NavSelectedBg = new Color(0.31f, 0.22f, 0.12f);
            public static readonly Color NavStripeNormal = new Color(0.20f, 0.16f, 0.13f);

            public static readonly Color PageTitle = new Color(0.95f, 0.90f, 0.80f);
            public static readonly Color PageDesc = new Color(0.80f, 0.74f, 0.67f);
        }

        public struct NavEntry
        {
            public VisualElement Button;
            public VisualElement Stripe;
            public string Key;
        }

        public static Label CreateTitleLabel(string text, int fontSize, bool bold, Color color)
        {
            var label = new Label(text);
            label.style.fontSize = fontSize;
            label.style.color = color;
            label.style.whiteSpace = WhiteSpace.Normal;
            if (bold)
            {
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
            }

            return label;
        }

        public static Label CreateDescriptionLabel(string text) =>
            CreateTitleLabel(text, 11, false, Theme.TextSecondary);

        public static Label CreateTinyPathLabel(string text) =>
            CreateTitleLabel(text, 11, false, Theme.TextPath);

        public static Label CreateChecklistLabel(string text)
        {
            var label = CreateTitleLabel("• " + text, 11, false, Theme.TextChecklist);
            label.style.marginBottom = 6;
            return label;
        }

        public static VisualElement WrapControl(string label, string description, VisualElement field)
        {
            var row = new VisualElement();
            row.style.paddingTop = 8;
            row.style.paddingBottom = 8;
            row.style.marginBottom = 4;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = Theme.Divider;

            row.Add(CreateTitleLabel(label, 13, true, Theme.TextPrimary));

            if (!string.IsNullOrEmpty(description))
            {
                var desc = CreateDescriptionLabel(description);
                desc.style.marginTop = 3;
                desc.style.marginBottom = 6;
                row.Add(desc);
            }

            field.style.flexGrow = 1;
            row.Add(field);
            return row;
        }

        /// <summary>标签与控件同一行，用于高密度配置面板。</summary>
        public static VisualElement WrapControlRow(
            string label,
            VisualElement field,
            float labelWidth = 118f,
            string tooltip = null)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 2;
            row.style.paddingTop = 1;
            row.style.paddingBottom = 1;
            row.style.minHeight = 22;

            var lbl = CreateTitleLabel(label, 11, false, Theme.TextSecondary);
            lbl.style.width = labelWidth;
            lbl.style.flexShrink = 0;
            lbl.style.unityTextAlign = TextAnchor.MiddleLeft;
            if (!string.IsNullOrEmpty(tooltip))
            {
                lbl.tooltip = tooltip;
                field.tooltip = tooltip;
            }

            row.Add(lbl);
            field.style.flexGrow = 1;
            field.style.flexShrink = 1;
            field.style.minWidth = 40;
            row.Add(field);
            return row;
        }

        public static void AddTwoColumnGrid(VisualElement column, IReadOnlyList<VisualElement> items, float columnGap = 8f)
        {
            VisualElement row = null;
            for (var i = 0; i < items.Count; i++)
            {
                if (i % 2 == 0)
                {
                    row = new VisualElement();
                    row.style.flexDirection = FlexDirection.Row;
                    row.style.marginBottom = 2;
                    column.Add(row);
                }

                var cell = new VisualElement();
                cell.style.flexGrow = 1;
                cell.style.flexBasis = 0;
                cell.style.minWidth = 0;
                if (i % 2 == 0)
                {
                    cell.style.marginRight = columnGap;
                }

                cell.Add(items[i]);
                row.Add(cell);
            }
        }

        public static VisualElement CreateInlineFieldGroup(params VisualElement[] fields)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 4;
            row.style.flexWrap = Wrap.Wrap;

            for (var i = 0; i < fields.Length; i++)
            {
                var wrap = fields[i];
                wrap.style.flexGrow = 1;
                wrap.style.flexBasis = 0;
                wrap.style.minWidth = 72;
                if (i < fields.Length - 1)
                {
                    wrap.style.marginRight = 6;
                }

                row.Add(wrap);
            }

            return row;
        }

        public static VisualElement CreatePageHeaderCompact(string title, string description)
        {
            var block = new VisualElement();
            block.style.marginBottom = 8;
            block.Add(CreateTitleLabel(title, 18, true, Theme.PageTitle));

            if (!string.IsNullOrEmpty(description))
            {
                var desc = CreateTitleLabel(description, 11, false, Theme.PageDesc);
                desc.style.marginTop = 2;
                desc.style.marginBottom = 6;
                block.Add(desc);
            }

            return block;
        }

        public static VisualElement CreateStatCard(string title, string value, string description)
        {
            var card = new VisualElement();
            card.style.flexDirection = FlexDirection.Row;
            card.style.width = 250;
            card.style.marginRight = 10;
            card.style.marginBottom = 10;
            card.style.backgroundColor = Theme.StatCardBg;
            card.style.borderTopLeftRadius = card.style.borderTopRightRadius = 8;
            card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 8;
            card.style.overflow = Overflow.Hidden;

            var stripe = new VisualElement();
            stripe.style.width = 3;
            stripe.style.backgroundColor = Theme.AccentMid;
            card.Add(stripe);

            var body = new VisualElement();
            body.style.flexGrow = 1;
            body.style.paddingTop = 12;
            body.style.paddingBottom = 10;
            body.style.paddingLeft = 12;
            body.style.paddingRight = 10;

            body.Add(CreateTitleLabel(title, 12, true, Theme.TextPrimary));
            var val = CreateTitleLabel(value, 20, true, Theme.AccentGoldValue);
            val.style.marginTop = 4;
            body.Add(val);
            var desc = CreateDescriptionLabel(description);
            desc.style.marginTop = 4;
            body.Add(desc);

            card.Add(body);
            return card;
        }

        public static VisualElement CreateStatsGrid(params (string title, string value, string desc)[] items)
        {
            var grid = new VisualElement();
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.marginBottom = 16;
            foreach (var item in items)
            {
                grid.Add(CreateStatCard(item.title, item.value, item.desc));
            }

            return grid;
        }

        public static VisualElement CreateSectionCard(string title, string description, Action<VisualElement> build)
        {
            var outer = new VisualElement();
            outer.style.flexDirection = FlexDirection.Row;
            outer.style.marginBottom = 6;
            outer.style.backgroundColor = Theme.SectionCardBg;
            outer.style.borderTopLeftRadius = outer.style.borderTopRightRadius = 8;
            outer.style.borderBottomLeftRadius = outer.style.borderBottomRightRadius = 8;
            outer.style.overflow = Overflow.Hidden;

            var stripe = new VisualElement();
            stripe.style.width = 3;
            stripe.style.backgroundColor = Theme.AccentWeak;
            outer.Add(stripe);

            var inner = new VisualElement();
            inner.style.flexGrow = 1;
            inner.style.paddingTop = 6;
            inner.style.paddingBottom = 6;
            inner.style.paddingLeft = 6;
            inner.style.paddingRight = 8;

            var foldout = new Foldout { text = title, value = true };
            foldout.style.unityFontStyleAndWeight = FontStyle.Bold;
            foldout.style.fontSize = 13;
            foldout.style.color = Theme.TextPrimary;
            inner.Add(foldout);

            if (!string.IsNullOrEmpty(description))
            {
                var desc = CreateDescriptionLabel(description);
                desc.style.marginLeft = 4;
                desc.style.marginTop = 6;
                desc.style.marginBottom = 8;
                foldout.contentContainer.Add(desc);
            }

            var column = new VisualElement { style = { flexDirection = FlexDirection.Column } };
            build?.Invoke(column);
            foldout.contentContainer.Add(column);

            inner.Add(foldout);
            outer.Add(inner);
            return outer;
        }

        public static VisualElement CreateNavButton(string title, string description, string key,
            Action onClick, List<NavEntry> registry)
        {
            var btn = new VisualElement();
            btn.style.flexDirection = FlexDirection.Row;
            btn.style.backgroundColor = Theme.NavNormalBg;
            btn.style.borderTopLeftRadius = btn.style.borderTopRightRadius = 6;
            btn.style.borderBottomLeftRadius = btn.style.borderBottomRightRadius = 6;
            btn.style.marginBottom = 8;
            btn.style.overflow = Overflow.Hidden;
            btn.userData = key;

            var stripe = new VisualElement();
            stripe.style.width = 4;
            stripe.style.backgroundColor = Theme.NavStripeNormal;
            btn.Add(stripe);

            var content = new VisualElement();
            content.style.flexGrow = 1;
            content.style.paddingTop = 10;
            content.style.paddingBottom = 10;
            content.style.paddingLeft = 10;
            content.style.paddingRight = 10;

            var titleLabel = CreateTitleLabel(title, 14, true, Theme.TextPrimary);
            titleLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            content.Add(titleLabel);

            if (!string.IsNullOrEmpty(description))
            {
                var descLabel = CreateDescriptionLabel(description);
                descLabel.style.marginTop = 4;
                descLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
                content.Add(descLabel);
            }

            btn.Add(content);
            btn.RegisterCallback<ClickEvent>(_ => onClick?.Invoke());

            registry.Add(new NavEntry { Button = btn, Stripe = stripe, Key = key });
            return btn;
        }

        public static void UpdateNavigationStyles(IReadOnlyList<NavEntry> entries, string selectedKey)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                var selected = entries[i].Key == selectedKey;
                entries[i].Button.style.backgroundColor = selected ? Theme.NavSelectedBg : Theme.NavNormalBg;
                entries[i].Stripe.style.backgroundColor = selected ? Theme.AccentStrong : Theme.NavStripeNormal;
            }
        }

        public static VisualElement CreatePageHeader(string title, string description)
        {
            var block = new VisualElement();
            block.style.marginBottom = 14;
            block.Add(CreateTitleLabel(title, 24, true, Theme.PageTitle));

            if (!string.IsNullOrEmpty(description))
            {
                var desc = CreateTitleLabel(description, 12, false, Theme.PageDesc);
                desc.style.marginTop = 6;
                desc.style.marginBottom = 14;
                block.Add(desc);
            }

            return block;
        }

        public static VisualElement CreateButtonRow(params Button[] buttons)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.marginBottom = 12;

            foreach (var btn in buttons)
            {
                btn.style.height = 28;
                btn.style.marginRight = 8;
                btn.style.marginBottom = 8;
                row.Add(btn);
            }

            return row;
        }

        public static HelpBox CreateStatusHelpBox(string message, HelpBoxMessageType type = HelpBoxMessageType.Info)
        {
            var box = new HelpBox(message, type);
            box.style.marginBottom = 12;
            return box;
        }

        public static VisualElement BuildHeader(string title, string subtitle)
        {
            var header = new VisualElement();
            header.style.backgroundColor = Theme.HeaderBg;
            header.style.paddingLeft = 16;
            header.style.paddingRight = 16;
            header.style.paddingTop = 14;
            header.style.paddingBottom = 10;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = Theme.Divider;

            header.Add(CreateTitleLabel(title, 22, true, Theme.TextPrimary));
            var sub = CreateTitleLabel(subtitle, 12, false, Theme.TextSecondary);
            sub.style.marginTop = 6;
            sub.style.whiteSpace = WhiteSpace.Normal;
            header.Add(sub);
            return header;
        }

        public static Toolbar BuildToolbar(params (string text, Action click, string tooltip)[] items)
        {
            var toolbar = new Toolbar();
            toolbar.style.height = 34;
            toolbar.style.paddingLeft = 8;
            toolbar.style.paddingRight = 8;
            toolbar.style.backgroundColor = Theme.HeaderBg;
            toolbar.style.borderBottomWidth = 1;
            toolbar.style.borderBottomColor = Theme.Divider;

            foreach (var item in items)
            {
                var btn = new ToolbarButton(item.click) { text = item.text };
                btn.tooltip = item.tooltip;
                toolbar.Add(btn);
            }

            return toolbar;
        }

        public static ScrollView CreateContentScroll(out VisualElement contentRoot)
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.style.backgroundColor = Theme.ContentBg;
            scroll.contentContainer.style.paddingLeft = 18;
            scroll.contentContainer.style.paddingRight = 18;
            scroll.contentContainer.style.paddingTop = 14;
            scroll.contentContainer.style.paddingBottom = 20;
            contentRoot = scroll.contentContainer;
            return scroll;
        }

        /// <summary>
        /// 悬停在<strong>已收起</strong>的 PopupField 上时，用滚轮切换当前选项（并拦住外层 ScrollView）。
        /// 无法驱动 Unity 展开后的原生下拉列表滚动——长列表请改用
        /// <see cref="SearchableChoiceField"/>（内部 ScrollView 原生支持滚轮）。
        /// </summary>
        public static void EnablePopupWheelScroll<T>(PopupField<T> field)
        {
            if (field == null)
            {
                return;
            }

            field.RegisterCallback<WheelEvent>(evt =>
            {
                var choices = field.choices;
                if (choices == null || choices.Count <= 1)
                {
                    return;
                }

                if (Mathf.Approximately(evt.delta.y, 0f))
                {
                    return;
                }

                var index = choices.IndexOf(field.value);
                if (index < 0)
                {
                    index = 0;
                }

                // delta.y > 0：滚轮向下 → 下一项
                var next = evt.delta.y > 0f ? index + 1 : index - 1;
                next = Mathf.Clamp(next, 0, choices.Count - 1);
                if (next != index)
                {
                    field.value = choices[next];
                }

                evt.StopPropagation();
                evt.PreventDefault();
            }, TrickleDown.TrickleDown);
        }
    }
}
#endif
