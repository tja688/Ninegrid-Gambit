using System;
using System.Collections.Generic;

namespace NineGrid.Core
{
    public sealed class PresentationEventMapEntry
    {
        public PresentationEventMapEntry(
            CoreEventType eventType,
            PresentationInstructionKind instructionKind,
            PresentationEventCategory category,
            bool requiresPlayback,
            bool locksInput,
            PresentationBeat beat,
            string label,
            string noneReason = null)
        {
            if (beat == PresentationBeat.None && string.IsNullOrWhiteSpace(noneReason))
            {
                throw new ArgumentException(
                    "PresentationBeat.None requires a non-empty noneReason for " + eventType,
                    "noneReason");
            }

            EventType = eventType;
            InstructionKind = instructionKind;
            Category = category;
            RequiresPlayback = requiresPlayback;
            LocksInput = locksInput;
            Beat = beat;
            Label = label ?? string.Empty;
            NoneReason = noneReason ?? string.Empty;
        }

        public CoreEventType EventType { get; private set; }
        public PresentationInstructionKind InstructionKind { get; private set; }
        public PresentationEventCategory Category { get; private set; }
        public bool RequiresPlayback { get; private set; }
        public bool LocksInput { get; private set; }
        public PresentationBeat Beat { get; private set; }
        public string Label { get; private set; }

        /// <summary>仅当 <see cref="Beat"/> 为 <see cref="PresentationBeat.None"/> 时有意义；说明为何无表演消费。</summary>
        public string NoneReason { get; private set; }
    }

