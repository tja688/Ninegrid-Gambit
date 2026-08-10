using System.Collections.Generic;

namespace NineGrid.Flow
{
    /// <summary>
    /// 遗物 JSON 装配倒计时解析；委托 <see cref="EffectCountdownProjection"/>。
    /// </summary>
    internal static class RelicCountdownProjection
    {
        public readonly struct Entry
        {
            public Entry(string projectKey, int period)
            {
                ProjectKey = projectKey ?? string.Empty;
                Period = period;
            }

            public string ProjectKey { get; }
            public int Period { get; }
        }

        public static bool TryGetEntries(string relicDefId, List<Entry> into)
        {
            into?.Clear();
            if (into == null)
            {
                return false;
            }

            var shared = new List<EffectCountdownProjection.Entry>(4);
            if (!EffectCountdownProjection.TryGetEntries(relicDefId, shared))
            {
                return false;
            }

            for (var i = 0; i < shared.Count; i++)
            {
                var entry = shared[i];
                into.Add(new Entry(entry.ProjectKey, entry.Period));
            }

            return into.Count > 0;
        }
    }
}
