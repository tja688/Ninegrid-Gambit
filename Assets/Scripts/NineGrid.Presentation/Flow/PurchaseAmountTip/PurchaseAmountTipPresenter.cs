using NineGrid.Cards;
using TMPro;
using UnityEngine;

namespace NineGrid.Flow.PurchaseAmountTip
{
    /// <summary>
    /// 购买选项金额提示（新一轮设计）：鼠标悬停购买选项时，在选项所处格位中心显示对应金额，
    /// 不悬停即隐藏。文字源为场景中制作好的 <c>Panels/ShopPanel/金额购买提示模板</c>
    /// （3D TMP + TmpBitmapPixelOutline，MeshRenderer sortingOrder 999）。
    ///
    /// 状态机（对齐 BoardBriefTipSession 思路，保证健壮）：
    /// Show 返回代数，Hide(代数) 只清同代——旧悬停的退出事件不会把新悬停的提示清掉；
    /// 场地板撤场经 <see cref="HideAll"/> 兜底硬清并推进代数。
    /// </summary>
    public static class PurchaseAmountTipPresenter
    {
        public const string TemplateObjectName = "购买金额";
        public const string LegacyTemplateObjectName = "金额购买提示模板";

        private static GameObject sTemplateGo;
        private static TMP_Text sText;
        private static int sGeneration;
        private static bool sLookedUp;

        /// <summary>
        /// 在格位中心显示金额；返回本次显示代数（供 Hide 用）。0 表示未找到模板/锚点（不显示）。
        /// </summary>
        public static int Show(int slot, int amount)
        {
            if (!EnsureTemplate() || sText == null)
            {
                return 0;
            }

            var field = GroundFieldGeometryHook.FieldOrNull();
            var anchor = field != null ? field.GetGroundAnchor(slot) : null;
            if (anchor == null)
            {
                // 几何未就绪（撤场 / 未绑定）：不残留旧提示。
                HideAll();
                return 0;
            }

            sText.text = "+" + Mathf.Max(0, amount);
            var pos = anchor.position;
            pos.z = -0.05f;
            sTemplateGo.transform.position = pos;
            if (!sTemplateGo.activeSelf)
            {
                sTemplateGo.SetActive(true);
            }

            return ++sGeneration;
        }

        /// <summary>按代数隐藏：仅当 <paramref name="generation"/> 仍是当前代数才隐藏（防旧悬停脏写）。</summary>
        public static void Hide(int generation)
        {
            if (generation <= 0 || generation != sGeneration)
            {
                return;
            }

            HideAll();
        }

        /// <summary>无条件硬清（撤场兜底），并推进代数使所有旧 Hide 失效。</summary>
        public static void HideAll()
        {
            sGeneration++;
            if (sTemplateGo != null && sTemplateGo.activeSelf)
            {
                sTemplateGo.SetActive(false);
            }
        }

        public static bool IsVisible
        {
            get { return sTemplateGo != null && sTemplateGo.activeInHierarchy; }
        }

        public static void ResetForTests()
        {
            sTemplateGo = null;
            sText = null;
            sGeneration = 0;
            sLookedUp = false;
        }

        private static bool EnsureTemplate()
        {
            if (sTemplateGo != null && sText != null)
            {
                return true;
            }

            // 1. 优先从 Panels/ShopPanel（含未激活）寻找用户制作的「购买金额」或含 TMP 的子对象
            var templateTransform = FindInShopPanel();

            // 2. 场景全局查找候选对象名
            if (templateTransform == null)
            {
                templateTransform = FindInSceneAll();
            }

            if (templateTransform != null)
            {
                sTemplateGo = templateTransform.gameObject;
                sText = sTemplateGo.GetComponentInChildren<TMP_Text>(true);
                // 模板挂在未激活的 ShopPanel 下：脱离父级后提示本体可独立显隐渲染，
                // 不依赖 ShopPanel 激活态（后续 Shop 面板 UI 启用也不会误伤本提示）。
                if (sTemplateGo.transform.parent != null)
                {
                    sTemplateGo.transform.SetParent(null, worldPositionStays: true);
                }

                var mr = sTemplateGo.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    if (string.IsNullOrEmpty(mr.sortingLayerName) || mr.sortingLayerName == "Default")
                    {
                        mr.sortingLayerName = "Main";
                    }

                    mr.sortingOrder = Mathf.Max(94, mr.sortingOrder);
                }

                sTemplateGo.SetActive(false);
                return sText != null;
            }

            if (sLookedUp)
            {
                return sTemplateGo != null && sText != null;
            }

            sLookedUp = true;

            // 3. 兜底：动态创建标准金色 TMP 提示对象，永不静默失效
            return CreateFallbackTemplate();
        }

        private static Transform FindInShopPanel()
        {
            var all = Resources.FindObjectsOfTypeAll<Transform>();
            Transform shopPanel = null;
            for (var i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (t != null && t.name == "ShopPanel" && t.gameObject.scene.IsValid())
                {
                    shopPanel = t;
                    break;
                }
            }

            if (shopPanel == null)
            {
                return null;
            }

            // 精确名匹配
            var direct = shopPanel.Find(TemplateObjectName) ?? shopPanel.Find(LegacyTemplateObjectName);
            if (direct != null)
            {
                return direct;
            }

            // 查找含 TMP_Text 的子对象
            var tmp = shopPanel.GetComponentInChildren<TMP_Text>(true);
            return tmp != null ? tmp.transform : null;
        }

        private static Transform FindInSceneAll()
        {
            var all = Resources.FindObjectsOfTypeAll<Transform>();
            for (var i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (t == null || !t.gameObject.scene.IsValid())
                {
                    continue;
                }

                if (t.name == TemplateObjectName || t.name == LegacyTemplateObjectName)
                {
                    return t;
                }
            }

            return null;
        }

        private static bool CreateFallbackTemplate()
        {
            var go = new GameObject(TemplateObjectName);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = "+0";
            tmp.fontSize = 6f;
            tmp.color = new Color(0.972f, 0.806f, 0.440f, 1f);
            tmp.alignment = TextAlignmentOptions.Center;

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sortingLayerName = "Main";
                mr.sortingOrder = 94;
            }

            sTemplateGo = go;
            sText = tmp;
            sTemplateGo.SetActive(false);
            return true;
        }
    }
}
