using System;
using UnityEngine;

namespace NineGrid.Cards
{
    [Serializable]
    public sealed class SkeletonDeckLayoutSettings
    {
        [Tooltip("合体：参与卡缓动撞向中心点的时长（秒）。")]
        public float fusionCrashDuration = 0.45f;

        [Tooltip("合体：参与卡撞向中心点的缓动强度（InBack 振幅）。")]
        public float fusionCrashOvershoot = 1.35f;

        [Tooltip("合体：重叠后闪白前短暂停顿（秒）。")]
        public float fusionOverlapHoldDuration = 0.18f;

        [Tooltip("合体：结果卡显现后、离场入组前的停顿（秒）。")]
        public float fusionResultHoldDuration = 0.12f;

        [Tooltip("合体：结果卡显现时的缩放弹出时长（秒）。")]
        public float fusionResultAppearDuration = 0.18f;
    }
}
