using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.Presentation.Visuals
{
    /// <summary>
    /// 可被鼠标悬停高亮的场景元素（如厂房、船坞）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SelectableSceneElement : MonoBehaviour
    {
        static readonly int HitFlashAmountId = Shader.PropertyToID("_HitFlashAmount");
        static readonly int HitFlashColorId = Shader.PropertyToID("_HitFlashColor");
        static readonly List<Vector2> PhysicsShapePath = new List<Vector2>(64);

        [SerializeField] string elementId;
        [SerializeField] Color hoverFlashColor = new Color(1f, 0.92f, 0.55f, 1f);
        [SerializeField] [Range(0f, 1f)] float hoverFlashAmount = 0.4f;
        [SerializeField] bool ensurePhysicsCollider = true;

        SpriteRenderer spriteRenderer;
        Collider2D hitCollider;
        MaterialPropertyBlock propertyBlock;
        Color baseColor;
        bool supportsHitFlash;
        bool hovered;

        public string ElementId => string.IsNullOrEmpty(elementId) ? name : elementId;
        public SpriteRenderer SpriteRenderer => spriteRenderer;
        public int SortingOrder => spriteRenderer != null ? spriteRenderer.sortingOrder : 0;
        public bool IsHovered => hovered;

        void Awake()
        {
            CacheComponents();
            EnsureHitCollider();
            ApplyVisual();
        }

        void OnEnable()
        {
            SceneElementPointerSelector.Register(this);
        }

        void OnDisable()
        {
            SceneElementPointerSelector.Unregister(this);
            hovered = false;
            ApplyVisual();
        }

        void OnValidate()
        {
            CacheComponents();
        }

        public void SetHovered(bool value)
        {
            if (hovered == value)
            {
                return;
            }

            hovered = value;
            ApplyVisual();
        }

        public bool ContainsWorldPoint(Vector2 worldPoint)
        {
            if (hitCollider == null)
            {
                hitCollider = GetComponent<Collider2D>();
            }

            return hitCollider != null && hitCollider.enabled && hitCollider.OverlapPoint(worldPoint);
        }

        void CacheComponents()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (spriteRenderer == null)
            {
                return;
            }

            baseColor = spriteRenderer.color;
            propertyBlock ??= new MaterialPropertyBlock();
            supportsHitFlash = HasHitFlashProperties(spriteRenderer.sharedMaterial);
            hitCollider = GetComponent<Collider2D>();
        }

        void EnsureHitCollider()
        {
            if (!ensurePhysicsCollider)
            {
                return;
            }

            hitCollider = GetComponent<Collider2D>();
            if (hitCollider != null)
            {
                hitCollider.isTrigger = true;
                return;
            }

            var sprite = spriteRenderer != null ? spriteRenderer.sprite : null;
            if (sprite == null)
            {
                return;
            }

            int shapeCount = sprite.GetPhysicsShapeCount();
            if (shapeCount <= 0)
            {
                var box = gameObject.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
                hitCollider = box;
                return;
            }

            var polygon = gameObject.AddComponent<PolygonCollider2D>();
            polygon.isTrigger = true;
            polygon.pathCount = shapeCount;
            for (int i = 0; i < shapeCount; i++)
            {
                PhysicsShapePath.Clear();
                sprite.GetPhysicsShape(i, PhysicsShapePath);
                polygon.SetPath(i, PhysicsShapePath);
            }

            hitCollider = polygon;
        }

        void ApplyVisual()
        {
            if (spriteRenderer == null)
            {
                CacheComponents();
            }

            if (spriteRenderer == null)
            {
                return;
            }

            float flashAmount = hovered ? hoverFlashAmount : 0f;

            if (supportsHitFlash)
            {
                spriteRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(HitFlashColorId, hoverFlashColor);
                propertyBlock.SetFloat(HitFlashAmountId, flashAmount);
                spriteRenderer.SetPropertyBlock(propertyBlock);
                spriteRenderer.color = baseColor;
            }
            else
            {
                spriteRenderer.color = Color.Lerp(baseColor, hoverFlashColor, flashAmount);
            }
        }

        static bool HasHitFlashProperties(Material material)
        {
            return material != null
                   && material.HasProperty(HitFlashAmountId)
                   && material.HasProperty(HitFlashColorId);
        }
    }
}