    public static class PresentationEventMap
    {
        private static readonly PresentationEventMapEntry[] sEntries =
        {
            Entry(CoreEventType.ActionStarted, PresentationInstructionKind.MarkActionStarted, PresentationEventCategory.ActionLifecycle, false, false, PresentationBeat.None, "Action started", "Action lifecycle — no card-face stats"),
            Entry(CoreEventType.ActionFinished, PresentationInstructionKind.MarkActionFinished, PresentationEventCategory.ActionLifecycle, false, false, PresentationBeat.None, "Action finished", "Action lifecycle — no card-face stats"),
            Entry(CoreEventType.ActionRejected, PresentationInstructionKind.ShowRejectedIntent, PresentationEventCategory.Rejection, true, true, PresentationBeat.None, "Rejected intent", "Rejection feedback — no card-face stats"),
            Entry(CoreEventType.DamageDealt, PresentationInstructionKind.ShowDamage, PresentationEventCategory.Damage, true, true, PresentationBeat.Impact, "Damage"),
            Entry(CoreEventType.HpChanged, PresentationInstructionKind.UpdateHp, PresentationEventCategory.Stat, true, true, PresentationBeat.Impact, "HP changed"),
            Entry(CoreEventType.ArmorChanged, PresentationInstructionKind.UpdateArmor, PresentationEventCategory.Stat, true, true, PresentationBeat.Impact, "Armor changed"),
            Entry(CoreEventType.Healed, PresentationInstructionKind.UpdateHp, PresentationEventCategory.Stat, true, true, PresentationBeat.Impact, "Healed"),
            Entry(CoreEventType.GoldModified, PresentationInstructionKind.UpdateGold, PresentationEventCategory.Economy, true, true, PresentationBeat.None, "Gold changed", "Gold HUD/floater — not card-face stats"),
            Entry(CoreEventType.CardRemoved, PresentationInstructionKind.RemoveCard, PresentationEventCategory.Remove, true, true, PresentationBeat.None, "Card removed", "Removal animation — no card-face numeric commit"),
            Entry(CoreEventType.CardKilled, PresentationInstructionKind.KillCard, PresentationEventCategory.Kill, true, true, PresentationBeat.Settled, "Card killed"),
            Entry(CoreEventType.CardMoved, PresentationInstructionKind.MoveCard, PresentationEventCategory.Move, true, true, PresentationBeat.None, "Card moved", "Layout move — no card-face stats"),
            Entry(CoreEventType.CardSwapped, PresentationInstructionKind.SwapCards, PresentationEventCategory.Move, true, true, PresentationBeat.None, "Cards swapped", "Layout swap — no card-face stats"),
            Entry(CoreEventType.BoardRotated, PresentationInstructionKind.RotateBoard, PresentationEventCategory.Rotate, true, true, PresentationBeat.None, "Board rotated", "Board layout — no card-face stats"),
            Entry(CoreEventType.CardDealt, PresentationInstructionKind.DealCard, PresentationEventCategory.Deal, true, true, PresentationBeat.Settled, "Card dealt"),
            Entry(CoreEventType.DrawPileExhausted, PresentationInstructionKind.ShowDrawPileExhausted, PresentationEventCategory.Deal, true, false, PresentationBeat.None, "Draw pile exhausted", "Deck UX — no card-face stats"),
            Entry(CoreEventType.SlotsFilled, PresentationInstructionKind.FillSlots, PresentationEventCategory.Deal, true, false, PresentationBeat.None, "Slots filled", "Board fill choreography — no card-face stats"),
            Entry(CoreEventType.InteractionChanged, PresentationInstructionKind.UpdateInteractionCount, PresentationEventCategory.Interaction, true, false, PresentationBeat.None, "Interaction changed", "Interaction counter — no card-face stats"),
            Entry(CoreEventType.PhaseChanged, PresentationInstructionKind.ChangePhase, PresentationEventCategory.Phase, true, true, PresentationBeat.None, "Phase changed", "Phase UI — no card-face stats"),
            Entry(CoreEventType.NodeStarted, PresentationInstructionKind.StartNode, PresentationEventCategory.Node, true, true, PresentationBeat.None, "Node started", "Node lifecycle — no card-face stats"),
            Entry(CoreEventType.NodeCompleted, PresentationInstructionKind.CompleteNode, PresentationEventCategory.Node, true, true, PresentationBeat.None, "Node completed", "Node lifecycle — no card-face stats"),
            Entry(CoreEventType.ItemPicked, PresentationInstructionKind.PickItem, PresentationEventCategory.Item, true, true, PresentationBeat.None, "Item picked", "Item pick VFX — no card-face stats"),
            Entry(CoreEventType.EmptyClicked, PresentationInstructionKind.ClickEmpty, PresentationEventCategory.Interaction, true, false, PresentationBeat.None, "Empty clicked", "Empty click feedback — no card-face stats"),
            Entry(CoreEventType.ItemUsed, PresentationInstructionKind.UseItem, PresentationEventCategory.Item, true, true, PresentationBeat.None, "Item used", "Item use choreography — stats via Stat events"),
            Entry(CoreEventType.EffectTriggered, PresentationInstructionKind.TriggerEffect, PresentationEventCategory.Effect, true, true, PresentationBeat.Impact, "Effect triggered"),
            Entry(CoreEventType.EffectModifierApplied, PresentationInstructionKind.ApplyModifier, PresentationEventCategory.Effect, true, false, PresentationBeat.None, "Effect modifier applied", "Modifier layer — card face uses BaseStatModified/Hp/Armor"),
            Entry(CoreEventType.EffectDeactivated, PresentationInstructionKind.DeactivateEffect, PresentationEventCategory.Effect, true, false, PresentationBeat.None, "Effect deactivated", "Effect teardown pulse — no card-face stats"),
            Entry(CoreEventType.CardSpawned, PresentationInstructionKind.SpawnCard, PresentationEventCategory.Deal, true, true, PresentationBeat.Settled, "Card spawned"),
            Entry(CoreEventType.AvatarAppeared, PresentationInstructionKind.ShowAvatar, PresentationEventCategory.Node, true, true, PresentationBeat.Settled, "Avatar appeared"),
            Entry(CoreEventType.BaseStatModified, PresentationInstructionKind.ModifyBaseStat, PresentationEventCategory.Stat, true, true, PresentationBeat.Settled, "Base stat modified"),
            Entry(CoreEventType.RelicGranted, PresentationInstructionKind.GrantRelic, PresentationEventCategory.Content, true, false, PresentationBeat.None, "Relic granted", "Relic grant UX — stats via follow-up Stat events"),
            Entry(CoreEventType.RewardOffered, PresentationInstructionKind.OfferReward, PresentationEventCategory.Reward, true, true, PresentationBeat.None, "Reward offered", "Reward Bounce face deferred — not card-face commit yet"),
            Entry(CoreEventType.RewardSelected, PresentationInstructionKind.SelectReward, PresentationEventCategory.Reward, true, true, PresentationBeat.None, "Reward selected", "Reward UI — no card-face stats"),
            Entry(CoreEventType.RewardSkipped, PresentationInstructionKind.SkipReward, PresentationEventCategory.Reward, true, true, PresentationBeat.None, "Reward skipped", "Reward UI — no card-face stats"),
            Entry(CoreEventType.RoomChoicesOffered, PresentationInstructionKind.OfferRooms, PresentationEventCategory.Room, true, true, PresentationBeat.None, "Room choices offered", "Room options are not cards — no card-face stats"),
            Entry(CoreEventType.RoomSelected, PresentationInstructionKind.SelectRoom, PresentationEventCategory.Room, true, true, PresentationBeat.None, "Room selected", "Room choice UI — no card-face stats"),
            Entry(CoreEventType.RoomResolved, PresentationInstructionKind.ResolveRoom, PresentationEventCategory.Room, true, true, PresentationBeat.None, "Room resolved", "Room choice UI — no card-face stats"),
            Entry(CoreEventType.NodeAdvanced, PresentationInstructionKind.AdvanceNode, PresentationEventCategory.Node, true, true, PresentationBeat.None, "Node advanced", "Node progression — no card-face stats"),
            Entry(CoreEventType.BoardMarked, PresentationInstructionKind.MarkBoard, PresentationEventCategory.Board, true, true, PresentationBeat.None, "Board marked", "Board mark VFX — no card-face stats"),
            Entry(CoreEventType.ContentLoaded, PresentationInstructionKind.LoadContent, PresentationEventCategory.Content, true, false, PresentationBeat.None, "Content loaded", "Content bootstrap — no card-face stats")
        };

