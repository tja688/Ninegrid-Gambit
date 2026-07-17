using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.Flow.Editor.LivingUi
{
    /// <summary>
    /// 读写「已切片」面板载体的世界轴对齐矩形（SpriteRenderer DrawMode.Sliced/Tiled + size）。
    /// </summary>
    internal static class LivingUiSlicedRectUtil
    {
        public static bool IsSlicedCarrier(GameObject go, out SpriteRenderer renderer)
        {
            renderer = null;
            if (go == null)
            {
                return false;
            }

            renderer = go.GetComponent<SpriteRenderer>();
            return IsSlicedCarrier(renderer);
        }

        public static bool IsSlicedCarrier(SpriteRenderer renderer)
        {
            if (renderer == null || renderer.sprite == null)
            {
                return false;
            }

            return renderer.drawMode == SpriteDrawMode.Sliced
                   || renderer.drawMode == SpriteDrawMode.Tiled;
        }

        public static Rect GetWorldRect(SpriteRenderer renderer)
        {
            var size = renderer.size;
            var center = renderer.transform.position;
            return new Rect(center.x - size.x * 0.5f, center.y - size.y * 0.5f, size.x, size.y);
        }

        public static void SetWorldRect(SpriteRenderer renderer, Rect rect, bool pixelSnap, float pixelsPerUnit)
        {
            if (pixelSnap && pixelsPerUnit > 0f)
            {
                rect = SnapRect(rect, pixelsPerUnit);
            }

            var transform = renderer.transform;
            var center = new Vector3(rect.center.x, rect.center.y, transform.position.z);
            transform.position = center;
            renderer.size = new Vector2(Mathf.Max(0.0001f, rect.width), Mathf.Max(0.0001f, rect.height));
        }

        public static Rect SnapRect(Rect rect, float pixelsPerUnit)
        {
            var unit = 1f / pixelsPerUnit;
            var xMin = Snap(rect.xMin, unit);
            var yMin = Snap(rect.yMin, unit);
            var xMax = Snap(rect.xMax, unit);
            var yMax = Snap(rect.yMax, unit);
            return Rect.MinMaxRect(xMin, yMin, Mathf.Max(xMin + unit, xMax), Mathf.Max(yMin + unit, yMax));
        }

        public static float Snap(float value, float unit)
        {
            return Mathf.Round(value / unit) * unit;
        }

        public static IReadOnlyList<SpriteRenderer> CollectSlicedFromSelection(IEnumerable<GameObject> selection)
        {
            var list = new List<SpriteRenderer>();
            var seen = new HashSet<int>();
            foreach (var go in selection)
            {
                if (go == null)
                {
                    continue;
                }

                if (IsSlicedCarrier(go, out var renderer) && seen.Add(renderer.GetInstanceID()))
                {
                    list.Add(renderer);
                    continue;
                }

                foreach (var child in go.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (IsSlicedCarrier(child) && seen.Add(child.GetInstanceID()))
                    {
                        list.Add(child);
                    }
                }
            }

            return list;
        }

        public static Rect OrthographicCameraWorldRect(Camera camera)
        {
            if (camera == null || !camera.orthographic)
            {
                return new Rect(-7.5f, -4.21875f, 15f, 8.4375f);
            }

            var halfH = camera.orthographicSize;
            var halfW = halfH * camera.aspect;
            var c = camera.transform.position;
            return new Rect(c.x - halfW, c.y - halfH, halfW * 2f, halfH * 2f);
        }
    }
}
