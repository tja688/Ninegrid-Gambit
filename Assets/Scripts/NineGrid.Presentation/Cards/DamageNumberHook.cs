using System;
using NineGrid.Flow.Presentation;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 伤害飘字统一入口：由 NineGrid.Presentation Controller 接线为 QF Event。
    /// </summary>
    public static class DamageNumberHook
    {
        /// <summary>由 Presentation Controller 注册，确保 Hook→Event 接线已安装。</summary>
        public static Action EnsureWired;

        public static Action<Vector3, int, DamageNumberKind> Spawn;

        public static void RequestSpawn(Vector3 worldPosition, int amount)
        {
            RequestSpawnInternal(worldPosition, amount, DamageNumberKind.Damage);
        }

        public static void RequestSpawnHeal(Vector3 worldPosition, int amount)
        {
            RequestSpawnInternal(worldPosition, amount, DamageNumberKind.Heal);
        }

        /// <summary>拆分模式：血量伤害飘字（红色）。</summary>
        public static void RequestSpawnHpDamage(Vector3 worldPosition, int amount)
        {
            RequestSpawnInternal(worldPosition, amount, DamageNumberKind.HpDamage);
        }

        /// <summary>拆分模式：护甲伤害飘字（绿灰色，净甲伤）。</summary>
        public static void RequestSpawnArmorDamage(Vector3 worldPosition, int amount)
        {
            RequestSpawnInternal(worldPosition, amount, DamageNumberKind.ArmorDamage);
        }

        /// <summary>
        /// 拆分模式：金币盔甲代偿飘字（金色，显示「-N」）。
        /// amount 为代偿消耗的金币数（正数），显示时带负号以对齐「扣款」语义。
        /// </summary>
        public static void RequestSpawnGoldSpend(Vector3 worldPosition, int amount)
        {
            EnsureWired?.Invoke();
            if (amount <= 0)
            {
                return;
            }

            // 传负值：DamageNumbersPro 对负 number 直接渲染 "-N"。
            Spawn?.Invoke(worldPosition, -amount, DamageNumberKind.GoldSpend);
        }

        private static void RequestSpawnInternal(Vector3 worldPosition, int amount, DamageNumberKind kind)
        {
            EnsureWired?.Invoke();
            if (amount <= 0)
            {
                return;
            }

            Spawn?.Invoke(worldPosition, amount, kind);
        }
    }
}
