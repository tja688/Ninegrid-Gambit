using NineGrid.Flow.Presentation;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Commands
{
    /// <summary>
    /// 请求伤害飘字表现事件（不改规则真相）。
    /// </summary>
    public sealed class RequestDamageNumberCommand : AbstractCommand
    {
        private readonly Vector3 mWorldPosition;
        private readonly int mAmount;
        private readonly bool mIsHeal;

        public RequestDamageNumberCommand(Vector3 worldPosition, int amount, bool isHeal = false)
        {
            mWorldPosition = worldPosition;
            mAmount = amount;
            mIsHeal = isHeal;
        }

        protected override void OnExecute()
        {
            this.SendEvent(new DamageNumberRequested
            {
                WorldPosition = mWorldPosition,
                Amount = mAmount,
                IsHeal = mIsHeal
            });
        }
    }
}
