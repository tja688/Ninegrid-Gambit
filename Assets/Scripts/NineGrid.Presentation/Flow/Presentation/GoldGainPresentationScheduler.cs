using System;
using NineGrid.Core;
using NineGrid.Core.Systems;
using QFramework;
using UnityEngine;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 扫描 EventLog 金币变更并广播 <see cref="GoldGainPresentationRequested"/>。
    /// </summary>
    public sealed class GoldGainPresentationScheduler
    {
        public const string UnusedHelpCardsGoldReason = "unusedHelpCards";

        public int PresentFromEventLog(
            IArchitecture architecture,
            int startIndex,
            Vector3? originWorld = null,
            string skipReason = null,
            Func<int, Vector3?> resolveCardWorldPosition = null)
        {
            if (architecture == null)
            {
                return 0;
            }

            var entries = architecture.GetSystem<IActionPipelineSystem>().EventLog.Entries;
            if (entries == null || startIndex >= entries.Count)
            {
                return 0;
            }

            var emitted = 0;
            for (var i = Math.Max(0, startIndex); i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.Type != CoreEventType.GoldModified || e.Delta == 0)
                {
                    continue;
                }

                if (string.Equals(e.Message, UnusedHelpCardsGoldReason, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(skipReason)
                    && string.Equals(e.Message, skipReason, StringComparison.Ordinal))
                {
                    continue;
                }

                Vector3? origin = originWorld;
                if (!origin.HasValue && resolveCardWorldPosition != null)
                {
                    if (e.CardUid > 0)
                    {
                        origin = resolveCardWorldPosition(e.CardUid);
                    }

                    if (!origin.HasValue && e.TargetUid > 0)
                    {
                        origin = resolveCardWorldPosition(e.TargetUid);
                    }
                }

                architecture.SendEvent(new GoldGainPresentationRequested
                {
                    Delta = e.Delta,
                    AmountAfter = e.Amount,
                    Reason = e.Message ?? string.Empty,
                    SourceDefId = e.SourceDefId ?? string.Empty,
                    ActionName = e.ActionName ?? string.Empty,
                    OriginWorld = origin,
                    IsSpend = e.Delta < 0
                });
                emitted++;
            }

            return emitted;
        }
    }
}
