using UnityEngine;

namespace NineGrid.Flow.Tutorial
{
    /// <summary>
    /// 教学挖洞半黑屏：复制场景「半黑屏BG」的视觉，用 SpriteMask 在框选处挖洞，
    /// 并随教学提示框呼吸缩放亮区。不 Acquire 局内 <see cref="BattleUiDimmerOverlay"/>，无 collider，不挡输入。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TutorialSpotlightPresenter : MonoBehaviour
    {
        public const string SpotlightObjectName = "教学挖洞遮罩";
        public const string MaskObjectName = "教学挖洞Mask";
        public const string SourceDimmerObjectName = "半黑屏BG";
        public const string PanelObjectName = "场景UI面板";

        private static TutorialSpotlightPresenter sInstance;
        private static Sprite sFallbackMaskSprite;

        [SerializeField] private GameObject spotlightRoot;
        [SerializeField] private SpriteRenderer dimmerRenderer;
        [SerializeField] private SpriteMask holeMask;

        private bool mIsActive;
        private Bounds mCurrentBounds;

        public static TutorialSpotlightPresenter InstanceOrNull()
        {
            if (sInstance != null)
            {
                return sInstance;
            }

            sInstance = FindFirstObjectByType<TutorialSpotlightPresenter>(FindObjectsInactive.Include);
            return sInstance;
        }

        public static TutorialSpotlightPresenter EnsureExists()
        {
            var existing = InstanceOrNull();
            if (existing != null)
            {
                existing.EnsureBindings();
                return existing;
            }

            var parent = FindPanelRoot();
            var go = new GameObject(SpotlightObjectName);
            if (parent != null)
            {
                go.transform.SetParent(parent.transform, false);
            }

            var presenter = go.AddComponent<TutorialSpotlightPresenter>();
            presenter.spotlightRoot = go;
            presenter.EnsureBindings();
            sInstance = presenter;
            go.SetActive(false);
            return presenter;
        }

        public static void Show(Bounds worldBounds)
        {
            var presenter = EnsureExists();
            presenter.ShowInternal(worldBounds);
        }

        public static void Hide()
        {
            InstanceOrNull()?.HideInternal();
        }

        public static void SyncBreathFromPrompt(TutorialPromptBoxPresenter prompt)
        {
            if (prompt == null)
            {
                return;
            }

            var presenter = InstanceOrNull();
            if (presenter == null || !presenter.mIsActive)
            {
                return;
            }

            presenter.ApplyHole(prompt.CurrentBounds, prompt.CurrentBreathSize);
        }

        public static bool IsSpotlightVisible => InstanceOrNull()?.IsVisible ?? false;

        public static void ResetForTests()
        {
            if (sInstance == null)
            {
                return;
            }

            var go = sInstance.gameObject;
            sInstance.HideInternal();
            sInstance = null;
            if (go != null)
            {
                Object.DestroyImmediate(go);
            }
        }

        public bool IsVisible => mIsActive && spotlightRoot != null && spotlightRoot.activeSelf;

        public Bounds CurrentBounds => mCurrentBounds;

        private void Awake()
        {
            EnsureBindings();
            if (gameObject.name == SpotlightObjectName)
            {
                sInstance = this;
            }
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(sInstance, this))
            {
                sInstance = null;
            }
        }

        private void Update()
        {
            if (!mIsActive)
            {
                return;
            }

            var prompt = TutorialPromptBoxPresenter.InstanceOrNull();
            if (prompt != null && prompt.IsVisible)
            {
                ApplyHole(prompt.CurrentBounds, prompt.CurrentBreathSize);
            }
        }

        private void ShowInternal(Bounds worldBounds)
        {
            EnsureBindings();
            mIsActive = true;
            mCurrentBounds = worldBounds;

            if (spotlightRoot != null && !spotlightRoot.activeSelf)
            {
                spotlightRoot.SetActive(true);
            }

            var prompt = TutorialPromptBoxPresenter.InstanceOrNull();
            var holeSize = prompt != null && prompt.IsVisible
                ? prompt.CurrentBreathSize
                : new Vector2(
                    Mathf.Max(0.5f, worldBounds.size.x),
                    Mathf.Max(0.5f, worldBounds.size.y));
            ApplyHole(worldBounds, holeSize);
        }

        private void HideInternal()
        {
            mIsActive = false;
            if (spotlightRoot != null && spotlightRoot.activeSelf)
            {
                spotlightRoot.SetActive(false);
            }
        }

        public void EnsureBindings()
        {
            if (spotlightRoot == null)
            {
                spotlightRoot = gameObject;
            }

            if (dimmerRenderer == null)
            {
                dimmerRenderer = spotlightRoot.GetComponent<SpriteRenderer>();
                if (dimmerRenderer == null)
                {
                    dimmerRenderer = spotlightRoot.AddComponent<SpriteRenderer>();
                }
            }

            CopyVisualFromSceneDimmer();
            EnsureHoleMask();
            StripColliders(spotlightRoot);
        }

