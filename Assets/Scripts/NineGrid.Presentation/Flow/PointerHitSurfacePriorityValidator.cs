using System.Collections.Generic;

namespace NineGrid.Flow
{
    /// <summary>
    /// 表面优先级装配校验：镜像通层 sortingOrder——同分即装配错误。
    /// </summary>
    public static class PointerHitSurfacePriorityValidator
    {
        public readonly struct DuplicateHit
        {
            public readonly int HitSortOrder;
            public readonly int HitTypePriority;
            public readonly string FirstName;
            public readonly string SecondName;

            public DuplicateHit(int hitSortOrder, int hitTypePriority, string firstName, string secondName)
            {
                HitSortOrder = hitSortOrder;
                HitTypePriority = hitTypePriority;
                FirstName = firstName;
                SecondName = secondName;
            }

            public override string ToString()
            {
                return $"sort={HitSortOrder} type={HitTypePriority}: {FirstName} ↔ {SecondName}";
            }
        }

        /// <summary>三表面常量自身必须互异。</summary>
        public static bool AreDeclaredSurfacePrioritiesDistinct()
        {
            var field = PointerHitSurfacePriorities.Field;
            var hand = PointerHitSurfacePriorities.Hand;
            var overlay = PointerHitSurfacePriorities.Overlay;
            return field != hand && field != overlay && hand != overlay;
        }

        /// <summary>
        /// 扫描已注册目标：相同 <c>HitSortOrder</c> + <c>HitTypePriority</c> 视为装配冲突。
        /// </summary>
        public static List<DuplicateHit> FindDuplicateScorePairs(IReadOnlyList<IPointerHitTarget> targets)
        {
            var hits = new List<DuplicateHit>();
            if (targets == null || targets.Count == 0)
            {
                return hits;
            }

            var seen = new Dictionary<long, string>();
            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target == null)
                {
                    continue;
                }

                var sort = target.HitSortOrder;
                var type = target.HitTypePriority;
                var key = ((long)sort << 32) ^ (uint)type;
                var name = Describe(target);
                if (seen.TryGetValue(key, out var existing))
                {
                    hits.Add(new DuplicateHit(sort, type, existing, name));
                }
                else
                {
                    seen[key] = name;
                }
            }

            return hits;
        }

        private static string Describe(IPointerHitTarget target)
        {
            if (target is UnityEngine.Object obj && obj != null)
            {
                return obj.name;
            }

            return target.GetType().Name;
        }
    }
}
