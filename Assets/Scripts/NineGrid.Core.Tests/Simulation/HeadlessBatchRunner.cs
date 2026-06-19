using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NineGrid.Core.Tests.Simulation
{
    public sealed class HeadlessBatchOptions
    {
        public HeadlessBatchOptions()
        {
            StartContentSeed = 1UL;
            StartAgentSeed = 1001UL;
            Count = 10;
            OutputDirectory = "Logs/sim";
            SimulationOptions = new HeadlessSimulationOptions { WriteTranscript = false };
        }

        public ulong StartContentSeed { get; set; }
        public ulong StartAgentSeed { get; set; }
        public int Count { get; set; }
        public string OutputDirectory { get; set; }
        public HeadlessSimulationOptions SimulationOptions { get; set; }
    }

    public sealed class HeadlessRunSummary
    {
        public ulong ContentSeed { get; set; }
        public ulong AgentSeed { get; set; }
        public HeadlessRunOutcome Outcome { get; set; }
        public int FinalFloor { get; set; }
        public int FinalNodeIndex { get; set; }
        public GamePhase FinalPhase { get; set; }
        public int Commands { get; set; }
        public int ResolvedActions { get; set; }
        public int MonsterKills { get; set; }
        public int AvatarDamage { get; set; }
        public int FinalHp { get; set; }
        public int Coins { get; set; }
        public string StopReason { get; set; }

        public static HeadlessRunSummary FromResult(HeadlessSimulationResult result)
        {
            return new HeadlessRunSummary
            {
                ContentSeed = result.ContentSeed,
                AgentSeed = result.AgentSeed,
                Outcome = result.Outcome,
                FinalFloor = result.FinalFloor,
                FinalNodeIndex = result.FinalNodeIndex,
                FinalPhase = result.FinalPhase,
                Commands = result.Commands.Count,
                ResolvedActions = result.TotalResolvedActions,
                MonsterKills = result.MonsterKills,
                AvatarDamage = result.TotalDamageToAvatar,
                FinalHp = result.FinalAvatarHp,
                Coins = result.FinalCoins,
                StopReason = result.StopReason
            };
        }
    }

    public sealed class HeadlessBatchResult
    {
        private readonly List<HeadlessRunSummary> mRuns = new List<HeadlessRunSummary>();

        public IReadOnlyList<HeadlessRunSummary> Runs
        {
            get { return mRuns; }
        }

        public int VictoryCount { get; set; }
        public int DefeatCount { get; set; }
        public int StalledCount { get; set; }
        public string CsvPath { get; set; }
        public string JsonPath { get; set; }

        public void Add(HeadlessRunSummary summary)
        {
            if (summary == null)
            {
                return;
            }

            mRuns.Add(summary);
            switch (summary.Outcome)
            {
                case HeadlessRunOutcome.Victory:
                    VictoryCount++;
                    break;
                case HeadlessRunOutcome.Defeat:
                    DefeatCount++;
                    break;
                case HeadlessRunOutcome.Stalled:
                case HeadlessRunOutcome.CommandRejected:
                case HeadlessRunOutcome.StepLimit:
                case HeadlessRunOutcome.InvariantFailed:
                    StalledCount++;
                    break;
            }
        }
    }

    public sealed class HeadlessBatchRunner
    {
        public HeadlessBatchResult Run(HeadlessBatchOptions options)
        {
            options = options ?? new HeadlessBatchOptions();
            var baseOptions = options.SimulationOptions ?? new HeadlessSimulationOptions { WriteTranscript = false };
            var result = new HeadlessBatchResult();
            var driver = new HeadlessCampaignDriver();

            for (var i = 0; i < options.Count; i++)
            {
                var contentSeed = options.StartContentSeed + (ulong)i;
                var agentSeed = options.StartAgentSeed + (ulong)i;
                var runOptions = baseOptions.CloneForSeeds(contentSeed, agentSeed);
                runOptions.WriteTranscript = false;
                var runResult = driver.Run(runOptions);
                result.Add(HeadlessRunSummary.FromResult(runResult));
            }

            if (!string.IsNullOrEmpty(options.OutputDirectory))
            {
                Directory.CreateDirectory(options.OutputDirectory);
                result.CsvPath = Path.Combine(options.OutputDirectory, "sim_batch_" + options.StartContentSeed + "_" + options.Count + ".csv");
                result.JsonPath = Path.Combine(options.OutputDirectory, "sim_batch_" + options.StartContentSeed + "_" + options.Count + ".json");
                File.WriteAllText(result.CsvPath, ToCsv(result), Encoding.UTF8);
                File.WriteAllText(result.JsonPath, ToJson(result), Encoding.UTF8);
            }

            return result;
        }

        private static string ToCsv(HeadlessBatchResult result)
        {
            var builder = new StringBuilder();
            builder.AppendLine("contentSeed,agentSeed,outcome,finalFloor,finalNodeIndex,finalPhase,commands,resolvedActions,monsterKills,avatarDamage,finalHp,coins,stopReason");
            for (var i = 0; i < result.Runs.Count; i++)
            {
                var run = result.Runs[i];
                builder.Append(run.ContentSeed).Append(",");
                builder.Append(run.AgentSeed).Append(",");
                builder.Append(run.Outcome).Append(",");
                builder.Append(run.FinalFloor).Append(",");
                builder.Append(run.FinalNodeIndex).Append(",");
                builder.Append(run.FinalPhase).Append(",");
                builder.Append(run.Commands).Append(",");
                builder.Append(run.ResolvedActions).Append(",");
                builder.Append(run.MonsterKills).Append(",");
                builder.Append(run.AvatarDamage).Append(",");
                builder.Append(run.FinalHp).Append(",");
                builder.Append(run.Coins).Append(",");
                builder.Append(EscapeCsv(run.StopReason)).AppendLine();
            }

            return builder.ToString();
        }

        private static string ToJson(HeadlessBatchResult result)
        {
            var builder = new StringBuilder();
            builder.AppendLine("{");
            builder.AppendLine("  \"victoryCount\": " + result.VictoryCount + ",");
            builder.AppendLine("  \"defeatCount\": " + result.DefeatCount + ",");
            builder.AppendLine("  \"stalledCount\": " + result.StalledCount + ",");
            builder.AppendLine("  \"runs\": [");
            for (var i = 0; i < result.Runs.Count; i++)
            {
                var run = result.Runs[i];
                builder.Append("    {");
                builder.Append("\"contentSeed\":").Append(run.ContentSeed).Append(",");
                builder.Append("\"agentSeed\":").Append(run.AgentSeed).Append(",");
                builder.Append("\"outcome\":\"").Append(run.Outcome).Append("\",");
                builder.Append("\"finalFloor\":").Append(run.FinalFloor).Append(",");
                builder.Append("\"finalNodeIndex\":").Append(run.FinalNodeIndex).Append(",");
                builder.Append("\"finalPhase\":\"").Append(run.FinalPhase).Append("\",");
                builder.Append("\"commands\":").Append(run.Commands).Append(",");
                builder.Append("\"resolvedActions\":").Append(run.ResolvedActions).Append(",");
                builder.Append("\"monsterKills\":").Append(run.MonsterKills).Append(",");
                builder.Append("\"avatarDamage\":").Append(run.AvatarDamage).Append(",");
                builder.Append("\"finalHp\":").Append(run.FinalHp).Append(",");
                builder.Append("\"coins\":").Append(run.Coins).Append(",");
                builder.Append("\"stopReason\":\"").Append(EscapeJson(run.StopReason)).Append("\"");
                builder.Append("}");
                if (i < result.Runs.Count - 1)
                {
                    builder.Append(",");
                }

                builder.AppendLine();
            }

            builder.AppendLine("  ]");
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static string EscapeCsv(string value)
        {
            value = value ?? string.Empty;
            if (value.IndexOf(',') < 0 && value.IndexOf('"') < 0 && value.IndexOf('\n') < 0)
            {
                return value;
            }

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static string EscapeJson(string value)
        {
            value = value ?? string.Empty;
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
