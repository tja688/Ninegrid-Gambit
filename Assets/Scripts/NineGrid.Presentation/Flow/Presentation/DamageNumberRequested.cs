using UnityEngine;

namespace NineGrid.Flow.Presentation
{
    /// <summary>飘字表现分类：决定颜色与动态曲线（总伤害 / 治疗 / 拆分后的血量、护甲伤害）。</summary>
    public enum DamageNumberKind
    {
        /// <summary>普通总伤害（白色，旧逻辑）。</summary>
        Damage,

        /// <summary>治疗（绿色）。</summary>
        Heal,

        /// <summary>拆分模式：血量伤害（红色）。</summary>
        HpDamage,

        /// <summary>拆分模式：护甲伤害（绿灰色）。</summary>
        ArmorDamage
    }

    /// <summary>请求在世界坐标弹出伤害数字（单向 FX，不回写规则）。</summary>
    public struct DamageNumberRequested
    {
        public Vector3 WorldPosition;
        public int Amount;

        /// <summary>飘字分类：决定颜色与动态曲线（见 <see cref="DamageNumberKind"/>）。</summary>
        public DamageNumberKind Kind;
    }
}
