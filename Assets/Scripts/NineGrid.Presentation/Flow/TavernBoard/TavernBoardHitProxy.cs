using System;
using NineGrid.Cards;
using NineGrid.Flow.PurchaseAmountTip;
using UnityEngine;

namespace NineGrid.Flow.TavernBoard
{
    /// <summary>
    /// 卡店服务 / 刷新 / 二级候选：格位认领 + 悬停简要文案 + 任意距离点击（#93 / ADR-0020 / ADR-0023）。
    /// <paramref name="amountGold"/> &gt; 0 时，悬停额外在格位中心显示金额提示（金额购买提示模板）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TavernBoardHitProxy : MonoBehaviour
    {
        private TavernBoardHitKind mKind;
        private int mOptionIndex;
        private string mTip = string.Empty;
        private Action<TavernBoardHitKind, int> mOnHit;

        public void Configure(
            TavernBoardHitKind kind,
            int optionIndex,
            string tip,
            Action<TavernBoardHitKind, int> onHit,
            int boardSlot,
            int amountGold = 0)
        {
            mKind = kind;
            mOptionIndex = optionIndex;
            mTip = tip ?? string.Empty;
            mOnHit = onHit;

            var field = GroundFieldGeometryHook.FieldOrNull();
            if (field == null)
            {
                return;
            }

            field.ReleaseAllClaimsForOwner(this);

            if (!GroundSlotTopology.IsValidSlot(boardSlot))
            {
                return;
            }

            // 金额提示代数按本次 Configure 局部捕获：旧认领者的退出事件只清自己的代数，
            // 不会把新悬停的提示清掉（与 BoardBriefTipSession 代数制同思路）。
            var amountTipGeneration = 0;
            Action hoverEnter = null;
            Action hoverExit = null;
            if (amountGold > 0)
            {
                hoverEnter = () =>
                {
                    amountTipGeneration = PurchaseAmountTipPresenter.Show(boardSlot, amountGold);
                };
                hoverExit = () => PurchaseAmountTipPresenter.Hide(amountTipGeneration);
            }

            var claimant = new SlotClaimant(this, mTip, ActivateHit, hoverEnter, hoverExit);
            if (!field.TryClaimSlot(boardSlot, claimant))
            {
                return;
            }
        }

        private void Awake()
        {
            DisableColliderIfPresent();
        }

        private void OnDisable()
        {
            ReleaseClaims();
        }

        private void OnDestroy()
        {
            ReleaseClaims();
        }

        /// <summary>立刻注销格位认领（换货 / 撤场前调用，勿依赖 Destroy 推迟 OnDisable）。</summary>
        public void ReleaseClaims()
        {
            var field = GroundFieldGeometryHook.FieldOrNull();
            if (field != null)
            {
                field.ReleaseAllClaimsForOwner(this);
            }
        }

        private void ActivateHit()
        {
            mOnHit?.Invoke(mKind, mOptionIndex);
        }

        private void DisableColliderIfPresent()
        {
            var collider = GetComponent<BoxCollider2D>();
            if (collider != null)
            {
                collider.enabled = false;
            }
        }
    }

    public enum TavernBoardHitKind
    {
        SelectService,
        Refresh,
        SelectFixCandidate
    }
}
