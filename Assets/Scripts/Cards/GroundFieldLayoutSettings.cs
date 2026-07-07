using System;
using UnityEngine;

namespace NineGrid.Cards
{
    [Serializable]
    public sealed class GroundFieldLayoutSettings
    {
        [Tooltip("场地卡牌移动/旋转换位时长（秒）。")]
        public float moveDuration = 0.35f;

        [Tooltip("旋转/移动前轻微放大强度（相对 localScale）。")]
        public float punchScaleIntensity = 0.18f;

        [Tooltip("轻微放大动效时长（秒）。")]
        public float punchScaleDuration = 0.22f;

        [Tooltip("空槽点击 BoxCollider2D 尺寸（世界单位）。")]
        public Vector2 slotHitBoxSize = new(1.6f, 2.2f);
    }
}
