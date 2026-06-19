using System.IO;
using System.Text;

namespace NineGrid.Core.Tests.Simulation
{
    public static class RunTranscriptWriter
    {
        public static string Build(HeadlessSimulationResult result, bool includeLifecycleEvents)
        {
            var builder = new StringBuilder();
            builder.AppendLine("TableNine Headless Simulation Report");
            builder.AppendLine("ContentSeed: " + result.ContentSeed);
            builder.AppendLine("AgentSeed: " + result.AgentSeed);
            builder.AppendLine("Outcome: " + result.Outcome + " (" + (result.StopReason ?? string.Empty) + ")");
            builder.AppendLine("Final: floor=" + result.FinalFloor
                + " node=" + result.FinalNodeIndex
                + " phase=" + result.FinalPhase
                + " hp=" + result.FinalAvatarHp
                + " armor=" + result.FinalAvatarArmor
                + " coins=" + result.FinalCoins);
            builder.AppendLine("Totals: commands=" + result.Commands.Count
                + " actions=" + result.TotalResolvedActions
                + " kills=" + result.MonsterKills
                + " avatarDamage=" + result.TotalDamageToAvatar);
            builder.AppendLine();

            builder.AppendLine("Deck selections");
            for (var i = 0; i < result.DeckSelections.Count; i++)
            {
                var selection = result.DeckSelections[i];
                builder.AppendLine("  Floor " + selection.Floor
                    + ": weak=" + selection.WeakEliteDeckId
                    + " strong=" + selection.StrongEliteDeckId
                    + " boss=" + selection.BossDeckId);
            }

            builder.AppendLine();
            builder.AppendLine("Enemy definitions seen");
            builder.AppendLine("  " + string.Join(", ", result.SeenEnemyDefIds));
            builder.AppendLine();

            if (result.InvariantIssues.Count > 0)
            {
                builder.AppendLine("Invariant issues");
                for (var i = 0; i < result.InvariantIssues.Count; i++)
                {
                    builder.AppendLine("  #" + result.InvariantIssues[i].Sequence + " " + result.InvariantIssues[i].Message);
                }

                builder.AppendLine();
            }

            builder.AppendLine("Command timeline");
            var lastFloor = -1;
            var lastNode = -1;
            for (var i = 0; i < result.Commands.Count; i++)
            {
                var command = result.Commands[i];
                if (command.Floor != lastFloor || command.NodeIndex != lastNode)
                {
                    lastFloor = command.Floor;
                    lastNode = command.NodeIndex;
                    builder.AppendLine();
                    builder.AppendLine("== Floor " + command.Floor + " Node " + command.NodeIndex + " ==");
                }

                builder.AppendLine("[" + command.Index + "] "
                    + command.PhaseBefore + " -> " + command.PhaseAfter
                    + " " + command.Description
                    + " accepted=" + command.Accepted
                    + " actions=" + command.ResolvedActions
                    + (string.IsNullOrEmpty(command.RejectReason) ? string.Empty : " reason=" + command.RejectReason));

                for (var rowIndex = command.EventStartIndex; rowIndex < command.EventEndIndex && rowIndex < result.ActionLogRows.Count; rowIndex++)
                {
                    var row = result.ActionLogRows[rowIndex];
                    if (!includeLifecycleEvents && row.Category == PresentationEventCategory.ActionLifecycle)
                    {
                        continue;
                    }

                    builder.AppendLine("  " + row.Summary);
                }
            }

            return builder.ToString();
        }

        public static string Write(HeadlessSimulationResult result, string directory, bool includeLifecycleEvents)
        {
            var transcript = Build(result, includeLifecycleEvents);
            result.Transcript = transcript;

            if (string.IsNullOrEmpty(directory))
            {
                return string.Empty;
            }

            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "sim_"
                + result.ContentSeed
                + "_"
                + result.AgentSeed
                + "_"
                + result.Outcome
                + ".txt");
            File.WriteAllText(path, transcript, Encoding.UTF8);
            result.TranscriptPath = path;
            return path;
        }
    }
}
