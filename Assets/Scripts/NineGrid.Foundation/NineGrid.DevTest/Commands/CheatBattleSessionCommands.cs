#if UNITY_EDITOR || DEVELOPMENT_BUILD

using NineGrid.Flow;
using QFramework;

namespace NineGrid.DevTest.Commands
{
    public sealed class CheatSetAvatarHpCommand : AbstractCommand
    {
        private readonly int mHp;

        public CheatSetAvatarHpCommand(int hp)
        {
            mHp = hp;
        }

        protected override void OnExecute()
        {
            BattleSessionCheat.TrySetAvatarHp(mHp);
        }
    }

    public sealed class CheatSetAvatarAttackCommand : AbstractCommand
    {
        private readonly int mAttack;

        public CheatSetAvatarAttackCommand(int attack)
        {
            mAttack = attack;
        }

        protected override void OnExecute()
        {
            BattleSessionCheat.TrySetAvatarAttack(mAttack);
        }
    }

    public sealed class CheatForceNodeVictoryCommand : AbstractCommand
    {
        protected override void OnExecute()
        {
            BattleSessionCheat.TryForceNodeVictory();
        }
    }
}

#endif
