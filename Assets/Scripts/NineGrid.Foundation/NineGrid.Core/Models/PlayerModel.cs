using System.Collections.Generic;
using NineGrid.Core.Stats;
using QFramework;

namespace NineGrid.Core
{
    public sealed class PlayerModel : AbstractModel
    {
        private readonly List<string> mRelicDefIds = new List<string>();
        private readonly List<HelpCardStackEntry> mHelpCardStacks = new List<HelpCardStackEntry>();

        public PlayerModel()
        {
            Stats = new StatBlock();
        }

        public StatBlock Stats { get; private set; }
        public BindableProperty<int> Coins { get; private set; }
        public BindableProperty<int> InteractionCount { get; private set; }
        public BindableProperty<string> ProfessionId { get; private set; }
        public BindableProperty<int> Version { get; private set; }

        public IReadOnlyList<string> RelicDefIds
        {
            get { return mRelicDefIds; }
        }

        public IReadOnlyList<HelpCardStackEntry> HelpCardStacks
        {
            get { return mHelpCardStacks; }
        }

        protected override void OnInit()
        {
            if (Coins == null)
            {
                Coins = new BindableProperty<int>(0);
                InteractionCount = new BindableProperty<int>(0);
                ProfessionId = new BindableProperty<string>(string.Empty);
                Version = new BindableProperty<int>(0);
            }
        }

        public void SetProfession(string defId)
        {
            ProfessionId.Value = defId ?? string.Empty;
            Touch();
        }

        public void AddCoins(int delta)
        {
            Coins.Value += delta;
            Touch();
        }

        public void AddInteractionCount(int delta)
        {
            InteractionCount.Value += delta;
            Touch();
        }

        public void AddRelic(string defId)
        {
            if (!string.IsNullOrEmpty(defId) && !mRelicDefIds.Contains(defId))
            {
                mRelicDefIds.Add(defId);
                Touch();
            }
        }

        public bool RemoveRelic(string defId)
        {
            var removed = mRelicDefIds.Remove(defId);
            if (removed)
            {
                Touch();
            }

            return removed;
        }

        public void AddHelpCard(string defId, int count = 1)
        {
            if (string.IsNullOrEmpty(defId) || count <= 0)
            {
                return;
            }

            for (var i = 0; i < mHelpCardStacks.Count; i++)
            {
                if (mHelpCardStacks[i].DefId != defId)
                {
                    continue;
                }

                mHelpCardStacks[i] = new HelpCardStackEntry(defId, mHelpCardStacks[i].Count + count);
                Touch();
                return;
            }

            mHelpCardStacks.Add(new HelpCardStackEntry(defId, count));
            Touch();
        }

        public bool TryRemoveHelpCard(string defId, int count = 1)
        {
            if (string.IsNullOrEmpty(defId) || count <= 0)
            {
                return false;
            }

            for (var i = 0; i < mHelpCardStacks.Count; i++)
            {
                if (mHelpCardStacks[i].DefId != defId)
                {
                    continue;
                }

                var next = mHelpCardStacks[i].Count - count;
                if (next > 0)
                {
                    mHelpCardStacks[i] = new HelpCardStackEntry(defId, next);
                }
                else
                {
                    mHelpCardStacks.RemoveAt(i);
                }

                Touch();
                return true;
            }

            return false;
        }

        public int CountHelpCardsByDefId(string defId)
        {
            if (string.IsNullOrEmpty(defId))
            {
                return 0;
            }

            for (var i = 0; i < mHelpCardStacks.Count; i++)
            {
                if (mHelpCardStacks[i].DefId == defId)
                {
                    return mHelpCardStacks[i].Count;
                }
            }

            return 0;
        }

        public int TotalHelpCardCount()
        {
            var total = 0;
            for (var i = 0; i < mHelpCardStacks.Count; i++)
            {
                total += mHelpCardStacks[i].Count;
            }

            return total;
        }

        public List<HelpCardStackEntry> ConsumeHelpCardStacksForNode()
        {
            if (mHelpCardStacks.Count == 0)
            {
                return new List<HelpCardStackEntry>();
            }

            var consumed = new List<HelpCardStackEntry>(mHelpCardStacks.Count);
            for (var i = 0; i < mHelpCardStacks.Count; i++)
            {
                consumed.Add(mHelpCardStacks[i]);
            }

            mHelpCardStacks.Clear();
            Touch();
            return consumed;
        }

        public void Reset()
        {
            Stats.Clear();
            Coins.Value = 0;
            InteractionCount.Value = 0;
            ProfessionId.Value = string.Empty;
            mRelicDefIds.Clear();
            mHelpCardStacks.Clear();
            Touch();
        }

        private void Touch()
        {
            if (Version != null)
            {
                Version.Value++;
            }
        }
    }
}
