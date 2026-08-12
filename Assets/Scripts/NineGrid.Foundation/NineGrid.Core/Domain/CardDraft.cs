using System.Collections.Generic;

namespace NineGrid.Core
{
    public sealed class CardDraft
    {
        private readonly List<string> mEffectIds = new List<string>();

        public CardDraft(string defId, CardKind kind)
        {
            DefId = defId ?? string.Empty;
            Kind = kind;
        }

        public string DefId { get; private set; }
        public CardKind Kind { get; private set; }
        public int MaxHp { get; set; }
        public int Hp { get; set; }
        public int Attack { get; set; }
        public int Armor { get; set; }
        public int Recovery { get; set; }
        public int GoldReward { get; set; }
        public bool IsElite { get; set; }
        public bool IsBoss { get; set; }
        public int Level { get; set; }
        /// <summary>节奏周期 X；&gt;0 且有节奏需求时进场写入共享倒计时（ADR-0038）。</summary>
        public int ActionFrequency { get; set; }
        /// <summary>攻击模式（ADR-0011）；写入 <see cref="CardInstance.AttackPattern"/>。</summary>
        public AttackPattern AttackPattern { get; set; }
        /// <summary>卡级节奏源（ADR-0038）。</summary>
        public CardRhythmSource RhythmSource { get; set; }
        /// <summary>是否挂有技能同步触发。</summary>
        public bool HasSyncRhythmSkills { get; set; }

        public IReadOnlyList<string> EffectIds
        {
            get { return mEffectIds; }
        }

        public CardDraft AddEffect(string effectId)
        {
            if (!string.IsNullOrEmpty(effectId) && !mEffectIds.Contains(effectId))
            {
                mEffectIds.Add(effectId);
            }

            return this;
        }

        public CardInstance Create(CardRegistry registry)
        {
            var card = registry.Create(DefId, Kind);
            if (MaxHp > 0)
            {
                card.Stats.SetBase(StatId.MaxHp, MaxHp);
                card.Stats.SetBase(StatId.Hp, Hp > 0 ? Hp : MaxHp);
            }

            if (Attack != 0)
            {
                card.Stats.SetBase(StatId.Attack, Attack);
            }

            if (Armor != 0)
            {
                card.Stats.SetBase(StatId.Armor, Armor);
                card.Stats.SetBase(StatId.CurrentArmor, Armor);
            }

            if (Recovery != 0)
            {
                card.Stats.SetBase(StatId.Recovery, Recovery);
            }

            if (GoldReward != 0)
            {
                card.Counters.Set(CoreCounterKeys.GoldReward, GoldReward);
            }

            if (IsElite)
            {
                card.Counters.Set(CoreCounterKeys.Elite, 1);
            }

            if (IsBoss)
            {
                card.Counters.Set(CoreCounterKeys.Elite, 1);
                card.Counters.Set(CoreCounterKeys.Boss, 1);
            }

            if (Level > 0)
            {
                card.Counters.Set(CoreCounterKeys.Level, Level);
            }

            card.AttackPattern = AttackPattern;
            card.RhythmSource = RhythmSource;
            card.RhythmPeriod = ActionFrequency > 0 ? ActionFrequency : 0;
            card.HasSyncRhythmSkills = HasSyncRhythmSkills;
            if (CardRhythmRules.NeedsRhythm(card)
                && CardRhythmRules.HasBoundSource(card.RhythmSource)
                && card.RhythmPeriod > 0)
            {
                card.Counters.Set(CoreCounterKeys.AttackPatternCountdown, card.RhythmPeriod);
            }

            for (var i = 0; i < mEffectIds.Count; i++)
            {
                card.AddEffect(mEffectIds[i]);
            }

            return card;
        }
    }

    public sealed class NodeDeckOptions
    {
        private readonly List<CardDraft> mPlayerCards = new List<CardDraft>();
        private readonly List<CardDraft> mEnemyCards = new List<CardDraft>();

        public NodeDeckOptions()
        {
            PlayerOpeningCount = 3;
            EnemyOpeningCount = 3;
            RequireElite = false;
        }

        public int PlayerOpeningCount { get; set; }
        public int EnemyOpeningCount { get; set; }
        public bool RequireElite { get; set; }

        /// <summary>
        /// 受控发牌（教学关卡专用）：OpeningDeal 跳过洗牌与离开机关落点重排，
        /// 抽牌堆保持装填顺序，配合 FillOrder 实现确定性铺场。正式流程保持 false。
        /// </summary>
        public bool PreserveDealOrder { get; set; }

        public IReadOnlyList<CardDraft> PlayerCards
        {
            get { return mPlayerCards; }
        }

        public IReadOnlyList<CardDraft> EnemyCards
        {
            get { return mEnemyCards; }
        }

        public NodeDeckOptions AddPlayerCard(CardDraft draft)
        {
            if (draft != null)
            {
                mPlayerCards.Add(draft);
            }

            return this;
        }

        public NodeDeckOptions AddEnemyCard(CardDraft draft)
        {
            if (draft != null)
            {
                mEnemyCards.Add(draft);
            }

            return this;
        }

        public static NodeDeckOptions CreateDefaultBattle()
        {
            return new NodeDeckOptions()
                .AddPlayerCard(new CardDraft("player.strike", CardKind.PlayerCard))
                .AddPlayerCard(new CardDraft("player.guard", CardKind.PlayerCard))
                .AddPlayerCard(new CardDraft("player.coin", CardKind.Item) { GoldReward = 1 })
                .AddEnemyCard(new CardDraft("monster.slime", CardKind.Monster) { MaxHp = 3, Attack = 1, GoldReward = 1 })
                .AddEnemyCard(new CardDraft("monster.bat", CardKind.Monster) { MaxHp = 2, Attack = 1, GoldReward = 1 })
                .AddEnemyCard(new CardDraft("monster.guard", CardKind.Monster) { MaxHp = 4, Attack = 1, Armor = 1, GoldReward = 2 });
        }

    }
}
