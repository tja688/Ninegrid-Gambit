using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.LivingUI
{
    /// <summary>内容投影器单帧输出：相对载体中心的局部位姿 + 是否应显示。</summary>
    public readonly struct LivingUiContentProjection
    {
        public LivingUiContentProjection(Vector3 localPosition, Vector3 localScale, bool visible)
        {
            LocalPosition = localPosition;
            LocalScale = localScale;
            Visible = visible;
        }

        public Vector3 LocalPosition { get; }
        public Vector3 LocalScale { get; }
        public bool Visible { get; }
    }

    /// <summary>
    /// 反应式内容投影器（纯数据）：同一份载体采样 rect → 内容 scale/offset/visible。
    /// 与 TransitionPlanner 同哲学；不是屏幕追逐。
    /// </summary>
    public static class LivingUiContentProjector
    {
        public const float DefaultStaggerSpan = 0.35f;
        public const float DefaultExitDuration = 0.1f;
        public const float VisibleScaleEpsilon = 0.001f;

        /// <summary>离场倍率：转场起始为 1，在 exitDuration 内线性缩至 0。</summary>
        public static float ComputeExitScaleFactor(float transitionElapsed, float exitDuration = DefaultExitDuration)
        {
            var duration = Mathf.Max(exitDuration, 0.0001f);
            return 1f - Mathf.Clamp01(transitionElapsed / duration);
        }

        /// <summary>
        /// 按跟随策略投影。RigidTravel 原样返回 authored 位姿（规模 1），由父子挂接完成世界跟随。
        /// </summary>
        public static LivingUiContentProjection Project(
            LivingUiContentFollowPolicy policy,
            LivingUiContentLocalPose authoredPose,
            Vector2 baselineSize,
            Vector2 currentCarrierSize,
            LivingUiPartialFollowEdge followEdge,
            float staggerSpan)
        {
            switch (policy)
            {
                case LivingUiContentFollowPolicy.RigidTravel:
                    return new LivingUiContentProjection(authoredPose.LocalPosition, authoredPose.LocalScale, true);

                case LivingUiContentFollowPolicy.BoundaryReactive:
                    return ProjectBoundaryReactive(
                        authoredPose, baselineSize, currentCarrierSize, staggerSpan);

                case LivingUiContentFollowPolicy.PartialFollow:
                    return ProjectPartialFollow(
                        authoredPose, baselineSize, currentCarrierSize, followEdge);

                default:
                    throw new ArgumentOutOfRangeException(nameof(policy), policy, null);
            }
        }

        /// <summary>
        /// 场景 b：缩放随 size/基线映射；错峰由归一化局部位决定（离中心越远越先坍缩到 0）。
        /// </summary>
        public static LivingUiContentProjection ProjectBoundaryReactive(
            LivingUiContentLocalPose authoredPose,
            Vector2 baselineSize,
            Vector2 currentCarrierSize,
            float staggerSpan)
        {
            var sizeRatio = ResolveSizeRatio(baselineSize, currentCarrierSize);
            return ProjectBoundaryReactiveFromRatio(authoredPose, baselineSize, sizeRatio, staggerSpan);
        }

        /// <summary>
        /// 进场专用：按转场起止载体尺寸插值，避免源构型比基线更大时一进场就满 scale。
        /// </summary>
        public static LivingUiContentProjection ProjectBoundaryReactiveEnter(
            LivingUiContentLocalPose authoredPose,
            Vector2 sourceCarrierSize,
            Vector2 targetCarrierSize,
            Vector2 currentCarrierSize,
            float staggerSpan)
        {
            var enterRatio = ComputeEnterProgress(sourceCarrierSize, targetCarrierSize, currentCarrierSize);
            return ProjectBoundaryReactiveFromRatio(authoredPose, targetCarrierSize, enterRatio, staggerSpan);
        }

        /// <summary>
        /// 转场进场进度 [0,1]：current 在 source 时为 0，到达 target 时为 1。
        /// </summary>
        public static float ComputeEnterProgress(
            Vector2 sourceCarrierSize,
            Vector2 targetCarrierSize,
            Vector2 currentCarrierSize)
        {
            var width = ProgressAxis(sourceCarrierSize.x, targetCarrierSize.x, currentCarrierSize.x);
            var height = ProgressAxis(sourceCarrierSize.y, targetCarrierSize.y, currentCarrierSize.y);
            return Mathf.Clamp01(Mathf.Min(width, height));
        }

        private static float ProgressAxis(float source, float target, float current)
        {
            if (Mathf.Approximately(source, target)) return 1f;
            return Mathf.Clamp01((current - source) / (target - source));
        }

        private static LivingUiContentProjection ProjectBoundaryReactiveFromRatio(
            LivingUiContentLocalPose authoredPose,
            Vector2 referenceSize,
            float sizeRatio,
            float staggerSpan)
        {
            var stagger01 = Stagger01FromLocal(authoredPose.LocalPosition, referenceSize);
            var scaleFactor = ResolveStaggeredScaleFactor(sizeRatio, stagger01, staggerSpan);
            var scale = authoredPose.LocalScale * scaleFactor;
            var visible = scaleFactor > VisibleScaleEpsilon;
            return new LivingUiContentProjection(authoredPose.LocalPosition, scale, visible);
        }

        private static float ResolveStaggeredScaleFactor(float sizeRatio, float stagger01, float staggerSpan)
        {
            var span = Mathf.Clamp01(staggerSpan);
            var threshold = stagger01 * span;
            var denom = Mathf.Max(1f - threshold, 0.0001f);
            return Mathf.Clamp01((sizeRatio - threshold) / denom);
        }

        /// <summary>
        /// 场景 c：贴一条边按尺寸差比例位移，不缩放，持续存在。
        /// 载体中心锚定：贴左边 → 宽度变化时 local.x 随左缘移动（-0.5·Δw）。
        /// </summary>
        public static LivingUiContentProjection ProjectPartialFollow(
            LivingUiContentLocalPose authoredPose,
            Vector2 baselineSize,
            Vector2 currentCarrierSize,
            LivingUiPartialFollowEdge followEdge)
        {
            var delta = currentCarrierSize - baselineSize;
            var local = authoredPose.LocalPosition;
            switch (followEdge)
            {
                case LivingUiPartialFollowEdge.Left:
                    local.x -= 0.5f * delta.x;
                    break;
                case LivingUiPartialFollowEdge.Right:
                    local.x += 0.5f * delta.x;
                    break;
                case LivingUiPartialFollowEdge.Bottom:
                    local.y -= 0.5f * delta.y;
                    break;
                case LivingUiPartialFollowEdge.Top:
                    local.y += 0.5f * delta.y;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(followEdge), followEdge, null);
            }

            return new LivingUiContentProjection(local, authoredPose.LocalScale, true);
        }

        /// <summary>size 相对基线的坍缩比：取宽高较小比，基线非法时视为 1。</summary>
        public static float ResolveSizeRatio(Vector2 baselineSize, Vector2 currentCarrierSize)
        {
            var bx = Mathf.Max(baselineSize.x, 0.0001f);
            var by = Mathf.Max(baselineSize.y, 0.0001f);
            return Mathf.Min(currentCarrierSize.x / bx, currentCarrierSize.y / by);
        }

        /// <summary>
        /// 归一化错峰键：相对基线半尺寸的切比雪夫距离，钳到 [0,1]。中心≈0，贴边≈1。
        /// </summary>
        public static float Stagger01FromLocal(Vector3 localPosition, Vector2 baselineSize)
        {
            var hx = Mathf.Max(baselineSize.x * 0.5f, 0.0001f);
            var hy = Mathf.Max(baselineSize.y * 0.5f, 0.0001f);
            var nx = Mathf.Abs(localPosition.x) / hx;
            var ny = Mathf.Abs(localPosition.y) / hy;
            return Mathf.Clamp01(Mathf.Max(nx, ny));
        }

        /// <summary>
        /// 随行 envelope 按载体聚合：取同载体各内容 envelope 的分量 max（不含 styleFloor）。
        /// </summary>
        public static Dictionary<int, Vector2> AggregateEnvelopeFloors(
            IReadOnlyList<LivingUiContentBinding> bindings)
        {
            var result = new Dictionary<int, Vector2>();
            if (bindings == null) return result;

            for (var i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                var env = binding.Envelope.Size;
                if (env.x <= 0f && env.y <= 0f) continue;

                if (result.TryGetValue(binding.CarrierId, out var existing))
                {
                    result[binding.CarrierId] = new Vector2(
                        Mathf.Max(existing.x, env.x),
                        Mathf.Max(existing.y, env.y));
                }
                else
                {
                    result[binding.CarrierId] = env;
                }
            }

            return result;
        }

        /// <summary>单载体有效流动下限 = max(styleFloor, override)。</summary>
        public static Vector2 ResolveCarrierFlowFloor(
            Vector2 styleFloor,
            IReadOnlyDictionary<int, Vector2> envelopeFloors,
            int carrierId)
        {
            if (envelopeFloors != null && envelopeFloors.TryGetValue(carrierId, out var raised))
            {
                return new Vector2(
                    Mathf.Max(styleFloor.x, raised.x),
                    Mathf.Max(styleFloor.y, raised.y));
            }

            return styleFloor;
        }
    }
}
