using System;
using NineGrid.Cards;
using UnityEngine;

namespace NineGrid.Flow.AttributeBoard
{
    /// <summary>
    /// 属性房候选真卡：格位认领 + 悬停简要文案 + 任意距离点击（#137 / ADR-0020 / ADR-0023）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AttributeBoardHitProxy : MonoBehaviour
    {
        private int mCandidateIndex;
        private string mTip = string.Empty;
        private Action<int> mOnHit;

        public void Configure(int candidateIndex, string tip, Action<int> onHit, int boardSlot)
        {
            mCandidateIndex = candidateIndex;
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
            mOnHit?.Invoke(mCandidateIndex);
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
