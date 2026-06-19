using System.Collections.Generic;
using NineGrid.Core.Content;
using QFramework;

namespace NineGrid.Core
{
    public sealed class BoardSlotView
    {
        public BoardSlotView(SlotId slot, int cardUid, string defId, CardKind kind, int hp, int armor, int attack, bool blessed)
        {
            Slot = slot;
            CardUid = cardUid;
            DefId = defId ?? string.Empty;
            Kind = kind;
            Hp = hp;
            Armor = armor;
            Attack = attack;
            Blessed = blessed;
        }

        public SlotId Slot { get; private set; }
        public int CardUid { get; private set; }
        public string DefId { get; private set; }
        public CardKind Kind { get; private set; }
        public int Hp { get; private set; }
        public int Armor { get; private set; }
        public int Attack { get; private set; }
        public bool Blessed { get; private set; }
    }

    public sealed class CoreViewSnapshot
    {
        public CoreViewSnapshot(
            int version,
            GamePhase phase,
            int nodeIndex,
            int coins,
            int interactionCount,
            int avatarUid,
            SlotId avatarSlot,
            PendingChoiceKind pendingChoiceKind,
            IReadOnlyList<BoardSlotView> boardSlots,
            IReadOnlyList<RewardEntry> rewardOptions,
            IReadOnlyList<RoomKind> roomOptions,
            RoomKind selectedRoom)
        {
            Version = version;
            Phase = phase;
            NodeIndex = nodeIndex;
            Coins = coins;
            InteractionCount = interactionCount;
            AvatarUid = avatarUid;
            AvatarSlot = avatarSlot;
            PendingChoiceKind = pendingChoiceKind;
            BoardSlots = boardSlots ?? new BoardSlotView[0];
            RewardOptions = rewardOptions ?? new RewardEntry[0];
            RoomOptions = roomOptions ?? new RoomKind[0];
            SelectedRoom = selectedRoom;
        }

        public int Version { get; private set; }
        public GamePhase Phase { get; private set; }
        public int NodeIndex { get; private set; }
        public int Coins { get; private set; }
        public int InteractionCount { get; private set; }
        public int AvatarUid { get; private set; }
        public SlotId AvatarSlot { get; private set; }
        public PendingChoiceKind PendingChoiceKind { get; private set; }
        public IReadOnlyList<BoardSlotView> BoardSlots { get; private set; }
        public IReadOnlyList<RewardEntry> RewardOptions { get; private set; }
        public IReadOnlyList<RoomKind> RoomOptions { get; private set; }
        public RoomKind SelectedRoom { get; private set; }

        public BoardSlotView GetSlot(SlotId slot)
        {
            for (var i = 0; i < BoardSlots.Count; i++)
            {
                if (BoardSlots[i].Slot == slot)
                {
                    return BoardSlots[i];
                }
            }

            return null;
        }
    }

    public static class CoreViewSnapshotFactory
    {
        public static CoreViewSnapshot Capture(IArchitecture architecture)
        {
            var registry = architecture.GetModel<CardRegistry>();
            var board = architecture.GetModel<BoardModel>();
            var run = architecture.GetModel<RunModel>();
            var player = architecture.GetModel<PlayerModel>();
            var pending = architecture.GetModel<PendingChoiceModel>();
            var slots = new List<BoardSlotView>();

            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var slot = SlotId.Board(i);
                var uid = board.GetCardUid(slot);
                if (uid == 0)
                {
                    slots.Add(new BoardSlotView(slot, 0, string.Empty, CardKind.Unknown, 0, 0, 0, board.IsBlessed(slot)));
                    continue;
                }

                var card = registry.Get(uid);
                slots.Add(new BoardSlotView(
                    slot,
                    uid,
                    card.DefId,
                    card.Kind,
                    (int)card.Stats.GetBase(StatId.Hp),
                    (int)card.Stats.GetBase(StatId.Armor),
                    (int)card.Stats.GetBase(StatId.Attack),
                    board.IsBlessed(slot)));
            }

            return new CoreViewSnapshot(
                board.Version.Value + registry.Version.Value + run.Version.Value + player.Version.Value + pending.Version.Value,
                run.Phase.Value,
                run.NodeIndex.Value,
                player.Coins.Value,
                player.InteractionCount.Value,
                board.AvatarUid.Value,
                board.AvatarSlot.Value,
                pending.Kind.Value,
                slots,
                new List<RewardEntry>(pending.RewardOptions),
                new List<RoomKind>(pending.RoomOptions),
                pending.SelectedRoom.Value);
        }
    }
}
