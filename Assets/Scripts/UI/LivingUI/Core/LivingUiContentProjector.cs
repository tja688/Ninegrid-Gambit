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
        public const float DefaultContentPadding = 0.05f;
        public const float VisibleScaleEpsilon = 0.001f;

        /// <summary>
        /// 按跟随策略投影。RigidTravel 原样返回 authored 位姿（规模 1），由父子挂接完成世界跟随。
        /// </summary>
        public static LivingUiContentProjection Project(
            LivingUiContentFollowPolicy policy,
            LivingUiContentLocalPose authoredPose,
            Vector2 baselineSize,
            Vector2 currentCarrierSize,
            LivingUiPartialFollowEdge followEdge,
            float staggerSpan,
            Vector2 envelopeSize = default,
            float contentPadding = DefaultContentPadding)
        {
            switch (policy)
            {
                case LivingUiContentFollowPolicy.RigidTravel:
                    return new LivingUiContentProjection(authoredPose.LocalPosition, authoredPose.LocalScale, true);

                case LivingUiContentFollowPolicy.BoundaryReactive:
                    return ProjectBoundaryReactive(
                        authoredPose,
                        baselineSize,
                        currentCarrierSize,
                        staggerSpan,
                        envelopeSize,
                        contentPadding);

                case LivingUiContentFollowPolicy.PartialFollow:
                    return ProjectPartialFollow(
                        authoredPose, baselineSize, currentCarrierSize, followEdge);

                default:
                    throw new ArgumentOutOfRangeException(nameof(policy), policy, null);
            }
        }

        /// <summary>
        /// 场景 b：锚点锁定在 authored 局部位；边界扫过锚点时 scale→0，扫过后再按可用间隙
        /// 「种子式」长大。AABB = 锚点 ± envelope·scale/2 始终落在 Inset(carrier) 内，绝不挪位跟边。
        /// staggerSpan 保留参数兼容；错峰由锚点离边远近自然产生。
        /// </summary>
        public static LivingUiContentProjection ProjectBoundaryReactive(
            LivingUiContentLocalPose authoredPose,
            Vector2 baselineSize,
            Vector2 currentCarrierSize,
            float staggerSpan,
            Vector2 envelopeSize = default,
            float contentPadding = DefaultContentPadding)
        {
            // 锚点固定：不随载体 size 比例重映射，也不向中心 clamp。
            var local = authoredPose.LocalPosition;
            _ = staggerSpan;

            var padding = Mathf.Max(contentPadding, 0f);
            var insetHalf = new Vector2(
                Mathf.Max(currentCarrierSize.x * 0.5f - padding, 0f),
                Mathf.Max(currentCarrierSize.y * 0.5f - padding, 0f));

            var absX = Mathf.Abs(local.x);
            var absY = Mathf.Abs(local.y);
            var roomX = insetHalf.x - absX;
            var roomY = insetHalf.y - absY;

            // 边界尚未覆盖锚点（或刚好扫过）→ 必然已消失。
            if (roomX <= 0f || roomY <= 0f)
            {
                return new LivingUiContentProjection(local, Vector3.zero, false);
            }

            var envHalfX = Mathf.Max(envelopeSize.x, 0f) * 0.5f;
            var envHalfY = Mathf.Max(envelopeSize.y, 0f) * 0.5f;

            float scaleFactor;
            if (envHalfX > 0.0001f || envHalfY > 0.0001f)
            {
                // 间隙刚好等于半包络 → 满尺寸；更小则成比例缩小，外缘贴着 inset。
                var sx = envHalfX > 0.0001f ? roomX / envHalfX : float.PositiveInfinity;
                var sy = envHalfY > 0.0001f ? roomY / envHalfY : float.PositiveInfinity;
                scaleFactor = Mathf.Clamp01(Mathf.Min(sx, sy));
            }
            else
            {
                // 无 envelope：相对基线间隙做种子生长，基线满尺寸为 1。
                var baseInsetHalf = new Vector2(
                    Mathf.Max(baselineSize.x * 0.5f - padding, 0f),
                    Mathf.Max(baselineSize.y * 0.5f - padding, 0f));
                var baseRoomX = Mathf.Max(baseInsetHalf.x - absX, 0.0001f);
                var baseRoomY = Mathf.Max(baseInsetHalf.y - absY, 0.0001f);
                scaleFactor = Mathf.Clamp01(Mathf.Min(roomX / baseRoomX, roomY / baseRoomY));
            }

            var scale = authoredPose.LocalScale * scaleFactor;
            var visible = scaleFactor > VisibleScaleEpsilon;
            return new LivingUiContentProjection(local, scale, visible);
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
        /// BoundaryReactive 的出场错峰由锚点间隙自然产生；此函数仍可供诊断/他策略使用。
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
