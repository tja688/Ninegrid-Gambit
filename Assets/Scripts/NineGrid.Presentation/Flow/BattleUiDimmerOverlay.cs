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
        public const int HitSort = 100000;
        public const int CloseHitSort = 100001;
        private const int TypePriority = 100;

        private static BattleUiDimmerOverlay s_instance;
        private static int s_refCount;

        private BoxCollider2D _collider;
        private SpriteRenderer _renderer;

        public static bool IsActive =>
            s_refCount > 0 && s_instance != null && s_instance.gameObject.activeInHierarchy;

        public static BattleUiDimmerOverlay InstanceOrNull() => s_instance;

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
            if (s_instance == null)
            {
                Debug.LogWarning("[BattleUiDimmer] 半黑屏BG 未找到，无法 Acquire reason=" + reason);
                return false;
            }

            s_refCount++;
            if (s_refCount == 1)
            {
                s_instance.ShowInternal();
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
            if (s_refCount <= 0)
            {
                s_refCount = 0;
                s_instance?.HideInternal();
            }
        }

        public static void ForceClear(string reason = null)
        {
            _ = reason;
            s_refCount = 0;
            s_instance?.HideInternal();
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
            if (ReferenceEquals(s_instance, this))
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

        private static void EnsureInstanceFromScene()
        {
            if (s_instance != null)
            {
                return;
            }

            var root = GameObject.Find("UI面板");
            if (root == null)
            {
                // Find 不含 inactive；半黑屏可能已隐藏，从根扫。
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
