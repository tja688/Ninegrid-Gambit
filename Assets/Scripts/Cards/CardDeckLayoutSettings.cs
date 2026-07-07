using System;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 牌组布局与动效参数，可在 Inspector 配置。
    /// </summary>
    [Serializable]
    public sealed class CardDeckLayoutSettings
    {
        [Tooltip("卡组最大槽位数。")]
        public int maxSlots = 20;

        [Tooltip("InGame 模式下相邻卡牌的世界空间水平间距。")]
        public float cardSpacing = 1.05f;

        [Tooltip("InGame 左对齐布局的基准 Y（世界坐标）。为 0 时由运行时从锚点或容器推导。")]
        public float layoutBaseY;

        [Tooltip("InGame 左对齐布局的基准 Z（世界坐标）。")]
        public float layoutBaseZ;

        [Tooltip("Entry 模式逐张发牌到卡组槽的间隔（秒）。")]
        public float entryDealInterval = 0.06f;

        [Tooltip("发到 Ground 时多张牌的间隔（秒）。")]
        public float dealInterval = 0.08f;

        [Tooltip("Ripple 动效中，每远离变化点一个槽位增加的延迟（秒）。")]
        public float rippleDelayPerSlot = 0.04f;

        [Tooltip("卡牌移动缓动时长（秒）。")]
        public float moveDuration = 0.28f;

        [Tooltip("Standby 待命区堆叠时的 Z 轴步进，避免同深度闪烁。")]
        public float standbyStackZStep = 0.01f;

        [Tooltip("左起第一张卡的 SortingGroup 基准 order；索引越大 order 越低。")]
        public int sortingOrderBase = 10;

        [Tooltip("每向右一个槽位 sortingOrder 的递减量。")]
        public int sortingOrderStep = 1;
    }
}
