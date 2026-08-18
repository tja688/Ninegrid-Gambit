using UnityEngine;

namespace NineGrid.Flow.Tutorial
{
    /// <summary>
    /// 教学挖洞半黑屏：复制场景「半黑屏BG」的颜色与 sorting，用四条遮罩条围出框选亮区，
    /// 并随教学提示框呼吸缩放。不走 SpriteMask（半黑屏自定义材质不参与 mask），
    /// 不 Acquire 局内 <see cref="BattleUiDimmerOverlay"/>，无 collider，不挡输入。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TutorialSpotlightPresenter : MonoBehaviour
    {
        public const string SpotlightObjectName = "教学挖洞遮罩";
        public const string SourceDimmerObjectName = "半黑屏BG";
        public const string PanelObjectName = "场景UI面板";

        private static readonly string[] BarNames = { "挖洞上", "挖洞下", "挖洞左", "挖洞右" };

        private static TutorialSpotlightPresenter sInstance;
        private static Sprite sFallbackSprite;

        [SerializeField] private GameObject spotlightRoot;
        [SerializeField] private SpriteRenderer[] holeBars = new SpriteRenderer[4];

        private bool mIsActive;
        private Bounds mCurrentBounds;
        private Color mDimmerColor = new Color(0f, 0f, 0f, 0.95f);
        private int mSortingLayerId;
        private int mSortingOrder = -1;
        private Sprite mBarSprite;

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

        public int HoleBarCount
        {
            get
            {
                var count = 0;
                if (holeBars == null)
                {
                    return 0;
                }

                for (var i = 0; i < holeBars.Length; i++)
                {
                    if (holeBars[i] != null)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

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

            spotlightRoot.transform.localScale = Vector3.one;
            CopyStyleFromSceneDimmer();
            EnsureBars();
            StripColliders(spotlightRoot);
        }

        private void CopyStyleFromSceneDimmer()
        {
            var source = FindSceneDimmerRenderer();
            if (source != null)
            {
                mDimmerColor = source.color;
                mSortingLayerId = source.sortingLayerID;
                mSortingOrder = source.sortingOrder;
                mBarSprite = source.sprite != null ? source.sprite : GetFallbackSprite();
            }
            else
            {
                mDimmerColor = new Color(0f, 0f, 0f, 0.95f);
                mSortingLayerId = 0;
                mSortingOrder = -1;
                mBarSprite = GetFallbackSprite();
            }
        }

        private void EnsureBars()
        {
            if (holeBars == null || holeBars.Length != 4)
            {
                holeBars = new SpriteRenderer[4];
            }

            for (var i = 0; i < 4; i++)
            {
                if (holeBars[i] == null)
                {
                    var existing = spotlightRoot.transform.Find(BarNames[i]);
                    GameObject barGo;
                    if (existing != null)
                    {
                        barGo = existing.gameObject;
                    }
                    else
                    {
                        barGo = new GameObject(BarNames[i]);
                        barGo.transform.SetParent(spotlightRoot.transform, false);
                    }

                    holeBars[i] = barGo.GetComponent<SpriteRenderer>();
                    if (holeBars[i] == null)
                    {
                        holeBars[i] = barGo.AddComponent<SpriteRenderer>();
                    }
                }

                var bar = holeBars[i];
                bar.sprite = mBarSprite;
                bar.color = mDimmerColor;
                bar.sortingLayerID = mSortingLayerId;
                bar.sortingOrder = mSortingOrder;
                bar.drawMode = SpriteDrawMode.Simple;
                bar.maskInteraction = SpriteMaskInteraction.None;
                bar.sharedMaterial = null;
                StripColliders(bar.gameObject);
            }
        }

        private void ApplyHole(Bounds worldBounds, Vector2 holeSize)
        {
            mCurrentBounds = worldBounds;
            if (holeBars == null)
            {
                return;
            }

            var cover = ResolveCoverBounds();
            var holeCenter = worldBounds.center;
            var half = new Vector2(Mathf.Max(0.25f, holeSize.x * 0.5f), Mathf.Max(0.25f, holeSize.y * 0.5f));
            var holeMinX = Mathf.Max(cover.min.x, holeCenter.x - half.x);
            var holeMaxX = Mathf.Min(cover.max.x, holeCenter.x + half.x);
            var holeMinY = Mathf.Max(cover.min.y, holeCenter.y - half.y);
            var holeMaxY = Mathf.Min(cover.max.y, holeCenter.y + half.y);
            if (holeMaxX <= holeMinX || holeMaxY <= holeMinY)
            {
                holeMinX = holeCenter.x - 0.25f;
                holeMaxX = holeCenter.x + 0.25f;
                holeMinY = holeCenter.y - 0.25f;
                holeMaxY = holeCenter.y + 0.25f;
            }

            var z = spotlightRoot != null ? spotlightRoot.transform.position.z : 0f;
            SetBar(0, cover.min.x, holeMaxY, cover.max.x, cover.max.y, z);
            SetBar(1, cover.min.x, cover.min.y, cover.max.x, holeMinY, z);
            SetBar(2, cover.min.x, holeMinY, holeMinX, holeMaxY, z);
            SetBar(3, holeMaxX, holeMinY, cover.max.x, holeMaxY, z);
        }

        private void SetBar(int index, float minX, float minY, float maxX, float maxY, float z)
        {
            if (holeBars == null || index < 0 || index >= holeBars.Length || holeBars[index] == null)
            {
                return;
            }

            var width = maxX - minX;
            var height = maxY - minY;
            var bar = holeBars[index];
            var active = width > 0.001f && height > 0.001f;
            if (bar.gameObject.activeSelf != active)
            {
                bar.gameObject.SetActive(active);
            }

            if (!active)
            {
                return;
            }

            bar.transform.position = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, z);
            var spriteSize = bar.sprite != null ? bar.sprite.bounds.size : Vector3.one;
            var worldX = spriteSize.x > 0.001f ? width / spriteSize.x : width;
            var worldY = spriteSize.y > 0.001f ? height / spriteSize.y : height;
            var parentScale = bar.transform.parent != null ? bar.transform.parent.lossyScale : Vector3.one;
            bar.transform.localScale = new Vector3(
                parentScale.x > 0.001f ? worldX / parentScale.x : worldX,
                parentScale.y > 0.001f ? worldY / parentScale.y : worldY,
                1f);
        }

        private Bounds ResolveCoverBounds()
        {
            var source = FindSceneDimmerRenderer();
            if (source != null && source.bounds.size.x > 1f && source.bounds.size.y > 1f)
            {
                return source.bounds;
            }

            var cam = Camera.main;
            if (cam != null && cam.orthographic)
            {
                var height = cam.orthographicSize * 2.2f;
                var width = height * cam.aspect;
                return new Bounds(cam.transform.position, new Vector3(width, height, 0f));
            }

            return new Bounds(Vector3.zero, new Vector3(40f, 24f, 0f));
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

        private static Sprite GetFallbackSprite()
        {
            if (sFallbackSprite != null)
            {
                return sFallbackSprite;
            }

            var texture = Texture2D.whiteTexture;
            sFallbackSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                1f);
            return sFallbackSprite;
        }
    }
}
