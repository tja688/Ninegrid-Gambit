using System.Collections.Generic;
using System.Text;

namespace NineGrid.Presentation.Debugging.Trace
{
    internal static class BattleTraceSummaryGenerator
    {
        public static string Generate(BattleTraceSession session)
        {
            if (session == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder(2048);
            builder.AppendLine("# BattleTrace " + session.SessionId);
            builder.AppendLine();
            builder.AppendLine("- JSONL: `" + session.JsonlPath + "`");
            builder.AppendLine("- Level: " + session.Level);
            builder.AppendLine();

            IReadOnlyList<BattleTraceViolationRecord> violations = session.Violations;
            builder.AppendLine("## Violations (" + violations.Count + ")");
            if (violations.Count == 0)
            {
                builder.AppendLine("- none");
            }
            else
            {
                for (var i = 0; i < violations.Count; i++)
                {
                    BattleTraceViolationRecord violation = violations[i];
                    builder.Append("- [seq=").Append(violation.Seq).Append("] ")
                        .Append(violation.Code);
                    if (violation.Slot.HasValue)
                    {
                        builder.Append(" slot=").Append(violation.Slot.Value);
                    }

                    if (violation.ExpectedUid.HasValue)
                    {
                        builder.Append(" uid=").Append(violation.ExpectedUid.Value);
                    }

                    builder.Append(" — ").Append(violation.Message);
                    builder.AppendLine();
                    builder.AppendLine("  - hint: " + HintFor(violation.Code));
                }
            }

            builder.AppendLine();
            builder.AppendLine("## Timeline (condensed)");
            IReadOnlyList<BattleTraceTimelineRecord> timeline = session.Timeline;
            if (timeline.Count == 0)
            {
                builder.AppendLine("- none");
            }
            else
            {
                for (var i = 0; i < timeline.Count; i++)
                {
                    BattleTraceTimelineRecord entry = timeline[i];
                    builder.Append("- ").Append(entry.Time.ToString("F3"))
                        .Append("s [seq=").Append(entry.Seq).Append("] ")
                        .AppendLine(entry.Label);
                }
            }

            builder.AppendLine();
            builder.AppendLine("## Recent Snapshots");
            IReadOnlyList<BattleTraceSnapshotRecord> snapshots = session.Snapshots;
            int start = snapshots.Count > 6 ? snapshots.Count - 6 : 0;
            for (var i = start; i < snapshots.Count; i++)
            {
                BattleTraceSnapshotRecord snapshot = snapshots[i];
                builder.Append("- [seq=").Append(snapshot.Seq).Append("] ")
                    .Append(snapshot.Tag)
                    .Append(" hand=[")
                    .Append(string.Join(",", snapshot.Data.Hand))
                    .Append("] boardSlots=")
                    .Append(snapshot.Data.Board.Count)
                    .AppendLine();
            }

            builder.AppendLine();
            builder.AppendLine("## AI triage");
            builder.AppendLine("1. Scan `Violation` + `FlowResolve` with status `fallback_noop`.");
            builder.AppendLine("2. For each violation, read ±10 lines around its `seq` in the JSONL.");
            builder.AppendLine("3. Compare `pre_batch_*` vs `post_batch_*` snapshot tags.");

            return builder.ToString();
        }

        private static string HintFor(string code)
        {
            switch (code)
            {
                case "BOARD_GHOST":
                    return "Core says board slot has a card but ViewRegistry has no actor — check FillSlots/CardDeal resolve or spawn.";
                case "HAND_GHOST":
                    return "ItemSlotUids has uid but no hand actor — check CardAcquisition spawn or ItemUse despawn.";
                case "HAND_ORDER_MISMATCH":
                    return "ItemSlotUids order differs from coordinator/layout actor order — check CardAcquisitionFlowBinding iteration.";
                case "FLOW_SILENT_NOOP":
                    return "FlowBindingFallback InstantAlign ran — Core changed but no visual flow played.";
                case "PARALLEL_MOTION":
                    return "Multiple motion flows in same ActionId group — possible race on slot/actor.";
                case "ACTOR_INACTIVE":
                    return "Actor exists but SetActive(false) — check Despawn pool or ItemUseFlow.";
                case "REGISTRY_DUPLICATE_UID":
                    return "Two transforms registered for same uid — check double spawn.";
                default:
                    return "See JSONL context around seq.";
            }
        }
    }
}
