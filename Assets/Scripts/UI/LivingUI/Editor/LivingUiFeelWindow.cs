using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NineGrid.LivingUI.Editor
{
    /// <summary>
    /// 灵动 UI · 动效手感调试窗口。
    /// 集中暴露转场速度、卡农、缓动等可调参数，支持从基础缓动函数选或手写 AnimationCurve。
    /// </summary>
    public sealed class LivingUiFeelWindow : EditorWindow
    {
        private const string MenuPath = "NineGrid/Living UI/动效手感调试";

        private Unity.LivingUiDirector _director;
        private SerializedObject _directorSo;
        private SerializedProperty _playbackSpeedProp;
        private SerializedProperty _styleProp;
        private Vector2 _scroll;

        [MenuItem(MenuPath)]
        public static void Open()
        {
            var window = GetWindow<LivingUiFeelWindow>();
            window.titleContent = new GUIContent("动效手感");
            window.minSize = new Vector2(360f, 480f);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.update += RepaintOnPlay;
        }

        private void OnDisable()
        {
            EditorApplication.update -= RepaintOnPlay;
        }

        private void RepaintOnPlay()
        {
            if (Application.isPlaying) Repaint();
        }

        private void OnGUI()
        {
            TryResolveDirector();

            if (_director == null)
            {
                EditorGUILayout.HelpBox(
                    "当前场景中没有 LivingUiDirector。请打开 UITestSence 或包含该组件的场景。",
                    MessageType.Info);
                return;
            }

            _directorSo.Update();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawGlobalSpeed();
            EditorGUILayout.Space(8f);
            DrawTimingParams();
            EditorGUILayout.Space(8f);
            DrawSizeBounds();
            EditorGUILayout.Space(8f);
            DrawEasing();

            EditorGUILayout.EndScrollView();
            _directorSo.ApplyModifiedProperties();
        }

        private void TryResolveDirector()
        {
            if (_director != null) return;

            _director = FindAnyObjectByType<Unity.LivingUiDirector>();
            if (_director == null) return;

            _directorSo = new SerializedObject(_director);
            _playbackSpeedProp = _directorSo.FindProperty("playbackSpeed");
            _styleProp = _directorSo.FindProperty("transitionStyle");
        }

        // ── 全局速度 ──────────────────────────────

        private void DrawGlobalSpeed()
        {
            EditorGUILayout.LabelField("全局速度", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_playbackSpeedProp, new GUIContent("播放速度倍率",
                "全局时间缩放；1=正常速度，0.5=慢放，2=快放。运行中可实时调整。"));
            EditorGUILayout.LabelField("说明", "所有面板运动统一乘以此倍率推进，不影响卡农比例与曲线形状。",
                EditorStyles.wordWrappedMiniLabel);
        }

        // ── 时长与卡农 ──────────────────────────

        private void DrawTimingParams()
        {
            EditorGUILayout.LabelField("时长与卡农", EditorStyles.boldLabel);

            StyleField("BaseDuration", "基础时长（秒）",
                "每条面板的保底运动时长，与距离增量叠加。");
            StyleField("DistanceSecondsPerUnit", "距离增量（秒/单位）",
                "面板源→目标位移每 1 世界单位增加此时长。");
            StyleField("MaximumDistanceAddition", "距离增量上限（秒）",
                "距离增量封顶值，防止远程面板时长过长。");
            StyleField("CanonSpan", "卡农跨度（秒）",
                "面板依舞台水平位置从左到右渐进出发的最大时间差。0=同时出发。");
            StyleField("ExpelledLead", "出画领先量（秒）",
                "离开舞台的面板获得的额外提前量，保证出画不被场内面板遮挡。");
            StyleField("EnteringDelay", "入画延迟（秒）",
                "新入场面板在出画波次之后额外等待的时间。");
        }

        // ── 尺寸边界 ──────────────────────────────

        private void DrawSizeBounds()
        {
            EditorGUILayout.LabelField("尺寸边界", EditorStyles.boldLabel);

            StyleField("FlowWidthFloor", "宽度下限（世界单位）",
                "尺寸运动过程中宽度不应低于此值，防止面板坍缩。");
            StyleField("FlowHeightFloor", "高度下限（世界单位）",
                "尺寸运动过程中高度不应低于此值。");
            StyleField("SizeCeilingMultiplier", "尺寸上限倍率",
                "瞬态尺寸 ≤ max(起点尺寸, 终点尺寸) × 此倍率。");
        }

        // ── 缓动 ──────────────────────────────

        private void DrawEasing()
        {
            EditorGUILayout.LabelField("缓动", EditorStyles.boldLabel);

            var easingProp = _styleProp.FindPropertyRelative("EasingType");
            EditorGUILayout.PropertyField(easingProp, new GUIContent("缓动类型",
                "位置和尺寸运动的缓动函数。Custom 时使用下方的自定义曲线。"));

            var currentType = (LivingUiEasingType)easingProp.enumValueIndex;
            if (currentType == LivingUiEasingType.Custom)
            {
                var curveProp = _styleProp.FindPropertyRelative("CustomEasingCurve");
                EditorGUILayout.PropertyField(curveProp, new GUIContent("自定义缓动曲线",
                    "横轴 t: 0→1，纵轴 value: 0→1。右键曲线可选用 Unity 内置预设。"));
            }

            EditorGUILayout.LabelField("说明",
                "缓动仅对从静止出发的面板生效（初始速度≈0）；转场中途被打断重定向时，自动回退到五次多项式以保证 C1 连续。",
                EditorStyles.wordWrappedMiniLabel);
        }

        // ── 辅助 ──────────────────────────────

        private void StyleField(string propertyName, string label, string tooltip)
        {
            var prop = _styleProp.FindPropertyRelative(propertyName);
            if (prop != null)
                EditorGUILayout.PropertyField(prop, new GUIContent(label, tooltip));
            else
                EditorGUILayout.HelpBox($"序列化字段 '{propertyName}' 未找到。", MessageType.Warning);
        }

        private void OnInspectorUpdate()
        {
            Repaint();
        }
    }
}
