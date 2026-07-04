using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.Presentation.Visuals
{
    public enum SelectionVisualMode
    {
        Flash = 0,
        Outline = 1,
        FlashAndOutline = 2,
    }

    /// <summary>
    /// 可被鼠标悬停高亮的场景元素。
    /// Outline 模式用 8 向剪影偏移，适配 Animator 换帧的精灵。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SelectableSceneElement : MonoBehaviour
    {
        static readonly int HitFlashAmountId = Shader.PropertyToID("_HitFlashAmount");
        static readonly int HitFlashColorId = Shader.PropertyToID("_HitFlashColor");
        static readonly List<Vector2> PhysicsShapePath = new List<Vector2>(64);
        static readonly Vector2Int[] OutlineOffsets =
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
            new Vector2Int(1, 1),
            new Vector2Int(1, -1),
            new Vector2Int(-1, 1),
            new Vector2Int(-1, -1),
        };

        const string OutlineRootName = "__SelectionOutline";
        const string SilhouetteShaderName = "TableNine/SpriteSilhouette";

        [SerializeField] string elementId;
        [SerializeField] SelectionVisualMode visualMode = SelectionVisualMode.Flash;
        [SerializeField] Color hoverFlashColor = new Color(1f, 0.92f, 0.55f, 1f);
        [SerializeField] [Range(0f, 1f)] float hoverFlashAmount = 0.4f;
        [SerializeField] Color outlineColor = new Color(1f, 0.86f, 0.32f, 1f);
        [SerializeField] [Range(1, 3)] int outlineWidthPixels = 1;
        [SerializeField] bool pulseOutline = true;
        [SerializeField] float pulseSpeed = 2.6f;
        [SerializeField] [Range(0f, 1f)] float pulseMinAlpha = 0.72f;
        [SerializeField] [Range(0f, 1f)] float pulseMaxAlpha = 1f;
        [SerializeField] Material outlineMaterial;
        [SerializeField] bool ensurePhysicsCollider = true;

        SpriteRenderer spriteRenderer;
        Collider2D hitCollider;
        MaterialPropertyBlock propertyBlock;
        Color baseColor;
        bool supportsHitFlash;
        bool hovered;

        Transform outlineRoot;
        SpriteRenderer[] outlineRenderers;
        Material runtimeOutlineMaterial;
        float pulseTime;

        public string ElementId => string.IsNullOrEmpty(elementId) ? name : elementId;
        public SpriteRenderer SpriteRenderer => spriteRenderer;
        public int SortingOrder => spriteRenderer != null ? spriteRenderer.sortingOrder : 0;
        public bool IsHovered => hovered;

        void Awake()
        {
            CacheComponents();
            EnsureHitCollider();
            EnsureOutlineRenderers();
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

        void OnDestroy()
        {
            if (runtimeOutlineMaterial != null)
            {
                Destroy(runtimeOutlineMaterial);
                runtimeOutlineMaterial = null;
            }
        }

        void OnValidate()
        {
            CacheComponents();
            outlineWidthPixels = Mathf.Clamp(outlineWidthPixels, 1, 3);
            pulseMinAlpha = Mathf.Clamp01(pulseMinAlpha);
            pulseMaxAlpha = Mathf.Clamp01(Mathf.Max(pulseMaxAlpha, pulseMinAlpha));
        }

        void LateUpdate()
        {
            if (!hovered || !UsesOutline)
            {
                return;
            }

            SyncOutlineRenderers();
        }

        public void SetHovered(bool value)
        {
            if (hovered == value)
            {
                return;
            }

            hovered = value;
            if (hovered)
            {
                pulseTime = 0f;
            }

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

        bool UsesOutline =>
            visualMode == SelectionVisualMode.Outline
            || visualMode == SelectionVisualMode.FlashAndOutline;

        bool UsesFlash =>
            visualMode == SelectionVisualMode.Flash
            || visualMode == SelectionVisualMode.FlashAndOutline;

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

        void EnsureOutlineRenderers()
        {
            if (!UsesOutline || spriteRenderer == null)
            {
                return;
            }

            if (outlineRoot == null)
            {
                Transform existing = transform.Find(OutlineRootName);
                if (existing != null)
                {
                    outlineRoot = existing;
                }
                else
                {
                    var rootObject = new GameObject(OutlineRootName);
                    rootObject.transform.SetParent(transform, false);
                    rootObject.transform.localPosition = Vector3.zero;
                    rootObject.transform.localRotation = Quaternion.identity;
                    rootObject.transform.localScale = Vector3.one;
                    rootObject.hideFlags = HideFlags.DontSave;
                    outlineRoot = rootObject.transform;
                }
            }

            Material material = ResolveOutlineMaterial();
            int rendererCount = OutlineOffsets.Length * outlineWidthPixels;
            if (outlineRenderers != null && outlineRenderers.Length == rendererCount)
            {
                return;
            }

            for (int i = outlineRoot.childCount - 1; i >= 0; i--)
            {
                DestroyOutlineChild(outlineRoot.GetChild(i).gameObject);
            }

            outlineRenderers = new SpriteRenderer[rendererCount];
            int index = 0;
            for (int width = 1; width <= outlineWidthPixels; width++)
            {
                for (int i = 0; i < OutlineOffsets.Length; i++)
                {
                    var child = new GameObject($"Outline_{width}_{i}");
                    child.transform.SetParent(outlineRoot, false);
                    child.hideFlags = HideFlags.DontSave;

                    var renderer = child.AddComponent<SpriteRenderer>();
                    renderer.sharedMaterial = material;
                    renderer.color = outlineColor;
                    renderer.sortingLayerID = spriteRenderer.sortingLayerID;
                    renderer.sortingOrder = spriteRenderer.sortingOrder - 1;
                    renderer.maskInteraction = spriteRenderer.maskInteraction;
                    renderer.drawMode = SpriteDrawMode.Simple;
                    renderer.enabled = false;

                    outlineRenderers[index++] = renderer;
                }
            }
        }

        Material ResolveOutlineMaterial()
        {
            if (outlineMaterial != null)
            {
                return outlineMaterial;
            }

            if (runtimeOutlineMaterial != null)
            {
                return runtimeOutlineMaterial;
            }

            Shader shader = Shader.Find(SilhouetteShaderName);
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            }

            runtimeOutlineMaterial = new Material(shader)
            {
                name = "RuntimeSelectionOutline",
                hideFlags = HideFlags.HideAndDontSave,
            };
            return runtimeOutlineMaterial;
        }

        void SyncOutlineRenderers()
        {
            EnsureOutlineRenderers();
            if (outlineRenderers == null || spriteRenderer == null)
            {
                return;
            }

            Sprite sprite = spriteRenderer.sprite;
            if (sprite == null)
            {
                SetOutlineVisible(false);
                return;
            }

            float pixelSize = 1f / sprite.pixelsPerUnit;
            Color color = EvaluateOutlineColor();
            int index = 0;

            for (int width = 1; width <= outlineWidthPixels; width++)
            {
                float distance = pixelSize * width;
                for (int i = 0; i < OutlineOffsets.Length; i++)
                {
                    SpriteRenderer outlineRenderer = outlineRenderers[index++];
                    if (outlineRenderer == null)
                    {
                        continue;
                    }

                    Vector2Int offset = OutlineOffsets[i];
                    outlineRenderer.enabled = true;
                    outlineRenderer.sprite = sprite;
                    outlineRenderer.flipX = spriteRenderer.flipX;
                    outlineRenderer.flipY = spriteRenderer.flipY;
                    outlineRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
                    outlineRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;
                    outlineRenderer.color = color;
                    outlineRenderer.transform.localPosition = new Vector3(
                        offset.x * distance,
                        offset.y * distance,
                        0f);
                }
            }
        }

        Color EvaluateOutlineColor()
        {
            Color color = outlineColor;
            if (!pulseOutline)
            {
                return color;
            }

            pulseTime += Time.unscaledDeltaTime * pulseSpeed;
            float wave = (Mathf.Sin(pulseTime) + 1f) * 0.5f;
            color.a *= Mathf.Lerp(pulseMinAlpha, pulseMaxAlpha, wave);

            // 轻微冷暖呼吸，避免死板单色描边。
            Color warm = outlineColor;
            Color cool = Color.Lerp(outlineColor, Color.white, 0.35f);
            Color pulsed = Color.Lerp(warm, cool, wave * 0.45f);
            pulsed.a = color.a;
            return pulsed;
        }

        void SetOutlineVisible(bool visible)
        {
            if (outlineRoot != null)
            {
                outlineRoot.gameObject.SetActive(visible);
            }

            if (outlineRenderers == null)
            {
                return;
            }

            for (int i = 0; i < outlineRenderers.Length; i++)
            {
                if (outlineRenderers[i] != null)
                {
                    outlineRenderers[i].enabled = visible;
                }
            }
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

            float flashAmount = hovered && UsesFlash ? hoverFlashAmount : 0f;

            if (supportsHitFlash)
            {
                spriteRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(HitFlashColorId, hoverFlashColor);
                propertyBlock.SetFloat(HitFlashAmountId, flashAmount);
                spriteRenderer.SetPropertyBlock(propertyBlock);
                spriteRenderer.color = baseColor;
            }
            else if (UsesFlash)
            {
                spriteRenderer.color = Color.Lerp(baseColor, hoverFlashColor, flashAmount);
            }
            else
            {
                spriteRenderer.color = baseColor;
            }

            if (UsesOutline)
            {
                if (hovered)
                {
                    EnsureOutlineRenderers();
                    SetOutlineVisible(true);
                    SyncOutlineRenderers();
                }
                else
                {
                    SetOutlineVisible(false);
                }
            }
            else
            {
                SetOutlineVisible(false);
            }
        }

        static void DestroyOutlineChild(GameObject child)
        {
            if (child == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(child);
            }
            else
            {
                DestroyImmediate(child);
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
