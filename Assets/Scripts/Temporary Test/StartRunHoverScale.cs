using Unity.Pipeline.HotReload;
using UnityEngine;

namespace NineGrid.TemporaryTest
{
    /// <summary>
    /// 临时热重载实验（override 工作流） FLOW_EDIT_MARKER 21:44:43：StartRun hover 放大。
    /// Editor 下 in-place [HotReload] 不会自动 RegisterReloadableMethod；改用官方 helper 路径。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StartRunHoverScale : MonoBehaviour
    {
        public float hoverScale = 1.2f;
        public Vector3 baseScale = Vector3.one;
        public bool hovering;

        private void Awake()
        {
            baseScale = transform.localScale;
            HotReloadRegistry.RegisterReloadableType(typeof(StartRunHoverScale));
        }

        [HotReloadWithOverrides]
        public void OnMouseEnter()
        {
            HotReloadHelper.ExecuteWithHotReload(this, "OnMouseEnter", OriginalOnMouseEnter);
        }

        public void OriginalOnMouseEnter()
        {
            hovering = true;
            transform.localScale = baseScale * hoverScale;
            Debug.Log($"[StartRunHoverScale] Enter original scale={hoverScale}");
        }

        [HotReloadWithOverrides]
        public void OnMouseExit()
        {
            HotReloadHelper.ExecuteWithHotReload(this, "OnMouseExit", OriginalOnMouseExit);
        }

        public void OriginalOnMouseExit()
        {
            hovering = false;
            transform.localScale = baseScale;
            Debug.Log("[StartRunHoverScale] Exit original");
        }
    }
}
