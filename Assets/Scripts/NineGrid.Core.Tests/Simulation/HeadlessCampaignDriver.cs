using System.Collections.Generic;
using NineGrid.Content;
using NineGrid.Core.Content;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using QFramework;

namespace NineGrid.Core.Tests.Simulation
{
    public sealed class HeadlessCampaignDriver
    {
        public HeadlessSimulationResult Run(HeadlessSimulationOptions options)
        {
            options = options ?? new HeadlessSimulationOptions();

            NineGridArchitecture.ResetForTests();
            var architecture = NineGridArchitecture.Current;
            var catalog = options.CatalogOverride ?? TableNineContentCatalog.CreateDefault();
            architecture.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, catalog);
            architecture.GetSystem<IContentSystem>().Load(catalog);
            InitialGameFactory.Create(
                architecture,
                new InitialGameOptions
                {
                    Seed = options.ContentSeed,
                    AvatarMaxHp = options.AvatarMaxHp,
                    AvatarAttack = options.AvatarAttack,
                    AvatarRecovery = options.AvatarRecovery
                });
            architecture.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, catalog);
            architecture.GetSystem<IContentSystem>().Load(catalog);

            var result = new HeadlessSimulationResult
            {
                ContentSeed = options.ContentSeed,
                AgentSeed = options.AgentSeed,
                Outcome = HeadlessRunOutcome.None
            };

            var agent = new RandomAgent(options.AgentSeed);
            var checker = new HeadlessInvariantChecker(architecture.GetModel<PlayerModel>().Coins.Value);
            var commandsThisNode = 0;
            var noProgressCommands = 0;
            HeadlessDeckSelection currentSelection = null;

            while (result.Outcome == HeadlessRunOutcome.None)
            {
                var run = architecture.GetModel<RunModel>();
                if (run.Phase.Value == GamePhase.Victory || run.Phase.Value == GamePhase.Defeat)
                {
                    result.Outcome = OutcomeFromPhase(run.Phase.Value);
                    result.StopReason = "Terminal phase reached.";
                    break;
                }

                if (result.Commands.Count >= options.MaxTotalCommands)
                {
                    result.Outcome = HeadlessRunOutcome.StepLimit;
                    result.StopReason = "Max total command limit reached.";
                    break;
                }

                HeadlessCommandDecision decision;
                if (ShouldStartNode(run.Phase.Value))
                {
                    var nodeIndex = run.NodeIndex.Value + 1;
                    currentSelection = EnsureDeckSelection(architecture, catalog, result, currentSelection, run.Floor.Value);
                    decision = BuildStartNodeDecision(architecture, catalog, options, currentSelection, run.Floor.Value, nodeIndex, result);
                    commandsThisNode = 0;
                    noProgressCommands = 0;
                }
                else
                {
                    if (commandsThisNode >= options.MaxCommandsPerNode)
                    {
                        result.Outcome = HeadlessRunOutcome.Stalled;
                        result.StopReason = "Max commands per node reached.";
                        break;
                    }

                    if (!agent.TryChoose(architecture, out decision))
                    {
                        result.Outcome = HeadlessRunOutcome.Stalled;
                        result.StopReason = "Agent found no legal concrete command in phase " + run.Phase.Value + ".";
                        break;
                    }
                }

                var record = ExecuteDecision(architecture, decision, result.Commands.Count);
                result.AddCommand(record);
                result.TotalResolvedActions += record.ResolvedActions;

                checker.CheckNewEvents(architecture);
                if (checker.HasIssues && options.StopOnInvariantFailure)
                {
                    result.Outcome = HeadlessRunOutcome.InvariantFailed;
                    result.StopReason = "Invariant checker reported an issue.";
                    break;
                }

                if (!record.Accepted)
                {
                    result.Outcome = HeadlessRunOutcome.CommandRejected;
                    result.StopReason = record.RejectReason;
                    break;
                }

                if (!ShouldStartNode(record.PhaseBefore))
                {
                    commandsThisNode++;
                    if (CommandMadeProgress(architecture, record.EventStartIndex, record.EventEndIndex))
                    {
                        noProgressCommands = 0;
                    }
                    else
                    {
                        noProgressCommands++;
                    }

                    if (options.NoProgressCommandLimit > 0 && noProgressCommands >= options.NoProgressCommandLimit)
                    {
                        result.Outcome = HeadlessRunOutcome.Stalled;
                        result.StopReason = "No progress command limit reached.";
                        break;
                    }
                }
            }

