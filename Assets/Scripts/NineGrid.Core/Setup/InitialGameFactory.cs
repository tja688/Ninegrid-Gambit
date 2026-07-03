using System.Text;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using QFramework;

namespace NineGrid.Core
{
    public sealed class InitialGameOptions
    {
        public InitialGameOptions()
        {
            Seed = 1UL;
            AvatarDefId = "avatar.default";
            AvatarMaxHp = 30;
            AvatarAttack = 1;
            AvatarRecovery = 1;
        }

        public ulong Seed { get; set; }
        public string AvatarDefId { get; set; }
        public int AvatarMaxHp { get; set; }
        public int AvatarAttack { get; set; }
        public int AvatarRecovery { get; set; }
    }

    public sealed class InitialGameSnapshot
    {
        public int AvatarUid { get; set; }
        public SlotId AvatarSlot { get; set; }
        public ulong Seed { get; set; }
        public string Report { get; set; }

        public override string ToString()
        {
            return Report ?? string.Empty;
        }
    }

    public static class InitialGameFactory
    {
        public static InitialGameSnapshot Create(IArchitecture architecture)
        {
            return Create(architecture, new InitialGameOptions());
        }

        public static InitialGameSnapshot Create(IArchitecture architecture, InitialGameOptions options)
        {
            options = options ?? new InitialGameOptions();

            var rng = architecture.GetUtility<IRngUtility>();
            var registry = architecture.GetModel<CardRegistry>();
            var board = architecture.GetModel<BoardModel>();
            var deck = architecture.GetModel<DeckModel>();
            var player = architecture.GetModel<PlayerModel>();
            var run = architecture.GetModel<RunModel>();
            var pendingChoice = architecture.GetModel<PendingChoiceModel>();

            rng.SetSeed(options.Seed);
            registry.Clear();
            board.Reset();
            deck.Clear();
            player.Reset();
            run.Reset(options.Seed);
            pendingChoice.Clear();

            var avatar = CreateAvatar(architecture, options, registry);
            avatar.Stats.SetBase(StatId.InteractionRange, 1);

            board.SetAvatar(avatar, SlotId.Board(5));

            var snapshot = new InitialGameSnapshot
            {
                AvatarUid = avatar.Uid,
                AvatarSlot = board.AvatarSlot.Value,
                Seed = options.Seed
            };
            snapshot.Report = BuildReport(architecture, snapshot);
            return snapshot;
        }

        public static string BuildReport(IArchitecture architecture, InitialGameSnapshot snapshot)
        {
            var registry = architecture.GetModel<CardRegistry>();
            var board = architecture.GetModel<BoardModel>();
            var deck = architecture.GetModel<DeckModel>();
            var avatar = registry.Get(snapshot.AvatarUid);

            var builder = new StringBuilder();
            builder.AppendLine("NineGrid Initial State");
            builder.AppendLine("Seed: " + snapshot.Seed);
            builder.AppendLine("Avatar: #" + avatar.Uid + " " + avatar.DefId + " @ " + board.AvatarSlot.Value);
            builder.AppendLine("BoardCards: " + (HasBoardCards(board) ? "occupied" : "empty"));
            builder.AppendLine("DrawPile: " + deck.DrawPileUids.Count);
            builder.AppendLine("PlayerPool: " + deck.PlayerCardPoolUids.Count);
            builder.AppendLine("EnemyPool: " + deck.EnemyCardPoolUids.Count);
            builder.AppendLine("ItemSlots: " + deck.ItemSlotUids.Count);
            return builder.ToString();
        }

        private static CardInstance CreateAvatar(IArchitecture architecture, InitialGameOptions options, CardRegistry registry)
        {
            var content = architecture.GetSystem<IContentSystem>();
            var draft = content.CreateDraft(options.AvatarDefId);
            CardInstance avatar;
            if (draft.Kind != CardKind.Unknown)
            {
                avatar = draft.Create(registry);
                content.ApplyContentToCard(avatar);
                return avatar;
            }

            avatar = registry.Create(options.AvatarDefId, CardKind.Avatar);
            avatar.Stats.SetBase(StatId.MaxHp, options.AvatarMaxHp);
            avatar.Stats.SetBase(StatId.Hp, options.AvatarMaxHp);
            avatar.Stats.SetBase(StatId.Attack, options.AvatarAttack);
            avatar.Stats.SetBase(StatId.Recovery, options.AvatarRecovery);
            return avatar;
        }

        private static bool HasBoardCards(BoardModel board)
        {
            foreach (var ignored in board.BoardCardUids())
            {
                return true;
            }

            return false;
        }
    }
}
