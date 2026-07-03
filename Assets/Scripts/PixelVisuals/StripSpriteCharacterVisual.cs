using UnityEngine;

namespace NineGrid.Presentation.Visuals
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(Animator))]
    public sealed class StripSpriteCharacterVisual : MonoBehaviour
    {
        [SerializeField] private StripSpriteVisualCatalog catalog;
        [SerializeField] private string visualId = "spr_ship_1_strip9";

        private SpriteRenderer spriteRenderer;
        private Animator animator;

        public StripSpriteVisualCatalog Catalog => catalog;
        public string VisualId => visualId;

        private void Awake()
        {
            CacheComponents();
            ApplyVisual();
        }

        private void OnValidate()
        {
            CacheComponents();
            ApplyVisual();
        }

        public void SetCatalog(StripSpriteVisualCatalog newCatalog)
        {
            catalog = newCatalog;
            ApplyVisual();
        }

        public void SetVisual(string newVisualId)
        {
            visualId = newVisualId;
            ApplyVisual();
        }

        public void ApplyVisual()
        {
            if (catalog == null || string.IsNullOrEmpty(visualId))
            {
                return;
            }

            CacheComponents();

            if (!catalog.TryGetEntry(visualId, out var entry) || entry.AnimatorController == null)
            {
                return;
            }

            animator.runtimeAnimatorController = entry.AnimatorController;
            animator.Rebind();
            animator.Update(0f);

            if (entry.PreviewSprite != null)
            {
                spriteRenderer.sprite = entry.PreviewSprite;
            }
        }

        private void CacheComponents()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }
        }
    }
}
