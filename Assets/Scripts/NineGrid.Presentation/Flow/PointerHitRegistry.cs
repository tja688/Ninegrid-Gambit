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
            PruneDestroyedTargets();
        }

        /// <summary>移除已销毁的 Unity 命中代理，避免 Router 每帧踩 MissingReference。</summary>
        private static void PruneDestroyedTargets()
        {
            for (var i = Targets.Count - 1; i >= 0; i--)
            {
                var target = Targets[i];
                if (target == null)
                {
                    Targets.RemoveAt(i);
                    continue;
                }

                if (target is UnityEngine.Object unityObject && !unityObject)
                {
                    Targets.RemoveAt(i);
                }
            }
        }

        /// <summary>EditMode 清理。</summary>
        public static void ClearForTests()
        {
            Targets.Clear();
        }
    }
}
