using System;
using NineGrid.Cards;
using UnityEngine;

namespace NineGrid.Flow.RewardBoard
{
    /// <summary>
    /// 特殊奖励房真卡：格位认领 + 悬停简要文案 + 任意距离点击（ADR-0020 / #94 / ADR-0023）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RewardBoardHitProxy : MonoBehaviour
    {
        private int mShelfIndex;
        private string mTip = string.Empty;
        private Action<int> mOnHit;

        public void Configure(int shelfIndex, string tip, Action<int> onHit, int boardSlot)
        {
            mShelfIndex = shelfIndex;
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

            var claimant = new SlotClaimant(this, mTip, ActivateHit);
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
            mOnHit?.Invoke(mShelfIndex);
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
}
