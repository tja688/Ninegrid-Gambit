using UnityEngine;

namespace NineGrid.Flow.Presentation
{
    /// <summary>请求在世界坐标弹出伤害数字（单向 FX，不回写规则）。</summary>
    public struct DamageNumberRequested
    {
        public Vector3 WorldPosition;
        public int Amount;
    }
}
