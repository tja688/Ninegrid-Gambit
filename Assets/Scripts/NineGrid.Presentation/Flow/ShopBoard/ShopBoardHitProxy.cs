using System;
using NineGrid.Cards;
using UnityEngine;

namespace NineGrid.Flow.ShopBoard
{
    /// <summary>
    /// 商店货架 / 刷新：格位认领 + 悬停简要文案 + 任意距离点击（ADR-0020 / ADR-0023）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShopBoardHitProxy : MonoBehaviour
    {
        private ShopBoardHitKind mKind;
        private int mShelfIndex;
        private string mTip = string.Empty;
        private Action<ShopBoardHitKind, int> mOnHit;

        public void Configure(
            ShopBoardHitKind kind,
            int shelfIndex,
            string tip,
            Action<ShopBoardHitKind, int> onHit,
            int boardSlot)
        {
            mKind = kind;
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
            field.TryClaimSlot(boardSlot, claimant);
        }

        private void Awake()
        {
            DisableColliderIfPresent();
        }

        private void OnDisable()
        {
            ReleaseClaims();
        }

        private void ReleaseClaims()
        {
            var field = GroundFieldGeometryHook.FieldOrNull();
            if (field != null)
            {
                field.ReleaseAllClaimsForOwner(this);
            }
        }

        private void ActivateHit()
        {
            mOnHit?.Invoke(mKind, mShelfIndex);
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
