using System;
using System.Collections.Generic;
using NineGrid.Core.Content;
using NineGrid.Core.Effects;
using NineGrid.Core.Stats;
using NineGrid.Core.Utilities;
using QFramework;

namespace NineGrid.Core.Systems
{
    public interface IContentSystem : ISystem
    {
        GameContentCatalog Catalog { get; }
        bool HasCatalog { get; }
        void Load(GameContentCatalog catalog);
        bool TryReloadFromConfig();
        ContentValidationReport ValidateCatalog();
        CardDraft CreateDraft(string defId);
        void ApplyContentToCard(CardInstance card);
        IReadOnlyList<EffectInstance> ActivateCardEffects(CardInstance card);
        IReadOnlyList<EffectInstance> ActivateRelic(string relicDefId);
        IReadOnlyList<string> DeactivateRuntimeEffectsByOwner(int ownerUid);
        void ClearRuntimeEffects();
    }

    public sealed class ContentValidationReport
    {
        private readonly List<string> mIssues = new List<string>();
        private readonly List<string> mImplementedEffectIds = new List<string>();
        private readonly List<string> mPendingEffectIds = new List<string>();

        public IReadOnlyList<string> Issues
        {
            get { return mIssues; }
        }

        public IReadOnlyList<string> ImplementedEffectIds
        {
            get { return mImplementedEffectIds; }
        }

        public IReadOnlyList<string> PendingEffectIds
        {
            get { return mPendingEffectIds; }
        }

        public bool IsValid
        {
            get { return mIssues.Count == 0; }
        }

        internal void AddIssue(string issue)
        {
            if (!string.IsNullOrEmpty(issue))
            {
                mIssues.Add(issue);
            }
        }

        internal void AddImplemented(string effectId)
        {
            if (!string.IsNullOrEmpty(effectId))
            {
                mImplementedEffectIds.Add(effectId);
            }
        }

        internal void AddPending(string effectId)
        {
            if (!string.IsNullOrEmpty(effectId))
            {
                mPendingEffectIds.Add(effectId);
            }
        }
    }

    public sealed class ContentSystem : AbstractSystem, IContentSystem
    {
        private readonly List<string> mRuntimeEffectInstanceIds = new List<string>();

        public GameContentCatalog Catalog { get; private set; }

        public bool HasCatalog
        {
            get { return Catalog != null; }
        }

        protected override void OnInit()
        {
            TryReloadFromConfig();
        }

        public void Load(GameContentCatalog catalog)
        {
            Catalog = catalog;
        }

        public bool TryReloadFromConfig()
        {
            GameContentCatalog catalog;
            if (this.GetUtility<IConfigUtility>().TryGet(ContentConfigKeys.DefaultCatalog, out catalog))
            {
                Load(catalog);
                return true;
            }

            return false;
        }

        public ContentValidationReport ValidateCatalog()
        {
            TryReloadFromConfig();

            var report = new ContentValidationReport();
            if (Catalog == null)
            {
                report.AddIssue("content.catalog.missing");
                return report;
            }

            ValidateEffectDefinitions(report);
            ValidateReferences(report);
            ValidateMonsterAttackPatterns(report);
            return report;
        }

        public CardDraft CreateDraft(string defId)
        {
            TryReloadFromConfig();

            CardContentDefinition definition;
            if (Catalog == null || !Catalog.TryGetCard(defId, out definition))
            {
                return new CardDraft(defId, CardKind.Unknown);
            }

            var draft = new CardDraft(definition.DefId, definition.Kind)
            {
                MaxHp = definition.Stats.MaxHp,
                Hp = definition.Stats.Hp,
                Attack = definition.Stats.Attack,
                Armor = definition.Stats.Armor,
                Recovery = definition.Stats.Recovery,
                IsElite = definition.IsElite || definition.IsBoss,
                Level = definition.Level,
                IsBoss = definition.IsBoss,
                ActionFrequency = definition.Stats.Action
            };

            for (var i = 0; i < definition.EffectIds.Count; i++)
            {
                draft.AddEffect(definition.EffectIds[i]);
            }

            for (var i = 0; i < definition.SkillIds.Count; i++)
            {
                SkillContentDefinition skill;
                if (!Catalog.TryGetSkill(definition.SkillIds[i], out skill))
                {
                    continue;
                }

                for (var j = 0; j < skill.EffectIds.Count; j++)
                {
                    draft.AddEffect(skill.EffectIds[j]);
                }
            }

            return draft;
        }

        public void ApplyContentToCard(CardInstance card)
        {
            if (card == null)
            {
                return;
            }

            TryReloadFromConfig();
            if (Catalog == null)
            {
                return;
            }

            CardContentDefinition definition;
            if (!Catalog.TryGetCard(card.DefId, out definition))
            {
                return;
            }

            ActivateCardEffects(card);
        }

        public IReadOnlyList<EffectInstance> ActivateCardEffects(CardInstance card)
        {
            var result = new List<EffectInstance>();
            if (card == null)
            {
                return result;
            }

            TryReloadFromConfig();
            if (Catalog == null)
            {
                return result;
            }

            CardContentDefinition definition;
            if (!Catalog.TryGetCard(card.DefId, out definition))
            {
                return result;
            }

            ActivateEffectIds(definition.EffectIds, ToContainerType(card.Kind), definition.DefId, card.Uid, result);
            for (var i = 0; i < definition.SkillIds.Count; i++)
            {
                SkillContentDefinition skill;
                if (!Catalog.TryGetSkill(definition.SkillIds[i], out skill))
                {
                    continue;
                }

                ActivateEffectIds(skill.EffectIds, skill.ContainerType, skill.DefId, card.Uid, result);
            }

            return result;
        }

