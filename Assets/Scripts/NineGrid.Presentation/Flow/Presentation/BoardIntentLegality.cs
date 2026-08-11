using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using QFramework;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// #10 / ADR-0004 Core 棋盘合法性：只裁 BoardModel / Phase 内容，不含表演时序。
    /// 忙时互斥由 IntentIntake→MainlineBusy→Director 缓冲承担。
    /// <see cref="IPresentationSyncSystem.IsInputLocked"/> 只挡 Core 解算下一拍，不得在此把
    /// 本应 Buffer 的点击误 Reject（PhaseSystem.CanExecute 在锁输入时会清空 Attack/Explore）。
    /// </summary>
    public static class BoardIntentLegality
    {
        public static bool TryExplainBoardWalk(IArchitecture arch, int groundSlot, out string rejectReason)
        {
            rejectReason = null;
            if (arch == null)
            {
                rejectReason = "noArchitecture";
                return false;
            }

            var walk = arch.GetSystem<IAvatarWalkSystem>();
            if (walk == null || !walk.IsEnabled)
            {
                rejectReason = "avatarWalkDisabled";
                return false;
            }

            if (!TryExplainPhaseAllowsBoardCommand(arch, GameCommandKind.MoveAvatar, out rejectReason))
            {
                return false;
            }

            if (groundSlot < SlotId.MinBoardIndex || groundSlot > SlotId.MaxBoardIndex)
            {
                rejectReason = "slotOutOfRange";
                return false;
            }

            var board = arch.GetModel<BoardModel>();
            var from = board.AvatarSlot.Value;
            if (!from.IsBoardSlot)
            {
                rejectReason = "avatarNotOnBoard";
                return false;
            }

            if (from.Index == groundSlot)
            {
                rejectReason = "alreadyAtDestination";
                return false;
            }

            var to = SlotId.Board(groundSlot);
            var occupancy = RoomIcons.RoomIconOccupancy.Current;
            var path = new List<SlotId>(4);
            // 终点：完全空格，或 WalkDestination 图标；SoftBlockOnly 货架/选项不可落格。
            System.Func<SlotId, bool> isDestination = slot =>
                occupancy != null && occupancy.IsWalkDestination(slot);
            System.Func<SlotId, bool> isSoftBlocked = slot =>
                occupancy != null && occupancy.IsSoftBlocked(slot);
            if (!AvatarWalkPathfinder.TryFindPath(
                    board,
                    from,
                    to,
                    path,
                    isDestination,
                    isSoftBlocked))
            {
                rejectReason = "noPath";
                return false;
            }

            return true;
        }

        public static bool TryExplainExplore(IArchitecture arch, int groundSlot, out string rejectReason)
        {
            rejectReason = null;
            if (arch == null)
            {
                rejectReason = "noArchitecture";
                return false;
            }

            if (IsAvatarWalkExclusive(arch, out rejectReason))
            {
                return false;
            }

            if (!TryExplainPhaseAllowsBoardCommand(arch, GameCommandKind.ClickEmpty, out rejectReason))
            {
                return false;
            }

            if (groundSlot < SlotId.MinBoardIndex || groundSlot > SlotId.MaxBoardIndex)
            {
                rejectReason = "slotOutOfRange";
                return false;
            }

            var slot = SlotId.Board(groundSlot);
            var board = arch.GetModel<BoardModel>();
            if (slot == board.AvatarSlot.Value || !board.IsEmpty(slot))
            {
                rejectReason = "notEmptyOrAvatar avatarSlot=" + board.AvatarSlot.Value
                    + " occupant=" + board.GetCardUid(slot);
                return false;
            }

            if (!arch.GetSystem<IBoardSystem>().AreAdjacent(board.AvatarSlot.Value, slot))
            {
                rejectReason = "notAdjacent avatarSlot=" + board.AvatarSlot.Value;
                return false;
            }

            var occupancy = RoomIcons.RoomIconOccupancy.Current;
            if (occupancy != null && occupancy.IsSoftBlocked(slot))
            {
                rejectReason = "softBlockedPresentationOccupancy";
                return false;
            }

            return true;
        }

        public static bool TryExplainAttack(IArchitecture arch, int groundSlot, out string rejectReason)
        {
            rejectReason = null;
            if (arch == null)
            {
                rejectReason = "noArchitecture";
                return false;
            }

            if (IsAvatarWalkExclusive(arch, out rejectReason))
            {
                return false;
            }

            if (!TryExplainPhaseAllowsBoardCommand(arch, GameCommandKind.Attack, out rejectReason))
            {
                return false;
            }

            if (groundSlot < SlotId.MinBoardIndex || groundSlot > SlotId.MaxBoardIndex)
            {
                rejectReason = "slotOutOfRange";
                return false;
            }

            var slot = SlotId.Board(groundSlot);
            var board = arch.GetModel<BoardModel>();
            if (!slot.IsBoardSlot || slot == board.AvatarSlot.Value)
            {
                rejectReason = "notBoardCardSlot";
                return false;
            }

            var targetUid = board.GetCardUid(slot);
            if (targetUid == 0)
            {
                rejectReason = "emptySlot";
                return false;
            }

            var registry = arch.GetModel<CardRegistry>();
            if (!registry.TryGet(targetUid, out var target)
                || !CardCombatRules.IsBoardCombatTarget(target.Kind))
            {
                rejectReason = "notCombatTarget uid=" + targetUid;
                return false;
            }

            if (!arch.GetSystem<IBoardSystem>().AreAdjacent(board.AvatarSlot.Value, slot))
            {
                rejectReason = "notAdjacent avatarSlot=" + board.AvatarSlot.Value;
                return false;
            }

            if (!target.FaceUp)
            {
                rejectReason = "faceDown uid=" + targetUid;
                return false;
            }

            // 嘲讽：允许点击非嘲讽邻怪；实际目标由 AttackIntentScriptFactory /
            // ResolvePlayerAttackTargetUid 重定向，Present 播 PlayTauntRedirectAttackAsync。
            // 此处拒点会与重定向演出打架。
            return true;
        }

        public static bool TryExplainRevealFace(IArchitecture arch, int groundSlot, out string rejectReason)
        {
            rejectReason = null;
            if (arch == null)
            {
                rejectReason = "noArchitecture";
                return false;
            }

            if (IsAvatarWalkExclusive(arch, out rejectReason))
            {
                return false;
            }

            if (!TryExplainPhaseAllowsBoardCommand(arch, GameCommandKind.RevealFace, out rejectReason))
            {
                return false;
            }

            if (groundSlot < SlotId.MinBoardIndex || groundSlot > SlotId.MaxBoardIndex)
            {
                rejectReason = "slotOutOfRange";
                return false;
            }

            var slot = SlotId.Board(groundSlot);
            var board = arch.GetModel<BoardModel>();
            if (!slot.IsBoardSlot || slot == board.AvatarSlot.Value)
            {
                rejectReason = "notBoardCardSlot";
                return false;
            }

            var targetUid = board.GetCardUid(slot);
            if (targetUid == 0)
            {
                rejectReason = "emptySlot";
                return false;
            }

            var registry = arch.GetModel<CardRegistry>();
            if (!registry.TryGet(targetUid, out var target) || target == null)
            {
                rejectReason = "missingCard uid=" + targetUid;
                return false;
            }

            if (target.Kind == CardKind.Avatar)
            {
                rejectReason = "isAvatar";
                return false;
            }

            if (target.FaceUp)
            {
                rejectReason = "alreadyFaceUp uid=" + targetUid;
                return false;
            }

            if (!arch.GetSystem<IBoardSystem>().AreAdjacent(board.AvatarSlot.Value, slot))
            {
                rejectReason = "notAdjacent avatarSlot=" + board.AvatarSlot.Value;
                return false;
            }

            return true;
        }

        public static bool TryExplainUseItem(
            IArchitecture arch,
            int itemUid,
            IReadOnlyList<int> selectedCardUids,
            string selectedOption,
            out string rejectReason)
        {
            rejectReason = null;
            if (arch == null)
            {
                rejectReason = "noArchitecture";
                return false;
            }

            if (!TryExplainPhaseAllowsBoardCommand(arch, GameCommandKind.UseItem, out rejectReason))
            {
                return false;
            }

            if (itemUid <= 0)
            {
                rejectReason = "invalidItemUid";
                return false;
            }

            var registry = arch.GetModel<CardRegistry>();
            if (!registry.TryGet(itemUid, out var card))
            {
                rejectReason = "itemMissing uid=" + itemUid;
                return false;
            }

            if (card.Zone.Value != ZoneId.ItemSlots)
            {
                rejectReason = "notInItemSlots zone=" + card.Zone.Value;
                return false;
            }

            if (!IsRegisteredInItemSlots(arch.GetModel<DeckModel>(), itemUid))
            {
                rejectReason = "notRegisteredInItemSlots";
                return false;
            }

            if (card.Kind != CardKind.Item && card.Kind != CardKind.HelpCard)
            {
                rejectReason = "notUsableItem kind=" + card.Kind;
                return false;
            }

            // ADR-0032：非战斗相位（RoomChoice / RewardItemChoice）仅 usableOutsideBattle=true 的卡放行；
            // InteractionLoop 恒放行（helper 内裁决）。
            if (!ItemUseEligibility.IsUsableInCurrentPhase(arch, card.DefId))
            {
                var phase = arch.GetSystem<IPhaseSystem>();
                rejectReason = "notUsableInPhase phase=" + (phase != null ? phase.CurrentPhase : GamePhase.None);
                return false;
            }

            // selectedCardUids / selectedOption 的细校验仍由 Resolve/BoardSelect 负责；此处只拦明显非法入队。
            _ = selectedCardUids;
            _ = selectedOption;
            return true;
        }

        public static bool TryExplainRecycleItem(IArchitecture arch, int itemUid, out string rejectReason)
        {
            rejectReason = null;
            if (arch == null)
            {
                rejectReason = "noArchitecture";
                return false;
            }

            if (!TryExplainPhaseAllowsBoardCommand(arch, GameCommandKind.RecycleItemSlot, out rejectReason))
            {
                return false;
            }

            if (itemUid <= 0)
            {
                rejectReason = "invalidItemUid";
                return false;
            }

            var registry = arch.GetModel<CardRegistry>();
            if (!registry.TryGet(itemUid, out var card))
            {
                rejectReason = "itemMissing uid=" + itemUid;
                return false;
            }

            if (card.Zone.Value != ZoneId.ItemSlots)
            {
                rejectReason = "notInItemSlots zone=" + card.Zone.Value;
                return false;
            }

            if (!IsRegisteredInItemSlots(arch.GetModel<DeckModel>(), itemUid))
            {
                rejectReason = "notRegisteredInItemSlots";
                return false;
            }

            return true;
        }

        public static bool TryExplainPickup(IArchitecture arch, int groundSlot, out string rejectReason)
        {
            rejectReason = null;
            if (arch == null)
            {
                rejectReason = "noArchitecture";
                return false;
            }

            if (IsAvatarWalkExclusive(arch, out rejectReason))
            {
                return false;
            }

            // 与 PhaseSystem.ApplyPickupItem 对齐：表现可信入口不查 CanExecute。
            // ADR-0004：表演锁步（IsInputLocked）不在此拒；忙时由 IntentIntake 缓冲。
            if (groundSlot < SlotId.MinBoardIndex || groundSlot > SlotId.MaxBoardIndex)
            {
                rejectReason = "slotOutOfRange";
                return false;
            }

            var slot = SlotId.Board(groundSlot);
            var board = arch.GetModel<BoardModel>();
            if (!slot.IsBoardSlot || slot == board.AvatarSlot.Value)
            {
                rejectReason = "notBoardCardSlot";
                return false;
            }

            var targetUid = board.GetCardUid(slot);
            if (targetUid == 0)
            {
                rejectReason = "emptySlot";
                return false;
            }

            var registry = arch.GetModel<CardRegistry>();
            // ADR-0017：可交战桶（Monster|Trap）拒拾取，与 PhaseSystem.ApplyPickupItem 对齐。
            if (!registry.TryGet(targetUid, out var target)
                || CardCombatRules.IsBoardCombatTarget(target.Kind))
            {
                rejectReason = "notPickupable uid=" + targetUid;
                return false;
            }

            if (!arch.GetSystem<IBoardSystem>().AreAdjacent(board.AvatarSlot.Value, slot))
            {
                rejectReason = "notAdjacent avatarSlot=" + board.AvatarSlot.Value;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Phase 内容门：忽略「仅因 IsInputLocked 而 CanExecute=false」——那是时序轴，不是棋盘非法。
        /// </summary>
        private static bool TryExplainPhaseAllowsBoardCommand(
            IArchitecture arch,
            GameCommandKind command,
            out string rejectReason)
        {
            rejectReason = null;
            var phase = arch.GetSystem<IPhaseSystem>();
            if (phase.CanExecute(command))
            {
                return true;
            }

            var sync = arch.GetSystem<IPresentationSyncSystem>();
            var pending = arch.GetModel<PendingChoiceModel>().Kind.Value;
            if (sync != null
                && sync.IsInputLocked
                && pending == PendingChoiceKind.None
                && IsBoardCommandPhaseWithoutLock(phase.CurrentPhase, command))
            {
                return true;
            }

            rejectReason = "notLegal phase=" + phase.CurrentPhase + " pendingChoice=" + pending;
            return false;
        }

        private static bool IsBoardCommandPhaseWithoutLock(GamePhase phase, GameCommandKind command)
        {
            switch (phase)
            {
                case GamePhase.InteractionLoop:
                    return command == GameCommandKind.Attack
                           || command == GameCommandKind.ClickEmpty
                           || command == GameCommandKind.UseItem
                           || command == GameCommandKind.PickupItem
                           || command == GameCommandKind.RevealFace
                           || command == GameCommandKind.RecycleItemSlot;
                case GamePhase.RoomChoice:
                    // ADR-0032：选房相位 UseItem 按卡级 usableOutsideBattle 裁决（TryExplainUseItem）。
                    return command == GameCommandKind.PickupItem
                           || command == GameCommandKind.MoveAvatar
                           || command == GameCommandKind.UseItem
                           || command == GameCommandKind.RecycleItemSlot;
                case GamePhase.RoomEvent:
                    return command == GameCommandKind.MoveAvatar
                           || command == GameCommandKind.EnterRoom
                           || command == GameCommandKind.RecycleItemSlot;
                case GamePhase.RewardItemChoice:
                    // ADR-0032：房内会话相位 UseItem 按卡级 usableOutsideBattle 裁决（TryExplainUseItem）。
                    return command == GameCommandKind.MoveAvatar
                           || command == GameCommandKind.UseItem
                           || command == GameCommandKind.RecycleItemSlot;
                default:
                    return false;
            }
        }

        private static bool IsAvatarWalkExclusive(IArchitecture arch, out string rejectReason)
        {
            rejectReason = null;
            var walk = arch.GetSystem<IAvatarWalkSystem>();
            if (walk != null && walk.IsEnabled)
            {
                rejectReason = "avatarWalkExclusive";
                return true;
            }

            return false;
        }

        private static bool IsRegisteredInItemSlots(DeckModel deck, int itemUid)
        {
            if (deck == null)
            {
                return false;
            }

            var itemSlots = deck.ItemSlotUids;
            for (var i = 0; i < itemSlots.Count; i++)
            {
                if (itemSlots[i] == itemUid)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
