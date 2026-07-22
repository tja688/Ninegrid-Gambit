using System;

namespace NineGrid.Cards
{
    /// <summary>
    /// 场地几何 / 收敛宿主桥：Cards 不引 Presentation，由 Controller 注入显式引用。
    /// V7：跨模块解析优先走 Hook，不再互取静态 Instance。
    /// </summary>
    public static class GroundFieldGeometryHook
    {
        /// <summary>由 Presentation Controller 注册；接收 Field 显式绑定。</summary>
        public static Action<GroundFieldManagerSingleton> Wire;

        public static Func<GroundFieldManagerSingleton> ResolveField;

        public static void Reset()
        {
            Wire = null;
            ResolveField = null;
        }

        /// <summary>生产路径：绑定 Field 并通知 Presentation Controller 登记 QF System。</summary>
        public static void RequestWire(GroundFieldManagerSingleton field)
        {
            ResolveField = field != null ? () => field : null;
            Wire?.Invoke(field);
        }

        public static GroundFieldManagerSingleton FieldOrNull()
        {
            return ResolveField?.Invoke();
        }
    }
}
