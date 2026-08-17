using System.Collections.Generic;
using NineGrid.Cards;
using UnityEngine;

namespace NineGrid.Flow.InRoomBoard
{
    /// <summary>
    /// 房内货架 / 服务 / 候选换货时的格位认领生命周期（ADR-0023）。
    /// 未保留对象须在 Destroy 前立刻注销认领——Destroy 推迟 OnDisable，不能依赖它释放格位。
    /// </summary>
    public static class InRoomOfferClaimLifecycle
    {
        /// <summary>
        /// 立刻注销 GO 上所有认领代理并隐藏（同步触发 OnDisable → Release）。
        /// </summary>
        public static void ReleaseNow(GameObject go)
        {
            if (go == null)
            {
                return;
            }

            ReleaseProxiesOn(go);
            if (go.activeSelf)
            {
                go.SetActive(false);
            }
        }

        /// <summary>真卡货架：对 View 根做 <see cref="ReleaseNow"/>。</summary>
        public static void ReleaseNow(ManagedCard card)
        {
            if (card?.View == null)
            {
                return;
            }

            ReleaseNow(card.View.gameObject);
        }

        private static void ReleaseProxiesOn(GameObject go)
        {
            var shop = go.GetComponent<ShopBoard.ShopBoardHitProxy>();
            shop?.ReleaseClaims();

            var tavern = go.GetComponent<TavernBoard.TavernBoardHitProxy>();
            tavern?.ReleaseClaims();

            var reward = go.GetComponent<RewardBoard.RewardBoardHitProxy>();
            reward?.ReleaseClaims();

            var brief = go.GetComponent<BoardBriefTip.BoardBriefTipHitProxy>();
            brief?.ReleaseClaims();

            go.GetComponent<GroundCardHitProxy>()?.ReleaseClaim();
        }

        /// <summary>换货前立刻注销全部旧货架认领。</summary>
        public static void ReleaseAllShelfClaims(
            IReadOnlyList<ManagedCard> cards,
            IReadOnlyList<GameObject> optionGos)
        {
            if (cards != null)
            {
                for (var i = 0; i < cards.Count; i++)
                {
                    ReleaseNow(cards[i]);
                }
            }

            if (optionGos != null)
            {
                for (var i = 0; i < optionGos.Count; i++)
                {
                    ReleaseNow(optionGos[i]);
                }
            }
        }

        /// <summary>换货前立刻注销未保留旧货架认领。</summary>
        public static void ReleaseUnkeptShelfClaims(
            IReadOnlyList<ManagedCard> cards,
            IReadOnlyList<GameObject> optionGos,
            bool[] keptOld)
        {
            if (keptOld == null)
            {
                return;
            }

            for (var j = 0; j < keptOld.Length; j++)
            {
                if (keptOld[j])
                {
                    continue;
                }

                if (cards != null && j < cards.Count)
                {
                    ReleaseNow(cards[j]);
                }

                if (optionGos != null && j < optionGos.Count)
                {
                    ReleaseNow(optionGos[j]);
                }
            }
        }
    }
}
