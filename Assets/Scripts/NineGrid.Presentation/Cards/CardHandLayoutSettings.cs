using System;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 手牌布局与动效参数，可在 Inspector 配置。
    /// </summary>
    [Serializable]
    public sealed class CardHandLayoutSettings
    {
        [Tooltip("手牌最大槽位数。")]
        public int maxSlots = 5;

        [Tooltip("无场景锚点时的相邻卡牌水平间距回退值；有 CardHandAnchors 时以锚点为准。")]
        public float cardSpacing = 0.25f;

        [Tooltip("Ripple 动效中，每远离变化点一个槽位增加的延迟（秒）。")]
        public float rippleDelayPerSlot = 0.04f;

        [Tooltip("卡牌移动缓动时长（秒）。")]
        public float moveDuration = 0.28f;

        [Tooltip("左起第一张卡的 SortingGroup 基准 order；索引越大 order 越低（左高右低，与卡组一致）。")]
        public int sortingOrderBase = 10;

        [Tooltip("每向右一个槽位 sortingOrder 的递减量。")]
        public int sortingOrderStep = 1;

        [Tooltip("hover 时相对 sortingOrderBase 的额外抬升量，保证抽出卡压在最上层。")]
        public int hoverSortingBoost = 15;

        [Header("Hover")]
        [Tooltip("手牌 hover 命中区世界尺寸（以槽位锚点为心，不随抽出动效移动）。")]
        public Vector2 handHitBoxSize = new(1.6f, 2.2f);

        [Tooltip("越过相邻槽位中点后，额外需要的 X 位移才切换 hover，抑制边界抖动。")]
        public float hoverSwitchHysteresis = 0.05f;

        [Tooltip("手牌 hover 时相对锚点的世界 Y 抬升量（抽出感）。")]
        public float hoverPopYOffset = 0.35f;

        [Tooltip("手牌 hover 时 localScale 相对基准的放大增量。")]
        public float hoverScaleIntensity = 0.06f;

        [Tooltip("手牌 hover 进入动效时长（秒）。")]
        public float hoverEnterDuration = 0.14f;

        [Tooltip("手牌 hover 退出缩回基准的时长（秒）。")]
        public float hoverExitDuration = 0.1f;

        [Tooltip("手牌 hover 时 Z 轴摇晃角度（度）。")]
        public float hoverPunchAngle = 3f;

        [Tooltip("手牌 hover 摇晃振动次数感（DOPunchRotation vibrato）。")]
        public int hoverPunchVibrato = 5;

        [Tooltip("非 hover 手牌的 alpha（半透明其他卡）。")]
        [Range(0.2f, 1f)]
        public float nonHoveredAlpha = 0.45f;

        [Header("Drag")]
        [Tooltip("拖拽中覆盖场地卡时，被拖拽卡的 alpha。")]
        [Range(0.2f, 1f)]
        public float dragAlphaWhenOverGround = 0.55f;

        [Tooltip("拖拽时相对 HandCardMode sortingOrder 的额外提升。")]
        public int dragSortingBoost = 30;

        [Header("Apply")]
        [Tooltip("手牌在 ApplyZone 释放成功后的缩小消失时长（秒）。")]
        public float applyVanishDuration = 0.18f;
    }
}
