using System;
using System.Collections.Generic;
using NineGrid.Core.Commands;
using NineGrid.Core.Utilities;
using NineGrid.Core.Systems;
using QFramework;

namespace NineGrid.Core.Tests.Simulation
{
    public sealed class HeadlessCommandDecision
    {
        private readonly Func<IArchitecture, CoreCommandResult> mExecute;

        private HeadlessCommandDecision(GameCommandKind kind, string description, Func<IArchitecture, CoreCommandResult> execute)
        {
            Kind = kind;
            Description = description ?? string.Empty;
            mExecute = execute;
        }

        public GameCommandKind Kind { get; private set; }
        public string Description { get; private set; }

        public CoreCommandResult Execute(IArchitecture architecture)
        {
            return mExecute(architecture);
        }

        public static HeadlessCommandDecision StartNode(NodeDeckOptions options, string description)
        {
            return new HeadlessCommandDecision(
                GameCommandKind.StartNode,
                description,
                architecture => architecture.SendCommand(new StartNodeCommand(options)));
        }

        public static HeadlessCommandDecision Attack(SlotId slot)
        {
            return new HeadlessCommandDecision(
                GameCommandKind.Attack,
                "Attack " + slot,
                architecture => architecture.SendCommand(new AttackCommand(slot)));
        }

        public static HeadlessCommandDecision Pickup(SlotId slot)
        {
            return new HeadlessCommandDecision(
                GameCommandKind.PickupItem,
                "Pickup " + slot,
                architecture => architecture.SendCommand(new PickupItemCommand(slot)));
        }

        public static HeadlessCommandDecision ClickEmpty(SlotId slot)
        {
            return new HeadlessCommandDecision(
                GameCommandKind.ClickEmpty,
                "ClickEmpty " + slot,
                architecture => architecture.SendCommand(new ClickEmptyCommand(slot)));
        }

        public static HeadlessCommandDecision UseItem(int uid, string defId)
        {
            return new HeadlessCommandDecision(
                GameCommandKind.UseItem,
                "UseItem #" + uid + " " + defId,
                architecture => architecture.SendCommand(new UseItemCommand(uid)));
        }

        public static HeadlessCommandDecision SelectReward(int optionIndex)
        {
            return new HeadlessCommandDecision(
                GameCommandKind.SelectReward,
                "SelectReward " + optionIndex,
                architecture => architecture.SendCommand(new SelectRewardCommand(optionIndex)));
        }

        public static HeadlessCommandDecision SkipHelpChoice()
        {
            return new HeadlessCommandDecision(
                GameCommandKind.SkipHelpChoice,
                "SkipHelpChoice",
                architecture => architecture.SendCommand(new SkipHelpChoiceCommand()));
        }

        public static HeadlessCommandDecision SelectRoom(int optionIndex)
        {
            return new HeadlessCommandDecision(
                GameCommandKind.SelectRoom,
                "SelectRoom " + optionIndex,
                architecture => architecture.SendCommand(new SelectRoomCommand(optionIndex)));
        }

        public static HeadlessCommandDecision EnterRoom()
        {
            return new HeadlessCommandDecision(
                GameCommandKind.EnterRoom,
                "EnterRoom",
                architecture => architecture.SendCommand(new EnterRoomCommand()));
        }
    }

    public sealed class RandomAgent
    {
        private readonly DeterministicRngUtility mRng;
        private readonly HashSet<int> mUsedItemUids = new HashSet<int>();

        public RandomAgent(ulong seed)
        {
            mRng = new DeterministicRngUtility(seed);
        }

        public bool TryChoose(IArchitecture architecture, out HeadlessCommandDecision decision)
        {
            var phaseSystem = architecture.GetSystem<IPhaseSystem>();
            switch (phaseSystem.CurrentPhase)
            {
                case GamePhase.InteractionLoop:
                    return TryChooseInteraction(architecture, out decision);
                case GamePhase.RewardItemChoice:
                    return TryChooseReward(architecture, out decision);
                case GamePhase.RoomChoice:
                    return TryChooseRoom(architecture, out decision);
                case GamePhase.RoomEvent:
                    decision = HeadlessCommandDecision.EnterRoom();
                    return true;
                default:
                    decision = null;
                    return false;
            }
        }

