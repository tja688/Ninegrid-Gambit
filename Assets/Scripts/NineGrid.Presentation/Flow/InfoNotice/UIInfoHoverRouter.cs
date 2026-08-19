using NineGrid.Core;
using NineGrid.Flow.BattleInfoPreview;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Systems;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace NineGrid.Flow.InfoNotice
{
    /// <summary>
    /// UI 判定框（Tag: UIInfo）纯观察者式指针悬停路由。
    /// 仅执行纯几何无害悬停检测，指向时在头顶消息提示栏显示对应简要介绍，移开时清退。
    /// 有覆盖面板（开战前准备、右键详述、半黑屏叠层等）打开时一律不响应，避免点到覆盖物仍冒出场内介绍。
    /// 指针按住（拖动卡牌/遗物等长时间按下鼠标）时也不响应，拖动过程中划过判定框不会频繁冒提示。
    /// 拖放离手后若指针仍停在判定框上（回收区、卡组等），继续抑制，直到指针移出后再无动作移入才展示。
    /// 绝不注册入 PointerHitRegistry、绝不拦截任何鼠标事件，100% 保障左键点击、右键详述、手牌/遗物拖拽等现有交互完全不受干扰。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-35)]
    public sealed class UIInfoHoverRouter : MonoBehaviour
    {
        /// <summary>按住期间屏幕位移超过此像素视为拖动，松手后需先离开判定框才允许再出介绍。</summary>
        public const float DragMoveThresholdPixels = 8f;

        private static UIInfoHoverRouter sInstance;

        private Camera mCamera;
        private Collider2D mCurrentHoveredCollider;
        private readonly Collider2D[] mHitBuffer = new Collider2D[16];
        private DragReleaseLatch mDragLatch;

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

            mDragLatch.ObservePointer(WorldPointerUtility.IsPrimaryHeld(), screen);

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
            if (mDragLatch.BlockHover(hitCollider != null))
            {
                ClearHover();
                return;
            }
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
        /// 开战前准备菜单、右键详述、半黑屏叠层均算覆盖面板；
        /// 主菜单相位也抑制，避免开始界面冒出血条等局内介绍。
        /// </summary>
        public static bool ShouldSuppressFieldHover()
        {
            return ShouldSuppressFieldHover(
                NineGrid.Flow.BattleUiDimmerOverlay.IsActive,
                NineGrid.Flow.CardInspectOverlayPresenter.IsOpen,
                BattleInfoPreviewPresenter.IsOpen,
                IsMainMenuShell(),
                WorldPointerUtility.IsPrimaryHeld());
        }

        /// <summary>
        /// 纯判定：任一覆盖面板为开即抑制。供测试直接注入标志位。
        /// </summary>
        public static bool ShouldSuppressFieldHover(bool dimmerActive, bool inspectOpen, bool previewOpen)
        {
            return ShouldSuppressFieldHover(dimmerActive, inspectOpen, previewOpen, mainMenu: false, pointerHeld: false);
        }

        /// <summary>
        /// 纯判定：覆盖面板或主菜单相位任一成立即抑制。
        /// </summary>
        public static bool ShouldSuppressFieldHover(
            bool dimmerActive,
            bool inspectOpen,
            bool previewOpen,
            bool mainMenu)
        {
            return ShouldSuppressFieldHover(dimmerActive, inspectOpen, previewOpen, mainMenu, pointerHeld: false);
        }

        /// <summary>
        /// 纯判定：覆盖面板、主菜单相位或指针按住（拖动卡牌/遗物等长时间按下鼠标）任一成立即抑制。
        /// 指针按住时不展示 UI 判定框悬停介绍，避免拖动过程中频繁冒提示。
        /// </summary>
        public static bool ShouldSuppressFieldHover(
            bool dimmerActive,
            bool inspectOpen,
            bool previewOpen,
            bool mainMenu,
            bool pointerHeld)
        {
            return dimmerActive || inspectOpen || previewOpen || mainMenu || pointerHeld;
        }

        private static bool IsMainMenuShell()
        {
            var shell = NineGridArchitecture.Interface?.GetSystem<IGameFlowShellSystem>()
                        ?? NineGridArchitecture.Current?.GetSystem<IGameFlowShellSystem>();
            return shell != null && shell.State.Value == GameFlowShellState.MainMenu;
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

        /// <summary>
        /// 拖放离手闩：按住并移动后松手，指针仍停在 UIInfo 上时挡住介绍，直到移出判定框。
        /// 纯状态机，供 EditMode 测试直接驱动。
        /// </summary>
        public struct DragReleaseLatch
        {
            public bool AwaitLeave;

            private bool mWasHeld;
            private bool mMovedWhileHeld;
            private Vector2 mHoldStartScreen;

            public void ObservePointer(bool pointerHeld, Vector2 screen)
            {
                ObservePointer(pointerHeld, screen, DragMoveThresholdPixels);
            }

            public void ObservePointer(bool pointerHeld, Vector2 screen, float dragThresholdPixels)
            {
                if (pointerHeld)
                {
                    if (!mWasHeld)
                    {
                        mHoldStartScreen = screen;
                        mMovedWhileHeld = false;
                    }
                    else if (!mMovedWhileHeld)
                    {
                        var delta = screen - mHoldStartScreen;
                        var threshold = dragThresholdPixels * dragThresholdPixels;
                        mMovedWhileHeld = delta.sqrMagnitude >= threshold;
                    }

                    mWasHeld = true;
                    return;
                }

                if (mWasHeld && mMovedWhileHeld)
                {
                    AwaitLeave = true;
                }

                mWasHeld = false;
                mMovedWhileHeld = false;
            }

            /// <summary>
            /// 仍停在判定框上则挡住介绍；移出后解除闩并允许下一次无动作移入。
            /// </summary>
            public bool BlockHover(bool hasUiInfoHit)
            {
                if (!AwaitLeave)
                {
                    return false;
                }

                if (!hasUiInfoHit)
                {
                    AwaitLeave = false;
                    return false;
                }

                return true;
            }
        }
    }
}
