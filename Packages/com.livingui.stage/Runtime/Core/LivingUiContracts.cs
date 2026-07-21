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
        [Tooltip("每条面板运动的基础时长（秒），与距离增量叠加后得到单条时长。")]
        [Min(0.05f)] public float BaseDuration = 0.48f;

        [Tooltip("每世界单位距离增加的秒数；面板位移越大则运动越久。")]
        [Min(0f)] public float DistanceSecondsPerUnit = 0.025f;

        [Tooltip("距离增量上限（秒），避免远距离面板耗时过长。")]
        [Min(0f)] public float MaximumDistanceAddition = 0.28f;

        [Tooltip("卡农跨度（秒）；面板按舞台水平位置从 0 到 CanonSpan 渐进偏移。")]
        [Min(0f)] public float CanonSpan = 0.16f;

        [Tooltip("出画面板领先量（秒）；离开舞台的面板最先触发。")]
        [Min(0f)] public float ExpelledLead = 0.06f;

        [Tooltip("入画面板额外延迟（秒）；新入场面板在出画波次之后才启动。")]
        [Min(0f)] public float EnteringDelay = 0.08f;

        [Tooltip("尺寸下限-宽度（世界单位），防止面板坍缩为零。")]
        [Min(0.01f)] public float FlowWidthFloor = 0.18f;

        [Tooltip("尺寸下限-高度（世界单位），防止面板坍缩为零。")]
        [Min(0.01f)] public float FlowHeightFloor = 0.18f;

        [Tooltip("尺寸上限倍率；瞬态尺寸不超过 max(源, 目标) 的此倍数。")]
        [Min(1f)] public float SizeCeilingMultiplier = 3f;

        [Tooltip("位置/尺寸运动的缓动函数类型；Custom 时使用下方 CustomEasingCurve。")]
        public LivingUiEasingType EasingType = LivingUiEasingType.EaseInOutCubic;

        [Tooltip("自定义缓动曲线（仅 EasingType = Custom 时生效）；横轴 t: 0→1，纵轴 value: 0→1。")]
        public AnimationCurve CustomEasingCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    }

    public interface ILivingUiTransitionPlanner
    {
        LivingUiTransitionPlan Plan(
            IReadOnlyList<LivingUiCarrierState> liveStates,
            LivingUiLayout target,
            Rect stageBounds,
            LivingUiTransitionStyle style,
            IReadOnlyDictionary<int, Vector2> carrierFlowFloorOverrides = null);
    }
}
