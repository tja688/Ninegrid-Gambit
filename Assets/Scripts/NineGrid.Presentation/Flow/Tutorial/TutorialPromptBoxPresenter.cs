using System;
using NineGrid.Cards;
using NineGrid.Cards.Slots;
using NineGrid.Core;
using NineGrid.Flow.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NineGrid.Flow.Tutorial
{
    /// <summary>
    /// 教学提示框驱动：基于场景预置 <c>Panels/场景UI面板/教学提示框</c>。
    /// 套住场地卡世界包围盒（不框 HUD），通过切片中腹宽高变化实现呼吸指认，
    /// 四角图案尺寸保持不变，物体缩放不变。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TutorialPromptBoxPresenter : MonoBehaviour
    {
        public const string BoxObjectName = "教学提示框";
        public const string PanelObjectName = "场景UI面板";

        public const float DefaultBreathFrequency = 3.0f;
        public const float DefaultBreathAmplitude = 0.15f;
        public static readonly Vector3 FallbackCardFrameSize = new Vector3(2.0f, 2.8f, 0f);

        private static TutorialPromptBoxPresenter sInstance;

        [Tooltip("提示框根节点（留空自动按 教学提示框 查找）。")]
        [SerializeField] private GameObject boxRoot;

        [Tooltip("提示框切片 SpriteRenderer。")]
        [SerializeField] private SpriteRenderer boxSpriteRenderer;

        [Tooltip("呼吸频率（弧度/秒）。")]
        [SerializeField] private float breathFrequency = DefaultBreathFrequency;

        [Tooltip("呼吸幅度（Unity 世界单位）。")]
        [SerializeField] private float breathAmplitude = DefaultBreathAmplitude;

        [Tooltip("包围盒额外边距（宽高）。")]
        [SerializeField] private Vector2 padding = Vector2.zero;

        private Transform mTargetTransform;
        private ManagedCard mTargetCard;
        private int mTargetSlot = -1;
        private Bounds mCurrentBounds;
        private Vector2 mBaseSize = new Vector2(4.078125f, 5.109375f);
        private bool mIsActive;

        public static TutorialPromptBoxPresenter InstanceOrNull()
        {
            if (sInstance != null)
            {
                return sInstance;
            }

            sInstance = FindFirstObjectByType<TutorialPromptBoxPresenter>(FindObjectsInactive.Include);
            return sInstance;
        }

        public static TutorialPromptBoxPresenter EnsureExists()
        {
            var box = FindBoxRoot();
            if (box != null)
            {
                var onBox = box.GetComponent<TutorialPromptBoxPresenter>();
                if (onBox == null)
                {
                    onBox = box.AddComponent<TutorialPromptBoxPresenter>();
                }

                onBox.boxRoot = box;
                onBox.EnsureBindings();
                AdoptInstance(onBox);
                return onBox;
            }

            var existing = InstanceOrNull();
            if (existing != null)
            {
                existing.EnsureBindings();
                return existing;
            }

            var go = new GameObject(BoxObjectName);
            var presenter = go.AddComponent<TutorialPromptBoxPresenter>();
            presenter.EnsureBindings();
            AdoptInstance(presenter);
            return presenter;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            EnsureExists();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureExists();
        }

        private void Awake()
        {
            EnsureBindings();
            if (IsAuthoritativeBox(gameObject))
            {
                AdoptInstance(this);
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

            if (mTargetTransform != null)
            {
                if (TryCalculateWorldBounds(mTargetTransform.gameObject, out var updatedBounds))
                {
                    ApplyBoundsGeometry(updatedBounds);
                }
            }
            else if (mTargetSlot >= 1 && mTargetSlot <= 9)
            {
                if (TryResolveSlotBounds(mTargetSlot, out var slotBounds))
                {
                    ApplyBoundsGeometry(slotBounds);
                }
            }

            ApplyBreathing();
        }

        public bool IsVisible => mIsActive && boxRoot != null && boxRoot.activeSelf;

        public Vector2 BaseSize => mBaseSize;

        public Bounds CurrentBounds => mCurrentBounds;

        public Vector2 CurrentBreathSize
        {
            get
            {
                var offset = mIsActive
                    ? Mathf.Sin(Time.unscaledTime * breathFrequency) * breathAmplitude
                    : 0f;
                return new Vector2(
                    Mathf.Max(0.2f, mBaseSize.x + offset),
                    Mathf.Max(0.2f, mBaseSize.y + offset));
            }
        }

        public static void ShowTarget(ManagedCard card)
        {
            var presenter = EnsureExists();
            presenter.Show(card);
        }

        public static void ShowTarget(int slot)
        {
            var presenter = EnsureExists();
            presenter.Show(slot);
        }

        public static void ShowTarget(GameObject targetGo)
        {
            var presenter = EnsureExists();
            presenter.Show(targetGo);
        }

        public static void ShowTarget(Bounds worldBounds)
        {
            var presenter = EnsureExists();
            presenter.Show(worldBounds);
        }

        public static void HidePrompt()
        {
            var presenter = InstanceOrNull();
            presenter?.Hide();
        }

        public static bool IsPromptVisible => InstanceOrNull()?.IsVisible ?? false;

        public static void ResetForTests()
        {
            TutorialSpotlightPresenter.ResetForTests();
            if (sInstance != null)
            {
                sInstance.Hide();
                sInstance = null;
            }
        }

        public void Show(ManagedCard card)
        {
            EnsureBindings();
            if (card == null || card.GameObject == null)
            {
                Hide();
                return;
            }

            mTargetCard = card;
            mTargetTransform = card.Transform;
            mTargetSlot = -1;

            if (TryCalculateWorldBounds(card.GameObject, out var bounds))
            {
                Show(bounds);
            }
            else
            {
                Show(new Bounds(card.Transform.position, FallbackCardFrameSize));
            }
        }

        public void Show(int slot)
        {
            EnsureBindings();
            mTargetSlot = slot;
            mTargetCard = null;
            mTargetTransform = null;

            if (TryResolveSlotBounds(slot, out var bounds))
            {
                Show(bounds);
            }
            else
            {
                Hide();
            }
        }

        public void Show(GameObject targetGo)
        {
            EnsureBindings();
            if (targetGo == null)
            {
                Hide();
                return;
            }

            mTargetTransform = targetGo.transform;
            mTargetCard = null;
            mTargetSlot = -1;

            if (TryCalculateWorldBounds(targetGo, out var bounds))
            {
                Show(bounds);
            }
            else
            {
                Show(new Bounds(targetGo.transform.position, FallbackCardFrameSize));
            }
        }

        public void Show(Bounds worldBounds)
        {
            EnsureBindings();
            mCurrentBounds = worldBounds;
            mIsActive = true;

            if (boxRoot != null && !boxRoot.activeSelf)
            {
                boxRoot.SetActive(true);
            }

            ApplyBoundsGeometry(worldBounds);
            ApplyBreathing();
            TutorialSpotlightPresenter.Show(worldBounds);
        }

        public void Hide()
        {
            mIsActive = false;
            mTargetTransform = null;
            mTargetCard = null;
            mTargetSlot = -1;
            TutorialSpotlightPresenter.Hide();

            if (boxRoot != null)
            {
                boxRoot.SetActive(false);
            }
        }

        public void EnsureBindings()
        {
            if (boxRoot == null)
            {
                boxRoot = FindBoxRoot();
                if (boxRoot == null)
                {
                    boxRoot = gameObject;
                }
            }

            if (boxSpriteRenderer == null && boxRoot != null)
            {
                boxSpriteRenderer = boxRoot.GetComponent<SpriteRenderer>();
                if (boxSpriteRenderer == null)
                {
                    boxSpriteRenderer = boxRoot.AddComponent<SpriteRenderer>();
                }
            }

            if (boxSpriteRenderer != null)
            {
                boxSpriteRenderer.drawMode = SpriteDrawMode.Sliced;
                if (boxSpriteRenderer.size.x > 0.01f && boxSpriteRenderer.size.y > 0.01f)
                {
                    mBaseSize = boxSpriteRenderer.size;
                }
            }
        }

        private void ApplyBoundsGeometry(Bounds worldBounds)
        {
            mCurrentBounds = worldBounds;

            if (boxRoot != null)
            {
                var currentZ = boxRoot.transform.position.z;
                boxRoot.transform.position = new Vector3(worldBounds.center.x, worldBounds.center.y, currentZ);
            }

            mBaseSize = new Vector2(
                Mathf.Max(0.5f, worldBounds.size.x + padding.x),
                Mathf.Max(0.5f, worldBounds.size.y + padding.y));
        }

        private void ApplyBreathing()
        {
            if (boxSpriteRenderer == null)
            {
                return;
            }

            var offset = Mathf.Sin(Time.unscaledTime * breathFrequency) * breathAmplitude;
            boxSpriteRenderer.drawMode = SpriteDrawMode.Sliced;
            boxSpriteRenderer.size = new Vector2(
                Mathf.Max(0.2f, mBaseSize.x + offset),
                Mathf.Max(0.2f, mBaseSize.y + offset));
            TutorialSpotlightPresenter.SyncBreathFromPrompt(this);
        }

        public static bool TryCalculateWorldBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            if (go == null)
            {
                return false;
            }

            if (TryGetCardFrameBounds(go.transform, out bounds))
            {
                return true;
            }

            bounds = new Bounds(go.transform.position, FallbackCardFrameSize);
            return true;
        }

        public static bool TryGetCardFrameBounds(Transform faceRoot, out Bounds bounds)
        {
            bounds = default;
            if (faceRoot == null)
            {
                return false;
            }

            if (!CardFaceSlotNodeMap.TryFindRenderer(faceRoot, CardFaceSlotCodes.CardFrame, out var frame)
                || frame == null
                || !frame.enabled
                || !frame.gameObject.activeInHierarchy)
            {
                return false;
            }

            bounds = frame.bounds;
            return true;
        }

        private bool TryResolveSlotBounds(int slot, out Bounds bounds)
        {
            bounds = default;
            if (TryGetAuthoritativeCardAtSlot(slot, out var card) && card?.GameObject != null)
            {
                mTargetCard = card;
                mTargetTransform = card.Transform;
                return TryCalculateWorldBounds(card.GameObject, out bounds);
            }

            var field = GroundFieldGeometryHook.FieldOrNull();
            if (field != null && field.TryGetAnchor(slot, out var anchor) && anchor != null)
            {
                mTargetTransform = anchor;
                bounds = new Bounds(anchor.position, FallbackCardFrameSize);
                return true;
            }

            return false;
        }

        private static bool TryGetAuthoritativeCardAtSlot(int slot, out ManagedCard card)
        {
            card = null;
            var board = NineGridArchitecture.Current?.GetModel<BoardModel>();
            if (board != null)
            {
                var uid = board.GetCardUid(SlotId.Board(slot));
                if (uid > 0 && CardEntityLifecycleHook.TryGetCard(uid, out card) && card != null)
                {
                    return true;
                }
            }

            var field = GroundFieldGeometryHook.FieldOrNull();
            if (field != null
                && field.TryGetCardAt(slot, out card)
                && card != null
                && field.TryGetSlotOf(card.Uid, out var liveSlot)
                && liveSlot == slot)
            {
                return true;
            }

            card = null;
            return false;
        }

        private static void AdoptInstance(TutorialPromptBoxPresenter presenter)
        {
            if (presenter == null)
            {
                return;
            }

            if (sInstance != null
                && !ReferenceEquals(sInstance, presenter)
                && IsOrphanFallback(sInstance))
            {
                var orphan = sInstance.gameObject;
                sInstance = presenter;
                if (orphan != null)
                {
                    Destroy(orphan);
                }

                return;
            }

            sInstance = presenter;
        }

        private static bool IsOrphanFallback(TutorialPromptBoxPresenter presenter)
        {
            return presenter != null
                   && presenter.gameObject != null
                   && presenter.gameObject.name == nameof(TutorialPromptBoxPresenter)
                   && !IsAuthoritativeBox(presenter.gameObject);
        }

        private static bool IsAuthoritativeBox(GameObject go)
        {
            return go != null && go.name == BoxObjectName;
        }

        private static GameObject FindBoxRoot()
        {
            var all = Resources.FindObjectsOfTypeAll<Transform>();
            for (var i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (t == null || t.name != BoxObjectName || !t.gameObject.scene.IsValid())
                {
                    continue;
                }

                return t.gameObject;
            }

            return GameObject.Find(BoxObjectName);
        }
    }
}
