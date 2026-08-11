using System;

namespace NineGrid.Core
{
    /// <summary>
    /// 怪物层数数值叠加：相对第 1 层基准，每提升一层攻击 +1、血量 +2（任意主题卡组均适用）。
    /// </summary>
    public static class MonsterFloorStatScaling
    {
        public const int AttackPerFloorAboveBase = 1;
        public const int HpPerFloorAboveBase = 2;

        public static int FloorTiersAboveBase(int floor)
        {
            return Math.Max(0, floor - 1);
        }

        public static int AttackBonus(int floor)
        {
            return FloorTiersAboveBase(floor) * AttackPerFloorAboveBase;
        }

        public static int HpBonus(int floor)
        {
            return FloorTiersAboveBase(floor) * HpPerFloorAboveBase;
        }

        public static void ApplyToDraft(CardDraft draft, int floor)
        {
            if (draft == null || draft.Kind != CardKind.Monster)
            {
                return;
            }

            var attackBonus = AttackBonus(floor);
            var hpBonus = HpBonus(floor);
            if (attackBonus == 0 && hpBonus == 0)
            {
                return;
            }

            draft.Attack += attackBonus;
            draft.MaxHp += hpBonus;
            if (draft.Hp > 0)
            {
                draft.Hp += hpBonus;
            }
            else
            {
                draft.Hp = draft.MaxHp;
            }
        }
    }
}