        private static readonly Dictionary<CoreEventType, PresentationEventMapEntry> sByType = BuildLookup();

        public static IReadOnlyList<PresentationEventMapEntry> Entries
        {
            get { return sEntries; }
        }

        public static bool TryGet(CoreEventType eventType, out PresentationEventMapEntry entry)
        {
            return sByType.TryGetValue(eventType, out entry);
        }

        public static PresentationEventMapEntry Get(CoreEventType eventType)
        {
            PresentationEventMapEntry entry;
            if (!TryGet(eventType, out entry))
            {
                throw new InvalidOperationException("Core event has no presentation mapping: " + eventType);
            }

            return entry;
        }

        public static List<CoreEventType> FindMissingCoreEvents()
        {
            var missing = new List<CoreEventType>();
            var values = (CoreEventType[])Enum.GetValues(typeof(CoreEventType));
            for (var i = 0; i < values.Length; i++)
            {
                if (!sByType.ContainsKey(values[i]))
                {
                    missing.Add(values[i]);
                }
            }

            return missing;
        }

        /// <summary>
        /// 找出 Beat=None 却未写理由的条目（构造期也会拦；此方法供穷尽性测试复用）。
        /// </summary>
        public static List<CoreEventType> FindNoneBeatsMissingReason()
        {
            var missing = new List<CoreEventType>();
            for (var i = 0; i < sEntries.Length; i++)
            {
                var entry = sEntries[i];
                if (entry.Beat == PresentationBeat.None && string.IsNullOrWhiteSpace(entry.NoneReason))
                {
                    missing.Add(entry.EventType);
                }
            }

            return missing;
        }

        private static PresentationEventMapEntry Entry(
            CoreEventType eventType,
            PresentationInstructionKind instructionKind,
            PresentationEventCategory category,
            bool requiresPlayback,
            bool locksInput,
            PresentationBeat beat,
            string label,
            string noneReason = null)
        {
            return new PresentationEventMapEntry(
                eventType,
                instructionKind,
                category,
                requiresPlayback,
                locksInput,
                beat,
                label,
                noneReason);
        }

        private static Dictionary<CoreEventType, PresentationEventMapEntry> BuildLookup()
        {
            var lookup = new Dictionary<CoreEventType, PresentationEventMapEntry>();
            for (var i = 0; i < sEntries.Length; i++)
            {
                lookup.Add(sEntries[i].EventType, sEntries[i]);
            }

            return lookup;
        }
    }
}
