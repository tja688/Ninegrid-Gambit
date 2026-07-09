namespace NineGrid.Cards
{
    /// <summary>
    /// 战斗编排意图。由 FieldBattleManager 构造，经 Catalog 路由到 Encounter Profile。
    /// </summary>
    public enum BattleIntent
    {
        Attack = 0,
        AttackLethal = 1,
        CounterAttack = 2,
        CounterAttackLethal = 3,
    }

    public static class BattleIntentUtility
    {
        public static bool IsLethal(BattleIntent intent)
        {
            return intent == BattleIntent.AttackLethal
                || intent == BattleIntent.CounterAttackLethal;
        }

        public static bool IsCounter(BattleIntent intent)
        {
            return intent == BattleIntent.CounterAttack
                || intent == BattleIntent.CounterAttackLethal;
        }

        public static BattleIntent FromFlags(bool counter, bool lethal)
        {
            if (counter)
            {
                return lethal ? BattleIntent.CounterAttackLethal : BattleIntent.CounterAttack;
            }

            return lethal ? BattleIntent.AttackLethal : BattleIntent.Attack;
        }
    }
}
