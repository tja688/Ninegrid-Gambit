using System;
using UnityEngine;

namespace NineGrid.LivingUI
{
    /// <summary>
    /// 内容相对载体矩形的锚点（类 CSS）。必选；默认左上。
    /// 四角对应各自两条边的交点；中心为载体中心。
    /// </summary>
    public enum LivingUiContentAnchor
    {
        TopLeft = 0,
        BottomLeft = 1,
        BottomRight = 2,
        TopRight = 3,
        Center = 4,
    }

    /// <summary>
    /// 转场运动模式：由外部控制器按核心规则下发，元素自身不持有。
    /// 跨构型存在 → Invariant；非跨构型 → Scale。
    /// </summary>
    public enum LivingUiContentMotionMode
    {
        /// <summary>按锚点随面板；不缩放进退场。</summary>
        Invariant = 0,

        /// <summary>大盘构型变换时缩放进/退场。</summary>
        Scale = 1,
    }

    /// <summary>内容相对锚点的作者局部位姿（偏移相对所选锚点角/中心）。</summary>
    public readonly struct LivingUiContentLocalPose
    {
        public LivingUiContentLocalPose(Vector3 localPosition, Vector3 localScale)
        {
            LocalPosition = localPosition;
            LocalScale = localScale;
        }

        /// <summary>相对锚点的局部偏移（非相对载体中心，除非 Anchor=Center）。</summary>
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

    /// <summary>纯数据内容绑定：内容ID → 载体 → 锚点 → 局部位姿。</summary>
    public readonly struct LivingUiContentBinding
    {
        public LivingUiContentBinding(
            string contentId,
            int carrierId,
            LivingUiContentLocalPose localPose,
            LivingUiContentAnchor anchor,
            LivingUiLayoutId? faceLayout,
            LivingUiContentEnvelope envelope,
            Vector2 authoringSize = default)
        {
            ContentId = contentId ?? throw new ArgumentNullException(nameof(contentId));
            CarrierId = carrierId;
            LocalPose = localPose;
            Anchor = anchor;
            FaceLayout = faceLayout;
            Envelope = envelope;
            AuthoringSize = authoringSize;
        }

        public string ContentId { get; }
        public int CarrierId { get; }
        public LivingUiContentLocalPose LocalPose { get; }
        public LivingUiContentAnchor Anchor { get; }

        /// <summary>登记的主 Face；跨构型复用时由控制器映射表扩展，可为 null。</summary>
        public LivingUiLayoutId? FaceLayout { get; }

        public LivingUiContentEnvelope Envelope { get; }

        /// <summary>作者化时的载体尺寸；用于中心↔锚点换算与 Scale 位姿固定参考。</summary>
        public Vector2 AuthoringSize { get; }
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
        /// <summary>
        /// 核心规则：元素在 source 与 target 都存在 → Invariant；否则 Scale。
        /// 非转场时返回 Invariant（停稳按锚点）。
        /// </summary>
        public static LivingUiContentMotionMode ResolveMotionMode(
            bool presentInSource,
            bool presentInTarget,
            bool isTransitioning)
        {
            if (!isTransitioning) return LivingUiContentMotionMode.Invariant;
            if (presentInSource && presentInTarget) return LivingUiContentMotionMode.Invariant;
            return LivingUiContentMotionMode.Scale;
        }

        /// <summary>
        /// 内容阶段：停稳时仅 effective Face；转场中区分进场（target）与退场（source）。
        /// presentIn* 由控制器映射表提供（同一 GO 可挂多个构型）。
        /// </summary>
        public static LivingUiContentPhase EvaluatePhase(
            bool presentInSource,
            bool presentInTarget,
            bool presentInEffective,
            bool isTransitioning)
        {
            if (!isTransitioning)
            {
                return presentInEffective ? LivingUiContentPhase.Stable : LivingUiContentPhase.Hidden;
            }

            if (presentInSource && presentInTarget) return LivingUiContentPhase.Stable;
            if (presentInSource) return LivingUiContentPhase.Exiting;
            if (presentInTarget) return LivingUiContentPhase.Entering;
            return LivingUiContentPhase.Hidden;
        }

        /// <summary>兼容：单 Face 绑定时的阶段求值。</summary>
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
            return EvaluatePhase(
                presentInSource: face == sourceLayout,
                presentInTarget: face == targetLayout,
                presentInEffective: face == effectiveLayout,
                isTransitioning);
        }
    }
}
