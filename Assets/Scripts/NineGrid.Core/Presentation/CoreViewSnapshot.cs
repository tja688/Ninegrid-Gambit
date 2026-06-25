using System.Collections.Generic;
using NineGrid.Core.Content;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using QFramework;

namespace NineGrid.Core
{
    public sealed class CardStatView
    {
        public CardStatView(
            int baseMaxHp,
            int effectiveMaxHp,
            int baseHp,
            int effectiveHp,
            int baseArmor,
            int effectiveArmor,
            int baseAttack,
            int effectiveAttack,
            int baseRecovery,
            int effectiveRecovery,
            int baseInteractionRange,
            int effectiveInteractionRange)
        {
            BaseMaxHp = baseMaxHp;
            EffectiveMaxHp = effectiveMaxHp;
            BaseHp = baseHp;
            EffectiveHp = effectiveHp;
            BaseArmor = baseArmor;
            EffectiveArmor = effectiveArmor;
            BaseAttack = baseAttack;
            EffectiveAttack = effectiveAttack;
            BaseRecovery = baseRecovery;
            EffectiveRecovery = effectiveRecovery;
            BaseInteractionRange = baseInteractionRange;
            EffectiveInteractionRange = effectiveInteractionRange;
        }

        public int BaseMaxHp { get; private set; }
        public int EffectiveMaxHp { get; private set; }
        public int BaseHp { get; private set; }
        public int EffectiveHp { get; private set; }
        public int BaseArmor { get; private set; }
        public int EffectiveArmor { get; private set; }
        public int BaseAttack { get; private set; }
        public int EffectiveAttack { get; private set; }
        public int BaseRecovery { get; private set; }
        public int EffectiveRecovery { get; private set; }
        public int BaseInteractionRange { get; private set; }
        public int EffectiveInteractionRange { get; private set; }

        public static readonly CardStatView Empty = new CardStatView(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    }

    public sealed class CardView
    {
        public CardView(
            int uid,
            string defId,
            CardKind kind,
            ZoneId zone,
            SlotId slot,
            CardStatView stats,
            IReadOnlyList<string> effectIds)
        {
            Uid = uid;
            DefId = defId ?? string.Empty;
            Kind = kind;
            Zone = zone;
            Slot = slot;
            Stats = stats ?? CardStatView.Empty;
            EffectIds = effectIds ?? new string[0];
        }

        public int Uid { get; private set; }
        public string DefId { get; private set; }
        public CardKind Kind { get; private set; }
        public ZoneId Zone { get; private set; }
        public SlotId Slot { get; private set; }
        public CardStatView Stats { get; private set; }
        public IReadOnlyList<string> EffectIds { get; private set; }
    }

    public sealed class BoardSlotView
    {
        public BoardSlotView(
            SlotId slot,
            int cardUid,
            string defId,
            CardKind kind,
            int hp,
            int effectiveHp,
            int maxHp,
            int effectiveMaxHp,
            int armor,
            int effectiveArmor,
            int attack,
            int effectiveAttack,
            bool blessed)
        {
            Slot = slot;
            CardUid = cardUid;
            DefId = defId ?? string.Empty;
            Kind = kind;
            Hp = hp;
            EffectiveHp = effectiveHp;
            MaxHp = maxHp;
            EffectiveMaxHp = effectiveMaxHp;
            Armor = armor;
            EffectiveArmor = effectiveArmor;
            Attack = attack;
            EffectiveAttack = effectiveAttack;
            Blessed = blessed;
        }

        public SlotId Slot { get; private set; }
        public int CardUid { get; private set; }
        public string DefId { get; private set; }
        public CardKind Kind { get; private set; }
        public int Hp { get; private set; }
        public int EffectiveHp { get; private set; }
        public int MaxHp { get; private set; }
        public int EffectiveMaxHp { get; private set; }
        public int Armor { get; private set; }
        public int EffectiveArmor { get; private set; }
        public int Attack { get; private set; }
        public int EffectiveAttack { get; private set; }
        public bool Blessed { get; private set; }
    }

    public sealed class BoardView
    {
        public BoardView(IReadOnlyList<BoardSlotView> slots, int avatarUid, SlotId avatarSlot)
        {
            Slots = slots ?? new BoardSlotView[0];
            AvatarUid = avatarUid;
            AvatarSlot = avatarSlot;
        }

