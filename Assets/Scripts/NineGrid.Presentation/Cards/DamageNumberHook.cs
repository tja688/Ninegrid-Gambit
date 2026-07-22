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

        public static Action<Vector3, int> Spawn;

        public static void RequestSpawn(Vector3 worldPosition, int amount)
        {
            EnsureWired?.Invoke();
            if (amount <= 0)
            {
                return;
            }

            Spawn?.Invoke(worldPosition, amount);
        }
    }
}
