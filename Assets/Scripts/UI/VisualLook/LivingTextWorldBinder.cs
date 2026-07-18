using UnityEngine;

namespace NineGrid.VisualLook
{
    /// <summary>
    /// 把 Screen Space Camera 下的 RectTransform 对齐到世界目标（Sprite/Transform）的屏幕投影点。
    /// 用于证明文字已回到同一相机域后，可以跟随灵动背板，而不再依赖脱离世界的 Overlay 槽位。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LivingTextWorldBinder : MonoBehaviour
    {
        [System.Serializable]
        public sealed class Binding
        {
            [Tooltip("手动装配：要跟随的 TMP/UI RectTransform。")]
            public RectTransform text;

            [Tooltip("手动装配：世界侧目标（面板 SpriteRenderer 所在物体等）。")]
            public Transform worldTarget;

            [Tooltip("相对目标中心的世界空间偏移。")]
            public Vector3 worldOffset;

            [Tooltip("关闭则跳过该绑定。")]
            public bool enabled = true;
        }

        [SerializeField]
        [Tooltip("投影用相机；留空则运行时用 Canvas.worldCamera 或 Camera.main。")]
        Camera targetCamera;

        [SerializeField]
        [Tooltip("文字所在 Canvas；留空则运行时 GetComponent。")]
        Canvas canvas;

        [SerializeField]
        [Tooltip("文字 ↔ 世界目标绑定列表。")]
        Binding[] bindings;

        void Awake()
        {
            if (canvas == null)
            {
                canvas = GetComponent<Canvas>();
            }
        }

        void LateUpdate()
        {
            if (bindings == null || bindings.Length == 0)
            {
                return;
            }

            var cam = targetCamera;
            if (cam == null && canvas != null)
            {
                cam = canvas.worldCamera;
            }

            if (cam == null)
            {
                cam = Camera.main;
            }

            if (cam == null || canvas == null)
            {
                return;
            }

            var canvasRect = canvas.transform as RectTransform;
            for (int i = 0; i < bindings.Length; i++)
            {
                var b = bindings[i];
                if (b == null || !b.enabled || b.text == null || b.worldTarget == null)
                {
                    continue;
                }

                Vector3 world = b.worldTarget.position + b.worldOffset;
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, world);
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        canvasRect,
                        screen,
                        canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cam,
                        out var local))
                {
                    b.text.anchoredPosition = local;
                }
            }
        }
    }
}
