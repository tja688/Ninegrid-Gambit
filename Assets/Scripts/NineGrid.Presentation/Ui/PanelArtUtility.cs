using UnityEngine;

namespace NineGrid.Presentation.Ui
{
    /// <summary>
    /// 面板装饰美术小工具（选人 / 结算共用）：
    /// __Art 子节点序列帧装载 + 按目标世界高度等比缩放。只服务面板装饰层，不进卡面表现管线。
    /// </summary>
    internal static class PanelArtUtility
    {
        public const string ArtChildName = "__Art";

        /// <summary>确保 host 下有 __Art 精灵子节点并挂上 Resources 序列帧循环。</summary>
        public static SpriteRenderer EnsureLoopArt(
            Transform host,
            string resourcesKey,
            Color tint,
            int sortingOrder,
            string sortingLayerName = "UI")
        {
            var renderer = EnsureArtRenderer(host, sortingOrder, sortingLayerName);
            if (renderer == null)
            {
                return null;
            }

            renderer.color = tint;
            var loop = renderer.GetComponent<ResourcesSpriteLoop>();
            if (loop == null)
            {
                loop = renderer.gameObject.AddComponent<ResourcesSpriteLoop>();
            }

            loop.SetResourcesKey(resourcesKey);
            return renderer;
        }

        /// <summary>确保 host 下有 __Art 精灵子节点并直接填静态图标。</summary>
        public static SpriteRenderer SetStaticArt(
            Transform host,
            Sprite sprite,
            int sortingOrder,
            string sortingLayerName = "UI")
        {
            var renderer = EnsureArtRenderer(host, sortingOrder, sortingLayerName);
            if (renderer == null)
            {
                return null;
            }

            var loop = renderer.GetComponent<ResourcesSpriteLoop>();
            if (loop != null)
            {
                loop.enabled = false;
            }

            renderer.color = Color.white;
            renderer.sprite = sprite;
            renderer.enabled = sprite != null;
            return renderer;
        }

        /// <summary>
        /// 按当前精灵包围盒把 __Art 等比缩放到目标世界高度，并把包围盒中心对回宿主锚点
        /// （战士序列帧 pivot 在脚底，不回中会整体上跑）。须在精灵已装载后调用。
        /// </summary>
        public static void FitWorldHeight(SpriteRenderer renderer, float targetWorldHeight)
        {
            if (renderer == null || renderer.sprite == null)
            {
                return;
            }

            var t = renderer.transform;
            var parentLossyY = t.parent != null ? Mathf.Abs(t.parent.lossyScale.y) : 1f;
            var spriteHeight = renderer.sprite.bounds.size.y;
            if (spriteHeight < 0.0001f || parentLossyY < 0.0001f)
            {
                return;
            }

            var scale = targetWorldHeight / (spriteHeight * parentLossyY);
            t.localScale = new Vector3(scale, scale, 1f);
            var center = renderer.sprite.bounds.center;
            t.localPosition = new Vector3(-center.x * scale, -center.y * scale, 0f);
        }

        private static SpriteRenderer EnsureArtRenderer(
            Transform host,
            int sortingOrder,
            string sortingLayerName)
        {
            if (host == null)
            {
                return null;
            }

            var art = host.Find(ArtChildName);
            if (art == null)
            {
                var go = new GameObject(ArtChildName);
                art = go.transform;
                art.SetParent(host, false);
                art.localPosition = Vector3.zero;
                art.localRotation = Quaternion.identity;
                art.localScale = Vector3.one;
            }

            var renderer = art.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = art.gameObject.AddComponent<SpriteRenderer>();
            }

            renderer.sortingLayerName = sortingLayerName;
            renderer.sortingOrder = sortingOrder;
            renderer.maskInteraction = SpriteMaskInteraction.None;
            renderer.enabled = true;

            // 拷贝自战斗信息预览的槽位可能带根占位精灵 / SpriteMask，一律关掉只留 __Art。
            var rootRenderer = host.GetComponent<SpriteRenderer>();
            if (rootRenderer != null && rootRenderer != renderer)
            {
                rootRenderer.enabled = false;
            }

            var mask = host.GetComponent<SpriteMask>();
            if (mask != null)
            {
                mask.enabled = false;
            }

            return renderer;
        }
    }
}
