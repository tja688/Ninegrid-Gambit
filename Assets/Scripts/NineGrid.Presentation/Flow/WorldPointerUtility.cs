using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NineGrid.Flow
{
    /// <summary>
    /// 世界空间点选工具：唯一指针读口（屏幕 / 世界 / 主键边沿）。
    /// 高回报率 mitigation 激活时由 Win32 注入 Input System，本类仍只读 Mouse.current。
    /// </summary>
    public static class WorldPointerUtility
    {
        /// <summary>可注入的指针源（EditMode 假指针 / 平台兜底）。</summary>
        public interface IPointerSource
        {
            bool TryGetScreenPosition(out Vector2 screen);
            bool IsPrimaryHeld { get; }
            bool WasPrimaryPressedThisFrame { get; }
            bool WasPrimaryReleasedThisFrame { get; }
            bool WasSecondaryPressedThisFrame { get; }
        }

        private static IPointerSource s_override;

        public static void SetOverrideSource(IPointerSource source)
        {
            s_override = source;
        }

        public static void ClearOverrideSource()
        {
            s_override = null;
        }

        public static Camera ResolveCamera(Camera preferred)
        {
            if (preferred != null)
            {
                return preferred;
            }

            return Camera.main;
        }

        public static bool TryGetPointerScreen(out Vector2 screen)
        {
            if (s_override != null && s_override.TryGetScreenPosition(out screen))
            {
                return true;
            }

#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse != null)
            {
                screen = mouse.position.ReadValue();
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            var legacy = Input.mousePosition;
            screen = new Vector2(legacy.x, legacy.y);
            return true;
#else
            screen = default;
            return false;
#endif
        }

        public static bool TryGetPointerWorld(Camera camera, out Vector3 worldPoint)
        {
            worldPoint = Vector3.zero;
            var cam = ResolveCamera(camera);
            if (cam == null)
            {
                return false;
            }

            if (!TryGetPointerScreen(out var screen))
            {
                return false;
            }

            worldPoint = cam.ScreenToWorldPoint(
                new Vector3(screen.x, screen.y, Mathf.Abs(cam.transform.position.z)));
            worldPoint.z = 0f;
            return true;
        }

        public static bool TryGetPointerWorldOnPlane(Camera camera, float planeZ, out Vector3 worldPoint)
        {
            worldPoint = Vector3.zero;
            var cam = ResolveCamera(camera);
            if (cam == null)
            {
                return false;
            }

            if (!TryGetPointerScreen(out var screen))
            {
                return false;
            }

            worldPoint = cam.ScreenToWorldPoint(
                new Vector3(screen.x, screen.y, Mathf.Abs(cam.transform.position.z - planeZ)));
            worldPoint.z = planeZ;
            return true;
        }

        public static bool WasPrimaryPressedThisFrame()
        {
            if (s_override != null)
            {
                return s_override.WasPrimaryPressedThisFrame;
            }

#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse != null)
            {
                return mouse.leftButton.wasPressedThisFrame;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(0);
#else
            return false;
#endif
        }

        public static bool WasPrimaryReleasedThisFrame()
        {
            if (s_override != null)
            {
                return s_override.WasPrimaryReleasedThisFrame;
            }

#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse != null)
            {
                return mouse.leftButton.wasReleasedThisFrame;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonUp(0);
#else
            return false;
#endif
        }

        public static bool IsPrimaryHeld()
        {
            if (s_override != null)
            {
                return s_override.IsPrimaryHeld;
            }

#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse != null)
            {
                return mouse.leftButton.isPressed;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButton(0);
#else
            return false;
#endif
        }

        public static bool WasSecondaryPressedThisFrame()
        {
            if (s_override != null)
            {
                return s_override.WasSecondaryPressedThisFrame;
            }

#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse != null)
            {
                return mouse.rightButton.wasPressedThisFrame;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(1);
#else
            return false;
#endif
        }

        /// <summary>
        /// 在 collider 自身平面上做 Overlap（与 PointerHitRouter 同换算）；
        /// 替代已退役的 z=0 <c>TryPickCollider</c>（ADR-0023 / #103）。
        /// </summary>
        public static bool TryOverlapColliderOnPlane(Camera camera, Collider2D collider)
        {
            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
            {
                return false;
            }

            var planeZ = collider.bounds.center.z;
            if (!TryGetPointerWorldOnPlane(camera, planeZ, out var world))
            {
                return false;
            }

            return collider.OverlapPoint(world);
        }
    }
}
