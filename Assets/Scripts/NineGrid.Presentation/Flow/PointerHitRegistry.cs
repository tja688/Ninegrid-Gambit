using System.Collections.Generic;

namespace NineGrid.Flow
{
    /// <summary>
    /// 指针命中目标登记表：proxy OnEnable/OnDisable 注册，供 <see cref="PointerHitRouter"/> 轮询。
    /// </summary>
    public static class PointerHitRegistry
    {
        private static readonly List<IPointerHitTarget> Targets = new List<IPointerHitTarget>(64);

        public static IReadOnlyList<IPointerHitTarget> All => Targets;

        public static void Register(IPointerHitTarget target)
        {
            if (target == null || Targets.Contains(target))
            {
                return;
            }

            Targets.Add(target);
        }

        public static void Unregister(IPointerHitTarget target)
        {
            if (target == null)
            {
                return;
            }

            Targets.Remove(target);
        }

        /// <summary>EditMode 清理。</summary>
        public static void ClearForTests()
        {
            Targets.Clear();
        }
    }
}
