using UnityEngine;

namespace NineGrid.VisualLook
{
    /// <summary>
    /// 把 Screen Space Camera 下的 RectTransform 跟随到世界目标的屏幕投影点。
    /// 默认在进入 Play 时捕获编辑器里已摆好的相对偏移，避免「绑完就飞、再手调 offset」。
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

            [Tooltip("相对目标中心的世界空间额外偏移（在自动捕获的编辑器偏移之上叠加）。一般保持 0。")]
            public Vector3 worldOffset;

            [Tooltip("关闭则跳过该绑定。")]
            public bool enabled = true;

            [System.NonSerialized]
            public Vector2 runtimeLocalOffset;

            [System.NonSerialized]
            public bool runtimeOffsetCaptured;
        }

        [SerializeField]
        [Tooltip("投影用相机；留空则运行时用 Canvas.worldCamera 或 Camera.main。")]
        Camera targetCamera;

        [SerializeField]
        [Tooltip("文字所在 Canvas；留空则运行时 GetComponent。")]
        Canvas canvas;

        [SerializeField]
        [Tooltip("文字 ↔ 世界目标绑定列表。在编辑器摆好相对位置后，只需指定 text 与 worldTarget。")]
        Binding[] bindings;

        [SerializeField]
        [Tooltip("进入 Play 时自动捕获当前文字相对目标的父节点局部偏移，保证所见即所得。")]
        bool captureAuthoredOffsetOnPlay = true;

        void Awake()
        {
            if (canvas == null)
            {
                canvas = GetComponent<Canvas>();
            }
        }

        void OnDisable()
        {
            ClearCapturedOffsets();
        }

        [ContextMenu("Recapture Authored Offsets Now")]
        public void RecaptureAuthoredOffsets()
        {
            ClearCapturedOffsets();
            if (!TryResolveCamera(out var cam) || canvas == null)
            {
                return;
            }

            CaptureAllOffsets(cam);
            ApplyAll(cam);
        }

        void LateUpdate()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (bindings == null || bindings.Length == 0)
            {
                return;
            }

            if (!TryResolveCamera(out var cam) || canvas == null)
            {
                return;
            }

            if (captureAuthoredOffsetOnPlay && !AllOffsetsCaptured())
            {
                CaptureAllOffsets(cam);
            }

            ApplyAll(cam);
        }

        bool TryResolveCamera(out Camera cam)
        {
            cam = targetCamera;
            if (cam == null && canvas != null)
            {
                cam = canvas.worldCamera;
            }

            if (cam == null)
            {
                cam = Camera.main;
            }

            return cam != null;
        }

        bool AllOffsetsCaptured()
        {
            for (int i = 0; i < bindings.Length; i++)
            {
                var b = bindings[i];
                if (b == null || !b.enabled || b.text == null || b.worldTarget == null)
                {
                    continue;
                }

                if (!b.runtimeOffsetCaptured)
                {
                    return false;
                }
            }

            return true;
        }

        void ClearCapturedOffsets()
        {
            if (bindings == null)
            {
                return;
            }

            for (int i = 0; i < bindings.Length; i++)
            {
                var b = bindings[i];
                if (b == null)
                {
                    continue;
                }

                b.runtimeOffsetCaptured = false;
                b.runtimeLocalOffset = Vector2.zero;
            }
        }

        void CaptureAllOffsets(Camera cam)
        {
            for (int i = 0; i < bindings.Length; i++)
            {
                var b = bindings[i];
                if (b == null || !b.enabled || b.text == null || b.worldTarget == null)
                {
                    continue;
                }

                if (!TryProjectToParent(cam, b.text, b.worldTarget.position, out var targetLocal))
                {
                    continue;
                }

                // 用当前编辑器摆位相对「目标中心投影」的差，作为跟随偏移。
                // 不把 worldOffset 算进捕获，这样 worldOffset 仍可作运行时微调。
                var currentLocal = (Vector2)b.text.localPosition;
                b.runtimeLocalOffset = currentLocal - targetLocal;
                b.runtimeOffsetCaptured = true;
            }
        }

        void ApplyAll(Camera cam)
        {
            for (int i = 0; i < bindings.Length; i++)
            {
                var b = bindings[i];
                if (b == null || !b.enabled || b.text == null || b.worldTarget == null)
                {
                    continue;
                }

                Vector3 world = b.worldTarget.position + b.worldOffset;
                if (!TryProjectToParent(cam, b.text, world, out var targetLocal))
                {
                    continue;
                }

                Vector2 follow = captureAuthoredOffsetOnPlay && b.runtimeOffsetCaptured
                    ? b.runtimeLocalOffset
                    : Vector2.zero;

                SetLocalXY(b.text, targetLocal + follow);
            }
        }

        bool TryProjectToParent(Camera cam, RectTransform text, Vector3 world, out Vector2 localInParent)
        {
            localInParent = default;
            var parent = text.parent as RectTransform;
            if (parent == null)
            {
                return false;
            }

            Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, world);
            Camera eventCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cam;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parent,
                screen,
                eventCam,
                out localInParent);
        }

        static void SetLocalXY(RectTransform text, Vector2 local)
        {
            var lp = text.localPosition;
            text.localPosition = new Vector3(local.x, local.y, lp.z);
        }
    }
}
