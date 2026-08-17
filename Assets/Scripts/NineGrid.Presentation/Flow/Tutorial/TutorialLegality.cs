using System;
using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Core.Systems;
using NineGrid.Flow.InfoNotice;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation;
using QFramework;

namespace NineGrid.Flow.Tutorial
{
    /// <summary>
    /// 教学合法性层：经 IntentIntake.SetLegalityOverride 接入，
    /// 拒绝时弹 InfoNotice（阶段1错点 / 阶段2范围外）。
    /// </summary>
    public sealed class TutorialLegality
    {
        private readonly IArchitecture mArchitecture;
        private int mPhase = 1;
        private readonly HashSet<int> mPhase1AllowedSlots = new HashSet<int>();
        private readonly HashSet<string> mHintDefIds = new HashSet<string>(StringComparer.Ordinal);

        public TutorialLegality(IArchitecture architecture)
        {
            mArchitecture = architecture;
        }

        public int Phase => mPhase;

        public int Phase2KillCount { get; set; }

        public void ApplyPhase(int phase)
        {
            mPhase = phase;
            Phase2KillCount = 0;
            mPhase1AllowedSlots.Clear();
            mHintDefIds.Clear();

            foreach (var hint in TutorialPhaseLayouts.GetHintDefIds(phase))
            {
                mHintDefIds.Add(hint);
            }

            if (phase != 1)
            {
                return;
            }

            var board = mArchitecture.GetModel<BoardModel>();
            var registry = mArchitecture.GetModel<CardRegistry>();
            for (var slot = SlotId.MinBoardIndex; slot <= SlotId.MaxBoardIndex; slot++)
            {
                var uid = board.GetCardUid(SlotId.Board(slot));
                if (uid <= 0 || !registry.TryGet(uid, out var card) || card == null)
                {
                    continue;
                }

                var defId = card.DefId ?? string.Empty;
                if (mHintDefIds.Contains(defId) || defId == TutorialContentIds.DummyTrapDefId)
                {
                    mPhase1AllowedSlots.Add(slot);
                }
            }
        }

        public bool TryExplain(InputIntent intent)
        {
            if (string.IsNullOrEmpty(intent.Kind))
            {
                return false;
            }

            if (string.Equals(intent.Kind, InputIntentKinds.Attack, StringComparison.Ordinal))
            {
                return TryExplainAttack(intent.TargetId);
            }

            if (string.Equals(intent.Kind, InputIntentKinds.Explore, StringComparison.Ordinal))
            {
                return TryExplainExplore(intent.TargetId);
            }

            if (string.Equals(intent.Kind, InputIntentKinds.UseItem, StringComparison.Ordinal))
            {
                return BoardIntentLegality.TryExplainUseItem(
                    mArchitecture,
                    intent.TargetId,
                    intent.SelectedCardUids,
                    intent.SelectedOption,
                    out _);
            }

            if (string.Equals(intent.Kind, InputIntentKinds.Pickup, StringComparison.Ordinal))
            {
                return BoardIntentLegality.TryExplainPickup(mArchitecture, intent.TargetId, out _);
            }

            if (string.Equals(intent.Kind, InputIntentKinds.RecycleItem, StringComparison.Ordinal))
            {
                return BoardIntentLegality.TryExplainRecycleItem(mArchitecture, intent.TargetId, out _);
            }

            if (string.Equals(intent.Kind, InputIntentKinds.RevealFace, StringComparison.Ordinal))
            {
                return BoardIntentLegality.TryExplainRevealFace(mArchitecture, intent.TargetId, out _);
            }

            if (string.Equals(intent.Kind, InputIntentKinds.BoardWalk, StringComparison.Ordinal))
            {
                return BoardIntentLegality.TryExplainBoardWalk(mArchitecture, intent.TargetId, out _);
            }

            return false;
        }

        private bool TryExplainAttack(int groundSlot)
        {
            if (!BoardIntentLegality.TryExplainAttack(mArchitecture, groundSlot, out _))
            {
                if (mPhase == 2 && IsOccupiedCombatTarget(groundSlot))
                {
                    ShowNotice(TutorialContentIds.NoticeOutOfRange);
                }

                return false;
            }

            if (mPhase == 1 && IsHintAtSlot(groundSlot))
            {
                ShowNotice(TutorialContentIds.NoticeAttackDummy);
            }

            return true;
        }

        private bool TryExplainExplore(int groundSlot)
        {
            if (mPhase == 1)
            {
                if (mPhase1AllowedSlots.Contains(groundSlot))
                {
                    return false;
                }

                ShowNotice(TutorialContentIds.NoticeAttackDummy);
                return false;
            }

            return BoardIntentLegality.TryExplainExplore(mArchitecture, groundSlot, out _);
        }

        private bool IsHintAtSlot(int groundSlot)
        {
            var board = mArchitecture.GetModel<BoardModel>();
            var registry = mArchitecture.GetModel<CardRegistry>();
            var uid = board.GetCardUid(SlotId.Board(groundSlot));
            if (uid <= 0 || !registry.TryGet(uid, out var card) || card == null)
            {
                return false;
            }

            return mHintDefIds.Contains(card.DefId ?? string.Empty);
        }

        private bool IsOccupiedCombatTarget(int groundSlot)
        {
            var board = mArchitecture.GetModel<BoardModel>();
            var registry = mArchitecture.GetModel<CardRegistry>();
            var uid = board.GetCardUid(SlotId.Board(groundSlot));
            return uid > 0
                   && registry.TryGet(uid, out var card)
                   && card != null
                   && CardCombatRules.IsBoardCombatTarget(card.Kind);
        }

        private static void ShowNotice(string message)
        {
            InfoNoticePresenter.Show(message);
        }
    }
}
