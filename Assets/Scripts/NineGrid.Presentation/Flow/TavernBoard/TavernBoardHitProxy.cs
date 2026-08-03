using System;
using NineGrid.Cards;
using UnityEngine;

namespace NineGrid.Flow.TavernBoard
{
    /// <summary>
    /// 卡店服务 / 刷新 / 二级候选：格位认领 + 悬停简要文案 + 任意距离点击（#93 / ADR-0020 / ADR-0023）。
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
            int boardSlot)
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
