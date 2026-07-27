#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System.Collections.Generic;
using System.Text;
using NineGrid.Cards;
using NineGrid.Flow;
using UnityEngine;

namespace NineGrid.Presentation.Diagnostics
{
    /// <summary>
    /// 热区掉帧 A/B 隔离模式（互斥）。由 DevTest 热键宿主切换；业务侧只读 flags。
    /// </summary>
    public enum PerfHoverKillMode
    {
        Off = 0,
        /// <summary>A：禁用全场景 Collider2D。</summary>
        DisableAllColliders = 1,
        /// <summary>B：GroundCardHitProxy OnMouseEnter/Exit 直接 return。</summary>
        SuppressGroundOnMouseHover = 2,
        /// <summary>C：Hover/Base 视觉瞬时落地，跳过 DOTween。</summary>
        InstantHoverNoTween = 3,
        /// <summary>D：仅禁用 Ground 槽 + Relic/ContentIcon 槽 Collider。</summary>
        DisableSlotAndRelicColliders = 4,
    }

    /// <summary>
    /// Player/Editor 共用的 hover 掉帧 kill-switch 状态与副作用（Collider / hover 回调）。
    /// </summary>
    public static class PerfHoverKillSwitch
    {
        private static readonly Dictionary<Collider2D, bool> sColliderEnabledByRef = new();

        public static PerfHoverKillMode ActiveMode { get; private set; } = PerfHoverKillMode.Off;

        public static bool SuppressGroundOnMouseHover =>
            ActiveMode == PerfHoverKillMode.SuppressGroundOnMouseHover;

        public static bool InstantHoverNoTween =>
            ActiveMode == PerfHoverKillMode.InstantHoverNoTween;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ActiveMode = PerfHoverKillMode.Off;
            sColliderEnabledByRef.Clear();
        }

        public static string Describe(PerfHoverKillMode mode)
        {
            return mode switch
            {
                PerfHoverKillMode.Off => "0-Off 基线（全部恢复）",
                PerfHoverKillMode.DisableAllColliders => "A-DisableAllColliders2D（SendMouseEvents 命中面清空）",
                PerfHoverKillMode.SuppressGroundOnMouseHover => "B-SuppressGroundOnMouseHover（OnMouseEnter/Exit no-op）",
                PerfHoverKillMode.InstantHoverNoTween => "C-InstantHoverNoTween（无 DOTween Punch/Scale 序列）",
                PerfHoverKillMode.DisableSlotAndRelicColliders => "D-DisableSlotAndRelicColliders（九宫槽+Relic 盒）",
                _ => mode.ToString(),
            };
        }

        public static void SetMode(PerfHoverKillMode mode, out string detail)
        {
            var sb = new StringBuilder(256);
            RestoreSideEffects(sb);
            ActiveMode = mode;
            ApplySideEffects(mode, sb);
            detail = sb.ToString();
        }

        /// <summary>
        /// 模式 A/D 下每帧补洞：新生成的 Collider 也会被关掉并记入还原表。
        /// </summary>
        public static void TickColliderPolicy()
        {
            switch (ActiveMode)
            {
                case PerfHoverKillMode.DisableAllColliders:
                    DisableMatchingColliders(_ => true);
                    break;
                case PerfHoverKillMode.DisableSlotAndRelicColliders:
                    DisableMatchingColliders(IsSlotOrRelicCollider);
                    break;
            }
        }

        public static int CountEnabledColliders2D()
        {
            var colliders = Object.FindObjectsByType<Collider2D>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            var enabled = 0;
            for (var i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null && colliders[i].enabled)
                {
                    enabled++;
                }
            }

            return enabled;
        }

        private static void ApplySideEffects(PerfHoverKillMode mode, StringBuilder sb)
        {
            switch (mode)
            {
                case PerfHoverKillMode.Off:
                    sb.Append("sideEffects=none");
                    break;

                case PerfHoverKillMode.DisableAllColliders:
                {
                    var n = DisableMatchingColliders(_ => true);
                    sb.Append("disabledColliders=").Append(n)
                        .Append(" enabledLeft=").Append(CountEnabledColliders2D());
                    break;
                }

                case PerfHoverKillMode.SuppressGroundOnMouseHover:
                    sb.Append("flag=SuppressGroundOnMouseHover");
                    break;

                case PerfHoverKillMode.InstantHoverNoTween:
                    sb.Append("flag=InstantHoverNoTween");
                    break;

                case PerfHoverKillMode.DisableSlotAndRelicColliders:
                {
                    var n = DisableMatchingColliders(IsSlotOrRelicCollider);
                    sb.Append("disabledSlotOrRelic=").Append(n)
                        .Append(" enabledLeft=").Append(CountEnabledColliders2D());
                    break;
                }
            }
        }

        private static void RestoreSideEffects(StringBuilder sb)
        {
            if (sColliderEnabledByRef.Count <= 0)
            {
                return;
            }

            var restored = 0;
            var missing = 0;
            foreach (var pair in sColliderEnabledByRef)
            {
                var collider = pair.Key;
                if (collider == null)
                {
                    missing++;
                    continue;
                }

                collider.enabled = pair.Value;
                restored++;
            }

            sColliderEnabledByRef.Clear();
            sb.Append("restoredColliders=").Append(restored)
                .Append(" missing=").Append(missing)
                .Append("; ");
        }

        private static int DisableMatchingColliders(System.Func<Collider2D, bool> predicate)
        {
            var colliders = Object.FindObjectsByType<Collider2D>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            var disabledNow = 0;
            for (var i = 0; i < colliders.Length; i++)
            {
                var collider = colliders[i];
                if (collider == null || !predicate(collider))
                {
                    continue;
                }

                if (!sColliderEnabledByRef.ContainsKey(collider))
                {
                    sColliderEnabledByRef[collider] = collider.enabled;
                }

                if (collider.enabled)
                {
                    collider.enabled = false;
                    disabledNow++;
                }
            }

            return disabledNow;
        }

        private static bool IsSlotOrRelicCollider(Collider2D collider)
        {
            if (collider == null)
            {
                return false;
            }

            return collider.GetComponent<GroundSlotHitProxy>() != null
                   || collider.GetComponent<ContentIconSlotHitProxy>() != null;
        }
    }
}

#endif
