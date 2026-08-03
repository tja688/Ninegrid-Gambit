using NineGrid.Flow;
using Unity.Pipeline.HotReload;
using UnityEngine;

namespace NineGrid.TemporaryTest
{
    /// <summary>
    /// 临时热重载实验（override 工作流）：StartRun hover 放大。
    /// 用指针平面 Overlap 轮询，不用 legacy OnMouse*（ADR-0023 / #103）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StartRunHoverScale : MonoBehaviour
    {
        public float hoverScale = 1.2f;
        public Vector3 baseScale = Vector3.one;
        public bool hovering;

        private Collider2D _collider;

        private void Awake()
        {
            baseScale = transform.localScale;
            _collider = GetComponent<Collider2D>();
            HotReloadRegistry.RegisterReloadableType(typeof(StartRunHoverScale));
        }

        private void Update()
        {
            var cam = Camera.main;
            if (cam == null || _collider == null)
            {
                return;
            }

            var over = WorldPointerUtility.TryOverlapColliderOnPlane(cam, _collider);
            if (over == hovering)
            {
                return;
            }

            if (over)
            {
                ApplyHoverEnter();
            }
            else
            {
                ApplyHoverExit();
            }
        }

        [HotReloadWithOverrides]
        public void ApplyHoverEnter()
        {
            HotReloadHelper.ExecuteWithHotReload(this, "ApplyHoverEnter", OriginalApplyHoverEnter);
        }

        public void OriginalApplyHoverEnter()
        {
            hovering = true;
            transform.localScale = baseScale * hoverScale;
            Debug.Log($"[StartRunHoverScale] Enter original scale={hoverScale}");
        }

        [HotReloadWithOverrides]
        public void ApplyHoverExit()
        {
            HotReloadHelper.ExecuteWithHotReload(this, "ApplyHoverExit", OriginalApplyHoverExit);
        }

        public void OriginalApplyHoverExit()
        {
            hovering = false;
            transform.localScale = baseScale;
            Debug.Log("[StartRunHoverScale] Exit original");
        }
    }
}
