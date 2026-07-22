using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NineGrid.Presentation.Editor.LivingUi
{
    /// <summary>
    /// 大盘构型前置布局会话：锁定集合、舞台矩形、Scene 叠画与手柄。
    /// </summary>
    [InitializeOnLoad]
    internal static class LivingUiGrandLayoutSession
    {
        private const string PrefStageX = "NineGrid.LivingUi.Stage.X";
        private const string PrefStageY = "NineGrid.LivingUi.Stage.Y";
        private const string PrefStageW = "NineGrid.LivingUi.Stage.W";
        private const string PrefStageH = "NineGrid.LivingUi.Stage.H";
        private const string PrefDraw = "NineGrid.LivingUi.DrawOverlay";
        private const string PrefLocks = "NineGrid.LivingUi.Locks";

        private static readonly HashSet<string> LockedGlobalIds = new();
        private static bool _hooked;
        private static Vector2 _centerDragStart;
        private static Rect _stageAtCenterDragStart;
        private static bool _centerDragging;

        public static Rect StageBounds { get; set; } = new Rect(-7.5f, -4.21875f, 15f, 8.4375f);
        public static bool DrawOverlay { get; set; } = true;
        public static bool ShowLockedOnlyInOverlay { get; set; }

        static LivingUiGrandLayoutSession()
        {
            LoadPrefs();
            EnsureHooked();
        }

        public static void EnsureHooked()
        {
            if (_hooked)
            {
                return;
            }

            SceneView.duringSceneGui += OnSceneGui;
            _hooked = true;
        }

        public static int LockedCount => LockedGlobalIds.Count;

        public static bool IsLocked(Object obj)
        {
            if (obj == null)
            {
                return false;
            }

            return LockedGlobalIds.Contains(ToGlobalId(obj));
        }

        public static void Lock(Object obj)
        {
            if (obj == null)
            {
                return;
            }

            LockedGlobalIds.Add(ToGlobalId(obj));
            PersistLocks();
            SceneView.RepaintAll();
        }

        public static void Unlock(Object obj)
        {
            if (obj == null)
            {
                return;
            }

            LockedGlobalIds.Remove(ToGlobalId(obj));
            PersistLocks();
            SceneView.RepaintAll();
        }

        public static void ToggleLock(Object obj)
        {
            if (obj == null)
            {
                return;
            }

            var id = ToGlobalId(obj);
            if (!LockedGlobalIds.Add(id))
            {
                LockedGlobalIds.Remove(id);
            }

            PersistLocks();
            SceneView.RepaintAll();
        }

        public static void LockMany(IEnumerable<Object> objects)
        {
            foreach (var obj in objects)
            {
                if (obj != null)
                {
                    LockedGlobalIds.Add(ToGlobalId(obj));
                }
            }

            PersistLocks();
            SceneView.RepaintAll();
        }

        public static void ClearAllLocks()
        {
            LockedGlobalIds.Clear();
            PersistLocks();
            SceneView.RepaintAll();
        }

        public static IEnumerable<SpriteRenderer> EnumerateLockedRenderers()
        {
            foreach (var id in LockedGlobalIds)
            {
                if (TryResolve(id, out var sr))
                {
                    yield return sr;
                }
            }
        }

        public static void PruneDeadLocks()
        {
            var removed = LockedGlobalIds.RemoveWhere(id => !TryResolve(id, out _));
            if (removed > 0)
            {
                PersistLocks();
            }
        }

        public static void FitStageToMainCamera()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                camera = Object.FindFirstObjectByType<Camera>();
            }

            StageBounds = LivingUiSlicedRectUtil.OrthographicCameraWorldRect(camera);
            SavePrefs();
            SceneView.RepaintAll();
        }

        public static void SavePrefs()
        {
            var s = StageBounds;
            EditorPrefs.SetFloat(PrefStageX, s.x);
            EditorPrefs.SetFloat(PrefStageY, s.y);
            EditorPrefs.SetFloat(PrefStageW, s.width);
            EditorPrefs.SetFloat(PrefStageH, s.height);
            EditorPrefs.SetBool(PrefDraw, DrawOverlay);
            PersistLocks();
        }

        private static void PersistLocks()
        {
            EditorPrefs.SetString(PrefLocks, string.Join("\n", LockedGlobalIds));
        }

        private static void LoadPrefs()
        {
            if (EditorPrefs.HasKey(PrefStageW))
            {
                StageBounds = new Rect(
                    EditorPrefs.GetFloat(PrefStageX, StageBounds.x),
                    EditorPrefs.GetFloat(PrefStageY, StageBounds.y),
                    EditorPrefs.GetFloat(PrefStageW, StageBounds.width),
                    EditorPrefs.GetFloat(PrefStageH, StageBounds.height));
                DrawOverlay = EditorPrefs.GetBool(PrefDraw, true);
            }

            LockedGlobalIds.Clear();
            var raw = EditorPrefs.GetString(PrefLocks, string.Empty);
            if (string.IsNullOrEmpty(raw))
            {
                return;
            }

            foreach (var line in raw.Split('\n'))
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    LockedGlobalIds.Add(line.Trim());
                }
            }
        }

        private static string ToGlobalId(Object obj) =>
            GlobalObjectId.GetGlobalObjectIdSlow(obj).ToString();

        private static bool TryResolve(string globalId, out SpriteRenderer renderer)
        {
            renderer = null;
            if (!GlobalObjectId.TryParse(globalId, out var gid))
            {
                return false;
            }

            var obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid);
            renderer = obj as SpriteRenderer;
            if (renderer == null && obj is GameObject go)
            {
                renderer = go.GetComponent<SpriteRenderer>();
            }

            return renderer != null;
        }

        private static void OnSceneGui(SceneView sceneView)
        {
            if (!DrawOverlay)
            {
                return;
            }

            PruneDeadLocks();
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
            DrawStage();
            DrawCarriers();
            HandleStageDrag();
        }

        private static void DrawStage()
        {
            var s = StageBounds;
            var corners = new Vector3[]
            {
                new(s.xMin, s.yMin, 0f),
                new(s.xMax, s.yMin, 0f),
                new(s.xMax, s.yMax, 0f),
                new(s.xMin, s.yMax, 0f),
            };

            Handles.DrawSolidRectangleWithOutline(
                corners,
                new Color(0.15f, 0.75f, 1f, 0.05f),
                new Color(0.2f, 0.85f, 1f, 0.95f));

            Handles.color = new Color(0.2f, 0.85f, 1f, 0.95f);
            Handles.Label(
                new Vector3(s.xMin, s.yMax + 0.15f, 0f),
                $"舞台 {s.width:0.###} × {s.height:0.###}");
        }

        private static void DrawCarriers()
        {
            foreach (var sr in EnumerateLockedRenderers())
            {
                DrawRectOutline(LivingUiSlicedRectUtil.GetWorldRect(sr), new Color(1f, 0.35f, 0.25f, 0.95f), locked: true);
            }

            if (ShowLockedOnlyInOverlay)
            {
                return;
            }

            foreach (var go in Selection.gameObjects)
            {
                if (!LivingUiSlicedRectUtil.IsSlicedCarrier(go, out var sr))
                {
                    continue;
                }

                if (IsLocked(sr))
                {
                    continue;
                }

                DrawRectOutline(LivingUiSlicedRectUtil.GetWorldRect(sr), new Color(0.35f, 1f, 0.45f, 0.95f), locked: false);
            }
        }

        private static void DrawRectOutline(Rect rect, Color color, bool locked)
        {
            var corners = new Vector3[]
            {
                new(rect.xMin, rect.yMin, 0f),
                new(rect.xMax, rect.yMin, 0f),
                new(rect.xMax, rect.yMax, 0f),
                new(rect.xMin, rect.yMax, 0f),
            };

            Handles.DrawSolidRectangleWithOutline(
                corners,
                new Color(color.r, color.g, color.b, 0.04f),
                color);

            if (locked)
            {
                Handles.color = color;
                Handles.Label(new Vector3(rect.xMin, rect.yMax, 0f), "LOCK");
            }
        }

        private static void HandleStageDrag()
        {
            var s = StageBounds;
            var bl = new Vector3(s.xMin, s.yMin, 0f);
            var br = new Vector3(s.xMax, s.yMin, 0f);
            var tr = new Vector3(s.xMax, s.yMax, 0f);
            var tl = new Vector3(s.xMin, s.yMax, 0f);
            var center = new Vector3(s.center.x, s.center.y, 0f);

            EditorGUI.BeginChangeCheck();
            bl = CornerHandle(bl);
            br = CornerHandle(br);
            tr = CornerHandle(tr);
            tl = CornerHandle(tl);

            var centerSize = HandleUtility.GetHandleSize(center) * 0.1f;
            var newCenter = Handles.FreeMoveHandle(center, centerSize, Vector3.zero, Handles.CircleHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                if ((new Vector2(newCenter.x, newCenter.y) - s.center).sqrMagnitude > 0.00001f
                    && Mathf.Abs(bl.x - s.xMin) < 0.0001f
                    && Mathf.Abs(br.x - s.xMax) < 0.0001f)
                {
                    if (!_centerDragging)
                    {
                        _centerDragging = true;
                        _centerDragStart = s.center;
                        _stageAtCenterDragStart = s;
                    }

                    var delta = new Vector2(newCenter.x, newCenter.y) - _centerDragStart;
                    StageBounds = new Rect(
                        _stageAtCenterDragStart.x + delta.x,
                        _stageAtCenterDragStart.y + delta.y,
                        _stageAtCenterDragStart.width,
                        _stageAtCenterDragStart.height);
                }
                else
                {
                    _centerDragging = false;
                    var minX = Mathf.Min(bl.x, br.x, tr.x, tl.x);
                    var maxX = Mathf.Max(bl.x, br.x, tr.x, tl.x);
                    var minY = Mathf.Min(bl.y, br.y, tr.y, tl.y);
                    var maxY = Mathf.Max(bl.y, br.y, tr.y, tl.y);
                    StageBounds = Rect.MinMaxRect(
                        minX,
                        minY,
                        Mathf.Max(minX + 0.5f, maxX),
                        Mathf.Max(minY + 0.5f, maxY));
                }

                SavePrefs();
            }
            else if (Event.current.type == EventType.MouseUp)
            {
                _centerDragging = false;
            }
        }

        private static Vector3 CornerHandle(Vector3 point)
        {
            var size = HandleUtility.GetHandleSize(point) * 0.08f;
            return Handles.FreeMoveHandle(point, size, Vector3.zero, Handles.RectangleHandleCap);
        }
    }
}
