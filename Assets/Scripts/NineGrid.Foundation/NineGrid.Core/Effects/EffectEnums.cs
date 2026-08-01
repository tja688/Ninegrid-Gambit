using System;

namespace NineGrid.Core.Effects
{
    public enum EffectKind
    {
        Unknown,
        Triggered,
        Modifier,
        RuleModifier
    }

    public enum EffectContainerType
    {
        Unknown,
        Relic,
        MonsterSkill,
        HelpCard,
        Trap
    }

    public enum EffectAtomKind
    {
        Trigger,
        Condition,
        Target,
        Action
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class EffectAtomAttribute : Attribute
    {
        public EffectAtomAttribute(string id, EffectAtomKind kind)
        {
            Id = id ?? string.Empty;
            Kind = kind;
        }

        public string Id { get; private set; }
        public EffectAtomKind Kind { get; private set; }
    }
}
