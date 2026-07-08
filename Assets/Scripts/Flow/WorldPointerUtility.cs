using UnityEngine;

namespace NineGrid.Flow
{
    /// <summary>
    /// 世界空间点选工具：鼠标 → 世界坐标 / Collider2D 命中。
    /// </summary>
    public static class WorldPointerUtility
    {
        public static Camera ResolveCamera(Camera preferred)
        {
            if (preferred != null)
            {
                return preferred;
            }

            return Camera.main;
        }

        public static bool TryGetPointerWorld(Camera camera, out Vector3 worldPoint)
        {
            worldPoint = Vector3.zero;
            var cam = ResolveCamera(camera);
            if (cam == null)
            {
                return false;
            }

            var screen = Input.mousePosition;
            worldPoint = cam.ScreenToWorldPoint(
                new Vector3(screen.x, screen.y, Mathf.Abs(cam.transform.position.z)));
            worldPoint.z = 0f;
            return true;
        }

        public static bool WasPrimaryPressedThisFrame()
        {
            return Input.GetMouseButtonDown(0);
        }

        public static bool TryPickCollider(Camera camera, Collider2D collider)
        {
            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (!TryGetPointerWorld(camera, out var world))
            {
                return false;
            }

            return collider.OverlapPoint(world);
        }
    }
}