        private void CopyVisualFromSceneDimmer()
        {
            var source = FindSceneDimmerRenderer();
            if (source != null)
            {
                dimmerRenderer.sprite = source.sprite;
                dimmerRenderer.color = source.color;
                dimmerRenderer.sharedMaterial = source.sharedMaterial;
                dimmerRenderer.sortingLayerID = source.sortingLayerID;
                dimmerRenderer.sortingOrder = source.sortingOrder;
                dimmerRenderer.drawMode = SpriteDrawMode.Simple;

                var srcTransform = source.transform;
                var dstTransform = spotlightRoot.transform;
                if (srcTransform.parent == dstTransform.parent)
                {
                    dstTransform.localPosition = srcTransform.localPosition;
                    dstTransform.localRotation = srcTransform.localRotation;
                    dstTransform.localScale = srcTransform.localScale;
                }
                else
                {
                    dstTransform.position = srcTransform.position;
                    dstTransform.rotation = srcTransform.rotation;
                    var parentScale = dstTransform.parent != null
                        ? dstTransform.parent.lossyScale
                        : Vector3.one;
                    dstTransform.localScale = new Vector3(
                        parentScale.x > 0.001f ? srcTransform.lossyScale.x / parentScale.x : srcTransform.lossyScale.x,
                        parentScale.y > 0.001f ? srcTransform.lossyScale.y / parentScale.y : srcTransform.lossyScale.y,
                        parentScale.z > 0.001f ? srcTransform.lossyScale.z / parentScale.z : srcTransform.lossyScale.z);
                }
            }
            else
            {
                dimmerRenderer.sprite = GetFallbackMaskSprite();
                dimmerRenderer.color = new Color(0f, 0f, 0f, 0.95f);
                dimmerRenderer.sortingOrder = -1;
                spotlightRoot.transform.position = Vector3.zero;
                spotlightRoot.transform.localScale = new Vector3(40f, 24f, 1f);
            }

            dimmerRenderer.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;
        }

        private void EnsureHoleMask()
        {
            if (holeMask == null)
            {
                var maskTransform = spotlightRoot.transform.Find(MaskObjectName);
                GameObject maskGo;
                if (maskTransform != null)
                {
                    maskGo = maskTransform.gameObject;
                }
                else
                {
                    maskGo = new GameObject(MaskObjectName);
                    maskGo.transform.SetParent(spotlightRoot.transform, false);
                }

                holeMask = maskGo.GetComponent<SpriteMask>();
                if (holeMask == null)
                {
                    holeMask = maskGo.AddComponent<SpriteMask>();
                }
            }

            holeMask.sprite = dimmerRenderer != null && dimmerRenderer.sprite != null
                ? dimmerRenderer.sprite
                : GetFallbackMaskSprite();
            holeMask.alphaCutoff = 0.1f;
            holeMask.isCustomRangeActive = true;
            holeMask.frontSortingLayerID = dimmerRenderer.sortingLayerID;
            holeMask.backSortingLayerID = dimmerRenderer.sortingLayerID;
            holeMask.frontSortingOrder = dimmerRenderer.sortingOrder;
            holeMask.backSortingOrder = dimmerRenderer.sortingOrder;
            StripColliders(holeMask.gameObject);
        }

        private void ApplyHole(Bounds worldBounds, Vector2 holeSize)
        {
            mCurrentBounds = worldBounds;
            if (holeMask == null)
            {
                return;
            }

            var z = holeMask.transform.position.z;
            holeMask.transform.position = new Vector3(worldBounds.center.x, worldBounds.center.y, z);

            var spriteSize = holeMask.sprite != null ? holeMask.sprite.bounds.size : Vector3.one;
            var sx = spriteSize.x > 0.001f ? holeSize.x / spriteSize.x : holeSize.x;
            var sy = spriteSize.y > 0.001f ? holeSize.y / spriteSize.y : holeSize.y;

            // Mask 是遮罩根的子节点；根节点已按半黑屏全屏缩放，须用世界尺寸反推本地 scale。
            var parent = holeMask.transform.parent;
            var parentScale = parent != null ? parent.lossyScale : Vector3.one;
            var localX = parentScale.x > 0.001f ? sx / parentScale.x : sx;
            var localY = parentScale.y > 0.001f ? sy / parentScale.y : sy;
            holeMask.transform.localScale = new Vector3(localX, localY, 1f);
        }

        private static void StripColliders(GameObject go)
        {
            if (go == null)
            {
                return;
            }

            var colliders = go.GetComponents<Collider2D>();
            for (var i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = false;
                }
            }
        }

        private static SpriteRenderer FindSceneDimmerRenderer()
        {
            var overlay = BattleUiDimmerOverlay.InstanceOrNull();
            if (overlay != null)
            {
                var renderer = overlay.GetComponent<SpriteRenderer>();
                if (renderer != null)
                {
                    return renderer;
                }
            }

            var dimmer = FindNamedSceneObject(SourceDimmerObjectName);
            return dimmer != null ? dimmer.GetComponent<SpriteRenderer>() : null;
        }

        private static GameObject FindPanelRoot()
        {
            return FindNamedSceneObject(PanelObjectName);
        }

        private static GameObject FindNamedSceneObject(string objectName)
        {
            var all = Resources.FindObjectsOfTypeAll<Transform>();
            for (var i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (t == null || t.name != objectName || !t.gameObject.scene.IsValid())
                {
                    continue;
                }

                return t.gameObject;
            }

            return GameObject.Find(objectName);
        }

        private static Sprite GetFallbackMaskSprite()
        {
            if (sFallbackMaskSprite != null)
            {
                return sFallbackMaskSprite;
            }

            var texture = Texture2D.whiteTexture;
            sFallbackMaskSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                1f);
            return sFallbackMaskSprite;
        }
    }
}
