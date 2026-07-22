using NineGrid.Cards;
using NineGrid.Cards.Convergence;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Systems
{
    /// <summary>
    /// V7：持有 FieldBattle 显式引用，作为战斗忙碌 / 净土域 Handoff 的 QF 权威入口。
    /// </summary>
    public sealed class FieldBattlePresentationSystem : AbstractSystem, IFieldBattlePresentationSystem
    {
        private FieldBattleManagerSingleton mBattle;

        public bool IsBound => mBattle != null;

        public FieldBattleManagerSingleton Battle => mBattle;

        public bool IsBusy => mBattle != null && mBattle.IsBusy;

        public void Bind(FieldBattleManagerSingleton battle)
        {
            mBattle = battle;
        }

        public void Unbind()
        {
            mBattle = null;
        }

        public void CancelBattleWork()
        {
            mBattle?.CancelBattleWork();
        }

        public HandoffState EvictCard(ManagedCard card)
        {
            if (mBattle == null)
            {
                return HandoffState.AtRest(Vector3.zero);
            }

            return mBattle.EvictCard(card);
        }

        public void AdmitCard(ManagedCard card, in HandoffState state)
        {
            mBattle?.AdmitCard(card, in state);
        }

        protected override void OnInit()
        {
        }

        protected override void OnDeinit()
        {
            Unbind();
        }
    }
}