        public IReadOnlyList<BoardSlotView> Slots { get; private set; }
        public int AvatarUid { get; private set; }
        public SlotId AvatarSlot { get; private set; }
    }

    public sealed class DeckView
    {
        public DeckView(
            IReadOnlyList<int> drawPileUids,
            IReadOnlyList<int> itemSlotUids,
            IReadOnlyList<int> playerCardPoolUids,
            IReadOnlyList<int> enemyCardPoolUids)
        {
            DrawPileUids = drawPileUids ?? new int[0];
            ItemSlotUids = itemSlotUids ?? new int[0];
            PlayerCardPoolUids = playerCardPoolUids ?? new int[0];
            EnemyCardPoolUids = enemyCardPoolUids ?? new int[0];
        }

        public IReadOnlyList<int> DrawPileUids { get; private set; }
        public IReadOnlyList<int> ItemSlotUids { get; private set; }
        public IReadOnlyList<int> PlayerCardPoolUids { get; private set; }
        public IReadOnlyList<int> EnemyCardPoolUids { get; private set; }
    }

    public sealed class RunView
    {
        public RunView(GamePhase phase, int nodeIndex, int floor, RoomKind room, ulong seed)
        {
            Phase = phase;
            NodeIndex = nodeIndex;
            Floor = floor;
            Room = room;
            Seed = seed;
        }

        public GamePhase Phase { get; private set; }
        public int NodeIndex { get; private set; }
        public int Floor { get; private set; }
        public RoomKind Room { get; private set; }
        public ulong Seed { get; private set; }
    }

    public sealed class PlayerView
    {
        public PlayerView(
            int coins,
            int interactionCount,
            IReadOnlyList<string> relicDefIds,
            IReadOnlyList<string> skillDefIds,
            CardStatView avatarStats)
        {
            Coins = coins;
            InteractionCount = interactionCount;
            RelicDefIds = relicDefIds ?? new string[0];
            SkillDefIds = skillDefIds ?? new string[0];
            AvatarStats = avatarStats ?? CardStatView.Empty;
        }

        public int Coins { get; private set; }
        public int InteractionCount { get; private set; }
        public IReadOnlyList<string> RelicDefIds { get; private set; }
        public IReadOnlyList<string> SkillDefIds { get; private set; }
        public CardStatView AvatarStats { get; private set; }
    }

    public sealed class ChoiceView
    {
        public ChoiceView(
            PendingChoiceKind kind,
            string poolId,
            IReadOnlyList<RewardEntry> rewardOptions,
            IReadOnlyList<RoomKind> roomOptions,
            RoomKind selectedRoom)
        {
            Kind = kind;
            PoolId = poolId ?? string.Empty;
            RewardOptions = rewardOptions ?? new RewardEntry[0];
            RoomOptions = roomOptions ?? new RoomKind[0];
            SelectedRoom = selectedRoom;
        }

        public PendingChoiceKind Kind { get; private set; }
        public string PoolId { get; private set; }
        public IReadOnlyList<RewardEntry> RewardOptions { get; private set; }
        public IReadOnlyList<RoomKind> RoomOptions { get; private set; }
        public RoomKind SelectedRoom { get; private set; }
    }

    public sealed class CoreViewSnapshot
    {
        public CoreViewSnapshot(
            int version,
            RunView run,
            PlayerView player,
            BoardView board,
            DeckView deck,
            ChoiceView choice,
            IReadOnlyDictionary<int, CardView> cards)
        {
            Version = version;
            Run = run ?? new RunView(GamePhase.None, 0, 1, RoomKind.None, 0UL);
            Player = player ?? new PlayerView(0, 0, new string[0], new string[0], CardStatView.Empty);
            Board = board ?? new BoardView(new BoardSlotView[0], 0, SlotId.None);
            Deck = deck ?? new DeckView(new int[0], new int[0], new int[0], new int[0]);
            Choice = choice ?? new ChoiceView(PendingChoiceKind.None, string.Empty, new RewardEntry[0], new RoomKind[0], RoomKind.None);
            Cards = cards ?? new Dictionary<int, CardView>();
        }

