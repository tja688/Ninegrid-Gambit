using System.Collections.Generic;
using NineGrid.Core.Effects;
using NineGrid.Core.Stats;
using QFramework;

namespace NineGrid.Core
{
    public sealed class PlayerModel : AbstractModel
    {
        public const int DefaultItemDeckCapacity = 6;
        /// <summary>道具卡格容量初始值（ADR-0025）。</summary>
        public const int DefaultItemSlotsCapacity = 3;
        /// <summary>道具卡格容量上限（ADR-0025）。</summary>
        public const int MaxItemSlotsCapacity = 5;
        /// <summary>装备栏上限（设计案：12）。</summary>
        public const int MaxRelicSlots = 12;

        private readonly List<string> mRelicDefIds = new List<string>();
        // 一次性遗物效果消费标记（如黄金鱼竿「获得时给宝箱卡」）：
        // 阻止存档恢复 / 跨层重装 / StartNode 自愈重挂时重复发放。键=目录效果 id。
        private readonly List<string> mConsumedRelicEffectIds = new List<string>();
        private readonly List<string> mItemSourcePoolDefIds = new List<string>();
        private readonly List<string> mFixedItemCardDefIds = new List<string>();
        private int mItemDeckCapacity = DefaultItemDeckCapacity;
        private int mItemSlotsCapacity = DefaultItemSlotsCapacity;
        private int mItemStatBonus;
        // 卡店服务已购次数（价格步进的计数源；跨节点/跨层/存档持久，见 RunSaveGame / RunInventorySnapshot）。
        private int mTavernUpgradePurchaseCount;
        private int mTavernExpandPurchaseCount;

        // 神圣决斗（skill.holy_duel）：玩家侧交战记忆，先挂简单状态，预留日后 buff 化。
        // 语义：玩家主动与每只持有者交战后各自记下 uid；之后主动与其他怪开战 →
        // 每个仍存活且正面的持有者各对玩家 2 伤；仅该持有者死亡或离场时摘标（翻面保留）。
        private readonly List<int> mDuelMarkMonsterUids = new List<int>();

        public PlayerModel()
        {
            Stats = new StatBlock();
        }

        public StatBlock Stats { get; private set; }
        public BindableProperty<int> Coins { get; private set; }
        public BindableProperty<int> InteractionCount { get; private set; }
        public BindableProperty<string> ProfessionId { get; private set; }
        public BindableProperty<int> Version { get; private set; }

        /// <summary>神圣决斗标记的持有者怪物 uid 列表（插入序、去重）。</summary>
        public IReadOnlyList<int> DuelMarkMonsterUids
        {
            get { return mDuelMarkMonsterUids; }
        }

        public void AddDuelMark(int monsterUid)
        {
            if (monsterUid <= 0 || mDuelMarkMonsterUids.Contains(monsterUid))
            {
                return;
            }

            mDuelMarkMonsterUids.Add(monsterUid);
            Touch();
        }

        public void RemoveDuelMark(int monsterUid)
        {
            if (monsterUid <= 0 || !mDuelMarkMonsterUids.Remove(monsterUid))
            {
                return;
            }

            Touch();
        }

        public void ClearDuelMarks()
        {
            if (mDuelMarkMonsterUids.Count == 0)
            {
                return;
            }

            mDuelMarkMonsterUids.Clear();
            Touch();
        }

        public IReadOnlyList<string> RelicDefIds
        {
            get { return mRelicDefIds; }
        }

        /// <summary>装备栏是否已满（不可再获得新遗物，需先丢弃）。</summary>
        public bool IsRelicInventoryFull
        {
            get { return mRelicDefIds.Count >= MaxRelicSlots; }
        }

        /// <summary>玩家侧卡组容量（初始 6，可扩容）；每关按此数从来源池随机生成。</summary>
        public int ItemDeckCapacity
        {
            get { return mItemDeckCapacity; }
        }

        /// <summary>道具卡格容量（初始 3，最高 5）；与 <see cref="ItemDeckCapacity"/> 拆开（ADR-0025）。</summary>
        public int ItemSlotsCapacity
        {
            get { return mItemSlotsCapacity; }
        }

        /// <summary>道具卡来源池（通用卡组 + 角色卡组）；跨节点持久。</summary>
        public IReadOnlyList<string> ItemSourcePoolDefIds
        {
            get { return mItemSourcePoolDefIds; }
        }

        /// <summary>固定卡列表（卡店「道具卡固定」）；每关生成时占 ItemDeckCapacity 预算格。</summary>
        public IReadOnlyList<string> FixedItemCardDefIds
        {
            get { return mFixedItemCardDefIds; }
        }

        /// <summary>
        /// 卡店「道具卡数值强化」累计加成（每购一次 +3）；跨节点应用属 #97，本字段本票可写可读。
        /// </summary>
        public int ItemStatBonus
        {
            get { return mItemStatBonus; }
        }

        /// <summary>卡店「道具卡数值强化」本局已购次数（价格 = 75 + 25 × 次数）。</summary>
        public int TavernUpgradePurchaseCount
        {
            get { return mTavernUpgradePurchaseCount; }
        }

