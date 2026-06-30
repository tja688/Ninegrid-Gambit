using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace NineGrid.Presentation.Debugging
{
    public sealed class PerformanceDebugPanel : MonoBehaviour
    {
        private PerformanceDebugHarness harness;
        private PerformanceDebugCatalog catalog;
        private PerformanceDebugSequenceRunner runner;

        private Canvas canvas;
        private GameObject root;
        private Text titleText;
        private Text statusText;
        private Text logText;
        private InputField searchField;
        private Dropdown categoryDropdown;
        private Dropdown moduleDropdown;
        private Transform paramContainer;
        private readonly Dictionary<string, InputField> paramFields = new();
        private readonly List<IPerformanceDebugModule> filteredModules = new();
        private PerformanceDebugPayload workingPayload;
        private IPerformanceDebugModule selectedModule;
        private string searchFilter = string.Empty;

        public static PerformanceDebugPanel Create(
            GameObject host,
            PerformanceDebugHarness harness,
            PerformanceDebugCatalog catalog,
            PerformanceDebugSequenceRunner runner)
        {
            var panelObject = new GameObject("PerformanceDebugPanel");
            panelObject.transform.SetParent(host.transform, false);
            var panel = panelObject.AddComponent<PerformanceDebugPanel>();
            panel.Initialize(harness, catalog, runner);
            return panel;
        }

        private void Initialize(PerformanceDebugHarness harness, PerformanceDebugCatalog catalog, PerformanceDebugSequenceRunner runner)
        {
            this.harness = harness;
            this.catalog = catalog;
            this.runner = runner;
            BuildUi();
            RefreshModuleList();
        }

        public void ToggleVisible()
        {
            if (root != null)
            {
                root.SetActive(!root.activeSelf);
            }
        }

        private void Update()
        {
            RefreshStatus();
            RefreshLog();
        }

        private void BuildUi()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            gameObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            gameObject.AddComponent<GraphicRaycaster>();

            root = CreatePanel(transform, "Root", new Color(0.08f, 0.08f, 0.1f, 0.92f),
                anchorMin: new Vector2(0.02f, 0.08f), anchorMax: new Vector2(0.98f, 0.96f));

            var header = CreatePanel(root.transform, "Header", new Color(0.14f, 0.14f, 0.18f, 1f),
                anchorMin: new Vector2(0f, 0.9f), anchorMax: Vector2.one);
            titleText = CreateText(header.transform, "表演调试面板", 18, TextAnchor.MiddleLeft,
                new Vector2(0.02f, 0f), new Vector2(0.55f, 1f));
            statusText = CreateText(header.transform, string.Empty, 13, TextAnchor.MiddleRight,
                new Vector2(0.55f, 0f), new Vector2(0.98f, 1f));

            var left = CreatePanel(root.transform, "Left", new Color(0.11f, 0.11f, 0.14f, 1f),
                anchorMin: new Vector2(0f, 0.22f), anchorMax: new Vector2(0.28f, 0.9f));
            categoryDropdown = CreateDropdown(left.transform, "Category", new Vector2(0.05f, 0.9f), new Vector2(0.95f, 0.98f));
            searchField = CreateInput(left.transform, "Search", new Vector2(0.05f, 0.82f), new Vector2(0.95f, 0.89f));
            moduleDropdown = CreateDropdown(left.transform, "Module", new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.8f));

            var right = CreatePanel(root.transform, "Right", new Color(0.11f, 0.11f, 0.14f, 1f),
                anchorMin: new Vector2(0.3f, 0.22f), anchorMax: new Vector2(0.98f, 0.9f));
            CreateText(right.transform, "参数", 15, TextAnchor.UpperLeft, new Vector2(0.02f, 0.92f), new Vector2(0.3f, 0.99f));
            var paramScroll = CreatePanel(right.transform, "Params", new Color(0.09f, 0.09f, 0.12f, 1f),
                anchorMin: new Vector2(0.02f, 0.34f), anchorMax: new Vector2(0.98f, 0.9f));
            paramContainer = paramScroll.transform;
            BuildActionButtons(right.transform);

            var bottom = CreatePanel(root.transform, "Log", new Color(0.06f, 0.06f, 0.08f, 1f),
                anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(1f, 0.2f));
            logText = CreateText(bottom.transform, string.Empty, 12, TextAnchor.UpperLeft,
                new Vector2(0.01f, 0f), new Vector2(0.99f, 1f));
            logText.horizontalOverflow = HorizontalWrapMode.Wrap;
            logText.verticalOverflow = VerticalWrapMode.Overflow;

            PopulateCategoryDropdown();
            categoryDropdown.onValueChanged.AddListener(_ => RefreshModuleList());
            searchField.onValueChanged.AddListener(value =>
            {
                searchFilter = value ?? string.Empty;
                RefreshModuleList();
            });
            moduleDropdown.onValueChanged.AddListener(index => SelectModule(index));
        }

        private void BuildActionButtons(Transform parent)
        {
            CreateButton(parent, "Play", new Vector2(0.02f, 0.22f), new Vector2(0.18f, 0.3f), OnPlay);
            CreateButton(parent, "Replay", new Vector2(0.2f, 0.22f), new Vector2(0.36f, 0.3f), () => runner.Replay());
            CreateButton(parent, "Stop", new Vector2(0.38f, 0.22f), new Vector2(0.54f, 0.3f), () => runner.StopCurrent());
            CreateButton(parent, "Stop All", new Vector2(0.56f, 0.22f), new Vector2(0.74f, 0.3f), () => runner.StopAll());
            CreateButton(parent, "Reset", new Vector2(0.76f, 0.22f), new Vector2(0.98f, 0.3f), () => runner.ResetScene());
            CreateButton(parent, "Rebuild Context", new Vector2(0.02f, 0.12f), new Vector2(0.32f, 0.2f), OnRebuildContext);
            CreateButton(parent, "Copy Payload", new Vector2(0.34f, 0.12f), new Vector2(0.64f, 0.2f), OnCopyPayload);
            CreateButton(parent, "Hide (F1)", new Vector2(0.66f, 0.12f), new Vector2(0.98f, 0.2f), ToggleVisible);
        }

        private void PopulateCategoryDropdown()
        {
            categoryDropdown.options.Clear();
            categoryDropdown.options.Add(new Dropdown.OptionData("All"));
            foreach (PerformanceDebugCategory category in Enum.GetValues(typeof(PerformanceDebugCategory)))
            {
                categoryDropdown.options.Add(new Dropdown.OptionData(category.ToString()));
            }

            categoryDropdown.RefreshShownValue();
        }

        private void RefreshModuleList()
        {
            filteredModules.Clear();
            PerformanceDebugCategory? categoryFilter = GetSelectedCategoryFilter();

            for (var i = 0; i < catalog.Modules.Count; i++)
            {
                IPerformanceDebugModule module = catalog.Modules[i];
                if (categoryFilter.HasValue && module.Category != categoryFilter.Value)
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

            moduleDropdown.options.Clear();
            for (var i = 0; i < filteredModules.Count; i++)
            {
                IPerformanceDebugModule module = filteredModules[i];
                moduleDropdown.options.Add(new Dropdown.OptionData($"[{module.Category}] {module.DisplayName}"));
            }

            moduleDropdown.RefreshShownValue();
            if (filteredModules.Count > 0)
            {
                SelectModule(0);
            }
        }

        private PerformanceDebugCategory? GetSelectedCategoryFilter()
        {
            if (categoryDropdown.value <= 0)
            {
                return null;
            }

            string label = categoryDropdown.options[categoryDropdown.value].text;
            return Enum.TryParse(label, out PerformanceDebugCategory category) ? category : null;
        }

        private void SelectModule(int index)
        {
            if (index < 0 || index >= filteredModules.Count)
            {
                selectedModule = null;
                workingPayload = null;
                return;
            }

            selectedModule = filteredModules[index];
            workingPayload = selectedModule.Schema.CreateDefaultPayload();
            RebuildParamFields();
        }

        private void RebuildParamFields()
        {
            paramFields.Clear();
            for (var i = paramContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(paramContainer.GetChild(i).gameObject);
            }

            if (selectedModule == null)
            {
                return;
            }

            IReadOnlyList<PerformanceDebugFieldDef> fields = selectedModule.Schema.Fields;
            float yMax = 0.98f;
            const float rowHeight = 0.12f;
            for (var i = 0; i < fields.Count; i++)
            {
                PerformanceDebugFieldDef field = fields[i];
                float yMin = yMax - rowHeight;
                CreateText(paramContainer, field.Label, 13, TextAnchor.MiddleLeft,
                    new Vector2(0.02f, yMin), new Vector2(0.38f, yMax));
                InputField input = CreateInput(paramContainer, workingPayload.GetString(field.Key, field.DefaultValue),
                    new Vector2(0.4f, yMin + 0.01f), new Vector2(0.98f, yMax - 0.01f));
                string key = field.Key;
                input.onEndEdit.AddListener(value => workingPayload.Set(key, value));
                paramFields[key] = input;
                yMax = yMin - 0.02f;
            }
        }

        private void OnPlay()
        {
            if (selectedModule == null)
            {
                return;
            }

            SyncPayloadFromFields();
            runner.Play(selectedModule, workingPayload);
        }

        private void OnRebuildContext()
        {
            SyncPayloadFromFields();
            PerformanceDebugContextPreset preset = workingPayload?.GetContextPreset("contextPreset")
                ?? PerformanceDebugContextPreset.BattlePair;
            runner.RebuildContext(preset);
        }

        private void OnCopyPayload()
        {
            SyncPayloadFromFields();
            if (workingPayload == null)
            {
                return;
            }

            GUIUtility.systemCopyBuffer = workingPayload.ToJson();
            harness.Log.Info("Payload copied to clipboard.");
        }

        private void SyncPayloadFromFields()
        {
            if (workingPayload == null)
            {
                return;
            }

            foreach (KeyValuePair<string, InputField> pair in paramFields)
            {
                if (pair.Value != null)
                {
                    workingPayload.Set(pair.Key, pair.Value.text);
                }
            }
        }

        private void RefreshStatus()
        {
            if (statusText == null || harness == null)
            {
                return;
            }

            int actorCount = harness.Registry.Actors.Count;
            int anchorCount = harness.Registry.Anchors.Count;
            string moduleName = runner.CurrentModule != null ? runner.CurrentModule.DisplayName : "-";
            string playing = runner.CurrentModule != null && runner.CurrentModule.TryGetIsPlaying(harness.CreateContext(), out bool isPlaying) && isPlaying
                ? "Playing"
                : "Idle";
            string error = string.IsNullOrEmpty(runner.LastError) ? string.Empty : $" | Err: {runner.LastError}";
            statusText.text = $"{playing} | {moduleName} | actors {actorCount} | anchors {anchorCount}{error}";
        }

        private void RefreshLog()
        {
            if (logText == null || harness?.Log == null)
            {
                return;
            }

            IReadOnlyList<string> lines = harness.Log.Lines;
            if (lines.Count == 0)
            {
                logText.text = "日志为空。选择模块后点击 Play。";
                return;
            }

            var builder = new StringBuilder();
            int start = Mathf.Max(0, lines.Count - 8);
            for (var i = start; i < lines.Count; i++)
            {
                builder.AppendLine(lines[i]);
            }

            logText.text = builder.ToString();
        }

        private static GameObject CreatePanel(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return go;
        }

        private static Text CreateText(Transform parent, string content, int fontSize, TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return text;
        }

        private static InputField CreateInput(Transform parent, string placeholder, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject("Input", typeof(RectTransform), typeof(Image), typeof(InputField));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = new Color(0.18f, 0.18f, 0.22f, 1f);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Text text = CreateText(go.transform, string.Empty, 13, TextAnchor.MiddleLeft,
                new Vector2(0.04f, 0f), new Vector2(0.96f, 1f));
            text.color = Color.white;

            var input = go.GetComponent<InputField>();
            input.textComponent = text;
            input.text = placeholder;
            return input;
        }

        private static Dropdown CreateDropdown(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Dropdown));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.16f, 0.16f, 0.2f, 1f);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Text label = CreateText(go.transform, string.Empty, 13, TextAnchor.MiddleLeft,
                new Vector2(0.04f, 0f), new Vector2(0.92f, 1f));
            var dropdown = go.GetComponent<Dropdown>();
            dropdown.targetGraphic = go.GetComponent<Image>();
            dropdown.captionText = label;
            return dropdown;
        }

        private static void CreateButton(Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax, Action onClick)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.22f, 0.28f, 0.36f, 1f);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            CreateText(go.transform, label, 13, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
            go.GetComponent<Button>().onClick.AddListener(() => onClick?.Invoke());
        }
    }
}
