using System;

namespace NineGrid.Cards
{
    /// <summary>
    /// 场地战斗表现宿主桥：Cards 不引 Presentation，由 Controller 注入显式引用。
    /// V7：跨模块解析优先走 Hook，不再互取静态 Instance。
    /// </summary>
    public static class FieldBattlePresentationHook
    {
        /// <summary>由 Presentation Controller 注册；接收 Battle 显式绑定。</summary>
        public static Action<FieldBattleManagerSingleton> Wire;

        public static Func<FieldBattleManagerSingleton> ResolveBattle;

        public static void Reset()
        {
            Wire = null;
            ResolveBattle = null;
        }

        /// <summary>生产路径：绑定 Battle 并通知 Presentation Controller 登记 QF System。</summary>
        public static void RequestWire(FieldBattleManagerSingleton battle)
        {
            ResolveBattle = battle != null ? () => battle : null;
            Wire?.Invoke(battle);
        }

        public static FieldBattleManagerSingleton BattleOrNull()
        {
            return ResolveBattle?.Invoke();
        }
    }
}