        public int Version { get; private set; }
        public RunView Run { get; private set; }
        public PlayerView Player { get; private set; }
        public BoardView Board { get; private set; }
        public DeckView Deck { get; private set; }
        public ChoiceView Choice { get; private set; }
        public IReadOnlyDictionary<int, CardView> Cards { get; private set; }

        public GamePhase Phase
        {
            get { return Run.Phase; }
        }

        public int NodeIndex
        {
            get { return Run.NodeIndex; }
        }

        public int Coins
        {
            get { return Player.Coins; }
        }

        public int InteractionCount
        {
            get { return Player.InteractionCount; }
        }

        public int AvatarUid
        {
            get { return Board.AvatarUid; }
        }

        public SlotId AvatarSlot
        {
            get { return Board.AvatarSlot; }
        }

        public PendingChoiceKind PendingChoiceKind
        {
            get { return Choice.Kind; }
        }

        public IReadOnlyList<BoardSlotView> BoardSlots
        {
            get { return Board.Slots; }
        }

        public IReadOnlyList<RewardEntry> RewardOptions
        {
            get { return Choice.RewardOptions; }
        }

        public IReadOnlyList<RoomKind> RoomOptions
        {
            get { return Choice.RoomOptions; }
        }

        public RoomKind SelectedRoom
        {
            get { return Choice.SelectedRoom; }
        }

        public BoardSlotView GetSlot(SlotId slot)
        {
            for (var i = 0; i < Board.Slots.Count; i++)
            {
                if (Board.Slots[i].Slot == slot)
                {
                    return Board.Slots[i];
                }
            }

            return null;
        }

        public bool TryGetCard(int cardUid, out CardView card)
        {
            if (cardUid > 0 && Cards != null && Cards.TryGetValue(cardUid, out card))
            {
                return true;
            }

            card = null;
            return false;
        }
    }

    public static class CoreViewSnapshotFactory
    {
        public static CoreViewSnapshot Capture(IArchitecture architecture)
        {
            var registry = architecture.GetModel<CardRegistry>();
            var board = architecture.GetModel<BoardModel>();
            var deck = architecture.GetModel<DeckModel>();
            var run = architecture.GetModel<RunModel>();
            var player = architecture.GetModel<PlayerModel>();
            var pending = architecture.GetModel<PendingChoiceModel>();
            var statSystem = architecture.GetSystem<IStatSystem>();

            var boardSlots = CaptureBoardSlots(board, registry, statSystem);
            var boardView = new BoardView(boardSlots, board.AvatarUid.Value, board.AvatarSlot.Value);
            var deckView = CaptureDeckView(deck);
            var cards = CaptureVisibleCards(registry, statSystem, boardView, deckView);

            var avatarStats = CardStatView.Empty;
            if (board.AvatarUid.Value > 0 && registry.TryGet(board.AvatarUid.Value, out var avatar))
            {
                avatarStats = CaptureCardStats(statSystem, avatar);
            }

            var runView = new RunView(
                run.Phase.Value,
                run.NodeIndex.Value,
                run.Floor.Value,
                run.Room.Value,
                run.Seed.Value);

            var playerView = new PlayerView(
                player.Coins.Value,
                player.InteractionCount.Value,
                new List<string>(player.RelicDefIds),
                new List<string>(player.SkillDefIds),
                avatarStats);

            var choiceView = new ChoiceView(
                pending.Kind.Value,
                pending.PoolId.Value,
                new List<RewardEntry>(pending.RewardOptions),
                new List<RoomKind>(pending.RoomOptions),
                pending.SelectedRoom.Value);

            var version = board.Version.Value
                + registry.Version.Value
                + deck.Version.Value
                + run.Version.Value
                + player.Version.Value
                + pending.Version.Value;

            return new CoreViewSnapshot(version, runView, playerView, boardView, deckView, choiceView, cards);
        }

