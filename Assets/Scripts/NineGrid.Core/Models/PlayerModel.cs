using System.Collections.Generic;
using NineGrid.Core.Stats;
using QFramework;

namespace NineGrid.Core
{
    public sealed class PlayerModel : AbstractModel
    {
        private readonly List<string> mRelicDefIds = new List<string>();
        private readonly List<string> mSkillDefIds = new List<string>();

        public PlayerModel()
        {
            Stats = new StatBlock();
        }

        public StatBlock Stats { get; private set; }
        public BindableProperty<int> Coins { get; private set; }
        public BindableProperty<int> InteractionCount { get; private set; }
        public BindableProperty<int> Version { get; private set; }

        public IReadOnlyList<string> RelicDefIds
        {
            get { return mRelicDefIds; }
        }

        public IReadOnlyList<string> SkillDefIds
        {
            get { return mSkillDefIds; }
        }

        protected override void OnInit()
        {
            if (Coins == null)
            {
                Coins = new BindableProperty<int>(0);
                InteractionCount = new BindableProperty<int>(0);
                Version = new BindableProperty<int>(0);
            }
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

        public void AddSkill(string defId)
        {
            if (!string.IsNullOrEmpty(defId) && !mSkillDefIds.Contains(defId))
            {
                mSkillDefIds.Add(defId);
                Touch();
            }
        }

        public void Reset()
        {
            Stats.Clear();
            Coins.Value = 0;
            InteractionCount.Value = 0;
            mRelicDefIds.Clear();
            mSkillDefIds.Clear();
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
