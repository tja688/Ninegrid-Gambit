using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Pixel Snap - 运行时像素吸附组件
/// 将摄像机与 SpriteRenderer 的位置吸附到像素网格，消除亚像素抖动与闪烁。
/// 挂载到 Main Camera，需匹配 PixelPerfectCamera 的 PPU 设置。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class PixelSnap : MonoBehaviour
{
    [Header("Pixel Grid")]
    [Tooltip("每单位像素数，须与 PixelPerfectCamera Assets PPU 一致")]
    [SerializeField] private int pixelsPerUnit = 32;

    [Header("Snap Targets")]
    [Tooltip("吸附摄像机 XY 位置到像素网格（保留 Z 不变）")]
    [SerializeField] private bool snapCamera = true;
    [Tooltip("吸附所有 SpriteRenderer 位置到像素网格")]
    [SerializeField] private bool snapSprites = true;

    [Header("Debug")]
    [Tooltip("在 Scene/Game 视图绘制像素网格辅助线")]
    [SerializeField] private bool drawDebugGrid;
    [SerializeField] private Color gridColor = new Color(1f, 1f, 1f, 0.08f);
    [SerializeField] private int gridExtent = 20;

    private Camera _cam;
    private readonly List<SpriteRenderer> _sprites = new List<SpriteRenderer>();

    private float Unit => 1f / pixelsPerUnit;

    private void Awake()
    {
        _cam = GetComponent<Camera>();
    }

    private void LateUpdate()
    {
        float unit = Unit;

        if (snapCamera)
        {
            var p = transform.position;
            p.x = Mathf.Round(p.x / unit) * unit;
            p.y = Mathf.Round(p.y / unit) * unit;
            // Z 保持不变
            transform.position = p;
        }

        if (snapSprites)
        {
            // 每帧重新收集，以包含运行时新增/激活的 SpriteRenderer
            GetComponentsInChildren(true, _sprites);

            for (int i = 0; i < _sprites.Count; i++)
            {
                var sr = _sprites[i];
                var p = sr.transform.position;
                p.x = Mathf.Round(p.x / unit) * unit;
                p.y = Mathf.Round(p.y / unit) * unit;
                p.z = Mathf.Round(p.z / unit) * unit;
                sr.transform.position = p;
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (!drawDebugGrid) return;

        var cam = _cam != null ? _cam : GetComponent<Camera>();
        if (cam == null) return;

        float unit = 1f / pixelsPerUnit;
        var center = cam.transform.position;

        Gizmos.color = gridColor;

        float startX = (Mathf.Floor(center.x / unit) - gridExtent) * unit;
        float endX   = (Mathf.Ceil(center.x / unit) + gridExtent) * unit;
        float startY = (Mathf.Floor(center.y / unit) - gridExtent) * unit;
        float endY   = (Mathf.Ceil(center.y / unit) + gridExtent) * unit;
        float z = center.z + 1f;

        // 竖线
        for (float x = startX; x <= endX; x += unit)
            Gizmos.DrawLine(new Vector3(x, startY, z), new Vector3(x, endY, z));

        // 横线
        for (float y = startY; y <= endY; y += unit)
            Gizmos.DrawLine(new Vector3(startX, y, z), new Vector3(endX, y, z));
    }
}
