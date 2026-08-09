using System.Collections.Generic;
using NineGrid.Core.Stats;
using QFramework;

namespace NineGrid.Core
{
    public sealed class CardInstance
    {
        private readonly List<string> mEffectIds = new List<string>();

        public CardInstance(int uid, string defId, CardKind kind)
        {
            Uid = uid;
            DefId = defId ?? string.Empty;
            Kind = kind;
            FaceUp = true;
            Zone = new BindableProperty<ZoneId>(ZoneId.None);
            Slot = new BindableProperty<SlotId>(SlotId.None);
            Stats = new StatBlock();
            Counters = new CounterBag();
        }

        public int Uid { get; private set; }
        public string DefId { get; private set; }
        public CardKind Kind { get; private set; }
        /// <summary>牌面朝向权威（正面/背面）；默认正面。ADR 牌面朝向。</summary>
        public bool FaceUp { get; set; }
        /// <summary>怪物攻击模式（ADR-0011）；非怪保持 <see cref="AttackPattern.Unspecified"/>。</summary>
        public AttackPattern AttackPattern { get; set; }
        /// <summary>卡级节奏源（ADR-0038）；无节奏需求时可为 <see cref="CardRhythmSource.Unspecified"/>。</summary>
        public CardRhythmSource RhythmSource { get; set; }
        /// <summary>节奏周期 X（ADR-0038）；进场写入共享倒计时。</summary>
        public int RhythmPeriod { get; set; }
        /// <summary>是否挂有技能同步触发（订阅卡级开火窗口）。</summary>
        public bool HasSyncRhythmSkills { get; set; }
        public StatBlock Stats { get; private set; }
        public CounterBag Counters { get; private set; }
        public BindableProperty<ZoneId> Zone { get; private set; }
        public BindableProperty<SlotId> Slot { get; private set; }

        public IReadOnlyList<string> EffectIds
        {
            get { return mEffectIds; }
        }

        public void AddEffect(string effectId)
        {
            if (!string.IsNullOrEmpty(effectId) && !mEffectIds.Contains(effectId))
            {
                mEffectIds.Add(effectId);
            }
        }

        public void RemoveEffect(string effectId)
        {
            mEffectIds.Remove(effectId);
        }
    }

    public sealed class CounterBag
    {
        private readonly Dictionary<string, int> mCounters = new Dictionary<string, int>();

        public IReadOnlyDictionary<string, int> Values
        {
            get { return mCounters; }
        }

        public int Get(string key)
        {
            int value;
            return mCounters.TryGetValue(key ?? string.Empty, out value) ? value : 0;
        }

        public void Set(string key, int value)
        {
            mCounters[key ?? string.Empty] = value;
        }

        public void Add(string key, int delta)
        {
            Set(key, Get(key) + delta);
        }

        public bool Remove(string key)
        {
            return mCounters.Remove(key ?? string.Empty);
        }

        public void Clear()
        {
            mCounters.Clear();
        }
    }
}
