using System.Collections.Generic;

namespace NineGrid.Core.Stats
{
    public sealed class StatBlock
    {
        private readonly Dictionary<StatId, float> mBaseValues = new Dictionary<StatId, float>();
        private readonly List<StatModifier> mModifiers = new List<StatModifier>();

        public IReadOnlyDictionary<StatId, float> BaseValues
        {
            get { return mBaseValues; }
        }

        public IReadOnlyList<StatModifier> Modifiers
        {
            get { return mModifiers; }
        }

        public void SetBase(StatId stat, float value)
        {
            mBaseValues[stat] = value;
        }

        public float GetBase(StatId stat)
        {
            float value;
            return mBaseValues.TryGetValue(stat, out value) ? value : 0f;
        }

        public void AddModifier(StatModifier modifier)
        {
            mModifiers.Add(modifier);
        }

        public bool RemoveModifier(StatModifier modifier)
        {
            return mModifiers.Remove(modifier);
        }

        public int RemoveModifiersBySource(ModifierSource source)
        {
            return mModifiers.RemoveAll(modifier => modifier.Source == source);
        }

        public int ClearModifiersByScope(ModifierScope scope)
        {
            return mModifiers.RemoveAll(modifier => modifier.Scope == scope);
        }

        public void Clear()
        {
            mBaseValues.Clear();
            mModifiers.Clear();
        }
    }
}
