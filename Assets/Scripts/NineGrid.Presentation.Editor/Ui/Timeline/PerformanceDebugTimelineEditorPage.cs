#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NineGrid.Presentation.Debugging;
using NineGrid.Presentation.Debugging.Timeline;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NineGrid.Presentation.Editor.Ui.Timeline
{
    internal sealed class PerformanceDebugTimelinePoolPicker
    {
        private readonly PerformanceDebugCatalog catalog;
        private readonly string searchFilter;
        private VisualElement ghost;
        private string draggingModuleId;

        public PerformanceDebugTimelinePoolPicker(PerformanceDebugCatalog catalog, string searchFilter)
        {
            this.catalog = catalog;
            this.searchFilter = searchFilter ?? string.Empty;
        }

        public void UpdateSearch(string filter)
        {
            // search is read at build time via constructor field - parent rebuilds on search change
        }

        public VisualElement Build(VisualElement dragRoot, Action<string, Vector2> onDropAttempt)
        {
            var root = new VisualElement();
            root.style.flexGrow = 1;
            root.style.minWidth = 200;

            foreach (PerformanceDebugCategory category in GetPoolCategories())
            {
                bool expanded = EditorPrefs.GetBool(PoolExpandedKey(category), true);
                var foldout = new Foldout
                {
                    text = $"{PerformanceDebugWarmConsoleUi.GetPoolDisplayName(category)} ({CountInCategory(category)})",
                    value = expanded,
                };
                foldout.style.color = PerformanceDebugWarmConsoleUi.Theme.TextPrimary;
                foldout.style.marginBottom = 6;
                foldout.RegisterValueChangedCallback(evt =>
                {
                    EditorPrefs.SetBool(PoolExpandedKey(category), evt.newValue);
                });

                var chipWrap = new VisualElement();
                chipWrap.style.flexDirection = FlexDirection.Row;
                chipWrap.style.flexWrap = Wrap.Wrap;
                chipWrap.style.paddingTop = 4;
                chipWrap.style.paddingBottom = 4;

                IReadOnlyList<IPerformanceDebugModule> modules = catalog.GetByCategory(category);
                for (var i = 0; i < modules.Count; i++)
                {
                    IPerformanceDebugModule module = modules[i];
                    if (!MatchesSearch(module))
                    {
                        continue;
                    }

                    chipWrap.Add(CreateChip(module, dragRoot, onDropAttempt));
                }

                if (chipWrap.childCount == 0)
                {
                    chipWrap.Add(PerformanceDebugWarmConsoleUi.CreateDescriptionLabel("无匹配模块"));
                }

                foldout.Add(chipWrap);
                root.Add(foldout);
            }

            return root;
        }

        private static IEnumerable<PerformanceDebugCategory> GetPoolCategories()
        {
            yield return PerformanceDebugCategory.Flow;
            yield return PerformanceDebugCategory.Reaction;
            yield return PerformanceDebugCategory.Cue;
            yield return PerformanceDebugCategory.Interaction;
        }

        private int CountInCategory(PerformanceDebugCategory category)
        {
            return catalog.GetByCategory(category).Count(MatchesSearch);
        }

        private bool MatchesSearch(IPerformanceDebugModule module)
        {
            if (string.IsNullOrEmpty(searchFilter))
            {
                return true;
            }

            return module.DisplayName.IndexOf(searchFilter, StringComparison.OrdinalIgnoreCase) >= 0
                   || module.Id.IndexOf(searchFilter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private VisualElement CreateChip(
            IPerformanceDebugModule module,
            VisualElement dragRoot,
            Action<string, Vector2> onDropAttempt)
        {
            Color accent = PerformanceDebugWarmConsoleUi.GetPoolAccent(module.Category);
            var chip = new VisualElement();
            chip.style.flexDirection = FlexDirection.Row;
            chip.style.backgroundColor = new Color(0.18f, 0.14f, 0.11f);
            chip.style.borderTopLeftRadius = chip.style.borderTopRightRadius = 4;
            chip.style.borderBottomLeftRadius = chip.style.borderBottomRightRadius = 4;
            chip.style.marginRight = 6;
            chip.style.marginBottom = 6;
            chip.style.overflow = Overflow.Hidden;

            var stripe = new VisualElement();
            stripe.style.width = 3;
            stripe.style.backgroundColor = accent;
            chip.Add(stripe);

            var label = new Label(module.DisplayName);
            label.style.paddingLeft = 6;
            label.style.paddingRight = 8;
            label.style.paddingTop = 4;
            label.style.paddingBottom = 4;
            label.style.fontSize = 11;
            label.style.color = PerformanceDebugWarmConsoleUi.Theme.TextPrimary;
            chip.Add(label);

            chip.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0)
                {
                    return;
                }

                draggingModuleId = module.Id;
                CreateGhost(dragRoot, module.DisplayName, accent);
                chip.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            });

            chip.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (draggingModuleId != module.Id || ghost == null)
                {
                    return;
                }

                PositionGhost(evt.position);
                evt.StopPropagation();
            });

            chip.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (draggingModuleId != module.Id)
                {
                    return;
                }

                if (chip.HasPointerCapture(evt.pointerId))
                {
                    chip.ReleasePointer(evt.pointerId);
                }

                DestroyGhost();
                onDropAttempt?.Invoke(draggingModuleId, evt.position);
                draggingModuleId = null;
                evt.StopPropagation();
            });

            return chip;
        }

        private void CreateGhost(VisualElement dragRoot, string title, Color accent)
        {
            DestroyGhost();
            ghost = new VisualElement();
            ghost.style.position = Position.Absolute;
            ghost.style.backgroundColor = new Color(0.22f, 0.18f, 0.14f, 0.92f);
            ghost.style.borderTopLeftRadius = ghost.style.borderTopRightRadius = 4;
            ghost.style.borderBottomLeftRadius = ghost.style.borderBottomRightRadius = 4;
            ghost.style.paddingLeft = 8;
            ghost.style.paddingRight = 8;
            ghost.style.paddingTop = 4;
            ghost.style.paddingBottom = 4;
            ghost.style.borderLeftWidth = 3;
            ghost.style.borderLeftColor = accent;
            ghost.pickingMode = PickingMode.Ignore;

            var label = new Label(title);
            label.style.fontSize = 11;
            label.style.color = PerformanceDebugWarmConsoleUi.Theme.TextPrimary;
            ghost.Add(label);
            dragRoot.Add(ghost);
        }

        private void PositionGhost(Vector2 position)
        {
            if (ghost == null)
            {
                return;
            }

            Vector2 local = ghost.parent.WorldToLocal(position);
            ghost.style.left = local.x + 12;
            ghost.style.top = local.y + 12;
        }

        private void DestroyGhost()
        {
            ghost?.RemoveFromHierarchy();
            ghost = null;
        }

        private static string PoolExpandedKey(PerformanceDebugCategory category) =>
            $"NineGrid.PerfDebug.PoolExpanded.{category}";
    }

    public sealed class PerformanceDebugTimelineEditorPage
    {
        public const string PrefsArrangement = "NineGrid.PerfDebug.Timeline.Arrangement";
        public const string PrefsPixelsPerSecond = "NineGrid.PerfDebug.Timeline.PixelsPerSecond";
        public const string PresetsFolder = "Assets/Editor/PerformanceDebugTimelines";

        private readonly PerformanceDebugCatalog catalog;
        private readonly Func<PerformanceDebugTimelineRunner> getTimelineRunner;
        private readonly Action onArrangementSaved;
        private readonly string searchFilter;

        private PerformanceDebugTimelineArrangement arrangement;
        private PerformanceDebugTimelineCanvasView canvasView;
        private VisualElement inspectorRoot;
        private Slider zoomSlider;
        private string selectedClipId = string.Empty;
        private string searchFilterLive;

        public PerformanceDebugTimelineEditorPage(
            PerformanceDebugCatalog catalog,
            Func<PerformanceDebugTimelineRunner> getTimelineRunner,
            Action onArrangementSaved,
            string searchFilter)
        {
            this.catalog = catalog;
            this.getTimelineRunner = getTimelineRunner;
            this.onArrangementSaved = onArrangementSaved;
            this.searchFilter = searchFilter ?? string.Empty;
            searchFilterLive = this.searchFilter;
            arrangement = LoadArrangement();
        }

        public void Build(VisualElement contentRoot)
        {
            contentRoot.Clear();
            contentRoot.Add(PerformanceDebugWarmConsoleUi.CreatePageHeader(
                "时间轴编排",
                "从下方表演池拖拽模块到时间轴；调整位置后 Play 按 StartTime 调度点对点表演。"));

            var toolbarRow = new VisualElement();
            toolbarRow.style.flexDirection = FlexDirection.Row;
            toolbarRow.style.alignItems = Align.Center;
            toolbarRow.style.marginBottom = 8;
            toolbarRow.Add(BuildToolbar());

            zoomSlider = new Slider(
                PerformanceDebugTimelineLayout.MinPixelsPerSecond,
                PerformanceDebugTimelineLayout.MaxPixelsPerSecond)
            {
                value = EditorPrefs.GetFloat(PrefsPixelsPerSecond, PerformanceDebugTimelineLayout.DefaultPixelsPerSecond),
            };
            zoomSlider.style.width = 140;
            zoomSlider.style.marginLeft = 12;
            zoomSlider.RegisterValueChangedCallback(evt =>
            {
                EditorPrefs.SetFloat(PrefsPixelsPerSecond, evt.newValue);
                canvasView?.SetPixelsPerSecond(evt.newValue);
            });
            var zoomWrap = new VisualElement();
            zoomWrap.style.flexDirection = FlexDirection.Row;
            zoomWrap.style.alignItems = Align.Center;
            zoomWrap.style.marginLeft = 8;
            zoomWrap.Add(PerformanceDebugWarmConsoleUi.CreateTitleLabel(
                "缩放", 11, true, PerformanceDebugWarmConsoleUi.Theme.TextSecondary));
            zoomWrap.style.marginRight = 6;
            zoomWrap.Add(zoomSlider);
            toolbarRow.Add(zoomWrap);
            contentRoot.Add(toolbarRow);

            canvasView = new PerformanceDebugTimelineCanvasView(catalog);
            canvasView.Bind(arrangement);
            canvasView.SetPixelsPerSecond(EditorPrefs.GetFloat(PrefsPixelsPerSecond, PerformanceDebugTimelineLayout.DefaultPixelsPerSecond));
            canvasView.ClipSelected += OnClipSelected;
            canvasView.ArrangementChanged += SaveArrangement;

            var canvasCard = new VisualElement();
            canvasCard.style.flexGrow = 1;
            canvasCard.style.minHeight = 200;
            canvasCard.style.marginBottom = 10;
            canvasCard.style.backgroundColor = PerformanceDebugWarmConsoleUi.Theme.SectionCardBg;
            canvasCard.style.borderTopLeftRadius = canvasCard.style.borderTopRightRadius = 8;
            canvasCard.style.borderBottomLeftRadius = canvasCard.style.borderBottomRightRadius = 8;
            canvasCard.style.overflow = Overflow.Hidden;
            canvasCard.style.paddingTop = 6;
            canvasCard.style.paddingBottom = 6;
            canvasCard.style.paddingLeft = 6;
            canvasCard.style.paddingRight = 6;
            canvasCard.Add(canvasView.Build());
            contentRoot.Add(canvasCard);

            var lowerSplit = new TwoPaneSplitView(1, 320, TwoPaneSplitViewOrientation.Horizontal);
            lowerSplit.style.minHeight = 220;
            lowerSplit.style.flexGrow = 0;

            var poolScroll = new ScrollView(ScrollViewMode.Vertical);
            poolScroll.style.flexGrow = 1;
            poolScroll.style.backgroundColor = PerformanceDebugWarmConsoleUi.Theme.SectionCardBg;
            poolScroll.contentContainer.style.paddingLeft = 10;
            poolScroll.contentContainer.style.paddingRight = 10;
            poolScroll.contentContainer.style.paddingTop = 8;
            poolScroll.contentContainer.style.paddingBottom = 8;
            var poolPicker = new PerformanceDebugTimelinePoolPicker(catalog, searchFilterLive);
            poolScroll.Add(poolPicker.Build(contentRoot, OnPoolDrop));
            lowerSplit.Add(poolScroll);

            inspectorRoot = new ScrollView(ScrollViewMode.Vertical);
            inspectorRoot.style.flexGrow = 1;
            inspectorRoot.style.backgroundColor = PerformanceDebugWarmConsoleUi.Theme.SectionCardBg;
            inspectorRoot.contentContainer.style.paddingLeft = 12;
            inspectorRoot.contentContainer.style.paddingRight = 12;
            inspectorRoot.contentContainer.style.paddingTop = 10;
            inspectorRoot.contentContainer.style.paddingBottom = 12;
            lowerSplit.Add(inspectorRoot);

            contentRoot.Add(lowerSplit);
            RebuildInspector();
        }

        public void OnEditorUpdate()
        {
            PerformanceDebugTimelineRunner runner = getTimelineRunner?.Invoke();
            if (runner == null || canvasView == null)
            {
                return;
            }

            canvasView.SetPlayheadTime(runner.PlayheadTime);
        }

        public void SetSearchFilter(string filter)
        {
            searchFilterLive = filter ?? string.Empty;
        }

        private VisualElement BuildToolbar()
        {
            return PerformanceDebugWarmConsoleUi.CreateButtonRow(
                new Button(OnPlay) { text = "Play" },
                new Button(OnStop) { text = "Stop" },
                new Button(OnAddTrack) { text = "加轨" },
                new Button(OnRemoveTrack) { text = "删轨" },
                new Button(OnSavePreset) { text = "另存为" },
                new Button(OnLoadPreset) { text = "加载" },
                new Button(OnClear) { text = "清空" });
        }

        private void OnPlay()
        {
            PerformanceDebugTimelineRunner runner = getTimelineRunner?.Invoke();
            if (runner == null)
            {
                EditorUtility.DisplayDialog("未连接", "请先打开 PerformanceTestScene 并进入 Play Mode。", "确定");
                return;
            }

            runner.Play(arrangement);
        }

        private void OnStop()
        {
            getTimelineRunner?.Invoke()?.Stop();
        }

        private void OnAddTrack()
        {
            if (arrangement.TrackCount >= PerformanceDebugTimelineArrangement.MaxTrackCount)
            {
                return;
            }

            arrangement.TrackCount++;
            canvasView.Bind(arrangement);
            SaveArrangement();
        }

        private void OnRemoveTrack()
        {
            if (arrangement.TrackCount <= PerformanceDebugTimelineArrangement.MinTrackCount)
            {
                return;
            }

            arrangement.TrackCount--;
            for (var i = 0; i < arrangement.Clips.Count; i++)
            {
                if (arrangement.Clips[i].Track >= arrangement.TrackCount)
                {
                    arrangement.Clips[i].Track = arrangement.TrackCount - 1;
                }
            }

            canvasView.Bind(arrangement);
            SaveArrangement();
        }

        private void OnClear()
        {
            if (!EditorUtility.DisplayDialog("清空时间轴", "确定清空所有 clip？", "清空", "取消"))
            {
                return;
            }

            arrangement.Clear();
            selectedClipId = string.Empty;
            canvasView.Bind(arrangement);
            RebuildInspector();
            SaveArrangement();
        }

        private void OnSavePreset()
        {
            EnsurePresetsFolder();
            string path = EditorUtility.SaveFilePanel(
                "保存 Timeline 预设",
                PresetsFolder,
                "timeline",
                "json");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            File.WriteAllText(path, arrangement.ToJson());
            if (path.StartsWith(Application.dataPath))
            {
                AssetDatabase.Refresh();
            }
        }

        private void OnLoadPreset()
        {
            EnsurePresetsFolder();
            string path = EditorUtility.OpenFilePanel("加载 Timeline 预设", PresetsFolder, "json");
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return;
            }

            arrangement = PerformanceDebugTimelineArrangement.FromJson(File.ReadAllText(path));
            HydrateClipPayloads();
            selectedClipId = string.Empty;
            canvasView.Bind(arrangement);
            RebuildInspector();
            SaveArrangement();
        }

        private void OnPoolDrop(string moduleId, Vector2 worldPosition)
        {
            IPerformanceDebugModule module = catalog.FindById(moduleId);
            PerformanceDebugPayload payload = module?.Schema.CreateDefaultPayload();
            if (canvasView.TryDropModule(moduleId, worldPosition, payload))
            {
                selectedClipId = arrangement.Clips.LastOrDefault()?.ClipId ?? string.Empty;
                RebuildInspector();
                SaveArrangement();
            }
        }

        private void OnClipSelected(string clipId)
        {
            selectedClipId = clipId ?? string.Empty;
            RebuildInspector();
        }

        private void RebuildInspector()
        {
            if (inspectorRoot == null)
            {
                return;
            }

            inspectorRoot.Clear();
            PerformanceDebugTimelineClip clip = arrangement.FindClip(selectedClipId);
            if (clip == null)
            {
                inspectorRoot.Add(PerformanceDebugWarmConsoleUi.CreateDescriptionLabel("选中时间轴上的 clip 以编辑参数。"));
                return;
            }

            IPerformanceDebugModule module = catalog.FindById(clip.ModuleId);
            if (module == null)
            {
                inspectorRoot.Add(PerformanceDebugWarmConsoleUi.CreateDescriptionLabel("模块已失效。"));
                return;
            }

            if (clip.Payload == null)
            {
                clip.Payload = module.Schema.CreateDefaultPayload();
            }
            else
            {
                PerformanceDebugPayload defaults = module.Schema.CreateDefaultPayload();
                IReadOnlyList<PerformanceDebugFieldDef> schemaFields = module.Schema.Fields;
                for (var i = 0; i < schemaFields.Count; i++)
                {
                    PerformanceDebugFieldDef field = schemaFields[i];
                    if (string.IsNullOrEmpty(clip.Payload.GetString(field.Key)))
                    {
                        clip.Payload.Set(field.Key, defaults.GetString(field.Key, field.DefaultValue));
                    }
                }
            }

            inspectorRoot.Add(PerformanceDebugWarmConsoleUi.CreateTitleLabel(
                module.DisplayName, 14, true, PerformanceDebugWarmConsoleUi.Theme.TextPrimary));
            inspectorRoot.Add(PerformanceDebugWarmConsoleUi.CreateDescriptionLabel($"{module.Category} · {module.Id}"));

            var startField = new FloatField("起始时间 (s)") { value = clip.StartTime };
            startField.RegisterValueChangedCallback(evt =>
            {
                clip.StartTime = Mathf.Max(0f, evt.newValue);
                canvasView.Bind(arrangement);
                SaveArrangement();
            });
            inspectorRoot.Add(startField);

            var trackField = new IntegerField("轨道") { value = clip.Track };
            trackField.RegisterValueChangedCallback(evt =>
            {
                clip.Track = Mathf.Clamp(evt.newValue, 0, arrangement.TrackCount - 1);
                canvasView.Bind(arrangement);
                SaveArrangement();
            });
            inspectorRoot.Add(trackField);

            inspectorRoot.Add(PerformanceDebugWarmConsoleUi.CreateButtonRow(
                new Button(() => DuplicateClip(clip)) { text = "复制" },
                new Button(() => DeleteClip(clip)) { text = "删除" }));

            IReadOnlyList<PerformanceDebugFieldDef> fields = module.Schema.Fields;
            PerformanceDebugLayoutApplier.SyncDerivedDirection(clip.Payload);
            for (var i = 0; i < fields.Count; i++)
            {
                PerformanceDebugFieldDef field = fields[i];
                string current = clip.Payload.GetString(field.Key, field.DefaultValue);
                inspectorRoot.Add(PerformanceDebugFieldRowFactory.CreateParamRow(
                    field,
                    current,
                    clip.Payload,
                    (key, value) =>
                    {
                        clip.Payload.Set(key, value);
                        PerformanceDebugLayoutApplier.SyncDerivedDirection(clip.Payload);
                        PerformanceDebugFieldRowFactory.RefreshDerivedLabels(inspectorRoot, clip.Payload);
                        SaveArrangement();
                    },
                    () => PerformanceDebugLayoutApplier.SyncDerivedDirection(clip.Payload)));
            }
        }

        private void DuplicateClip(PerformanceDebugTimelineClip clip)
        {
            PerformanceDebugTimelineClip duplicate = arrangement.Duplicate(clip.ClipId);
            if (duplicate != null)
            {
                selectedClipId = duplicate.ClipId;
                canvasView.Bind(arrangement);
                canvasView.SetSelectedClip(selectedClipId);
                RebuildInspector();
                SaveArrangement();
            }
        }

        private void DeleteClip(PerformanceDebugTimelineClip clip)
        {
            arrangement.RemoveClip(clip.ClipId);
            selectedClipId = string.Empty;
            canvasView.Bind(arrangement);
            RebuildInspector();
            SaveArrangement();
        }

        private void SaveArrangement()
        {
            EditorPrefs.SetString(PrefsArrangement, arrangement.ToJson());
            onArrangementSaved?.Invoke();
        }

        private static PerformanceDebugTimelineArrangement LoadArrangement()
        {
            string json = EditorPrefs.GetString(PrefsArrangement, string.Empty);
            var loaded = PerformanceDebugTimelineArrangement.FromJson(json);
            HydrateClipPayloads(loaded);
            return loaded;
        }

        private void HydrateClipPayloads()
        {
            HydrateClipPayloads(arrangement);
        }

        private static void HydrateClipPayloads(PerformanceDebugTimelineArrangement target)
        {
            // payloads restored from JSON; module defaults applied on play if empty
        }

        private static void EnsurePresetsFolder()
        {
            if (!Directory.Exists(PresetsFolder))
            {
                Directory.CreateDirectory(PresetsFolder);
                AssetDatabase.Refresh();
            }
        }
    }
}
#endif
