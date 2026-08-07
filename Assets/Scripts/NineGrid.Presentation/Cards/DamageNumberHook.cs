using System;
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

        public static Action<Vector3, int, bool> Spawn;

        public static void RequestSpawn(Vector3 worldPosition, int amount)
        {
            RequestSpawnInternal(worldPosition, amount, isHeal: false);
        }

        public static void RequestSpawnHeal(Vector3 worldPosition, int amount)
        {
            RequestSpawnInternal(worldPosition, amount, isHeal: true);
        }

        private static void RequestSpawnInternal(Vector3 worldPosition, int amount, bool isHeal)
        {
            EnsureWired?.Invoke();
            if (amount <= 0)
            {
                return;
            }

            Spawn?.Invoke(worldPosition, amount, isHeal);
        }
    }
}
