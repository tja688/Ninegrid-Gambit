using System;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Flow;
using NineGrid.Presentation;
using UnityEngine;

namespace NineGrid.Flow.BoardBriefTip
{
    /// <summary>
    /// 非战斗场地对象：登记格位认领，悬停文案由 <see cref="GroundFieldHitSurface"/> 从认领者读取。
    /// 配置 <see cref="WalkBoardSlot"/> 后单击提交 BoardWalk（图标占格认领）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoardBriefTipHitProxy : MonoBehaviour
    {
        [SerializeField] private string tipText = string.Empty;

        private int mWalkBoardSlot;

        public string TipText
        {
            get => tipText;
            set => tipText = value ?? string.Empty;
        }

        /// <summary>1–9：跳格目标格；0 表示不认领、不转发点击。</summary>
        public int WalkBoardSlot => mWalkBoardSlot;

        public void Configure(string tip)
        {
            Configure(tip, walkBoardSlot: 0);
        }

        public void Configure(string tip, int walkBoardSlot)
        {
            Configure(tip, walkBoardSlot, hoverEnter: null, hoverExit: null);
        }

        /// <summary>
        /// 悬停附加反馈：<paramref name="hoverEnter"/> / <paramref name="hoverExit"/> 随格位认领生效
        /// （场地图标悬停弹「特色产物」预览用，ADR-0023）。
        /// </summary>
        public void Configure(string tip, int walkBoardSlot, Action hoverEnter, Action hoverExit)
        {
            TipText = tip;
            mWalkBoardSlot = walkBoardSlot;

            var field = GroundFieldGeometryHook.FieldOrNull();
            if (field == null)
            {
                return;
            }

            field.ReleaseAllClaimsForOwner(this);

            if (mWalkBoardSlot < SlotId.MinBoardIndex || mWalkBoardSlot > SlotId.MaxBoardIndex)
            {
                return;
            }

            var claimant = new SlotClaimant(this, tipText, ActivateWalk, hoverEnter, hoverExit);
            field.TryClaimSlot(mWalkBoardSlot, claimant);
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

        private void ActivateWalk()
        {
            if (mWalkBoardSlot < SlotId.MinBoardIndex || mWalkBoardSlot > SlotId.MaxBoardIndex)
            {
                return;
            }

            if (BoardWalkInputHook.TrySubmitBoardWalk == null)
            {
                Debug.LogWarning("[BoardBriefTip] BoardWalkInputHook.TrySubmitBoardWalk 未装配。");
                return;
            }

            // 合法性（含 walk 关闭）一律交 IntentIntake，禁止在此静默吞掉。
            Debug.Log(
                "[BoardBriefTip] BoardWalk submit slot=" + mWalkBoardSlot
                + " choiceOverlay=" + PresentationInputGates.ChoiceOverlayActive
                + " owner=" + PresentationInputGates.CurrentOwner);
            var accepted = BoardWalkInputHook.TrySubmitBoardWalk(mWalkBoardSlot);
            if (!accepted)
            {
                Debug.LogWarning(
                    "[BoardBriefTip] BoardWalk rejected/buffered-fail slot=" + mWalkBoardSlot
                    + " choiceOverlay=" + PresentationInputGates.ChoiceOverlayActive
                    + " owner=" + PresentationInputGates.CurrentOwner);
            }
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
