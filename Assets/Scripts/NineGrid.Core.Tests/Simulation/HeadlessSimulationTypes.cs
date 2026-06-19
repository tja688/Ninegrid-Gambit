using System.Collections.Generic;
using NineGrid.Core.Content;

namespace NineGrid.Core.Tests.Simulation
{
    public enum HeadlessRunOutcome
    {
        None,
        Victory,
        Defeat,
        Stalled,
        CommandRejected,
        StepLimit,
        InvariantFailed
    }

    public sealed class ForcedNodeCard
    {
        public int Floor { get; set; }
        public int NodeIndex { get; set; }
        public string DefId { get; set; }
        public CardKind Kind { get; set; }
        public bool EnemyCard { get; set; }
        public int Count { get; set; }

        public bool Matches(int floor, int nodeIndex)
        {
            return Floor == floor && NodeIndex == nodeIndex && !string.IsNullOrEmpty(DefId);
        }
    }

    public sealed class HeadlessSimulationOptions
    {
        private readonly List<ForcedNodeCard> mForcedNodeCards = new List<ForcedNodeCard>();

        public HeadlessSimulationOptions()
        {
            ContentSeed = 1UL;
            AgentSeed = 1001UL;
            AvatarMaxHp = 30;
            AvatarAttack = 1;
            AvatarRecovery = 1;
            MaxCommandsPerNode = 240;
            MaxTotalCommands = 8000;
            NoProgressCommandLimit = 80;
            StopOnInvariantFailure = true;
            WriteTranscript = true;
            TranscriptDirectory = "Logs/sim";
        }

        public ulong ContentSeed { get; set; }
        public ulong AgentSeed { get; set; }
        public int AvatarMaxHp { get; set; }
        public int AvatarAttack { get; set; }
        public int AvatarRecovery { get; set; }
        public int MaxCommandsPerNode { get; set; }
        public int MaxTotalCommands { get; set; }
        public int NoProgressCommandLimit { get; set; }
        public bool StopOnInvariantFailure { get; set; }
        public bool WriteTranscript { get; set; }
        public bool IncludeLifecycleEventsInTranscript { get; set; }
        public string TranscriptDirectory { get; set; }
        public GameContentCatalog CatalogOverride { get; set; }

        public IReadOnlyList<ForcedNodeCard> ForcedNodeCards
        {
            get { return mForcedNodeCards; }
        }

        public HeadlessSimulationOptions AddForcedNodeCard(ForcedNodeCard forced)
        {
            if (forced != null)
            {
                mForcedNodeCards.Add(forced);
            }

            return this;
        }

        public HeadlessSimulationOptions CloneForSeeds(ulong contentSeed, ulong agentSeed)
        {
            var clone = new HeadlessSimulationOptions
            {
                ContentSeed = contentSeed,
                AgentSeed = agentSeed,
                AvatarMaxHp = AvatarMaxHp,
                AvatarAttack = AvatarAttack,
                AvatarRecovery = AvatarRecovery,
                MaxCommandsPerNode = MaxCommandsPerNode,
                MaxTotalCommands = MaxTotalCommands,
                NoProgressCommandLimit = NoProgressCommandLimit,
                StopOnInvariantFailure = StopOnInvariantFailure,
                WriteTranscript = WriteTranscript,
                IncludeLifecycleEventsInTranscript = IncludeLifecycleEventsInTranscript,
                TranscriptDirectory = TranscriptDirectory,
                CatalogOverride = CatalogOverride
            };

            for (var i = 0; i < mForcedNodeCards.Count; i++)
            {
                clone.AddForcedNodeCard(mForcedNodeCards[i]);
            }

            return clone;
        }
    }

    public sealed class HeadlessDeckSelection
    {
        public HeadlessDeckSelection(int floor, string weakEliteDeckId, string strongEliteDeckId, string bossDeckId)
        {
            Floor = floor;
            WeakEliteDeckId = weakEliteDeckId ?? string.Empty;
            StrongEliteDeckId = strongEliteDeckId ?? string.Empty;
            BossDeckId = bossDeckId ?? string.Empty;
        }