        private bool TryChooseInteraction(IArchitecture architecture, out HeadlessCommandDecision decision)
        {
            var candidates = new List<HeadlessCommandDecision>();
            AddBoardInteractionCandidates(architecture, candidates);

            var itemCandidates = BuildItemCandidates(architecture);
            if (itemCandidates.Count > 0 && (candidates.Count == 0 || mRng.Range(0, 100) < 25))
            {
                AddRange(candidates, itemCandidates);
            }

            return Pick(candidates, out decision);
        }

        private bool TryChooseReward(IArchitecture architecture, out HeadlessCommandDecision decision)
        {
            var pending = architecture.GetModel<PendingChoiceModel>();
            var candidates = new List<HeadlessCommandDecision>();
            for (var i = 0; i < pending.RewardOptions.Count; i++)
            {
                candidates.Add(HeadlessCommandDecision.SelectReward(i));
            }

            candidates.Add(HeadlessCommandDecision.SkipHelpChoice());
            return Pick(candidates, out decision);
        }

        private bool TryChooseRoom(IArchitecture architecture, out HeadlessCommandDecision decision)
        {
            var pending = architecture.GetModel<PendingChoiceModel>();
            var candidates = new List<HeadlessCommandDecision>();
            for (var i = 0; i < pending.RoomOptions.Count; i++)
            {
                candidates.Add(HeadlessCommandDecision.SelectRoom(i));
            }

            return Pick(candidates, out decision);
        }

        private void AddBoardInteractionCandidates(IArchitecture architecture, List<HeadlessCommandDecision> candidates)
        {
            var registry = architecture.GetModel<CardRegistry>();
            var board = architecture.GetModel<BoardModel>();
            var avatarSlot = board.AvatarSlot.Value;
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var slot = SlotId.Board(i);
                if (slot == avatarSlot || !avatarSlot.IsAdjacentTo(slot))
                {
                    continue;
                }

                var uid = board.GetCardUid(slot);
                if (uid == 0)
                {
                    candidates.Add(HeadlessCommandDecision.ClickEmpty(slot));
                    continue;
                }

                var card = registry.Get(uid);
                if (card.Kind == CardKind.Monster)
                {
                    candidates.Add(HeadlessCommandDecision.Attack(slot));
                }
                else
                {
                    candidates.Add(HeadlessCommandDecision.Pickup(slot));
                }
            }
        }

        private List<HeadlessCommandDecision> BuildItemCandidates(IArchitecture architecture)
        {
            var candidates = new List<HeadlessCommandDecision>();
            var registry = architecture.GetModel<CardRegistry>();
            var deck = architecture.GetModel<DeckModel>();
            for (var i = 0; i < deck.ItemSlotUids.Count; i++)
            {
                var uid = deck.ItemSlotUids[i];
                if (mUsedItemUids.Contains(uid))
                {
                    continue;
                }

                CardInstance card;
                if (!registry.TryGet(uid, out card))
                {
                    continue;
                }

                if (card.Kind != CardKind.HelpCard && card.Kind != CardKind.Item)
                {
                    continue;
                }

                candidates.Add(HeadlessCommandDecision.UseItem(uid, card.DefId));
                mUsedItemUids.Add(uid);
            }

            return candidates;
        }

        private bool Pick(IReadOnlyList<HeadlessCommandDecision> candidates, out HeadlessCommandDecision decision)
        {
            if (candidates == null || candidates.Count == 0)
            {
                decision = null;
                return false;
            }

            decision = candidates[mRng.Range(0, candidates.Count)];
            return true;
        }

        private static void AddRange(List<HeadlessCommandDecision> target, IReadOnlyList<HeadlessCommandDecision> source)
        {
            for (var i = 0; i < source.Count; i++)
            {
                target.Add(source[i]);
            }
        }
    }
}
