using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NineGrid.Flow.Editor.LivingUi
{
    /// <summary>
    /// 灵动 UI · 大盘构型前置布局工具。
    /// 对已切片 SpriteRenderer 做锁定 / 理想间隔 / 贪心撑体自动布局，并在 Scene 绘制舞台底板。
    /// </summary>
    public sealed class LivingUiGrandLayoutWindow : EditorWindow
    {
        private const string MenuPath = "NineGrid/Living UI/大盘构型布局工具";

        private LivingUiLayoutMode _mode = LivingUiLayoutMode.InflateInPlace;
        private float _idealGap = 0.25f;
        private float _stagePadding = 0.125f;
        private float _lockClearance;
        private float _flowFloor = 0.5f;
        private bool _preserveAspect;
        private bool _pixelSnap = true;
        private float _pixelsPerUnit = 32f;
        private int _inflatePasses = 256;
        private float _inflateStep = 0.0625f;
        private bool _includeLockedAsObstacles = true;
        private Vector2 _scroll;
        private string _status = "选中已切片载体 → 锁定锚点 → 运行自动布局 → 再手调。";

        [MenuItem(MenuPath)]
        public static void Open()
        {
            var window = GetWindow<LivingUiGrandLayoutWindow>();
            window.titleContent = new GUIContent("大盘构型布局");
            window.minSize = new Vector2(360f, 520f);
            window.Show();
            LivingUiGrandLayoutSession.EnsureHooked();
        }

        private void OnEnable()
        {
            LivingUiGrandLayoutSession.EnsureHooked();
            Selection.selectionChanged += Repaint;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= Repaint;
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawHeader();
            DrawStageSection();
            DrawSelectionSection();
            DrawLockSection();
            DrawLayoutParams();
            DrawActions();
            DrawHelp();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("灵动 UI · 大盘构型前置布局", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "工作流：框选已切片载体 → 锁定已定稿块 → 调理想间隔 → 自动布局撑体 → Scene 手调精修。",
                MessageType.Info);
            EditorGUILayout.Space(4);
        }

        private void DrawStageSection()
        {
            EditorGUILayout.LabelField("舞台底板", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                var draw = EditorGUILayout.Toggle(
                    new GUIContent("Scene 绘制舞台", "在 Scene 窗口绘制底板矩形与锁定/选中描边"),
                    LivingUiGrandLayoutSession.DrawOverlay);
                if (draw != LivingUiGrandLayoutSession.DrawOverlay)
                {
                    LivingUiGrandLayoutSession.DrawOverlay = draw;
                    LivingUiGrandLayoutSession.SavePrefs();
                    SceneView.RepaintAll();
                }

                var stage = LivingUiGrandLayoutSession.StageBounds;
                EditorGUI.BeginChangeCheck();
                var center = EditorGUILayout.Vector2Field("中心", stage.center);
                var size = EditorGUILayout.Vector2Field("尺寸", new Vector2(stage.width, stage.height));
                if (EditorGUI.EndChangeCheck())
                {
                    size.x = Mathf.Max(0.5f, size.x);
                    size.y = Mathf.Max(0.5f, size.y);
                    LivingUiGrandLayoutSession.StageBounds = new Rect(
                        center.x - size.x * 0.5f,
                        center.y - size.y * 0.5f,
                        size.x,
                        size.y);
                    LivingUiGrandLayoutSession.SavePrefs();
                    SceneView.RepaintAll();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("贴合主相机"))
                    {
                        LivingUiGrandLayoutSession.FitStageToMainCamera();
                        _status = $"舞台已贴合相机：{FormatRect(LivingUiGrandLayoutSession.StageBounds)}";
                    }

                    if (GUILayout.Button("聚焦舞台"))
                    {
                        FrameStage();
                    }
                }
            }

            EditorGUILayout.Space(6);
        }

        private void DrawSelectionSection()
        {
            EditorGUILayout.LabelField("选择", EditorStyles.boldLabel);
            var sliced = LivingUiSlicedRectUtil.CollectSlicedFromSelection(Selection.gameObjects);
            EditorGUILayout.LabelField($"当前已切片载体：{sliced.Count}");
            using (new EditorGUI.IndentLevelScope())
            {
                foreach (var sr in sliced)
                {
                    var locked = LivingUiGrandLayoutSession.IsLocked(sr);
                    var rect = LivingUiSlicedRectUtil.GetWorldRect(sr);
                    EditorGUILayout.LabelField(
                        $"{(locked ? "[L] " : "    ")}{sr.gameObject.name}  {rect.width:0.##}×{rect.height:0.##} @ ({rect.center.x:0.##},{rect.center.y:0.##})");
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("选中当前大盘下全部切片"))
                {
                    SelectAllSlicedUnderActiveGrandLayout();
                }

                if (GUILayout.Button("仅保留切片选中"))
                {
                    NarrowSelectionToSliced();
                }
            }

            EditorGUILayout.Space(6);
        }

        private void DrawLockSection()
        {
            EditorGUILayout.LabelField("锁定", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"已锁定：{LivingUiGrandLayoutSession.LockedCount}");
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("锁定选中"))
                {
                    LockSelection(true);
                }

                if (GUILayout.Button("解锁选中"))
                {
                    LockSelection(false);
                }

                if (GUILayout.Button("切换锁定"))
                {
                    ToggleLockSelection();
                }
            }

            using (new EditorGUI.DisabledScope(LivingUiGrandLayoutSession.LockedCount == 0))
            {
                if (GUILayout.Button("一键清除全部锁定", GUILayout.Height(28)))
                {
                    LivingUiGrandLayoutSession.ClearAllLocks();
                    _status = "已清除全部锁定。";
                }
            }

            EditorGUILayout.Space(6);
        }

        private void DrawLayoutParams()
        {
            EditorGUILayout.LabelField("自动布局参数", EditorStyles.boldLabel);
            _mode = (LivingUiLayoutMode)EditorGUILayout.EnumPopup(
                new GUIContent("模式", "就地膨胀=粗摆后撑满；剩余装填=绕开锁定重新占位；均分网格=无锁时均匀铺开"),
                _mode);
            _idealGap = EditorGUILayout.Slider(
                new GUIContent("理想间隔", "仅约束未锁定↔未锁定；不对锁定对象生效"),
                _idealGap, 0f, 2f);
            _stagePadding = EditorGUILayout.Slider(
                new GUIContent("舞台内边距", "全体未锁定载体相对舞台边缘的留白"),
                _stagePadding, 0f, 2f);
            _lockClearance = EditorGUILayout.Slider(
                new GUIContent("锁定净空", "未锁定贴近锁定时的额外间隙，默认 0（间隔不对锁定生效）"),
                _lockClearance, 0f, 2f);
            _flowFloor = EditorGUILayout.Slider(
                new GUIContent("流动下限", "载体最小宽高，对应 spec 流动下限，防止缩没"),
                _flowFloor, 0.0625f, 4f);
            _preserveAspect = EditorGUILayout.Toggle(
                new GUIContent("保持宽高比", "膨胀/装填时维持当前比例"),
                _preserveAspect);
            _pixelSnap = EditorGUILayout.Toggle(
                new GUIContent("像素对齐", "按 PPU 吸附，默认 32（与 PixelPerfectCamera 一致）"),
                _pixelSnap);
            using (new EditorGUI.DisabledScope(!_pixelSnap))
            {
                _pixelsPerUnit = EditorGUILayout.FloatField("Pixels Per Unit", _pixelsPerUnit);
            }

            _includeLockedAsObstacles = EditorGUILayout.Toggle(
                new GUIContent("锁定作为障碍", "关闭则布局时忽略已锁定载体的占位（一般保持开启）"),
                _includeLockedAsObstacles);

            if (_mode == LivingUiLayoutMode.InflateInPlace || _mode == LivingUiLayoutMode.PackFreeSpace)
            {
                _inflatePasses = EditorGUILayout.IntSlider("膨胀轮次", _inflatePasses, 8, 1024);
                _inflateStep = EditorGUILayout.Slider("膨胀步长", _inflateStep, 0.03125f, 0.5f);
            }

            EditorGUILayout.Space(6);
        }

        private void DrawActions()
        {
            EditorGUILayout.LabelField("操作", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(
                       LivingUiSlicedRectUtil.CollectSlicedFromSelection(Selection.gameObjects).Count == 0))
            {
                if (GUILayout.Button("运行自动布局", GUILayout.Height(36)))
                {
                    RunLayout();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("像素对齐选中"))
                {
                    SnapSelection();
                }

                if (GUILayout.Button("启用选中渲染"))
                {
                    SetSelectionRendererEnabled(true);
                }

                if (GUILayout.Button("禁用选中渲染"))
                {
                    SetSelectionRendererEnabled(false);
                }
            }

            EditorGUILayout.HelpBox(_status, MessageType.None);
        }

        private void DrawHelp()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("交互说明", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "• 青色矩形 = 舞台底板（可拖四角/中心）\n" +
                "• 红色 LOCK = 已锁定，自动布局不移动不缩放\n" +
                "• 绿色 = 当前选中且未锁定\n" +
                "• 理想间隔只作用于未锁定对；锁定净空默认 0\n" +
                "• 建议：先粗放位置 → 锁定关键块 → 就地膨胀 → 手调\n" +
                "• 首次占位可用「均分网格」或「剩余空间装填」",
                MessageType.None);
        }

        private void RunLayout()
        {
            var sliced = LivingUiSlicedRectUtil.CollectSlicedFromSelection(Selection.gameObjects);
            if (sliced.Count == 0)
            {
                _status = "没有可选的已切片载体。";
                return;
            }

            var input = new List<LivingUiLayoutItem>(sliced.Count);
            var map = new Dictionary<int, SpriteRenderer>(sliced.Count);
            for (var i = 0; i < sliced.Count; i++)
            {
                var sr = sliced[i];
                var id = sr.GetInstanceID();
                map[id] = sr;
                var rect = LivingUiSlicedRectUtil.GetWorldRect(sr);
                var locked = LivingUiGrandLayoutSession.IsLocked(sr);
                input.Add(new LivingUiLayoutItem
                {
                    Id = id,
                    Rect = rect,
                    Locked = locked,
                    Aspect = rect.height > 0.0001f ? rect.width / rect.height : 1f,
                });
            }

            // 把「选区外但已锁定」的载体也纳入障碍，避免撞到已定稿块
            if (_includeLockedAsObstacles)
            {
                foreach (var sr in LivingUiGrandLayoutSession.EnumerateLockedRenderers())
                {
                    var id = sr.GetInstanceID();
                    if (map.ContainsKey(id))
                    {
                        continue;
                    }

                    if (!LivingUiSlicedRectUtil.IsSlicedCarrier(sr))
                    {
                        continue;
                    }

                    var rect = LivingUiSlicedRectUtil.GetWorldRect(sr);
                    input.Add(new LivingUiLayoutItem
                    {
                        Id = id,
                        Rect = rect,
                        Locked = true,
                        Aspect = rect.height > 0.0001f ? rect.width / rect.height : 1f,
                    });
                    map[id] = sr;
                }
            }

            var settings = new LivingUiLayoutSettings
            {
                Stage = LivingUiGrandLayoutSession.StageBounds,
                IdealGap = _idealGap,
                StagePadding = _stagePadding,
                LockClearance = _lockClearance,
                FlowFloor = _flowFloor,
                PreserveAspect = _preserveAspect,
                PixelSnap = _pixelSnap,
                PixelsPerUnit = Mathf.Max(1f, _pixelsPerUnit),
                Mode = _mode,
                InflatePasses = _inflatePasses,
                InflateStep = _inflateStep,
            };

            var result = LivingUiStageLayoutSolver.Solve(input, settings);

            Undo.SetCurrentGroupName("Living UI 自动布局");
            var group = Undo.GetCurrentGroup();
            var moved = 0;
            foreach (var item in result)
            {
                if (item.Locked || !map.TryGetValue(item.Id, out var sr))
                {
                    continue;
                }

                // 只写回「当前选中」集合里的未锁定项
                if (!IsInSelection(sr))
                {
                    continue;
                }

                Undo.RecordObject(sr.transform, "Living UI Layout Transform");
                Undo.RecordObject(sr, "Living UI Layout Size");
                LivingUiSlicedRectUtil.SetWorldRect(sr, item.Rect, _pixelSnap, _pixelsPerUnit);
                EditorUtility.SetDirty(sr);
                EditorUtility.SetDirty(sr.transform);
                moved++;
            }

            Undo.CollapseUndoOperations(group);
            _status = $"布局完成：模式={_mode}，处理未锁定 {moved} 个，间隔={_idealGap:0.###}，锁定障碍={LivingUiGrandLayoutSession.LockedCount}";
            SceneView.RepaintAll();
        }

        private static bool IsInSelection(SpriteRenderer sr)
        {
            foreach (var go in Selection.gameObjects)
            {
                if (go == sr.gameObject)
                {
                    return true;
                }

                if (go.GetComponentInChildren<SpriteRenderer>(true) == sr)
                {
                    return true;
                }

                foreach (var child in go.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (child == sr)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void SnapSelection()
        {
            var sliced = LivingUiSlicedRectUtil.CollectSlicedFromSelection(Selection.gameObjects);
            Undo.SetCurrentGroupName("Living UI 像素对齐");
            var group = Undo.GetCurrentGroup();
            foreach (var sr in sliced)
            {
                if (LivingUiGrandLayoutSession.IsLocked(sr))
                {
                    continue;
                }

                Undo.RecordObject(sr.transform, "Snap");
                Undo.RecordObject(sr, "Snap");
                var rect = LivingUiSlicedRectUtil.GetWorldRect(sr);
                LivingUiSlicedRectUtil.SetWorldRect(sr, rect, true, Mathf.Max(1f, _pixelsPerUnit));
                EditorUtility.SetDirty(sr);
            }

            Undo.CollapseUndoOperations(group);
            _status = $"已像素对齐 {sliced.Count} 个未锁定载体。";
        }

        private void SetSelectionRendererEnabled(bool enabled)
        {
            var sliced = LivingUiSlicedRectUtil.CollectSlicedFromSelection(Selection.gameObjects);
            foreach (var sr in sliced)
            {
                Undo.RecordObject(sr, enabled ? "Enable Renderer" : "Disable Renderer");
                sr.enabled = enabled;
                EditorUtility.SetDirty(sr);
            }

            _status = enabled ? $"已启用 {sliced.Count} 个渲染。" : $"已禁用 {sliced.Count} 个渲染。";
        }

        private void LockSelection(bool lockThem)
        {
            var sliced = LivingUiSlicedRectUtil.CollectSlicedFromSelection(Selection.gameObjects);
            if (lockThem)
            {
                LivingUiGrandLayoutSession.LockMany(sliced);
                _status = $"已锁定 {sliced.Count} 个。";
            }
            else
            {
                foreach (var sr in sliced)
                {
                    LivingUiGrandLayoutSession.Unlock(sr);
                }

                _status = $"已解锁 {sliced.Count} 个。";
            }
        }

        private void ToggleLockSelection()
        {
            var sliced = LivingUiSlicedRectUtil.CollectSlicedFromSelection(Selection.gameObjects);
            foreach (var sr in sliced)
            {
                LivingUiGrandLayoutSession.ToggleLock(sr);
            }

            _status = $"已切换 {sliced.Count} 个锁定状态。";
        }

        private void SelectAllSlicedUnderActiveGrandLayout()
        {
            Transform root = null;
            foreach (var go in Selection.gameObjects)
            {
                var t = go.transform;
                while (t != null)
                {
                    if (t.name.StartsWith("大盘构型"))
                    {
                        root = t;
                        break;
                    }

                    t = t.parent;
                }

                if (root != null)
                {
                    break;
                }
            }

            if (root == null)
            {
                foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
                {
                    if (t.parent == null && t.name.StartsWith("大盘构型") && t.gameObject.activeInHierarchy)
                    {
                        root = t;
                        break;
                    }
                }
            }

            if (root == null)
            {
                _status = "未找到「大盘构型*」根节点。请先点选某个大盘或其子物体。";
                return;
            }

            var list = new List<Object>();
            foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (LivingUiSlicedRectUtil.IsSlicedCarrier(sr))
                {
                    list.Add(sr.gameObject);
                }
            }

            Selection.objects = list.ToArray();
            _status = $"已选中「{root.name}」下 {list.Count} 个已切片载体。";
        }

        private void NarrowSelectionToSliced()
        {
            var sliced = LivingUiSlicedRectUtil.CollectSlicedFromSelection(Selection.gameObjects);
            var objects = new Object[sliced.Count];
            for (var i = 0; i < sliced.Count; i++)
            {
                objects[i] = sliced[i].gameObject;
            }

            Selection.objects = objects;
            _status = $"选中已收窄为 {objects.Length} 个已切片载体。";
        }

        private static void FrameStage()
        {
            var s = LivingUiGrandLayoutSession.StageBounds;
            var bounds = new Bounds(new Vector3(s.center.x, s.center.y, 0f), new Vector3(s.width, s.height, 1f));
            SceneView.lastActiveSceneView?.Frame(bounds, false);
        }

        private static string FormatRect(Rect r) =>
            $"{r.width:0.###}×{r.height:0.###} @ ({r.center.x:0.###},{r.center.y:0.###})";
    }
}