        public int Floor { get; private set; }
        public string WeakEliteDeckId { get; private set; }
        public string StrongEliteDeckId { get; private set; }
        public string BossDeckId { get; private set; }

        public string GetDeckId(MonsterDeckKind kind)
        {
            switch (kind)
            {
                case MonsterDeckKind.WeakElite:
                    return WeakEliteDeckId;
                case MonsterDeckKind.StrongElite:
                    return StrongEliteDeckId;
                case MonsterDeckKind.Boss:
                    return BossDeckId;
                default:
                    return string.Empty;
            }
        }
    }

    public sealed class HeadlessCommandRecord
    {
        public int Index { get; set; }
        public int Floor { get; set; }
        public int NodeIndex { get; set; }
        public GamePhase PhaseBefore { get; set; }
        public GamePhase PhaseAfter { get; set; }
        public GameCommandKind CommandKind { get; set; }
        public string Description { get; set; }
        public bool Accepted { get; set; }
        public string RejectReason { get; set; }
        public int ResolvedActions { get; set; }
        public int EventStartIndex { get; set; }
        public int EventEndIndex { get; set; }
    }

    public sealed class HeadlessSimulationResult
    {
        private readonly List<HeadlessCommandRecord> mCommands = new List<HeadlessCommandRecord>();
        private readonly List<ActionLogRow> mActionLogRows = new List<ActionLogRow>();
        private readonly List<HeadlessInvariantIssue> mInvariantIssues = new List<HeadlessInvariantIssue>();
        private readonly List<HeadlessDeckSelection> mDeckSelections = new List<HeadlessDeckSelection>();
        private readonly List<string> mSeenEnemyDefIds = new List<string>();

        public ulong ContentSeed { get; set; }
        public ulong AgentSeed { get; set; }
        public HeadlessRunOutcome Outcome { get; set; }
        public string StopReason { get; set; }
        public int FinalFloor { get; set; }
        public int FinalNodeIndex { get; set; }
        public GamePhase FinalPhase { get; set; }
        public int FinalCoins { get; set; }
        public int FinalAvatarHp { get; set; }
        public int FinalAvatarArmor { get; set; }
        public int TotalResolvedActions { get; set; }
        public int TotalDamageToAvatar { get; set; }
        public int MonsterKills { get; set; }
        public string Transcript { get; set; }
        public string TranscriptPath { get; set; }

        public IReadOnlyList<HeadlessCommandRecord> Commands
        {
            get { return mCommands; }
        }

        public IReadOnlyList<ActionLogRow> ActionLogRows
        {
            get { return mActionLogRows; }
        }

        public IReadOnlyList<HeadlessInvariantIssue> InvariantIssues
        {
            get { return mInvariantIssues; }
        }

        public IReadOnlyList<HeadlessDeckSelection> DeckSelections
        {
            get { return mDeckSelections; }
        }

        public IReadOnlyList<string> SeenEnemyDefIds
        {
            get { return mSeenEnemyDefIds; }
        }

        public void AddCommand(HeadlessCommandRecord record)
        {
            if (record != null)
            {
                mCommands.Add(record);
            }
        }

        public void AddActionRows(IReadOnlyList<ActionLogRow> rows)
        {
            mActionLogRows.Clear();
            if (rows == null)
            {
                return;
            }

            for (var i = 0; i < rows.Count; i++)
            {
                mActionLogRows.Add(rows[i]);
            }
        }

        public void AddInvariantIssue(HeadlessInvariantIssue issue)
        {
            if (issue != null)
            {
                mInvariantIssues.Add(issue);
            }
        }

        public void AddDeckSelection(HeadlessDeckSelection selection)
        {
            if (selection != null)
            {
                mDeckSelections.Add(selection);
            }
        }

        public void AddSeenEnemyDefId(string defId)
        {
            if (!string.IsNullOrEmpty(defId) && !mSeenEnemyDefIds.Contains(defId))
            {
                mSeenEnemyDefIds.Add(defId);
            }
        }
    }
}
