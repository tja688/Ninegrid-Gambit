using System;
using System.Collections.Generic;
using NineGrid.Core.Stats;
using QFramework;

namespace NineGrid.Core
{
    /// <summary>
    /// #120：遗物自身 run 内可变贡献（攻/甲）。跨节点保留，新 run 清空；经 Persistent modifier 汇入有效属性。
    /// </summary>
    public sealed class RelicRunContributionModel : AbstractModel
    {
        private readonly Dictionary<string, int> mValues =
            new Dictionary<string, int>(StringComparer.Ordinal);

        protected override void OnInit()
        {
        }

        public int Get(string relicDefId, StatId stat)
        {
            int value;
            return mValues.TryGetValue(BuildKey(relicDefId, stat), out value) ? value : 0;
        }

        public void Set(string relicDefId, StatId stat, int value)
        {
            if (string.IsNullOrEmpty(relicDefId))
            {
                return;
            }

            var key = BuildKey(relicDefId, stat);
            var clamped = Math.Max(0, value);
            if (clamped == 0)
            {
                mValues.Remove(key);
                return;
            }

            mValues[key] = clamped;
        }

        public int Adjust(string relicDefId, StatId stat, int delta, int floor = 0)
        {
            var next = Math.Max(floor, Get(relicDefId, stat) + delta);
            Set(relicDefId, stat, next);
            return next;
        }

        public void ClearRelic(string relicDefId)
        {
            if (string.IsNullOrEmpty(relicDefId))
            {
                return;
            }

            mValues.Remove(BuildKey(relicDefId, StatId.Attack));
            mValues.Remove(BuildKey(relicDefId, StatId.Armor));
        }

        public void ClearAll()
        {
            mValues.Clear();
        }

        public static string BuildModifierSourceId(string relicDefId, StatId stat)
        {
            return (relicDefId ?? string.Empty) + ".run." + stat;
        }

        private static string BuildKey(string relicDefId, StatId stat)
        {
            return (relicDefId ?? string.Empty) + "|" + ((int)stat).ToString();
        }
    }
}
