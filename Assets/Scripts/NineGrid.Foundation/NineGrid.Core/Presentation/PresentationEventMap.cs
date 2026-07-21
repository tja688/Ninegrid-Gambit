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
            string label)
        {
            EventType = eventType;
            InstructionKind = instructionKind;
            Category = category;
            RequiresPlayback = requiresPlayback;
            LocksInput = locksInput;
            Label = label ?? string.Empty;
        }

        public CoreEventType EventType { get; private set; }
        public PresentationInstructionKind InstructionKind { get; private set; }
        public PresentationEventCategory Category { get; private set; }
        public bool RequiresPlayback { get; private set; }
        public bool LocksInput { get; private set; }
        public string Label { get; private set; }
    }

    public static class PresentationEventMap
    {
        private static readonly PresentationEventMapEntry[] sEntries =
        {
            Entry(CoreEventType.ActionStarted, PresentationInstructionKind.MarkActionStarted, PresentationEventCategory.ActionLifecycle, false, false, "Action started"),
            Entry(CoreEventType.ActionFinished, PresentationInstructionKind.MarkActionFinished, PresentationEventCategory.ActionLifecycle, false, false, "Action finished"),
            Entry(CoreEventType.ActionRejected, PresentationInstructionKind.ShowRejectedIntent, PresentationEventCategory.Rejection, true, true, "Rejected intent"),
            Entry(CoreEventType.DamageDealt, PresentationInstructionKind.ShowDamage, PresentationEventCategory.Damage, true, true, "Damage"),
            Entry(CoreEventType.HpChanged, PresentationInstructionKind.UpdateHp, PresentationEventCategory.Stat, true, true, "HP changed"),
            Entry(CoreEventType.ArmorChanged, PresentationInstructionKind.UpdateArmor, PresentationEventCategory.Stat, true, true, "Armor changed"),
            Entry(CoreEventType.Healed, PresentationInstructionKind.UpdateHp, PresentationEventCategory.Stat, true, true, "Healed"),
            Entry(CoreEventType.GoldModified, PresentationInstructionKind.UpdateGold, PresentationEventCategory.Economy, true, true, "Gold changed"),
            Entry(CoreEventType.CardRemoved, PresentationInstructionKind.RemoveCard, PresentationEventCategory.Remove, true, true, "Card removed"),
            Entry(CoreEventType.CardKilled, PresentationInstructionKind.KillCard, PresentationEventCategory.Kill, true, true, "Card killed"),
            Entry(CoreEventType.CardMoved, PresentationInstructionKind.MoveCard, PresentationEventCategory.Move, true, true, "Card moved"),
            Entry(CoreEventType.CardSwapped, PresentationInstructionKind.SwapCards, PresentationEventCategory.Move, true, true, "Cards swapped"),
            Entry(CoreEventType.BoardRotated, PresentationInstructionKind.RotateBoard, PresentationEventCategory.Rotate, true, true, "Board rotated"),
            Entry(CoreEventType.CardDealt, PresentationInstructionKind.DealCard, PresentationEventCategory.Deal, true, true, "Card dealt"),
            Entry(CoreEventType.DrawPileExhausted, PresentationInstructionKind.ShowDrawPileExhausted, PresentationEventCategory.Deal, true, false, "Draw pile exhausted"),
            Entry(CoreEventType.SlotsFilled, PresentationInstructionKind.FillSlots, PresentationEventCategory.Deal, true, false, "Slots filled"),
            Entry(CoreEventType.InteractionChanged, PresentationInstructionKind.UpdateInteractionCount, PresentationEventCategory.Interaction, true, false, "Interaction changed"),
            Entry(CoreEventType.PhaseChanged, PresentationInstructionKind.ChangePhase, PresentationEventCategory.Phase, true, true, "Phase changed"),
            Entry(CoreEventType.NodeStarted, PresentationInstructionKind.StartNode, PresentationEventCategory.Node, true, true, "Node started"),
            Entry(CoreEventType.NodeCompleted, PresentationInstructionKind.CompleteNode, PresentationEventCategory.Node, true, true, "Node completed"),
            Entry(CoreEventType.ItemPicked, PresentationInstructionKind.PickItem, PresentationEventCategory.Item, true, true, "Item picked"),
            Entry(CoreEventType.EmptyClicked, PresentationInstructionKind.ClickEmpty, PresentationEventCategory.Interaction, true, false, "Empty clicked"),
            Entry(CoreEventType.ItemUsed, PresentationInstructionKind.UseItem, PresentationEventCategory.Item, true, true, "Item used"),
            Entry(CoreEventType.EffectTriggered, PresentationInstructionKind.TriggerEffect, PresentationEventCategory.Effect, true, true, "Effect triggered"),
            Entry(CoreEventType.EffectModifierApplied, PresentationInstructionKind.ApplyModifier, PresentationEventCategory.Effect, true, false, "Effect modifier applied"),
            Entry(CoreEventType.EffectDeactivated, PresentationInstructionKind.DeactivateEffect, PresentationEventCategory.Effect, true, false, "Effect deactivated"),
            Entry(CoreEventType.CardSpawned, PresentationInstructionKind.SpawnCard, PresentationEventCategory.Deal, true, true, "Card spawned"),
            Entry(CoreEventType.AvatarAppeared, PresentationInstructionKind.ShowAvatar, PresentationEventCategory.Node, true, true, "Avatar appeared"),
            Entry(CoreEventType.BaseStatModified, PresentationInstructionKind.ModifyBaseStat, PresentationEventCategory.Stat, true, true, "Base stat modified"),
            Entry(CoreEventType.RelicGranted, PresentationInstructionKind.GrantRelic, PresentationEventCategory.Content, true, false, "Relic granted"),
            Entry(CoreEventType.RewardOffered, PresentationInstructionKind.OfferReward, PresentationEventCategory.Reward, true, true, "Reward offered"),
            Entry(CoreEventType.RewardSelected, PresentationInstructionKind.SelectReward, PresentationEventCategory.Reward, true, true, "Reward selected"),
            Entry(CoreEventType.RewardSkipped, PresentationInstructionKind.SkipReward, PresentationEventCategory.Reward, true, true, "Reward skipped"),
            Entry(CoreEventType.RoomChoicesOffered, PresentationInstructionKind.OfferRooms, PresentationEventCategory.Room, true, true, "Room choices offered"),
            Entry(CoreEventType.RoomSelected, PresentationInstructionKind.SelectRoom, PresentationEventCategory.Room, true, true, "Room selected"),
            Entry(CoreEventType.RoomResolved, PresentationInstructionKind.ResolveRoom, PresentationEventCategory.Room, true, true, "Room resolved"),
            Entry(CoreEventType.NodeAdvanced, PresentationInstructionKind.AdvanceNode, PresentationEventCategory.Node, true, true, "Node advanced"),
            Entry(CoreEventType.BoardMarked, PresentationInstructionKind.MarkBoard, PresentationEventCategory.Board, true, true, "Board marked"),
            Entry(CoreEventType.ContentLoaded, PresentationInstructionKind.LoadContent, PresentationEventCategory.Content, true, false, "Content loaded")
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

        private static PresentationEventMapEntry Entry(
            CoreEventType eventType,
            PresentationInstructionKind instructionKind,
            PresentationEventCategory category,
            bool requiresPlayback,
            bool locksInput,
            string label)
        {
            return new PresentationEventMapEntry(eventType, instructionKind, category, requiresPlayback, locksInput, label);
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
