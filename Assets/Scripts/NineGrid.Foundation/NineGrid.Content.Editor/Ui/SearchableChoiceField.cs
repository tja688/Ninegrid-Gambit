#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace NineGrid.Content.Editor.Ui
{
    /// <summary>
    /// 可搜索选项控件：模糊匹配 Label / Value；展开列表用 ScrollView，原生支持滚轮。
    /// Unity <see cref="PopupField{T}"/> 展开菜单是宿主原生层，滚轮无法可靠接管——故用本控件替代。
    /// </summary>
    public sealed class SearchableChoiceField : VisualElement
    {
        public sealed class Choice
        {
            public string Label;
            public string Value;
            public string SearchHaystack;
        }

        private readonly Button toggleButton;
        private readonly VisualElement popupRoot;
        private readonly TextField searchField;
        private readonly ScrollView optionsScroll;
        private readonly List<Choice> allChoices = new List<Choice>();
        private string currentValue = string.Empty;
        private bool popupOpen;

        public event Action<string> ValueChanged;

        public string Value => currentValue;

        public SearchableChoiceField()
        {
            style.flexGrow = 1;
            style.flexDirection = FlexDirection.Column;

            toggleButton = new Button(TogglePopup) { text = "（未选择）" };
            toggleButton.style.unityTextAlign = TextAnchor.MiddleLeft;
            toggleButton.style.height = 22;
            toggleButton.style.flexGrow = 1;
            toggleButton.style.whiteSpace = WhiteSpace.Normal;
            Add(toggleButton);

            popupRoot = new VisualElement();
            popupRoot.style.display = DisplayStyle.None;
            popupRoot.style.flexDirection = FlexDirection.Column;
            popupRoot.style.marginTop = 4;
            popupRoot.style.paddingTop = 4;
            popupRoot.style.paddingBottom = 4;
            popupRoot.style.paddingLeft = 4;
            popupRoot.style.paddingRight = 4;
            popupRoot.style.backgroundColor = ContentVisualWarmConsoleUi.Theme.NavNormalBg;
            popupRoot.style.borderTopWidth = popupRoot.style.borderBottomWidth =
                popupRoot.style.borderLeftWidth = popupRoot.style.borderRightWidth = 1;
            popupRoot.style.borderTopColor = popupRoot.style.borderBottomColor =
                popupRoot.style.borderLeftColor = popupRoot.style.borderRightColor =
                    ContentVisualWarmConsoleUi.Theme.Divider;
            popupRoot.style.borderTopLeftRadius = popupRoot.style.borderTopRightRadius = 4;
            popupRoot.style.borderBottomLeftRadius = popupRoot.style.borderBottomRightRadius = 4;
            Add(popupRoot);

            searchField = new TextField { value = string.Empty };
            searchField.style.marginBottom = 4;
            searchField.RegisterValueChangedCallback(_ => RebuildFilteredOptions());
            popupRoot.Add(searchField);

            optionsScroll = new ScrollView(ScrollViewMode.Vertical);
            optionsScroll.style.maxHeight = 220;
            optionsScroll.style.minHeight = 80;
            popupRoot.Add(optionsScroll);
        }

        public void SetChoices(IReadOnlyList<Choice> choices, string selectedValue)
        {
            allChoices.Clear();
            if (choices != null)
            {
                for (var i = 0; i < choices.Count; i++)
                {
                    var choice = choices[i];
                    if (choice == null || string.IsNullOrEmpty(choice.Value))
                    {
                        continue;
                    }

                    if (string.IsNullOrEmpty(choice.SearchHaystack))
                    {
                        choice.SearchHaystack = (choice.Label ?? string.Empty) + " " + choice.Value;
                    }

                    allChoices.Add(choice);
                }
            }

            SetValueWithoutNotify(selectedValue);
            if (popupOpen)
            {
                RebuildFilteredOptions();
            }
        }

        public void SetValueWithoutNotify(string value)
        {
            currentValue = value ?? string.Empty;
            toggleButton.text = ResolveLabel(currentValue);
        }

        private void TogglePopup()
        {
            if (popupOpen)
            {
                ClosePopup();
                return;
            }

            popupOpen = true;
            popupRoot.style.display = DisplayStyle.Flex;
            searchField.SetValueWithoutNotify(string.Empty);
            RebuildFilteredOptions();
            searchField.schedule.Execute(() => searchField.Focus()).ExecuteLater(1);
        }

        private void ClosePopup()
        {
            popupOpen = false;
            popupRoot.style.display = DisplayStyle.None;
        }

        private void RebuildFilteredOptions()
        {
            optionsScroll.Clear();
            var query = searchField.value ?? string.Empty;
            var matched = 0;
            for (var i = 0; i < allChoices.Count; i++)
            {
                var choice = allChoices[i];
                if (!FuzzyMatch(choice.SearchHaystack, query)
                    && !FuzzyMatch(choice.Label, query)
                    && !FuzzyMatch(choice.Value, query))
                {
                    continue;
                }

                matched++;
                var captured = choice;
                var isSelected = string.Equals(captured.Value, currentValue, StringComparison.Ordinal);
                var row = new Button(() => Select(captured.Value))
                {
                    text = captured.Label ?? captured.Value,
                };
                row.style.unityTextAlign = TextAnchor.MiddleLeft;
                row.style.whiteSpace = WhiteSpace.Normal;
                row.style.marginBottom = 2;
                row.style.height = StyleKeyword.Auto;
                row.style.minHeight = 22;
                if (isSelected)
                {
                    row.style.backgroundColor = ContentVisualWarmConsoleUi.Theme.NavSelectedBg;
                }

                optionsScroll.Add(row);
            }

            if (matched == 0)
            {
                optionsScroll.Add(ContentVisualWarmConsoleUi.CreateDescriptionLabel("无匹配效果"));
            }
        }

        private void Select(string value)
        {
            var previous = currentValue;
            currentValue = value ?? string.Empty;
            toggleButton.text = ResolveLabel(currentValue);
            ClosePopup();
            if (!string.Equals(previous, currentValue, StringComparison.Ordinal))
            {
                ValueChanged?.Invoke(currentValue);
            }
        }

        private string ResolveLabel(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "（未选择）";
            }

            for (var i = 0; i < allChoices.Count; i++)
            {
                if (string.Equals(allChoices[i].Value, value, StringComparison.Ordinal))
                {
                    return allChoices[i].Label ?? value;
                }
            }

            return value;
        }

        /// <summary>
        /// 模糊匹配：忽略大小写的包含，或字符子序列（打字顺序保留）。
        /// </summary>
        public static bool FuzzyMatch(string haystack, string needle)
        {
            if (string.IsNullOrEmpty(needle))
            {
                return true;
            }

            if (string.IsNullOrEmpty(haystack))
            {
                return false;
            }

            if (haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            var h = haystack;
            var n = needle;
            var j = 0;
            for (var i = 0; i < h.Length && j < n.Length; i++)
            {
                if (char.ToLowerInvariant(h[i]) == char.ToLowerInvariant(n[j]))
                {
                    j++;
                }
            }

            return j == n.Length;
        }
    }
}
#endif
