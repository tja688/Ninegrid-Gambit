using UnityEngine;

namespace NineGrid.Flow
{
    /// <summary>
    /// 局内半黑屏遮罩：挡 PointerHitRouter 射线、不暂停主线。
    /// 引用计数 Acquire/Release，供右键详述与局内选卡（宝箱/遗物）复用。
    /// 命中吞点击由同节点 <see cref="UiOverlayHitProxy"/> 承接。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleUiDimmerOverlay : MonoBehaviour
    {
        /// <summary>覆层内排序：半黑屏底。表面优先级见 <see cref="PointerHitSurfacePriorities.Overlay"/>。</summary>
        public const int HitSort = 0;

        /// <summary>覆层内排序：关闭钮高于半黑屏 / 面板底。</summary>
        public const int CloseHitSort = 2;

        private const int TypePriority = PointerHitSurfacePriorities.Overlay;

        private static BattleUiDimmerOverlay s_instance;
        private static int s_refCount;

        private BoxCollider2D _collider;
        private SpriteRenderer _renderer;

        public static bool IsActive
        {
            get
            {
                if (s_refCount <= 0 || !TryGetLiveInstance(out var live))
                {
                    return false;
                }

                return live.gameObject.activeInHierarchy;
            }
        }

        public static BattleUiDimmerOverlay InstanceOrNull()
        {
            return TryGetLiveInstance(out var live) ? live : null;
        }

        /// <summary>场景装配：挂在 <c>UI面板/半黑屏BG</c>。</summary>
        public static BattleUiDimmerOverlay EnsureBound(GameObject dimmerGo)
        {
            if (dimmerGo == null)
            {
                return null;
            }

            var overlay = dimmerGo.GetComponent<BattleUiDimmerOverlay>();
            if (overlay == null)
            {
                overlay = dimmerGo.AddComponent<BattleUiDimmerOverlay>();
            }

            overlay.WireHitProxy();
            overlay.EnsureCollider();
            s_instance = overlay;
            if (s_refCount <= 0 && dimmerGo.activeSelf)
            {
                dimmerGo.SetActive(false);
            }

            return overlay;
        }

        public static bool TryAcquire(string reason)
        {
            EnsureInstanceFromScene();
            if (!TryGetLiveInstance(out var live))
            {
                Debug.LogWarning("[BattleUiDimmer] 半黑屏BG 未找到，无法 Acquire reason=" + reason);
                return false;
            }

            s_refCount++;
            if (s_refCount == 1)
            {
                live.ShowInternal();
            }

            return true;
        }

        public static void Release(string reason)
        {
            _ = reason;
            if (s_refCount <= 0)
            {
                return;
            }

            s_refCount--;
            if (s_refCount > 0)
            {
                return;
            }

            s_refCount = 0;
            if (TryGetLiveInstance(out var live))
            {
                live.HideInternal();
            }
        }

        public static void ForceClear(string reason = null)
        {
            _ = reason;
            s_refCount = 0;
            if (TryGetLiveInstance(out var live))
            {
                live.HideInternal();
            }
        }

        private void Awake()
        {
            s_instance = this;
            _renderer = GetComponent<SpriteRenderer>();
            WireHitProxy();
            EnsureCollider();
        }

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
                s_refCount = 0;
            }
        }

        private void ShowInternal()
        {
            EnsureCollider();
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
        }

        private void HideInternal()
        {
            if (this == null)
            {
                return;
            }

            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        private void WireHitProxy()
        {
            var proxy = GetComponent<UiOverlayHitProxy>();
            if (proxy == null)
            {
                proxy = gameObject.AddComponent<UiOverlayHitProxy>();
            }

            proxy.Configure(UiOverlayHitAction.Swallow, HitSort, TypePriority);
        }

        private void EnsureCollider()
        {
            _collider = GetComponent<BoxCollider2D>();
            if (_collider == null)
            {
                _collider = gameObject.AddComponent<BoxCollider2D>();
            }

            _renderer ??= GetComponent<SpriteRenderer>();
            if (_renderer != null && _renderer.sprite != null)
            {
                var size = _renderer.sprite.bounds.size;
                _collider.size = new Vector2(size.x * 1.05f, size.y * 1.05f);
            }
            else
            {
                _collider.size = new Vector2(40f, 24f);
            }

            _collider.offset = Vector2.zero;
            _collider.isTrigger = false;
            _collider.enabled = true;
        }

        /// <summary>
        /// Unity 已销毁对象对 C# <c>?</c> 仍非 null；须走重载 <c>==</c> 并清掉静态残留。
        /// </summary>
        private static bool TryGetLiveInstance(out BattleUiDimmerOverlay live)
        {
            if (s_instance == null)
            {
                s_instance = null;
                live = null;
                return false;
            }

            live = s_instance;
            return true;
        }

        private static void EnsureInstanceFromScene()
        {
            if (TryGetLiveInstance(out _))
            {
                return;
            }

            var root = GameObject.Find("UI面板");
            if (root == null)
            {
                var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
                for (var i = 0; i < roots.Length; i++)
                {
                    if (roots[i].name == "UI面板")
                    {
                        root = roots[i];
                        break;
                    }
                }
            }

            if (root == null)
            {
                return;
            }

            var dimmer = root.transform.Find("半黑屏BG");
            if (dimmer == null)
            {
                return;
            }

            EnsureBound(dimmer.gameObject);
        }
    }
}
