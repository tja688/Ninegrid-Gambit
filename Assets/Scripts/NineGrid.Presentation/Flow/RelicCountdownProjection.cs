using System;
using System.Collections.Generic;
using NineGrid.Content;
using NineGrid.Content.CardPresentation;

namespace NineGrid.Flow
{
    /// <summary>
    /// 从遗物 JSON 装配实参解析图标计数投影键与初始周期（ADR-0035）；
    /// 不直读 Core 计数器——仅提供作者声明的 projectKey / period。
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
            if (into == null
                || string.IsNullOrEmpty(relicDefId)
                || !CardPresentationConfigCatalog.TryGet(relicDefId, out var dto)
                || dto?.effectAssemblies == null)
            {
                return false;
            }

            for (var i = 0; i < dto.effectAssemblies.Length; i++)
            {
                var assembly = dto.effectAssemblies[i];
                if (assembly == null || string.IsNullOrWhiteSpace(assembly.argsJson))
                {
                    continue;
                }

                var args = EffectAssemblyResolver.ParseArgsJson(assembly.argsJson);
                if (!args.TryGetValue("projectKey", out var rawKey) || rawKey == null)
                {
                    continue;
                }

                var projectKey = Convert.ToString(rawKey)?.Trim() ?? string.Empty;
                if (projectKey.Length == 0)
                {
                    continue;
                }

                var period = ResolvePeriod(args);
                // period≤1 不碰计数器（ADR-0013），图标无需显示。
                if (period <= 1)
                {
                    continue;
                }

                into.Add(new Entry(projectKey, period));
            }

            return into.Count > 0;
        }

        private static int ResolvePeriod(IReadOnlyDictionary<string, object> args)
        {
            if (TryReadInt(args, "threshold", out var threshold))
            {
                return Math.Max(1, threshold);
            }

            if (TryReadInt(args, "every", out var every))
            {
                return Math.Max(1, every);
            }

            return 1;
        }

        private static bool TryReadInt(IReadOnlyDictionary<string, object> args, string key, out int value)
        {
            value = 0;
            if (args == null || !args.TryGetValue(key, out var raw) || raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToInt32(raw);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
