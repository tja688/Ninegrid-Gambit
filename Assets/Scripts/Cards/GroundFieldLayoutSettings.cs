using System;
using UnityEngine;

namespace NineGrid.Cards
{
    [Serializable]
    public sealed class GroundFieldLayoutSettings
    {
        [Tooltip("场地卡牌跳跃换位总时长（秒）。")]
        public float moveDuration = 0.35f;

        [Tooltip("双卡直线换位时长（秒）；先快后慢 OutCubic。")]
        public float swapMoveDuration = 0.3f;

        [Tooltip("跳跃弧顶相对起终点连线的世界 Y 抬升量（仅做轻微起跳感，不往棋盘中心拉拢）。")]
        public float hopArcHeight = 0.28f;

        [Tooltip("跳跃弧顶 localScale 相对基准的放大增量（如 0.06 表示弧顶约为 1.06 倍，模拟「高」）。")]
        public float hopPeakScaleIntensity = 0.06f;

        [Tooltip("落点 localScale 相对基准的缩小增量（如 0.04 表示落地瞬间约为 0.96 倍，模拟「矮」）。")]
        public float hopLandScaleIntensity = 0.04f;

        [Tooltip("场上无卡时外圈空转的表现时长（秒）。")]
        public float emptyRotateDuration = 0.2f;

        [Tooltip("移除卡牌时缩小消失时长（秒）。")]
        public float removeDisappearDuration = 0.18f;

        [Tooltip("Avatar（格5）入场缩放出现时长（秒）。与开局外圈发牌并行时建议与牌组 moveDuration 同量级。")]
        public float avatarRevealDuration = 0.28f;

        [Tooltip("空槽点击 BoxCollider2D 尺寸（世界单位）。")]
        public Vector2 slotHitBoxSize = new(1.6f, 2.2f);

        [Header("Empty Slot Explore")]
        [Tooltip("空牌位探求指数追锚响应强度（1/秒）。越大越快；约 20–28 对应先快后慢、总时约为线性 12u/s 的 1/2～1/3。旋转跳变锚点时会自动再加速。")]
        public float exploreChaseResponsiveness = 24f;

        [Tooltip("空牌位探求单帧最大位移（世界单位）。≤0 表示不封顶，仅走指数缓动。")]
        public float exploreChaseMaxStep = 0f;

        [Tooltip("空牌位探求就位判定距离（世界单位）；到达后贴齐锚点。")]
        public float exploreArriveThreshold = 0.04f;

        [Tooltip("多条探求依次启动的最小间隔（秒），贴近补牌防抖。")]
        public float exploreDealInterval = 0.04f;

        [Header("Field To Deck")]
        [Tooltip("场地卡垂直回卡组时 Y 轴离画阈值（世界单位）；物体 Y 超过此值视为已出画。")]
        public float fieldExitYThreshold = 8f;

        [Tooltip("场地卡垂直上飞离画的缓动时长（秒）。")]
        public float fieldExitDuration = 0.35f;

        [Header("Hover")]
        [Tooltip("场地卡 hover 时 localScale 相对基准的放大增量（如 0.05 表示约为 1.05 倍）。")]
        public float hoverScaleIntensity = 0.05f;

        [Tooltip("场地卡 hover 时 Z 轴摇晃角度（度）。")]
        public float hoverPunchAngle = 4f;

        [Tooltip("场地卡 hover 进入动效时长（秒）。")]
        public float hoverEnterDuration = 0.14f;

        [Tooltip("场地卡 hover 退出缩回基准的时长（秒）。")]
        public float hoverExitDuration = 0.1f;

        [Tooltip("场地卡 hover 摇晃振动次数感（DOPunchRotation vibrato）。")]
        public int hoverPunchVibrato = 6;
    }
}
