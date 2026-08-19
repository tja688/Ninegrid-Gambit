using NineGrid.Flow.BattleInfoPreview;
using NineGrid.Flow.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace NineGrid.Flow.InfoNotice
{
    /// <summary>
    /// UI 判定框（Tag: UIInfo）纯观察者式指针悬停路由。
    /// 仅执行纯几何无害悬停检测，指向时在头顶消息提示栏显示对应简要介绍，移开时清退。
    /// 有覆盖面板（开战前准备、右键详述、半黑屏叠层等）打开时一律不响应，避免点到覆盖物仍冒出场内介绍。
    /// 绝不注册入 PointerHitRegistry、绝不拦截任何鼠标事件，100% 保障左键点击、右键详述、手牌/遗物拖拽等现有交互完全不受干扰。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-35)]
    public sealed class UIInfoHoverRouter : MonoBehaviour
    {
        private static UIInfoHoverRouter sInstance;

        private Camera mCamera;
        private Collider2D mCurrentHoveredCollider;
        private readonly Collider2D[] mHitBuffer = new Collider2D[16];

        public static UIInfoHoverRouter EnsureExists()
        {
            if (sInstance != null)
            {
                return sInstance;
            }

            sInstance = FindFirstObjectByType<UIInfoHoverRouter>(FindObjectsInactive.Include);
            if (sInstance != null)
            {
                return sInstance;
            }

            var go = new GameObject(nameof(UIInfoHoverRouter));
            DontDestroyOnLoad(go);
            sInstance = go.AddComponent<UIInfoHoverRouter>();
            return sInstance;
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
            if (sInstance == null)
            {
                sInstance = this;
            }
            else if (!ReferenceEquals(sInstance, this))
            {
                Destroy(gameObject);
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
            Tick();
        }

        /// <summary>
        /// 每帧指针悬停判定 Tick（支持 EditMode / 测试直接调用）。
        /// </summary>
        public void Tick()
        {
            var cam = ResolveCamera();
            if (cam == null || !WorldPointerUtility.TryGetPointerScreen(out var screen))
            {
                ClearHover();
                return;
            }

            // 覆盖面板打开时，场内 UIInfo 判定框即使仍被物理命中也不出介绍
            if (ShouldSuppressFieldHover())
            {
                ClearHover();
                return;
            }

            // 当指针落在常规 UI 上（如确认框、弹窗等），让位给 UI
            if (IsPointerOverEventSystemUi())
            {
                ClearHover();
                return;
            }

            var hitCollider = QueryUIInfoCollider(cam, screen);
            if (hitCollider != null)
            {
                if (!ReferenceEquals(hitCollider, mCurrentHoveredCollider))
                {
                    mCurrentHoveredCollider = hitCollider;
                    if (UIInfoCatalog.TryResolveDescription(hitCollider.gameObject, out var description))
                    {
                        InfoNoticePresenter.ShowHover(description);
                    }
                }
            }
            else
            {
                ClearHover();
            }
        }

        private Collider2D QueryUIInfoCollider(Camera cam, Vector2 screen)
        {
            var worldPoint = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            var hitCount = Physics2D.OverlapPointNonAlloc(worldPoint, mHitBuffer);

            for (var i = 0; i < hitCount; i++)
            {
                var col = mHitBuffer[i];
                if (col == null || !col.enabled || !col.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (col.CompareTag(UIInfoCatalog.TagName))
                {
                    return col;
                }
            }

            return null;
        }

        /// <summary>
        /// 覆盖在场地 HUD 之上的面板打开时，禁止 UIInfo 悬停介绍。
        /// 开战前准备菜单、右键详述、半黑屏叠层均算覆盖面板。
        /// </summary>
        public static bool ShouldSuppressFieldHover()
        {
            return ShouldSuppressFieldHover(
                NineGrid.Flow.BattleUiDimmerOverlay.IsActive,
                NineGrid.Flow.CardInspectOverlayPresenter.IsOpen,
                BattleInfoPreviewPresenter.IsOpen);
        }

        /// <summary>
        /// 纯判定：任一覆盖面板为开即抑制。供测试直接注入标志位。
        /// </summary>
        public static bool ShouldSuppressFieldHover(bool dimmerActive, bool inspectOpen, bool previewOpen)
        {
            return dimmerActive || inspectOpen || previewOpen;
        }

        private void ClearHover()
        {
            if (mCurrentHoveredCollider != null)
            {
                mCurrentHoveredCollider = null;
                InfoNoticePresenter.ClearHover();
            }
        }

        private Camera ResolveCamera()
        {
            if (mCamera != null)
            {
                return mCamera;
            }

            mCamera = Camera.main;
            return mCamera;
        }

        private static bool IsPointerOverEventSystemUi()
        {
            var es = EventSystem.current;
            return es != null && es.IsPointerOverGameObject();
        }
    }
}