        private static List<BoardSlotView> CaptureBoardSlots(
            BoardModel board,
            CardRegistry registry,
            IStatSystem statSystem)
        {
            var slots = new List<BoardSlotView>();

            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var slot = SlotId.Board(i);
                var uid = board.GetCardUid(slot);
                if (uid == 0)
                {
                    slots.Add(new BoardSlotView(
                        slot,
                        0,
                        string.Empty,
                        CardKind.Unknown,
                        0,
                        0,
                        0,
                        0,
                        0,
                        0,
                        0,
                        0,
                        board.IsBlessed(slot)));
                    continue;
                }

                var card = registry.Get(uid);
                var stats = CaptureCardStats(statSystem, card);
                slots.Add(new BoardSlotView(
                    slot,
                    uid,
                    card.DefId,
                    card.Kind,
                    stats.BaseHp,
                    stats.EffectiveHp,
                    stats.BaseMaxHp,
                    stats.EffectiveMaxHp,
                    stats.BaseArmor,
                    stats.EffectiveArmor,
                    stats.BaseAttack,
                    stats.EffectiveAttack,
                    board.IsBlessed(slot)));
            }

            return slots;
        }

        private static DeckView CaptureDeckView(DeckModel deck)
        {
            return new DeckView(
                new List<int>(deck.DrawPileUids),
                new List<int>(deck.ItemSlotUids),
                new List<int>(deck.PlayerCardPoolUids),
                new List<int>(deck.EnemyCardPoolUids));
        }

        private static Dictionary<int, CardView> CaptureVisibleCards(
            CardRegistry registry,
            IStatSystem statSystem,
            BoardView board,
            DeckView deck)
        {
            var cards = new Dictionary<int, CardView>();
            var seen = new HashSet<int>();

            for (var i = 0; i < board.Slots.Count; i++)
            {
                var slotView = board.Slots[i];
                if (slotView.CardUid > 0)
                {
                    AddCardView(cards, seen, registry, statSystem, slotView.CardUid, ZoneId.Board, slotView.Slot);
                }
            }

            AddZoneCards(cards, seen, registry, statSystem, deck.DrawPileUids, ZoneId.DrawPile);
            AddZoneCards(cards, seen, registry, statSystem, deck.ItemSlotUids, ZoneId.ItemSlots);
            AddZoneCards(cards, seen, registry, statSystem, deck.PlayerCardPoolUids, ZoneId.PlayerCardPool);
            AddZoneCards(cards, seen, registry, statSystem, deck.EnemyCardPoolUids, ZoneId.EnemyCardPool);

            return cards;
        }

        private static void AddZoneCards(
            Dictionary<int, CardView> cards,
            HashSet<int> seen,
            CardRegistry registry,
            IStatSystem statSystem,
            IReadOnlyList<int> uids,
            ZoneId zone)
        {
            for (var i = 0; i < uids.Count; i++)
            {
                AddCardView(cards, seen, registry, statSystem, uids[i], zone, SlotId.None);
            }
        }

        private static void AddCardView(
            Dictionary<int, CardView> cards,
            HashSet<int> seen,
            CardRegistry registry,
            IStatSystem statSystem,
            int uid,
            ZoneId zone,
            SlotId slot)
        {
            if (uid <= 0 || !seen.Add(uid) || !registry.TryGet(uid, out var card))
            {
                return;
            }

            cards[uid] = new CardView(
                uid,
                card.DefId,
                card.Kind,
                zone,
                slot,
                CaptureCardStats(statSystem, card),
                new List<string>(card.EffectIds));
        }

        private static CardStatView CaptureCardStats(IStatSystem statSystem, CardInstance card)
        {
            if (card == null)
            {
                return CardStatView.Empty;
            }

            return new CardStatView(
                (int)card.Stats.GetBase(StatId.MaxHp),
                statSystem.GetEffectiveInt(card, StatId.MaxHp),
                (int)card.Stats.GetBase(StatId.Hp),
                statSystem.GetEffectiveInt(card, StatId.Hp),
                (int)card.Stats.GetBase(StatId.Armor),
                statSystem.GetEffectiveInt(card, StatId.Armor),
                (int)card.Stats.GetBase(StatId.Attack),
                statSystem.GetEffectiveInt(card, StatId.Attack),
                (int)card.Stats.GetBase(StatId.Recovery),
                statSystem.GetEffectiveInt(card, StatId.Recovery),
                (int)card.Stats.GetBase(StatId.InteractionRange),
                statSystem.GetEffectiveInt(card, StatId.InteractionRange));
        }
    }
}
