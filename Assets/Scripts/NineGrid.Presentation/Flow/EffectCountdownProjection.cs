using System;
using System.Collections.Generic;
using NineGrid.Content;
using NineGrid.Content.CardPresentation;
using UnityEngine;

namespace NineGrid.Flow
{
    /// <summary>
    /// 从卡 JSON 装配实参解析效果倒计时投影键与初始周期（ADR-0035）；
    /// 机关 ActionCount 槽与遗物栏计数共用，不直读 Core 计数器。
    /// </summary>
    internal static class EffectCountdownProjection
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

        public static bool TryGetEntries(string defId, List<Entry> into)
        {
            into?.Clear();
            if (into == null
                || string.IsNullOrEmpty(defId)
                || !CardPresentationConfigCatalog.TryGet(defId, out var dto)
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
                if (period <= 1)
                {
                    continue;
                }

                into.Add(new Entry(projectKey, period));
            }

            return into.Count > 0;
        }

        /// <summary>
        /// 解析机关行动计数槽应显示的数值；未 Settled 前用装配 every/threshold 初值。
        /// </summary>
        public static bool TryResolveActionCount(
            string defId,
            IReadOnlyDictionary<string, string> committedRemaining,
            out int actionCount,
            out bool showActionCount)
        {
            actionCount = 0;
            showActionCount = false;
            var scratch = new List<Entry>(4);
            if (!TryGetEntries(defId, scratch) || scratch.Count == 0)
            {
                return false;
            }

            var entry = scratch[0];
            showActionCount = true;
            if (committedRemaining != null
                && committedRemaining.TryGetValue(entry.ProjectKey, out var committed)
                && int.TryParse(committed, out var parsed))
            {
                actionCount = Mathf.Max(0, parsed);
            }
            else
            {
                actionCount = entry.Period;
            }

            return true;
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