            for (var i = 0; i < checker.Issues.Count; i++)
            {
                result.AddInvariantIssue(checker.Issues[i]);
            }

            FinalizeResult(architecture, options, result);
            return result;
        }

        private static HeadlessCommandDecision BuildStartNodeDecision(
            IArchitecture architecture,
            GameContentCatalog catalog,
            HeadlessSimulationOptions options,
            HeadlessDeckSelection selection,
            int floor,
            int nodeIndex,
            HeadlessSimulationResult result)
        {
            var kind = FindNodeDeckKind(catalog, nodeIndex);
            var deckId = selection == null ? string.Empty : selection.GetDeckId(kind);
            var nodeOptions = architecture.GetSystem<IRewardSystem>().BuildNodeDeckOptions(nodeIndex, deckId);
            ApplyForcedCards(architecture, options, nodeOptions, floor, nodeIndex);
            RecordSeenEnemyCards(nodeOptions, result);
            return HeadlessCommandDecision.StartNode(
                nodeOptions,
                "StartNode floor=" + floor + " node=" + nodeIndex + " deck=" + deckId);
        }

        private static void ApplyForcedCards(
            IArchitecture architecture,
            HeadlessSimulationOptions options,
            NodeDeckOptions nodeOptions,
            int floor,
            int nodeIndex)
        {
            var content = architecture.GetSystem<IContentSystem>();
            for (var i = 0; i < options.ForcedNodeCards.Count; i++)
            {
                var forced = options.ForcedNodeCards[i];
                if (!forced.Matches(floor, nodeIndex))
                {
                    continue;
                }

                var count = forced.Count <= 0 ? 1 : forced.Count;
                for (var j = 0; j < count; j++)
                {
                    var draft = content.CreateDraft(forced.DefId);
                    if (draft.Kind == CardKind.Unknown)
                    {
                        var fallbackKind = forced.Kind == CardKind.Unknown
                            ? forced.EnemyCard ? CardKind.Monster : CardKind.HelpCard
                            : forced.Kind;
                        draft = new CardDraft(forced.DefId, fallbackKind);
                    }

                    if (forced.EnemyCard)
                    {
                        nodeOptions.AddEnemyCard(draft);
                    }
                    else
                    {
                        nodeOptions.AddPlayerCard(draft);
                    }
                }
            }
        }

        private static void RecordSeenEnemyCards(NodeDeckOptions options, HeadlessSimulationResult result)
        {
            for (var i = 0; i < options.EnemyCards.Count; i++)
            {
                result.AddSeenEnemyDefId(options.EnemyCards[i].DefId);
            }
        }

        private static HeadlessDeckSelection EnsureDeckSelection(
            IArchitecture architecture,
            GameContentCatalog catalog,
            HeadlessSimulationResult result,
            HeadlessDeckSelection current,
            int floor)
        {
            if (current != null && current.Floor == floor)
            {
                return current;
            }

            var rng = architecture.GetUtility<IRngUtility>();
            var selection = new HeadlessDeckSelection(
                floor,
                PickDeck(catalog, MonsterDeckKind.WeakElite, rng),
                PickDeck(catalog, MonsterDeckKind.StrongElite, rng),
                PickDeck(catalog, MonsterDeckKind.Boss, rng));
            result.AddDeckSelection(selection);
            return selection;
        }

        private static string PickDeck(GameContentCatalog catalog, MonsterDeckKind kind, IRngUtility rng)
        {
            if (catalog == null)
            {
                return string.Empty;
            }

            var candidates = new List<string>();
            foreach (var pair in catalog.MonsterDecks)
            {
                if (pair.Value.Kind == kind)
                {
                    candidates.Add(pair.Key);
                }
            }

            if (candidates.Count == 0)
            {
                return string.Empty;
            }

            return candidates[rng.Range(0, candidates.Count)];
        }

        private static MonsterDeckKind FindNodeDeckKind(GameContentCatalog catalog, int nodeIndex)
        {
            if (catalog == null)
            {
                return MonsterDeckKind.Unknown;
            }

            for (var i = 0; i < catalog.Rewards.NodeDeckRules.Count; i++)
            {
                var rule = catalog.Rewards.NodeDeckRules[i];
                if (rule.NodeIndex == nodeIndex)
                {
                    return rule.DeckKind;
                }
            }

            return MonsterDeckKind.Unknown;
        }

        private static HeadlessCommandRecord ExecuteDecision(IArchitecture architecture, HeadlessCommandDecision decision, int index)
        {
            var run = architecture.GetModel<RunModel>();
            var pipeline = architecture.GetSystem<IActionPipelineSystem>();
            var startIndex = pipeline.EventLog.Entries.Count;
            var record = new HeadlessCommandRecord
            {
                Index = index,
                Floor = run.Floor.Value,
                NodeIndex = run.NodeIndex.Value + 1,
                PhaseBefore = run.Phase.Value,
                CommandKind = decision.Kind,
                Description = decision.Description,
                EventStartIndex = startIndex
            };

            var commandResult = decision.Execute(architecture);
            record.Accepted = commandResult.Accepted;
            record.RejectReason = commandResult.Reason;
            record.ResolvedActions = commandResult.ResolvedActions;
            record.PhaseAfter = run.Phase.Value;
            record.EventEndIndex = pipeline.EventLog.Entries.Count;
            return record;
        }

        private static bool CommandMadeProgress(IArchitecture architecture, int startIndex, int endIndex)
        {
            var entries = architecture.GetSystem<IActionPipelineSystem>().EventLog.Entries;
            for (var i = startIndex; i < endIndex && i < entries.Count; i++)
            {
                var entry = entries[i];
                switch (entry.Type)
                {
                    case CoreEventType.DamageDealt:
                        if (entry.Delta > 0)
                        {
                            return true;
                        }

                        break;
                    case CoreEventType.CardKilled:
                    case CoreEventType.ItemPicked:
                    case CoreEventType.NodeCompleted:
                    case CoreEventType.RewardSelected:
                    case CoreEventType.RewardSkipped:
                    case CoreEventType.RoomSelected:
                    case CoreEventType.RoomResolved:
                    case CoreEventType.NodeAdvanced:
                    case CoreEventType.PhaseChanged:
                        return true;
                }
            }

            return false;
        }

        private static bool ShouldStartNode(GamePhase phase)
        {
            return phase == GamePhase.None
                || phase == GamePhase.BuildEnemyPool
                || phase == GamePhase.NodeCompleted;
        }

        private static HeadlessRunOutcome OutcomeFromPhase(GamePhase phase)
        {
            if (phase == GamePhase.Victory)
            {
                return HeadlessRunOutcome.Victory;
            }

            if (phase == GamePhase.Defeat)
            {
                return HeadlessRunOutcome.Defeat;
            }

            return HeadlessRunOutcome.None;
        }

        private static void FinalizeResult(
            IArchitecture architecture,
            HeadlessSimulationOptions options,
            HeadlessSimulationResult result)
        {
            var run = architecture.GetModel<RunModel>();
            var registry = architecture.GetModel<CardRegistry>();
            var board = architecture.GetModel<BoardModel>();
            var player = architecture.GetModel<PlayerModel>();
            var avatar = registry.Get(board.AvatarUid.Value);
            var eventLog = architecture.GetSystem<IActionPipelineSystem>().EventLog;

            result.FinalFloor = run.Floor.Value;
            result.FinalNodeIndex = run.NodeIndex.Value;
            result.FinalPhase = run.Phase.Value;
            result.FinalCoins = player.Coins.Value;
            result.FinalAvatarHp = (int)avatar.Stats.GetBase(StatId.Hp);
            result.FinalAvatarArmor = (int)avatar.Stats.GetBase(StatId.Armor);
            result.MonsterKills = CountEvents(eventLog, CoreEventType.CardKilled);
            result.TotalDamageToAvatar = CountAvatarDamage(eventLog, avatar.Uid);
            result.AddActionRows(ActionLogProjector.FromEventLog(eventLog));

            if (options.WriteTranscript)
            {
                RunTranscriptWriter.Write(result, options.TranscriptDirectory, options.IncludeLifecycleEventsInTranscript);
            }
            else
            {
                result.Transcript = RunTranscriptWriter.Build(result, options.IncludeLifecycleEventsInTranscript);
            }
        }

        private static int CountEvents(EventLog eventLog, CoreEventType type)
        {
            var count = 0;
            for (var i = 0; i < eventLog.Entries.Count; i++)
            {
                if (eventLog.Entries[i].Type == type)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountAvatarDamage(EventLog eventLog, int avatarUid)
        {
            var total = 0;
            for (var i = 0; i < eventLog.Entries.Count; i++)
            {
                var entry = eventLog.Entries[i];
                if (entry.Type == CoreEventType.DamageDealt && entry.CardUid == avatarUid)
                {
                    total += entry.Delta;
                }
            }

            return total;
        }
    }
}
