using System.Text;
using NineGrid.Core.Effects;
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
            ProfessionId = ProfessionCatalog.Jester;
            AvatarDefId = "avatar.default";
            AvatarMaxHp = ProfessionCatalog.Default.MaxHp;
            AvatarAttack = ProfessionCatalog.Default.Attack;
            AvatarArmor = ProfessionCatalog.Default.Armor;
            AvatarRecovery = ProfessionCatalog.Default.Recovery;
        }

        public ulong Seed { get; set; }
        public string ProfessionId { get; set; }
        public string AvatarDefId { get; set; }
        public int AvatarMaxHp { get; set; }
        public int AvatarAttack { get; set; }
        public int AvatarArmor { get; set; }
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

            // Bootstrap / 失败重开会 registry.Clear 并重用 uid；若不先清效果运行时，
            // 上一局 Trigger 会挂到同 uid 新卡上，导致坚硬等 OnBattle 效果叠乘秒杀。
            // EffectSystem.Clear 会按实例 UnRegister 效果 Trigger；TriggerSystem.Clear 是兜底，
            // 但会一并清掉 Economy/Reward 等系统级反应，故 Clear 后必须 Rebind。
            architecture.GetSystem<IContentSystem>().ClearRuntimeEffects();
            architecture.GetSystem<IEffectSystem>().Clear();
            architecture.GetSystem<ITriggerSystem>().Clear();
            architecture.GetSystem<IEconomySystem>().RebindSystemTriggers();
            architecture.GetSystem<IRewardSystem>().RebindSystemTriggers();
            architecture.GetSystem<IActionPipelineSystem>().Clear();

            rng.SetSeed(options.Seed);
            registry.Clear();
            board.Reset();
            deck.Clear();
            player.Reset();
            run.Reset(options.Seed);
            pendingChoice.Clear();
            architecture.GetModel<BattleContextModel>().Reset();

            var avatar = CreateAvatar(architecture, options, registry);
            avatar.Stats.SetBase(StatId.InteractionRange, 1);

            board.SetAvatar(avatar, SlotId.Board(5));
            ApplyProfession(architecture, options, avatar);

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
            ApplyAvatarStats(avatar, options);
            return avatar;
        }

        private static void ApplyAvatarStats(CardInstance avatar, InitialGameOptions options)
        {
            avatar.Stats.SetBase(StatId.MaxHp, options.AvatarMaxHp);
            avatar.Stats.SetBase(StatId.Hp, options.AvatarMaxHp);
            avatar.Stats.SetBase(StatId.Attack, options.AvatarAttack);
            avatar.Stats.SetBase(StatId.Armor, options.AvatarArmor);
            avatar.Stats.SetBase(StatId.CurrentArmor, options.AvatarArmor);
            avatar.Stats.SetBase(StatId.Recovery, options.AvatarRecovery);
        }

        private static void ApplyProfession(IArchitecture architecture, InitialGameOptions options, CardInstance avatar)
        {
            var profession = ProfessionCatalog.Get(options.ProfessionId);
            var player = architecture.GetModel<PlayerModel>();
            var content = architecture.GetSystem<IContentSystem>();

            player.SetProfession(profession.DefId);
            ApplyAvatarStats(avatar, options);

            if (string.IsNullOrEmpty(profession.InitialSkillDefId))
            {
                return;
            }

            player.AddSkill(profession.InitialSkillDefId);
            content.ActivatePlayerSkill(profession.InitialSkillDefId);
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
