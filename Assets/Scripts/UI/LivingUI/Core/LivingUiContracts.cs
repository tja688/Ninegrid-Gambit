using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.LivingUI
{
    public enum LivingUiLayoutId
    {
        MainMenu,
        CharacterChoice,
        Battle,
        RewardChoice,
        DeckPreview,
        Room,
        Route,
    }

    public readonly struct LivingUiTerminal
    {
        public LivingUiTerminal(int carrierId, Vector2 position, Vector2 size, int sortingLayerId, int sortingOrder)
        {
            CarrierId = carrierId;
            Position = position;
            Size = size;
            SortingLayerId = sortingLayerId;
            SortingOrder = sortingOrder;
        }

        public int CarrierId { get; }
        public Vector2 Position { get; }
        public Vector2 Size { get; }
        public int SortingLayerId { get; }
        public int SortingOrder { get; }
    }

    public sealed class LivingUiLayout
    {
        private readonly IReadOnlyDictionary<int, LivingUiTerminal> _terminals;

        public LivingUiLayout(LivingUiLayoutId id, IReadOnlyDictionary<int, LivingUiTerminal> terminals)
        {
            Id = id;
            _terminals = terminals ?? throw new ArgumentNullException(nameof(terminals));
        }

        public LivingUiLayoutId Id { get; }
        public IReadOnlyDictionary<int, LivingUiTerminal> Terminals => _terminals;

        public LivingUiTerminal GetTerminal(int carrierId)
        {
            if (!_terminals.TryGetValue(carrierId, out var terminal))
            {
                throw new KeyNotFoundException($"布局 {Id} 缺少载体 {carrierId}。");
            }

            return terminal;
        }
    }

    public readonly struct LivingUiCarrierState
    {
        public LivingUiCarrierState(int carrierId, Vector2 position, Vector2 velocity, Vector2 size, Vector2 sizeVelocity)
        {
            CarrierId = carrierId;
            Position = position;
            Velocity = velocity;
            Size = size;
            SizeVelocity = sizeVelocity;
        }

        public int CarrierId { get; }
        public Vector2 Position { get; }
        public Vector2 Velocity { get; }
        public Vector2 Size { get; }
        public Vector2 SizeVelocity { get; }
    }

    [Serializable]
    public sealed class LivingUiTransitionStyle
    {
        [Min(0.05f)] public float BaseDuration = 0.48f;
        [Min(0f)] public float DistanceSecondsPerUnit = 0.025f;
        [Min(0f)] public float MaximumDistanceAddition = 0.28f;
        [Min(0f)] public float CanonSpan = 0.16f;
        [Min(0f)] public float ExpelledLead = 0.06f;
        [Min(0f)] public float EnteringDelay = 0.08f;
        [Min(0.01f)] public float FlowWidthFloor = 0.18f;
        [Min(0.01f)] public float FlowHeightFloor = 0.18f;
        [Min(1f)] public float SizeCeilingMultiplier = 3f;
    }

    public interface ILivingUiTransitionPlanner
    {
        LivingUiTransitionPlan Plan(
            IReadOnlyList<LivingUiCarrierState> liveStates,
            LivingUiLayout target,
            Rect stageBounds,
            LivingUiTransitionStyle style);
    }
}
