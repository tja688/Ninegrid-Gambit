using System;
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

        private static PresentationBatch CreateAttackKillRotateFill(PerformanceDebugPayload payload)
        {
            int actorUid = ResolveActorUid(payload);
            int targetUid = ResolveTargetUid(payload);
            int amount = ResolveAmount(payload, 3);
            int fromSlot = payload?.GetBoardSlot(PerformanceDebugPayloadKeys.FromSlot, 1) ?? 1;
            int toSlot = payload?.GetBoardSlot(PerformanceDebugPayloadKeys.ToSlot, 2) ?? 2;
            int rotateAmount = payload?.GetInt(PerformanceDebugPayloadKeys.RotateAmount, 1) ?? 1;

            var events = new List<CoreGameEvent>
            {
                new CoreGameEvent(CoreEventType.DamageDealt, 1, "attack")
                    .WithActor(actorUid)
                    .WithTarget(targetUid)
                    .WithAmount(amount),
                new CoreGameEvent(CoreEventType.CardKilled, 1, "attack")
                    .WithCard(targetUid),
                new CoreGameEvent(CoreEventType.BoardRotated, 1, "attack")
                    .WithAmount(rotateAmount),
                new CoreGameEvent(CoreEventType.CardMoved, 1, "attack")
                    .WithCard(PerformanceDebugActorUids.BoardCard(fromSlot))
                    .WithSlots(SlotId.Board(fromSlot), SlotId.Board(toSlot)),
            };

            return PresentationBatchFixture.Create(101, events, CreateMinimalSnapshot());
        }

        private static PresentationBatch CreateHelpCardChain(PerformanceDebugPayload payload)
        {
            int cardUid = ResolveActorUid(payload);
            string effectId = payload?.GetString(PerformanceDebugPayloadKeys.EffectId, "relic.chain") ?? "relic.chain";

            var events = new List<CoreGameEvent>
            {
                new CoreGameEvent(CoreEventType.ItemUsed, 5, "help")
                    .WithCard(cardUid),
                new CoreGameEvent(CoreEventType.EffectTriggered, 5, "help")
                    .WithSource(effectId, "help"),
                new CoreGameEvent(CoreEventType.EffectModifierApplied, 5, "help")
                    .WithSource(effectId, "modifier"),
            };

            return PresentationBatchFixture.Create(102, events, CreateMinimalSnapshot());
        }

        private static PresentationBatch CreateOpeningDeal(PerformanceDebugPayload payload)
        {
            int firstSlot = payload?.GetBoardSlot(PerformanceDebugPayloadKeys.ToSlot, 2) ?? 2;
            int secondSlot = firstSlot == 2 ? 4 : firstSlot + 2;

            var events = new List<CoreGameEvent>
            {
                new CoreGameEvent(CoreEventType.CardDealt, 10, "deal")
                    .WithCard(201)
                    .WithSlots(SlotId.None, SlotId.Board(firstSlot)),
                new CoreGameEvent(CoreEventType.CardDealt, 10, "deal")
                    .WithCard(202)
                    .WithSlots(SlotId.None, SlotId.Board(secondSlot)),
                new CoreGameEvent(CoreEventType.SlotsFilled, 10, "deal")
                    .WithAmount(2),
            };

            return PresentationBatchFixture.Create(103, events, CreateMinimalSnapshot());
        }

        private static int ResolveActorUid(PerformanceDebugPayload payload)
        {
            return payload?.GetInt(PerformanceDebugPayloadKeys.ActorUid, PerformanceDebugActorUids.Player)
                ?? PerformanceDebugActorUids.Player;
        }

        private static int ResolveTargetUid(PerformanceDebugPayload payload)
        {
            return payload?.GetInt(PerformanceDebugPayloadKeys.TargetUid, PerformanceDebugActorUids.Enemy)
                ?? PerformanceDebugActorUids.Enemy;
        }

        private static int ResolveAmount(PerformanceDebugPayload payload, int fallback)
        {
            if (payload == null)
            {
                return fallback;
            }

            int amount = payload.GetInt(PerformanceDebugPayloadKeys.Amount, fallback);
            return amount > 0 ? amount : fallback;
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
            Func<PerformanceDebugPayload, PresentationBatch> createBatch)
        {
            Id = id;
            DisplayName = displayName;
            Preset = preset;
            CreateBatch = createBatch;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public PerformanceDebugContextPreset Preset { get; }
        public Func<PerformanceDebugPayload, PresentationBatch> CreateBatch { get; }
    }
}
