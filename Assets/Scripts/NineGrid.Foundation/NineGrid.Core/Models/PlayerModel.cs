using System.Collections.Generic;
using NineGrid.Core.Stats;
using QFramework;

namespace NineGrid.Core
{
    public sealed class PlayerModel : AbstractModel
    {
        public const int DefaultItemDeckCapacity = 6;

        private readonly List<string> mRelicDefIds = new List<string>();
        private readonly List<string> mItemSourcePoolDefIds = new List<string>();
        private readonly List<string> mFixedItemCardDefIds = new List<string>();
        private readonly List<string> mCarryPackDefIds = new List<string>();
        private int mItemDeckCapacity = DefaultItemDeckCapacity;

        // 神圣决斗（skill.holy_duel）：玩家侧交战记忆，先挂简单状态，预留日后 buff 化。
        // 语义：玩家主动与本卡交战后记下持有者 uid；之后主动与其他怪开战 → 对玩家 2 伤；
        // 持有者翻面或离场清标记（翻面在 FlipCardAction 清，离场在交战前惰性校验）。
        private int mDuelMarkMonsterUid;

        public PlayerModel()
        {
            Stats = new StatBlock();
        }

        public StatBlock Stats { get; private set; }
        public BindableProperty<int> Coins { get; private set; }
        public BindableProperty<int> InteractionCount { get; private set; }
        public BindableProperty<string> ProfessionId { get; private set; }
        public BindableProperty<int> Version { get; private set; }

        /// <summary>神圣决斗标记的持有者怪物 uid；0 = 未标记。</summary>
        public int DuelMarkMonsterUid
        {
            get { return mDuelMarkMonsterUid; }
        }

        public void SetDuelMark(int monsterUid)
        {
            if (mDuelMarkMonsterUid == monsterUid)
            {
                return;
            }

            mDuelMarkMonsterUid = monsterUid;
            Touch();
        }

        public void ClearDuelMark()
        {
            if (mDuelMarkMonsterUid == 0)
            {
                return;
            }

            mDuelMarkMonsterUid = 0;
            Touch();
        }

        public IReadOnlyList<string> RelicDefIds
        {
            get { return mRelicDefIds; }
        }

        /// <summary>玩家侧卡组容量（初始 6，可扩容）；每关按此数从来源池随机生成。</summary>
        public int ItemDeckCapacity
        {
            get { return mItemDeckCapacity; }
        }

        /// <summary>道具卡来源池（通用卡组 + 角色卡组）；跨节点持久。</summary>
        public IReadOnlyList<string> ItemSourcePoolDefIds
        {
            get { return mItemSourcePoolDefIds; }
        }

        /// <summary>固定卡列表（卡店「道具卡固定」）；每关生成时追加。</summary>
        public IReadOnlyList<string> FixedItemCardDefIds
        {
            get { return mFixedItemCardDefIds; }
        }

        /// <summary>携带卡包；下一战斗节点开局倒空注入后清空。</summary>
        public IReadOnlyList<string> CarryPackDefIds
        {
            get { return mCarryPackDefIds; }
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

        public void SetItemDeckCapacity(int capacity)
        {
            var next = capacity < 0 ? 0 : capacity;
            if (mItemDeckCapacity == next)
            {
                return;
            }

            mItemDeckCapacity = next;
            Touch();
        }

        public void AddItemSourcePoolCard(string defId)
        {
            if (string.IsNullOrEmpty(defId) || mItemSourcePoolDefIds.Contains(defId))
            {
                return;
            }

            mItemSourcePoolDefIds.Add(defId);
            Touch();
        }

        public void ReplaceItemSourcePool(IEnumerable<string> defIds)
        {
            mItemSourcePoolDefIds.Clear();
            if (defIds != null)
            {
                foreach (var defId in defIds)
                {
                    if (string.IsNullOrEmpty(defId) || mItemSourcePoolDefIds.Contains(defId))
                    {
                        continue;
                    }

                    mItemSourcePoolDefIds.Add(defId);
                }
            }

            Touch();
        }

        public void AddFixedItemCard(string defId)
        {
            if (string.IsNullOrEmpty(defId))
            {
                return;
            }

            mFixedItemCardDefIds.Add(defId);
            Touch();
        }

        public void ReplaceFixedItemCards(IEnumerable<string> defIds)
        {
            mFixedItemCardDefIds.Clear();
            if (defIds != null)
            {
                foreach (var defId in defIds)
                {
                    if (!string.IsNullOrEmpty(defId))
                    {
                        mFixedItemCardDefIds.Add(defId);
                    }
                }
            }

            Touch();
        }

        public void AddToCarryPack(string defId, int count = 1)
        {
            if (string.IsNullOrEmpty(defId) || count <= 0)
            {
                return;
            }

            for (var i = 0; i < count; i++)
            {
                mCarryPackDefIds.Add(defId);
            }

            Touch();
        }

        public void ReplaceCarryPack(IEnumerable<string> defIds)
        {
            mCarryPackDefIds.Clear();
            if (defIds != null)
            {
                foreach (var defId in defIds)
                {
                    if (!string.IsNullOrEmpty(defId))
                    {
                        mCarryPackDefIds.Add(defId);
                    }
                }
            }

            Touch();
        }

        /// <summary>倒空携带卡包，返回本关待注入的 defId 列表。</summary>
        public List<string> DrainCarryPack()
        {
            if (mCarryPackDefIds.Count == 0)
            {
                return new List<string>();
            }

            var drained = new List<string>(mCarryPackDefIds.Count);
            for (var i = 0; i < mCarryPackDefIds.Count; i++)
            {
                drained.Add(mCarryPackDefIds[i]);
            }

            mCarryPackDefIds.Clear();
            Touch();
            return drained;
        }

        public int CountCarryPackByDefId(string defId)
        {
            if (string.IsNullOrEmpty(defId))
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < mCarryPackDefIds.Count; i++)
            {
                if (mCarryPackDefIds[i] == defId)
                {
                    count++;
                }
            }

            return count;
        }

        public void Reset()
        {
            Stats.Clear();
            Coins.Value = 0;
            InteractionCount.Value = 0;
            ProfessionId.Value = string.Empty;
            mRelicDefIds.Clear();
            mItemDeckCapacity = DefaultItemDeckCapacity;
            mItemSourcePoolDefIds.Clear();
            mFixedItemCardDefIds.Clear();
            mCarryPackDefIds.Clear();
            mDuelMarkMonsterUid = 0;
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