        /// <summary>卡店「道具卡扩容」本局已购次数（价格 = 150 + 50 × 次数）。</summary>
        public int TavernExpandPurchaseCount
        {
            get { return mTavernExpandPurchaseCount; }
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
            if (string.IsNullOrEmpty(defId) || mRelicDefIds.Contains(defId) || IsRelicInventoryFull)
            {
                return;
            }

            mRelicDefIds.Add(defId);
            Touch();
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

        /// <summary>已消费的一次性遗物效果 id（供存档/跨层快照捕获）。</summary>
        public IReadOnlyList<string> ConsumedRelicEffectIds
        {
            get { return mConsumedRelicEffectIds; }
        }

        public bool IsRelicEffectConsumed(string effectId)
        {
            return !string.IsNullOrEmpty(effectId) && mConsumedRelicEffectIds.Contains(effectId);
        }

        public void MarkRelicEffectConsumed(string effectId)
        {
            if (string.IsNullOrEmpty(effectId) || mConsumedRelicEffectIds.Contains(effectId))
            {
                return;
            }

            mConsumedRelicEffectIds.Add(effectId);
            Touch();
        }

        /// <summary>移除单个消费标记（遗物被丢弃/移除时按目录效果 id 逐个清除，重获后可再次生效）。</summary>
        public void ClearRelicEffectConsumed(string effectId)
        {
            if (mConsumedRelicEffectIds.Remove(effectId))
            {
                Touch();
            }
        }

        public void ReplaceConsumedRelicEffects(IEnumerable<string> effectIds)
        {
            mConsumedRelicEffectIds.Clear();
            if (effectIds != null)
            {
                foreach (var effectId in effectIds)
                {
                    if (!string.IsNullOrEmpty(effectId) && !mConsumedRelicEffectIds.Contains(effectId))
                    {
                        mConsumedRelicEffectIds.Add(effectId);
                    }
                }
            }

            Touch();
        }

        public void SetItemSlotsCapacity(int capacity)
        {
            var next = capacity < DefaultItemSlotsCapacity
                ? DefaultItemSlotsCapacity
                : (capacity > MaxItemSlotsCapacity ? MaxItemSlotsCapacity : capacity);
            if (mItemSlotsCapacity == next)
            {
                return;
            }

            mItemSlotsCapacity = next;
            Touch();
        }

        /// <summary>道具卡格是否已满（不可再写入；不挤掉旧卡）。</summary>
        public bool IsItemSlotsFull(DeckModel deck)
        {
            return deck != null && deck.ItemSlotUids.Count >= mItemSlotsCapacity;
        }

        /// <summary>道具卡格是否还能再收下 <paramref name="count"/> 张。</summary>
        public bool CanAcceptIntoItemSlots(DeckModel deck, int count)
        {
            if (deck == null || count <= 0)
            {
                return false;
            }

            return deck.ItemSlotUids.Count + count <= mItemSlotsCapacity;
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

        public void AddItemStatBonus(int delta)
        {
            if (delta == 0)
            {
                return;
            }

            mItemStatBonus += delta;
            if (mItemStatBonus < 0)
            {
                mItemStatBonus = 0;
            }

            Touch();
            ItemStatBonusChangedSink.RaiseChanged();
        }

        public void SetItemStatBonus(int bonus)
        {
            var next = bonus < 0 ? 0 : bonus;
            if (mItemStatBonus == next)
            {
                return;
            }

            mItemStatBonus = next;
            Touch();
            ItemStatBonusChangedSink.RaiseChanged();
        }

        public void AddTavernUpgradePurchase()
        {
            mTavernUpgradePurchaseCount++;
            Touch();
        }

        public void AddTavernExpandPurchase()
        {
            mTavernExpandPurchaseCount++;
            Touch();
        }

        public void SetTavernUpgradePurchaseCount(int count)
        {
            var next = count < 0 ? 0 : count;
            if (mTavernUpgradePurchaseCount == next)
            {
                return;
            }

            mTavernUpgradePurchaseCount = next;
            Touch();
        }

        public void SetTavernExpandPurchaseCount(int count)
        {
            var next = count < 0 ? 0 : count;
            if (mTavernExpandPurchaseCount == next)
            {
                return;
            }

            mTavernExpandPurchaseCount = next;
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

        public void Reset()
        {
            Stats.Clear();
            Coins.Value = 0;
            InteractionCount.Value = 0;
            ProfessionId.Value = string.Empty;
            mRelicDefIds.Clear();
            mConsumedRelicEffectIds.Clear();
            mItemDeckCapacity = DefaultItemDeckCapacity;
            mItemSlotsCapacity = DefaultItemSlotsCapacity;
            mItemSourcePoolDefIds.Clear();
            mFixedItemCardDefIds.Clear();
            mItemStatBonus = 0;
            mTavernUpgradePurchaseCount = 0;
            mTavernExpandPurchaseCount = 0;
            mDuelMarkMonsterUids.Clear();
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
