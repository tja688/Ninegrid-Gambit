using NineGrid.Flow;
using UnityEngine;

namespace NineGrid.Presentation.Ui
{
    /// <summary>
    /// 纯黑屏底幕（选人界面 / 完结结算专用）：绑定场景 <c>UI面板/纯黑屏BG</c>，
    /// 全屏纯黑 + 吞点击，引用计数开关。与半黑屏 <see cref="BattleUiDimmerOverlay"/> 分离，
    /// 不参与其单例（绑定时若发现误挂的 BattleUiDimmerOverlay 组件会先摘除，防单例被劫持）。
    /// </summary>
    public static class PureBlackScreenOverlay
    {
        public const string RootName = "纯黑屏BG";

        private static GameObject sRoot;
        private static int sRefCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            sRoot = null;
            sRefCount = 0;
        }

        public static bool IsActive => sRefCount > 0 && sRoot != null && sRoot.activeSelf;

        public static bool Acquire(string reason)
        {
            if (!EnsureBound())
            {
                Debug.LogWarning("[PureBlack] 纯黑屏BG 未找到，无法 Acquire reason=" + reason);
                return false;
            }

            sRefCount++;
            if (sRefCount == 1 && !sRoot.activeSelf)
            {
                sRoot.SetActive(true);
            }

            return true;
        }

        public static void Release(string reason)
        {
            _ = reason;
            if (sRefCount <= 0)
            {
                return;
            }

            sRefCount--;
            if (sRefCount > 0)
            {
                return;
            }

            sRefCount = 0;
            if (sRoot != null && sRoot.activeSelf)
            {
                sRoot.SetActive(false);
            }
        }

        public static void ForceClear(string reason = null)
        {
            _ = reason;
            sRefCount = 0;
            if (sRoot != null && sRoot.activeSelf)
            {
                sRoot.SetActive(false);
            }
        }

        private static bool EnsureBound()
        {
            if (sRoot != null)
            {
                return true;
            }

            var root = FindUiPanelRoot();
            var target = root != null ? root.transform.Find(RootName) : null;
            if (target == null)
            {
                return false;
            }

            var go = target.gameObject;

            // 场景中该节点常由半黑屏复制而来：BattleUiDimmerOverlay 是单例组件，
            // 一旦随激活 Awake 会劫持半黑屏静态实例，必须在激活前摘掉。
            var strayDimmer = go.GetComponent<BattleUiDimmerOverlay>();
            if (strayDimmer != null)
            {
                Object.Destroy(strayDimmer);
            }

            var collider = go.GetComponent<BoxCollider2D>();
            if (collider == null)
            {
                collider = go.AddComponent<BoxCollider2D>();
            }

            var renderer = go.GetComponent<SpriteRenderer>();
            if (renderer != null && renderer.sprite != null)
            {
                var size = renderer.sprite.bounds.size;
                collider.size = new Vector2(size.x * 1.05f, size.y * 1.05f);
            }
            else
            {
                collider.size = new Vector2(40f, 24f);
            }

            collider.offset = Vector2.zero;
            collider.isTrigger = false;
            collider.enabled = true;

            var proxy = go.GetComponent<UiOverlayHitProxy>();
            if (proxy == null)
            {
                proxy = go.AddComponent<UiOverlayHitProxy>();
            }

            proxy.Configure(
                UiOverlayHitAction.Swallow,
                BattleUiDimmerOverlay.HitSort,
                PointerHitSurfacePriorities.Overlay);

            sRoot = go;
            if (sRefCount <= 0 && go.activeSelf)
            {
                go.SetActive(false);
            }

            return true;
        }

        private static GameObject FindUiPanelRoot()
        {
            var direct = GameObject.Find("UI面板");
            if (direct != null)
            {
                return direct;
            }

            var roots = UnityEngine.SceneManagement.SceneManager
                .GetActiveScene()
                .GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == "UI面板")
                {
                    return roots[i];
                }
            }

            return null;
        }
    }
}
