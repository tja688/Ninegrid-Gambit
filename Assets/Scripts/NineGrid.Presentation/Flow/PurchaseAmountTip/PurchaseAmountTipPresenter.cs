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
        public const string TemplateObjectName = "金额购买提示模板";

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
            sTemplateGo.transform.position = anchor.position;
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
            if (sTemplateGo != null)
            {
                return true;
            }

            if (sLookedUp)
            {
                return false;
            }

            sLookedUp = true;
            var all = Resources.FindObjectsOfTypeAll<Transform>();
            for (var i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (t == null || t.name != TemplateObjectName)
                {
                    continue;
                }

                if (!t.gameObject.scene.IsValid())
                {
                    continue;
                }

                sTemplateGo = t.gameObject;
                sText = sTemplateGo.GetComponentInChildren<TMP_Text>(true);
                // 模板挂在未激活的 ShopPanel 下：脱离父级后提示本体可独立显隐渲染，
                // 不依赖 ShopPanel 激活态（后续 Shop 面板 UI 启用也不会误伤本提示）。
                if (sTemplateGo.transform.parent != null)
                {
                    sTemplateGo.transform.SetParent(null, worldPositionStays: true);
                }

                sTemplateGo.SetActive(false);
                return true;
            }

            return false;
        }
    }
}
