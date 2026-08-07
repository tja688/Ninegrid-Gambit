using UnityEngine;

namespace NineGrid.Flow.Presentation
{
    /// <summary>请求在世界坐标弹出伤害数字（单向 FX，不回写规则）。</summary>
    public struct DamageNumberRequested
    {
        public Vector3 WorldPosition;
        public int Amount;

        /// <summary>为真时按治疗飘字表现（绿色 + 同一动态大小/时长区间）。</summary>
        public bool IsHeal;
    }
}
