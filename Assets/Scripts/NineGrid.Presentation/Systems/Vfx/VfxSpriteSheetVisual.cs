using UnityEngine;

namespace NineGrid.Presentation.Systems.Vfx
{
    /// <summary>池化精灵表实例视图：只改本节点 SpriteRenderer，不碰共享相机 / Canvas / 卡级 SortingGroup。</summary>
    [DisallowMultipleComponent]
    internal sealed class VfxSpriteSheetVisual : MonoBehaviour
    {
        private SpriteRenderer mRenderer;

        public SpriteRenderer Renderer
        {
            get
            {
                EnsureRenderer();
                return mRenderer;
            }
        }

        public void EnsureRenderer()
        {
            if (mRenderer != null)
            {
                return;
            }

            mRenderer = GetComponent<SpriteRenderer>();
            if (mRenderer == null)
            {
                mRenderer = gameObject.AddComponent<SpriteRenderer>();
            }
        }

        public void ResetVisual()
        {
            EnsureRenderer();
            mRenderer.sprite = null;
            mRenderer.color = Color.white;
            mRenderer.enabled = false;
            mRenderer.maskInteraction = SpriteMaskInteraction.None;
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }
    }
}