        public IReadOnlyList<EffectInstance> ActivateRelic(string relicDefId)
        {
            var result = new List<EffectInstance>();
            TryReloadFromConfig();
            if (Catalog == null)
            {
                return result;
            }

            RelicContentDefinition relic;
            if (!Catalog.TryGetRelic(relicDefId, out relic))
            {
                return result;
            }

            ActivateEffectIds(relic.EffectIds, EffectContainerType.Relic, relic.DefId, 0, result);
            return result;
        }

        public void ClearRuntimeEffects()
        {
            var effectSystem = this.GetSystem<IEffectSystem>();
            for (var i = 0; i < mRuntimeEffectInstanceIds.Count; i++)
            {
                effectSystem.Deactivate(mRuntimeEffectInstanceIds[i]);
            }

            mRuntimeEffectInstanceIds.Clear();
        }

        public IReadOnlyList<string> DeactivateRuntimeEffectsByOwner(int ownerUid)
        {
            var effectSystem = this.GetSystem<IEffectSystem>();
            var ids = effectSystem.GetInstanceIdsByOwner(ownerUid);
            var deactivated = new List<string>();
            for (var i = 0; i < ids.Count; i++)
            {
                if (effectSystem.Deactivate(ids[i]))
                {
                    deactivated.Add(ids[i]);
                }
            }

            PruneRuntimeEffectInstanceIds(effectSystem);
            return deactivated;
        }

        private void ValidateEffectDefinitions(ContentValidationReport report)
        {
            var effectSystem = this.GetSystem<IEffectSystem>();
            foreach (var pair in Catalog.Effects)
            {
                var effect = pair.Value;
                if (effect.State != ContentImplementationState.Implemented)
                {
                    report.AddPending(effect.Id);
                    continue;
                }

                report.AddImplemented(effect.Id);
                try
                {
                    var definition = effectSystem.ParseJson(effect.Json);
                    var validation = effectSystem.Validate(definition);
                    if (!validation.IsValid)
                    {
                        for (var i = 0; i < validation.Issues.Count; i++)
                        {
                            report.AddIssue(effect.Id + ":" + validation.Issues[i]);
                        }
                    }
                }
                catch (Exception ex)
                {
                    report.AddIssue(effect.Id + ":parse:" + ex.Message);
                }
            }
        }

        private void ValidateReferences(ContentValidationReport report)
        {
            foreach (var pair in Catalog.Cards)
            {
                ValidateEffectRefs("card:" + pair.Key, pair.Value.EffectIds, report);
                for (var i = 0; i < pair.Value.SkillIds.Count; i++)
                {
                    if (!Catalog.Skills.ContainsKey(pair.Value.SkillIds[i]))
                    {
                        report.AddIssue("card:" + pair.Key + ":missing skill " + pair.Value.SkillIds[i]);
                    }
                }
            }

            foreach (var pair in Catalog.Skills)
            {
                ValidateEffectRefs("skill:" + pair.Key, pair.Value.EffectIds, report);
            }

            foreach (var pair in Catalog.Relics)
            {
                ValidateEffectRefs("relic:" + pair.Key, pair.Value.EffectIds, report);
            }
        }

        private void ValidateMonsterAttackPatterns(ContentValidationReport report)
        {
            foreach (var pair in Catalog.Cards)
            {
                var card = pair.Value;
                if (card.Kind != CardKind.Monster)
                {
                    continue;
                }

                if (card.AttackPattern == AttackPattern.Unspecified)
                {
                    report.AddIssue("card:" + pair.Key + ":missing or invalid attackPattern");
                }
            }
        }

        private void ValidateEffectRefs(string owner, IReadOnlyList<string> effectIds, ContentValidationReport report)
        {
            for (var i = 0; i < effectIds.Count; i++)
            {
                if (!Catalog.Effects.ContainsKey(effectIds[i]))
                {
                    report.AddIssue(owner + ":missing effect " + effectIds[i]);
                }
            }
        }

        private void ActivateEffectIds(
            IReadOnlyList<string> effectIds,
            EffectContainerType containerType,
            string sourceDefId,
            int ownerUid,
            List<EffectInstance> result)
        {
            if (effectIds == null || effectIds.Count == 0)
            {
                return;
            }

            var effectSystem = this.GetSystem<IEffectSystem>();
            for (var i = 0; i < effectIds.Count; i++)
            {
                ContentEffectDefinition contentEffect;
                if (!Catalog.TryGetEffect(effectIds[i], out contentEffect)
                    || contentEffect.State != ContentImplementationState.Implemented)
                {
                    continue;
                }

                var definition = effectSystem.ParseJson(contentEffect.Json);
                var instance = effectSystem.Activate(definition, new EffectOwner(containerType, sourceDefId, ownerUid));
                mRuntimeEffectInstanceIds.Add(instance.InstanceId);
                result.Add(instance);
            }
        }

        private void PruneRuntimeEffectInstanceIds(IEffectSystem effectSystem)
        {
            for (var i = mRuntimeEffectInstanceIds.Count - 1; i >= 0; i--)
            {
                EffectInstance instance;
                if (!effectSystem.TryGetInstance(mRuntimeEffectInstanceIds[i], out instance))
                {
                    mRuntimeEffectInstanceIds.RemoveAt(i);
                }
            }
        }

        private static EffectContainerType ToContainerType(CardKind kind)
        {
            switch (kind)
            {
                case CardKind.HelpCard:
                case CardKind.Item:
                    return EffectContainerType.HelpCard;
                case CardKind.Monster:
                    return EffectContainerType.MonsterSkill;
                case CardKind.Relic:
                    return EffectContainerType.Relic;
                default:
                    return EffectContainerType.Unknown;
            }
        }
    }
}
