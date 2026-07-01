using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Core.Content;

namespace NineGrid.Presentation.Debugging
{
    /// <summary>
    /// 手搓真批次夹具库：供调试控制台「批次编排」面注入共享管线。
    /// </summary>
    public static class PresentationBatchFixtureLibrary
    {
        public static readonly BatchFixtureEntry AttackKillRotateFill = new(
            "batch.attack-kill-rotate-fill",
            "攻击→击杀→旋转→补位",
            PerformanceDebugContextPreset.Board9,
            CreateAttackKillRotateFill);

        public static readonly BatchFixtureEntry HelpCardChain = new(
            "batch.help-card-chain",
            "帮助卡→遗物连锁",
            PerformanceDebugContextPreset.BattlePair,
            CreateHelpCardChain);

        public static readonly BatchFixtureEntry OpeningDeal = new(
            "batch.opening-deal",
            "开局发牌",
            PerformanceDebugContextPreset.None,
            CreateOpeningDeal);

        public static IReadOnlyList<BatchFixtureEntry> All { get; } = new[]
        {
            AttackKillRotateFill,
            HelpCardChain,
            OpeningDeal,
        };

        private static PresentationBatch CreateAttackKillRotateFill()
        {
            var events = new List<CoreGameEvent>
            {
                new CoreGameEvent(CoreEventType.DamageDealt, 1, "attack")
                    .WithActor(PerformanceDebugActorUids.Player)
                    .WithTarget(PerformanceDebugActorUids.Enemy)
                    .WithAmount(3),
                new CoreGameEvent(CoreEventType.CardKilled, 1, "attack")
                    .WithCard(PerformanceDebugActorUids.Enemy),
                new CoreGameEvent(CoreEventType.BoardRotated, 1, "attack")
                    .WithAmount(1),
                new CoreGameEvent(CoreEventType.CardMoved, 1, "attack")
                    .WithCard(PerformanceDebugActorUids.BoardCard(1))
                    .WithSlots(SlotId.Board(1), SlotId.Board(2)),
            };

            return PresentationBatchFixture.Create(101, events, CreateMinimalSnapshot());
        }

        private static PresentationBatch CreateHelpCardChain()
        {
            var events = new List<CoreGameEvent>
            {
                new CoreGameEvent(CoreEventType.ItemUsed, 5, "help")
                    .WithCard(PerformanceDebugActorUids.Player),
                new CoreGameEvent(CoreEventType.EffectTriggered, 5, "help")
                    .WithSource("relic.chain", "help"),
                new CoreGameEvent(CoreEventType.EffectModifierApplied, 5, "help")
                    .WithSource("relic.chain", "modifier"),
            };

            return PresentationBatchFixture.Create(102, events, CreateMinimalSnapshot());
        }

        private static PresentationBatch CreateOpeningDeal()
        {
            var events = new List<CoreGameEvent>
            {
                new CoreGameEvent(CoreEventType.CardDealt, 10, "deal")
                    .WithCard(201)
                    .WithSlots(SlotId.None, SlotId.Board(2)),
                new CoreGameEvent(CoreEventType.CardDealt, 10, "deal")
                    .WithCard(202)
                    .WithSlots(SlotId.None, SlotId.Board(4)),
                new CoreGameEvent(CoreEventType.SlotsFilled, 10, "deal")
                    .WithAmount(2),
            };

            return PresentationBatchFixture.Create(103, events, CreateMinimalSnapshot());
        }

        private static CoreViewSnapshot CreateMinimalSnapshot()
        {
            return new CoreViewSnapshot(
                1,
                new RunView(GamePhase.InteractionLoop, 0, 1, RoomKind.None, 0UL),
                new PlayerView(0, 3, new string[0], new string[0], CardStatView.Empty),
                new BoardView(new BoardSlotView[0], PerformanceDebugActorUids.Player, PerformanceDebugActorUids.PlayerSlot),
                new DeckView(new int[0], new int[0], new int[0], new int[0]),
                new ChoiceView(PendingChoiceKind.None, string.Empty, new RewardEntry[0], new RoomKind[0], RoomKind.None),
                new Dictionary<int, CardView>());
        }
    }

    public sealed class BatchFixtureEntry
    {
        public BatchFixtureEntry(
            string id,
            string displayName,
            PerformanceDebugContextPreset preset,
            System.Func<PresentationBatch> createBatch)
        {
            Id = id;
            DisplayName = displayName;
            Preset = preset;
            CreateBatch = createBatch;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public PerformanceDebugContextPreset Preset { get; }
        public System.Func<PresentationBatch> CreateBatch { get; }
    }
}
