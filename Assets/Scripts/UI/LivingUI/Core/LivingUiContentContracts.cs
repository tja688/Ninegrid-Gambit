using System;
using UnityEngine;

namespace NineGrid.LivingUI
{
    /// <summary>A 类内容对载体采样位姿的跟随策略。</summary>
    public enum LivingUiContentFollowPolicy
    {
        /// <summary>纯位置随行（场景 a）；挂载体子树即可，不随 9-slice size 缩放。</summary>
        RigidTravel = 0,

        /// <summary>随载体 size 依边界错峰缩放进/退场（场景 b）。</summary>
        BoundaryReactive = 1,

        /// <summary>贴一条边按比例位移、不缩放（场景 c）。</summary>
        PartialFollow = 2,
    }

    /// <summary>内容显隐 / 换文案策略。</summary>
    public enum LivingUiContentVisibilityPolicy
    {
        /// <summary>只要 Face 匹配就显示。</summary>
        AlwaysVisible = 0,

        /// <summary>转场播放期间隐藏，停稳后按 Face 再显。</summary>
        HideDuringTransit = 1,

        /// <summary>到达目标 Face 时切换文案（M1 预留，主菜单切片未用）。</summary>
        SwapOnFace = 2,
    }

    /// <summary>PartialFollow 贴边方向（相对载体中心轴对齐边）。</summary>
    public enum LivingUiPartialFollowEdge
    {
        Left = 0,
        Right = 1,
        Bottom = 2,
        Top = 3,
    }

    /// <summary>内容相对载体中心的局部位姿。</summary>
    public readonly struct LivingUiContentLocalPose
    {
        public LivingUiContentLocalPose(Vector3 localPosition, Vector3 localScale)
        {
            LocalPosition = localPosition;
            LocalScale = localScale;
        }

        public Vector3 LocalPosition { get; }
        public Vector3 LocalScale { get; }
    }

    /// <summary>
    /// 随行包裹尺寸（世界单位）。交给 TransitionPlanner 抬高流动下限。
    /// </summary>
    public readonly struct LivingUiContentEnvelope
    {
        public LivingUiContentEnvelope(Vector2 size)
        {
            Size = size;
        }

        public Vector2 Size { get; }
    }

    /// <summary>纯数据内容绑定：内容ID → 载体 → 策略 → 局部位姿。</summary>
    public readonly struct LivingUiContentBinding
    {
        public LivingUiContentBinding(
            string contentId,
            int carrierId,
            LivingUiContentLocalPose localPose,
            LivingUiContentFollowPolicy followPolicy,
            LivingUiContentVisibilityPolicy visibilityPolicy,
            LivingUiLayoutId? faceLayout,
            LivingUiContentEnvelope envelope,
            Vector2 baselineSize = default,
            LivingUiPartialFollowEdge followEdge = LivingUiPartialFollowEdge.Left,
            float staggerSpan = 0.35f)
        {
            ContentId = contentId ?? throw new ArgumentNullException(nameof(contentId));
            CarrierId = carrierId;
            LocalPose = localPose;
            FollowPolicy = followPolicy;
            VisibilityPolicy = visibilityPolicy;
            FaceLayout = faceLayout;
            Envelope = envelope;
            BaselineSize = baselineSize;
            FollowEdge = followEdge;
            StaggerSpan = staggerSpan;
        }

        public string ContentId { get; }
        public int CarrierId { get; }
        public LivingUiContentLocalPose LocalPose { get; }
        public LivingUiContentFollowPolicy FollowPolicy { get; }
        public LivingUiContentVisibilityPolicy VisibilityPolicy { get; }

        /// <summary>仅在该构型 Face 上显示；null 表示不限 Face。</summary>
        public LivingUiLayoutId? FaceLayout { get; }

        public LivingUiContentEnvelope Envelope { get; }

        /// <summary>反应式基线载体尺寸；(0,0) 表示运行时用构型终态尺寸。</summary>
        public Vector2 BaselineSize { get; }

        /// <summary>PartialFollow 贴边。</summary>
        public LivingUiPartialFollowEdge FollowEdge { get; }

        /// <summary>BoundaryReactive 错峰跨度 [0,1]。</summary>
        public float StaggerSpan { get; }
    }

    /// <summary>内容在转场中的参与阶段；驱动端据此决定缩放进/退场。</summary>
    public enum LivingUiContentPhase
    {
        Hidden = 0,
        Stable = 1,
        Entering = 2,
        Exiting = 3,
    }

    /// <summary>纯数据内容策略求值（镜像 TransitionPlanner：无 MonoBehaviour、可 EditMode 验）。</summary>
    public static class LivingUiContentPolicy
    {
        /// <summary>RigidTravel：世界位 = 载体位 + 局部偏移（忽略载体 size）。</summary>
        public static Vector3 RigidTravelWorldPosition(Vector2 carrierPosition, Vector3 localOffset)
        {
            return new Vector3(
                carrierPosition.x + localOffset.x,
                carrierPosition.y + localOffset.y,
                localOffset.z);
        }

        /// <summary>
        /// 内容阶段：停稳时仅 effective Face；转场中区分进场（target）与快速退场（source）。
        /// 可见性由 ContentDriver 写 scale 决定，不再用 SetActive 瞬闪。
        /// </summary>
        public static LivingUiContentPhase EvaluatePhase(
            LivingUiContentBinding binding,
            LivingUiLayoutId sourceLayout,
            LivingUiLayoutId targetLayout,
            LivingUiLayoutId effectiveLayout,
            bool isTransitioning)
        {
            if (!binding.FaceLayout.HasValue)
            {
                return isTransitioning ? LivingUiContentPhase.Entering : LivingUiContentPhase.Stable;
            }

            var face = binding.FaceLayout.Value;
            if (!isTransitioning)
            {
                return face == effectiveLayout ? LivingUiContentPhase.Stable : LivingUiContentPhase.Hidden;
            }

            var onSource = face == sourceLayout;
            var onTarget = face == targetLayout;
            if (onSource && onTarget) return LivingUiContentPhase.Stable;
            if (onSource) return LivingUiContentPhase.Exiting;
            if (onTarget) return LivingUiContentPhase.Entering;
            return LivingUiContentPhase.Hidden;
        }

        /// <summary>兼容：阶段非 Hidden 即视为策略可见（实际显隐由 scale 控制）。</summary>
        public static bool EvaluateVisible(
            LivingUiContentBinding binding,
            LivingUiLayoutId sourceLayout,
            LivingUiLayoutId targetLayout,
            LivingUiLayoutId effectiveLayout,
            bool isTransitioning)
        {
            return EvaluatePhase(binding, sourceLayout, targetLayout, effectiveLayout, isTransitioning)
                != LivingUiContentPhase.Hidden;
        }

        /// <summary>兼容重载：无转场端点时 source/target 均取 effective。</summary>
        public static bool EvaluateVisible(
            LivingUiContentBinding binding,
            LivingUiLayoutId effectiveLayout,
            bool isTransitioning)
        {
            return EvaluateVisible(binding, effectiveLayout, effectiveLayout, effectiveLayout, isTransitioning);
        }
    }
}
