#if UNITY_EDITOR
using System;
using NineGrid.Presentation.Debugging;
using NineGrid.Presentation.Debugging.Timeline;
using NineGrid.Presentation.Editor.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace NineGrid.Presentation.Editor.Ui.Timeline
{
    internal static class PerformanceDebugTimelineLayout
    {
        public const float DefaultPixelsPerSecond = 80f;
        public const float MinPixelsPerSecond = 40f;
        public const float MaxPixelsPerSecond = 200f;
        public const float RowHeight = 36f;
        public const float RulerHeight = 28f;
        public const float MinClipWidth = 48f;
        public const float SnapInterval = 0.1f;
    }

    internal sealed class PerformanceDebugTimelineCanvasView
    {
        public event Action<string> ClipSelected;
        public event Action ArrangementChanged;

        private readonly PerformanceDebugCatalog catalog;
        private PerformanceDebugTimelineArrangement arrangement;
        private VisualElement canvasRoot;
        private VisualElement trackLayer;
        private VisualElement clipLayer;
        private VisualElement playhead;
        private ScrollView scrollView;
        private float pixelsPerSecond = PerformanceDebugTimelineLayout.DefaultPixelsPerSecond;
        private string selectedClipId = string.Empty;
        private float playheadTime;

        private string draggingClipId;
        private Vector2 dragStartMouse;
        private float dragStartTime;
        private int dragStartTrack;
        private bool snapEnabled = true;

        public PerformanceDebugTimelineCanvasView(PerformanceDebugCatalog catalog)
        {
            this.catalog = catalog;
        }

        public VisualElement Root { get; private set; }

        public void Bind(PerformanceDebugTimelineArrangement source)
        {
            arrangement = source;
            Rebuild();
        }

        public void SetPixelsPerSecond(float value)
        {
            pixelsPerSecond = Mathf.Clamp(value, PerformanceDebugTimelineLayout.MinPixelsPerSecond,
                PerformanceDebugTimelineLayout.MaxPixelsPerSecond);
            Rebuild();
        }

        public float PixelsPerSecond => pixelsPerSecond;

        public void SetSelectedClip(string clipId)
        {
            selectedClipId = clipId ?? string.Empty;
            RefreshClipStyles();
        }

        public void SetPlayheadTime(float time)
        {
            playheadTime = Mathf.Max(0f, time);
            UpdatePlayhead();
        }

        public VisualElement Build()
        {
            Root = new VisualElement();
            Root.style.flexGrow = 1;
            Root.style.minHeight = 180;

            scrollView = new ScrollView(ScrollViewMode.Horizontal | ScrollViewMode.Vertical);
            scrollView.style.flexGrow = 1;
            scrollView.style.backgroundColor = new Color(0.08f, 0.065f, 0.05f);
            Root.Add(scrollView);

            canvasRoot = new VisualElement();
            canvasRoot.style.position = Position.Relative;
            canvasRoot.style.minHeight = 120;
            scrollView.Add(canvasRoot);

            trackLayer = new VisualElement();
            trackLayer.style.position = Position.Absolute;
            trackLayer.style.left = 0;
            trackLayer.style.top = PerformanceDebugTimelineLayout.RulerHeight;
            trackLayer.style.right = 0;
            canvasRoot.Add(trackLayer);

            clipLayer = new VisualElement();
            clipLayer.style.position = Position.Absolute;
            clipLayer.style.left = 0;
            clipLayer.style.top = PerformanceDebugTimelineLayout.RulerHeight;
            clipLayer.style.right = 0;
            canvasRoot.Add(clipLayer);

            playhead = new VisualElement();
            playhead.style.position = Position.Absolute;
            playhead.style.width = 2;
            playhead.style.top = 0;
            playhead.style.bottom = 0;
            playhead.style.backgroundColor = PerformanceDebugWarmConsoleUi.Theme.AccentStrong;
            playhead.pickingMode = PickingMode.Ignore;
            canvasRoot.Add(playhead);

            return Root;
        }

        public bool TryDropModule(string moduleId, Vector2 worldPosition, PerformanceDebugPayload defaultPayload)
        {
            if (arrangement == null || canvasRoot == null || string.IsNullOrEmpty(moduleId))
            {
                return false;
            }

            Rect bounds = canvasRoot.worldBound;
            if (!bounds.Contains(worldPosition))
            {
                return false;
            }

            Vector2 local = canvasRoot.WorldToLocal(worldPosition);
            float time = SnapTime(Mathf.Max(0f, local.x / pixelsPerSecond));
            int track = Mathf.Clamp(
                Mathf.FloorToInt((local.y - PerformanceDebugTimelineLayout.RulerHeight) / PerformanceDebugTimelineLayout.RowHeight),
                0,
                arrangement.TrackCount - 1);

            IPerformanceDebugModule module = catalog.FindById(moduleId);
            PerformanceDebugPayload payload = defaultPayload?.Clone() ?? module?.Schema.CreateDefaultPayload();
            PerformanceDebugTimelineClip clip = arrangement.AddClip(moduleId, time, track, payload);
            selectedClipId = clip.ClipId;
            Rebuild();
            ClipSelected?.Invoke(selectedClipId);
            ArrangementChanged?.Invoke();
            return true;
        }

        private void Rebuild()
        {
            if (canvasRoot == null || arrangement == null)
            {
                return;
            }

            canvasRoot.Clear();
            trackLayer = new VisualElement();
            trackLayer.style.position = Position.Absolute;
            trackLayer.style.left = 0;
            trackLayer.style.top = PerformanceDebugTimelineLayout.RulerHeight;
            canvasRoot.Add(trackLayer);

            clipLayer = new VisualElement();
            clipLayer.style.position = Position.Absolute;
            clipLayer.style.left = 0;
            clipLayer.style.top = PerformanceDebugTimelineLayout.RulerHeight;
            canvasRoot.Add(clipLayer);

            playhead = new VisualElement();
            playhead.style.position = Position.Absolute;
            playhead.style.width = 2;
            playhead.style.backgroundColor = PerformanceDebugWarmConsoleUi.Theme.AccentStrong;
            playhead.pickingMode = PickingMode.Ignore;
            canvasRoot.Add(playhead);

            float duration = arrangement.GetDuration(catalog);
            float canvasWidth = Mathf.Max(600f, duration * pixelsPerSecond + 120f);
            float canvasHeight = PerformanceDebugTimelineLayout.RulerHeight
                                 + arrangement.TrackCount * PerformanceDebugTimelineLayout.RowHeight
                                 + 8f;

            canvasRoot.style.width = canvasWidth;
            canvasRoot.style.height = canvasHeight;
            trackLayer.style.width = canvasWidth;
            trackLayer.style.height = arrangement.TrackCount * PerformanceDebugTimelineLayout.RowHeight;
            clipLayer.style.width = canvasWidth;
            clipLayer.style.height = arrangement.TrackCount * PerformanceDebugTimelineLayout.RowHeight;

            BuildRuler(canvasWidth, duration);
            BuildTracks();
            BuildClips();
            UpdatePlayhead();
        }

        private void BuildRuler(float width, float duration)
        {
            var ruler = new VisualElement();
            ruler.style.position = Position.Absolute;
            ruler.style.left = 0;
            ruler.style.top = 0;
            ruler.style.width = width;
            ruler.style.height = PerformanceDebugTimelineLayout.RulerHeight;
            ruler.style.backgroundColor = new Color(0.11f, 0.09f, 0.07f);
            ruler.style.borderBottomWidth = 1;
            ruler.style.borderBottomColor = PerformanceDebugWarmConsoleUi.Theme.Divider;
            canvasRoot.Add(ruler);

            int majorStep = pixelsPerSecond >= 100f ? 1 : pixelsPerSecond >= 60f ? 2 : 5;
            int tickCount = Mathf.CeilToInt(duration) + 2;
            for (var sec = 0; sec <= tickCount; sec += majorStep)
            {
                float x = sec * pixelsPerSecond;
                var tick = new Label($"{sec}s");
                tick.style.position = Position.Absolute;
                tick.style.left = x + 4;
                tick.style.top = 6;
                tick.style.fontSize = 10;
                tick.style.color = PerformanceDebugWarmConsoleUi.Theme.TextTertiary;
                tick.pickingMode = PickingMode.Ignore;
                ruler.Add(tick);

                var line = new VisualElement();
                line.style.position = Position.Absolute;
                line.style.left = x;
                line.style.top = PerformanceDebugTimelineLayout.RulerHeight - 6;
                line.style.width = 1;
                line.style.height = 6;
                line.style.backgroundColor = PerformanceDebugWarmConsoleUi.Theme.Divider;
                line.pickingMode = PickingMode.Ignore;
                ruler.Add(line);
            }
        }

        private void BuildTracks()
        {
            for (var track = 0; track < arrangement.TrackCount; track++)
            {
                var row = new VisualElement();
                row.style.position = Position.Absolute;
                row.style.left = 0;
                row.style.top = track * PerformanceDebugTimelineLayout.RowHeight;
                row.style.right = 0;
                row.style.height = PerformanceDebugTimelineLayout.RowHeight;
                row.style.backgroundColor = track % 2 == 0
                    ? new Color(0.10f, 0.08f, 0.065f)
                    : new Color(0.115f, 0.09f, 0.075f);
                row.style.borderBottomWidth = 1;
                row.style.borderBottomColor = PerformanceDebugWarmConsoleUi.Theme.Divider;
                row.pickingMode = PickingMode.Ignore;
                trackLayer.Add(row);
            }
        }

        private void BuildClips()
        {
            for (var i = 0; i < arrangement.Clips.Count; i++)
            {
                PerformanceDebugTimelineClip clip = arrangement.Clips[i];
                IPerformanceDebugModule module = catalog.FindById(clip.ModuleId);
                if (module == null)
                {
                    continue;
                }

                float duration = Mathf.Max(0.3f, module.TryGetExpectedDuration(null));
                float width = Mathf.Max(PerformanceDebugTimelineLayout.MinClipWidth, duration * pixelsPerSecond);
                var element = CreateClipElement(clip, module, width);
                clipLayer.Add(element);
            }

            RefreshClipStyles();
        }

        private VisualElement CreateClipElement(PerformanceDebugTimelineClip clip, IPerformanceDebugModule module, float width)
        {
            var element = new VisualElement();
            element.userData = clip.ClipId;
            element.style.position = Position.Absolute;
            element.style.left = clip.StartTime * pixelsPerSecond;
            element.style.top = clip.Track * PerformanceDebugTimelineLayout.RowHeight + 4;
            element.style.width = width;
            element.style.height = PerformanceDebugTimelineLayout.RowHeight - 8;
            element.style.backgroundColor = new Color(0.20f, 0.16f, 0.12f);
            element.style.borderTopLeftRadius = element.style.borderTopRightRadius = 4;
            element.style.borderBottomLeftRadius = element.style.borderBottomRightRadius = 4;
            element.style.overflow = Overflow.Hidden;
            element.style.flexDirection = FlexDirection.Row;

            var stripe = new VisualElement();
            stripe.name = "clip-stripe";
            stripe.style.width = 4;
            stripe.style.backgroundColor = PerformanceDebugWarmConsoleUi.GetPoolAccent(module.Category);
            element.Add(stripe);

            var label = new Label(module.DisplayName);
            label.style.flexGrow = 1;
            label.style.paddingLeft = 6;
            label.style.paddingRight = 4;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            label.style.fontSize = 11;
            label.style.color = PerformanceDebugWarmConsoleUi.Theme.TextPrimary;
            label.style.overflow = Overflow.Hidden;
            label.style.textOverflow = TextOverflow.Ellipsis;
            element.Add(label);

            element.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0)
                {
                    return;
                }

                draggingClipId = clip.ClipId;
                dragStartMouse = evt.position;
                dragStartTime = clip.StartTime;
                dragStartTrack = clip.Track;
                selectedClipId = clip.ClipId;
                RefreshClipStyles();
                ClipSelected?.Invoke(selectedClipId);
                element.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            });

            element.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (draggingClipId != clip.ClipId || !element.HasPointerCapture(evt.pointerId))
                {
                    return;
                }

                snapEnabled = !evt.ctrlKey;
                Vector2 delta = (Vector2)evt.position - dragStartMouse;
                float newTime = SnapTime(Mathf.Max(0f, dragStartTime + delta.x / pixelsPerSecond));
                int newTrack = Mathf.Clamp(
                    dragStartTrack + Mathf.RoundToInt(delta.y / PerformanceDebugTimelineLayout.RowHeight),
                    0,
                    arrangement.TrackCount - 1);

                clip.StartTime = newTime;
                clip.Track = newTrack;
                element.style.left = newTime * pixelsPerSecond;
                element.style.top = newTrack * PerformanceDebugTimelineLayout.RowHeight + 4;
                evt.StopPropagation();
            });

            element.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (draggingClipId != clip.ClipId)
                {
                    return;
                }

                if (element.HasPointerCapture(evt.pointerId))
                {
                    element.ReleasePointer(evt.pointerId);
                }

                draggingClipId = null;
                ArrangementChanged?.Invoke();
                evt.StopPropagation();
            });

            return element;
        }

        private void RefreshClipStyles()
        {
            if (clipLayer == null)
            {
                return;
            }

            clipLayer.Query<VisualElement>().Where(e => e.userData is string).ForEach(element =>
            {
                bool selected = (string)element.userData == selectedClipId;
                element.style.borderTopWidth = element.style.borderBottomWidth =
                    element.style.borderLeftWidth = element.style.borderRightWidth = selected ? 2 : 0;
                element.style.borderTopColor = element.style.borderBottomColor =
                    element.style.borderLeftColor = element.style.borderRightColor =
                        PerformanceDebugWarmConsoleUi.Theme.AccentGoldValue;
            });
        }

        private void UpdatePlayhead()
        {
            if (playhead == null)
            {
                return;
            }

            playhead.style.left = playheadTime * pixelsPerSecond;
            playhead.style.top = 0;
            playhead.style.height = canvasRoot.resolvedStyle.height;
        }

        private float SnapTime(float time)
        {
            if (!snapEnabled)
            {
                return time;
            }

            return Mathf.Round(time / PerformanceDebugTimelineLayout.SnapInterval)
                   * PerformanceDebugTimelineLayout.SnapInterval;
        }
    }
}
#endif
