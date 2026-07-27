#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System.Collections.Generic;
using System.Text;
using NineGrid.Cards;
using NineGrid.Flow;
using NineGrid.Presentation.Setup;
using UnityEngine;

namespace NineGrid.Presentation.Diagnostics
{
    /// <summary>
    /// 热区掉帧 A/B 隔离模式（互斥）。由 DevTest 热键宿主切换；业务侧只读 flags。
    /// </summary>
    public enum PerfHoverKillMode
    {
        Off = 0,
        /// <summary>A：软件光标改为硬件 Auto。</summary>
        SoftCursorAuto = 1,
        /// <summary>B：禁用全场景 Collider2D。</summary>
        DisableAllColliders = 2,
        /// <summary>C：GroundCardHitProxy OnMouseEnter/Exit 直接 return。</summary>
        SuppressGroundOnMouseHover = 3,
        /// <summary>D：Hover/Base 视觉瞬时落地，跳过 DOTween。</summary>
        InstantHoverNoTween = 4,
        /// <summary>E：仅禁用 Ground 槽 + Relic/ContentIcon 槽 Collider。</summary>
        DisableSlotAndRelicColliders = 5,
    }

    /// <summary>
    /// Player/Editor 共用的 hover 掉帧 kill-switch 状态与副作用（光标 / Collider）。
    /// </summary>
    public static class PerfHoverKillSwitch
    {
        private static readonly Dictionary<int, bool> sColliderEnabledByInstanceId = new();

        public static PerfHoverKillMode ActiveMode { get; private set; } = PerfHoverKillMode.Off;

        public static bool SuppressGroundOnMouseHover =>
            ActiveMode == PerfHoverKillMode.SuppressGroundOnMouseHover;

        public static bool InstantHoverNoTween =>
            ActiveMode == PerfHoverKillMode.InstantHoverNoTween;

        public static string Describe(PerfHoverKillMode mode)
        {
            return mode switch
            {
                PerfHoverKillMode.Off => "0-Off 基线（全部恢复）",
                PerfHoverKillMode.SoftCursorAuto => "A-SoftCursorAuto（ForceSoftware→Auto）",
                PerfHoverKillMode.DisableAllColliders => "B-DisableAllColliders2D（SendMouseEvents 命中面清空）",
                PerfHoverKillMode.SuppressGroundOnMouseHover => "C-SuppressGroundOnMouseHover（OnMouseEnter/Exit no-op）",
                PerfHoverKillMode.InstantHoverNoTween => "D-InstantHoverNoTween（无 DOTween Punch/Scale 序列）",
                PerfHoverKillMode.DisableSlotAndRelicColliders => "E-DisableSlotAndRelicColliders（九宫槽+Relic 盒）",
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
        /// 模式 B/E 下每帧补洞：新生成的 Collider 也会被关掉并记入还原表。
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

                case PerfHoverKillMode.SoftCursorAuto:
                    Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                    sb.Append("cursor=Auto(hardware)");
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
            if (ActiveMode == PerfHoverKillMode.SoftCursorAuto
                || ActiveMode == PerfHoverKillMode.Off)
            {
                // SoftCursor 离开时一律尝试恢复软件光标；Off 也无所谓多调一次。
            }

            if (sColliderEnabledByInstanceId.Count > 0)
            {
                var restored = 0;
                var missing = 0;
                foreach (var pair in sColliderEnabledByInstanceId)
                {
                    var collider = ResolveCollider(pair.Key);
                    if (collider == null)
                    {
                        missing++;
                        continue;
                    }

                    collider.enabled = pair.Value;
                    restored++;
                }

                sColliderEnabledByInstanceId.Clear();
                sb.Append("restoredColliders=").Append(restored)
                    .Append(" missing=").Append(missing)
                    .Append("; ");
            }

            if (ActiveMode == PerfHoverKillMode.SoftCursorAuto)
            {
                SoftwareCursorBootstrap.ReapplyForceSoftware();
                sb.Append("cursor=ForceSoftware.reapplied; ");
            }
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

                var id = collider.GetInstanceID();
                if (!sColliderEnabledByInstanceId.ContainsKey(id))
                {
                    sColliderEnabledByInstanceId[id] = collider.enabled;
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

        private static Collider2D ResolveCollider(int instanceId)
        {
            var obj = Resources.InstanceIDToObject(instanceId);
            return obj as Collider2D;
        }
    }
}

#endif
