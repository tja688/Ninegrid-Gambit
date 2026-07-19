using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.LivingUI
{
    /// <summary>内容投影器单帧输出：相对 ContentAttach（载体中心）的局部位姿 + 是否应显示。</summary>
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
    /// 内容投影器（纯数据）：锚点解算局部位 + Scale 进退场倍率。
    /// 与 TransitionPlanner 同哲学；不是屏幕追逐。
    /// </summary>
    public static class LivingUiContentProjector
    {
        public const float DefaultExitDuration = 0.1f;
        public const float VisibleScaleEpsilon = 0.001f;
        public const float EnterProgressChannelEpsilon = 0.01f;

        /// <summary>锚点角/中心相对载体中心的偏移（半尺寸）。</summary>
        public static Vector2 AnchorCorner(LivingUiContentAnchor anchor, Vector2 carrierSize)
        {
            var half = carrierSize * 0.5f;
            switch (anchor)
            {
                case LivingUiContentAnchor.TopLeft:
                    return new Vector2(-half.x, half.y);
                case LivingUiContentAnchor.BottomLeft:
                    return new Vector2(-half.x, -half.y);
                case LivingUiContentAnchor.BottomRight:
                    return new Vector2(half.x, -half.y);
                case LivingUiContentAnchor.TopRight:
                    return new Vector2(half.x, half.y);
                case LivingUiContentAnchor.Center:
                    return Vector2.zero;
                default:
                    throw new ArgumentOutOfRangeException(nameof(anchor), anchor, null);
            }
        }

        /// <summary>
        /// 相对锚点的偏移 → 相对载体中心的 localPosition。
        /// </summary>
        public static Vector3 ResolveAnchoredLocal(
            LivingUiContentAnchor anchor,
            Vector3 offsetFromAnchor,
            Vector2 carrierSize)
        {
            var corner = AnchorCorner(anchor, carrierSize);
            return new Vector3(
                corner.x + offsetFromAnchor.x,
                corner.y + offsetFromAnchor.y,
                offsetFromAnchor.z);
        }

        /// <summary>
        /// 把相对中心的局部位换算成相对锚点的偏移（在给定 carrierSize 下世界位不变）。
        /// </summary>
        public static Vector3 CenterLocalToAnchorOffset(
            LivingUiContentAnchor anchor,
            Vector3 centerLocal,
            Vector2 carrierSize)
        {
            var corner = AnchorCorner(anchor, carrierSize);
            return new Vector3(
                centerLocal.x - corner.x,
                centerLocal.y - corner.y,
                centerLocal.z);
        }

        /// <summary>不变模式：锚点钉角 + 作者缩放。</summary>
        public static LivingUiContentProjection ProjectInvariant(
            LivingUiContentAnchor anchor,
            LivingUiContentLocalPose authoredPose,
            Vector2 carrierSize)
        {
            var local = ResolveAnchoredLocal(anchor, authoredPose.LocalPosition, carrierSize);
            return new LivingUiContentProjection(local, authoredPose.LocalScale, true);
        }

        /// <summary>
        /// 缩放模式：局部位用固定参考尺寸解算（转场中不随 live size 漂移，对齐旧表现）；
        /// scale = authored * scaleFactor。
        /// </summary>
        public static LivingUiContentProjection ProjectScale(
            LivingUiContentAnchor anchor,
            LivingUiContentLocalPose authoredPose,
            Vector2 referenceCarrierSize,
            float scaleFactor)
        {
            var factor = Mathf.Max(0f, scaleFactor);
            var local = ResolveAnchoredLocal(anchor, authoredPose.LocalPosition, referenceCarrierSize);
            var scale = authoredPose.LocalScale * factor;
            var visible = factor > VisibleScaleEpsilon;
            return new LivingUiContentProjection(local, scale, visible);
        }

        /// <summary>离场倍率：转场起始为 1，在 exitDuration 内线性缩至 0。</summary>
        public static float ComputeExitScaleFactor(float transitionElapsed, float exitDuration = DefaultExitDuration)
        {
            var duration = Mathf.Max(exitDuration, 0.0001f);
            return 1f - Mathf.Clamp01(transitionElapsed / duration);
        }

        /// <summary>
        /// 转场进场进度 [0,1]（仅尺寸通道）：current 在 source 时为 0，到达 target 时为 1。
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

        /// <summary>
        /// 进场进度：尺寸有变 → 尺寸插值；否则位移有变 → 位移插值；否则用 timeProgress01。
        /// </summary>
        public static float ComputeEnterProgress(
            Vector2 sourceCarrierSize,
            Vector2 targetCarrierSize,
            Vector2 currentCarrierSize,
            Vector2 sourceCarrierPosition,
            Vector2 targetCarrierPosition,
            Vector2 currentCarrierPosition,
            float timeProgress01)
        {
            if (ChannelChanged(sourceCarrierSize, targetCarrierSize))
            {
                return ComputeEnterProgress(sourceCarrierSize, targetCarrierSize, currentCarrierSize);
            }

            var travel = Vector2.Distance(sourceCarrierPosition, targetCarrierPosition);
            if (travel > EnterProgressChannelEpsilon)
            {
                var remaining = Vector2.Distance(currentCarrierPosition, targetCarrierPosition);
                return Mathf.Clamp01(1f - remaining / travel);
            }

            return Mathf.Clamp01(timeProgress01);
        }

        private static bool ChannelChanged(Vector2 source, Vector2 target)
        {
            return Mathf.Abs(source.x - target.x) > EnterProgressChannelEpsilon
                || Mathf.Abs(source.y - target.y) > EnterProgressChannelEpsilon;
        }

        private static float ProgressAxis(float source, float target, float current)
        {
            if (Mathf.Abs(source - target) <= EnterProgressChannelEpsilon) return 1f;
            return Mathf.Clamp01((current - source) / (target - source));
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
