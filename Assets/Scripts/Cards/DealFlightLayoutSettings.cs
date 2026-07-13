using System;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 二阶贝塞尔飞牌追踪与动态就位预算参数。
    /// </summary>
    [Serializable]
    public sealed class DealFlightLayoutSettings
    {
        [Tooltip("基准飞牌就位时长（秒），与牌组 moveDuration 同量级。")]
        public float baseDuration = 0.28f;

        [Tooltip("距离归一化参考（世界单位）。")]
        public float refDistance = 3f;

        [Tooltip("距离→时长幂指数（约 0.35）。")]
        public float distanceExponent = 0.35f;

        [Tooltip("距离缩放下限。")]
        public float minDistScale = 0.8f;

        [Tooltip("距离缩放上限。")]
        public float maxDistScale = 1.4f;

        [Tooltip("贝塞尔控制点抬升弧高（世界 Y）。")]
        public float arcHeight = 0.22f;

        [Tooltip("距锚点较远时的 u 推进倍率（远快）。")]
        public float easeFar = 1.4f;

        [Tooltip("距锚点较近时的 u 推进倍率（近慢）。")]
        public float easeNear = 0.55f;

        [Tooltip("远近速因子 Remap 的远距参考（世界单位）。")]
        public float speedFarDistance = 2.5f;

        [Tooltip("远近速因子 Remap 的近距参考（世界单位）。")]
        public float speedNearDistance = 0.15f;

        [Tooltip("就位判定距离（世界单位）。")]
        public float arriveThreshold = 0.04f;

        [Tooltip("就位所需最小贝塞尔参数 u。")]
        public float arriveMinU = 0.85f;

        [Tooltip("旋转锚点跳变时的短暂 u 推进 boost 倍率。")]
        public float rotationJumpBoost = 1.8f;

        [Tooltip("锚点跳变判定距离²（世界单位²）。")]
        public float anchorJumpThresholdSqr = 0.25f;

        [Tooltip("每次锚点跳变额外消费预算（秒）。")]
        public float rotationJumpCost = 0.06f;

        [Tooltip("场地 busy 时追加预算（秒）。")]
        public float busySlack = 0.08f;

        [Tooltip("并发飞牌时每多一张追加预算（秒）。")]
        public float concurrentFlightSlack = 0.03f;

        [Tooltip("每个待播旋转步追加预算（秒）。")]
        public float perRotateConsume = 0.12f;

        [Tooltip("预算下限（秒）。")]
        public float minDuration = 0.18f;

        [Tooltip("预算上限（秒）。")]
        public float maxDuration = 0.65f;

        [Tooltip("预算耗尽后软着陆 u 推进倍率。")]
        public float exhaustedSnapBlend = 0.35f;

        [Tooltip("多条探求依次启动的最小间隔（秒）。")]
        public float exploreDealInterval = 0.04f;
    }
}
