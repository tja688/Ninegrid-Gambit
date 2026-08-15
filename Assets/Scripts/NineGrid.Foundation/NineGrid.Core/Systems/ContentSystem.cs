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
        IReadOnlyList<EffectInstance> ActivateSkillsOnCard(CardInstance card, IReadOnlyList<string> skillIds);
        IReadOnlyList<EffectInstance> ActivateRelic(string relicDefId);
        /// <summary>装配级激活：只挂 <paramref name="effectIdFilter"/> 放行的效果（自愈补挂缺失装配用）。</summary>
        IReadOnlyList<EffectInstance> ActivateRelic(string relicDefId, Func<string, bool> effectIdFilter);
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
            ValidateMonsterRhythm(report);
            ValidateRoomOpeningInjects(report);
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
                ActionFrequency = definition.RhythmPeriod > 0
                    ? definition.RhythmPeriod
                    : definition.Stats.Action,
                AttackPattern = definition.AttackPattern,
                RhythmSource = definition.RhythmSource,
                HasSyncRhythmSkills = definition.HasSyncRhythmSkills
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

            if (definition.Kind == CardKind.Monster)
            {
                var run = this.GetModel<RunModel>();
                var floor = run != null && run.Floor != null ? run.Floor.Value : 1;
                var nodeIndex = run != null && run.NodeIndex != null ? run.NodeIndex.Value : 0;
                var difficultyId = run != null && run.DifficultyId != null
                    ? run.DifficultyId.Value
                    : RunDifficultyIds.Normal;
                var isHard = DungeonEnvironmentCatalog.IsHardDifficulty(difficultyId);
                MonsterFloorStatScaling.ApplyToDraft(draft, floor, nodeIndex, isHard);
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

            // ADR-0017：Trap 静默挂永久 CounterAttackBanned（不进检视技能列表）。
            if (card.Kind == CardKind.Trap)
            {
                this.GetSystem<IStatSystem>().RuleModifiers.Add(new RuleModifier(
                    RuleId.CounterAttackBanned,
                    ModifierOp.Add,
                    1f,
                    ModifierLayer.Persistent,
                    new ModifierSource("intrinsic.trap:" + card.DefId),
                    ModifierScope.Permanent,
                    new TargetUidCondition(card.Uid)));
            }

            return result;
        }

        public IReadOnlyList<EffectInstance> ActivateSkillsOnCard(
            CardInstance card,
            IReadOnlyList<string> skillIds)
        {
            var result = new List<EffectInstance>();
            if (card == null || skillIds == null || skillIds.Count == 0)
            {
                return result;
            }

            TryReloadFromConfig();
            if (Catalog == null)
            {
                return result;
            }

            for (var i = 0; i < skillIds.Count; i++)
            {
                SkillContentDefinition skill;
                if (!Catalog.TryGetSkill(skillIds[i], out skill))
                {
                    continue;
                }

                ActivateEffectIds(skill.EffectIds, skill.ContainerType, skill.DefId, card.Uid, result);
            }

            return result;
        }

        public IReadOnlyList<EffectInstance> ActivateRelic(string relicDefId)
        {
            return ActivateRelic(relicDefId, null);
        }

        public IReadOnlyList<EffectInstance> ActivateRelic(string relicDefId, Func<string, bool> effectIdFilter)
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

            ActivateEffectIds(relic.EffectIds, EffectContainerType.Relic, relic.DefId, 0, result, effectIdFilter);
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

        private void ValidateMonsterRhythm(ContentValidationReport report)
        {
            foreach (var pair in Catalog.Cards)
            {
                var card = pair.Value;
                if (card.Kind != CardKind.Monster)
                {
                    continue;
                }

                var needs = CardRhythmRules.NeedsRhythm(card.AttackPattern, card.HasSyncRhythmSkills);
                if (!needs)
                {
                    continue;
                }

                if (!CardRhythmRules.HasBoundSource(card.RhythmSource))
                {
                    report.AddIssue("card:" + pair.Key + ":missing or invalid rhythmSource");
                }

                if (card.RhythmPeriod <= 0)
                {
                    report.AddIssue("card:" + pair.Key + ":missing or invalid rhythmPeriod");
                }
            }

            foreach (var pair in Catalog.Effects)
            {
                var json = pair.Value != null ? pair.Value.Json : null;
                if (string.IsNullOrEmpty(json))
                {
                    continue;
                }

                if (json.IndexOf("\"OnCardRhythmFire\"", System.StringComparison.Ordinal) < 0
                    && json.IndexOf("OnCardRhythmFire", System.StringComparison.Ordinal) < 0)
                {
                    continue;
                }

                // 同步技能禁止自带节奏 every（ADR-0038）。
                if (System.Text.RegularExpressions.Regex.IsMatch(
                        json,
                        "\"atom\"\\s*:\\s*\"OnCardRhythmFire\"[\\s\\S]{0,200}?\"every\"\\s*:"))
                {
                    report.AddIssue("effect:" + pair.Key + ":OnCardRhythmFire must not declare every");
                }
            }
        }

        private void ValidateRoomOpeningInjects(ContentValidationReport report)
        {
            foreach (var pair in Catalog.Rewards.Rooms)
            {
                var room = pair.Value;
                var injects = room.OpeningInjects;
                for (var i = 0; i < injects.Count; i++)
                {
                    ValidateOneRoomInject(room.Kind, i, injects[i], report);
                }
            }
        }

        private void ValidateOneRoomInject(
            RoomKind roomKind,
            int index,
            RoomInjectDeclaration inject,
            ContentValidationReport report)
        {
            var prefix = "room:" + roomKind + ":inject[" + index + "]";
            if (inject == null)
            {
                report.AddIssue(prefix + ":null");
                return;
            }

            if (inject.Side != RoomInjectSide.Player && inject.Side != RoomInjectSide.Monster)
            {
                report.AddIssue(prefix + ":invalid side");
            }

            if (inject.Count <= 0)
            {
                report.AddIssue(prefix + ":count must be > 0");
            }

            switch (inject.SourceKind)
            {
                case RoomInjectSourceKind.FixedCard:
                    if (string.IsNullOrWhiteSpace(inject.CardDefId))
                    {
                        report.AddIssue(prefix + ":fixed card missing cardDefId");
                        break;
                    }

                    if (!Catalog.Cards.ContainsKey(inject.CardDefId))
                    {
                        report.AddIssue(prefix + ":missing card " + inject.CardDefId);
                    }

                    break;

                case RoomInjectSourceKind.WeightedPool:
                    if (inject.Pool == null || inject.Pool.Count == 0)
                    {
                        report.AddIssue(prefix + ":weighted pool empty");
                        break;
                    }

                    var totalWeight = 0;
                    for (var p = 0; p < inject.Pool.Count; p++)
                    {
                        var option = inject.Pool[p];
                        if (option == null || string.IsNullOrWhiteSpace(option.CardDefId))
                        {
                            report.AddIssue(prefix + ":pool[" + p + "]:missing cardDefId");
                            continue;
                        }

                        if (option.Weight <= 0)
                        {
                            report.AddIssue(prefix + ":pool[" + p + "]:weight must be > 0");
                        }

                        totalWeight += option.Weight;
                        if (!Catalog.Cards.ContainsKey(option.CardDefId))
                        {
                            report.AddIssue(prefix + ":missing card " + option.CardDefId);
                        }
                    }

                    if (totalWeight <= 0)
                    {
                        report.AddIssue(prefix + ":weighted pool total weight must be > 0");
                    }

                    break;

                case RoomInjectSourceKind.FloorMonsterSequence:
                    if (inject.Side != RoomInjectSide.Monster)
                    {
                        report.AddIssue(prefix + ":floor monster sequence must target Monster side");
                    }

                    if (inject.MonsterSequence < 1 || inject.MonsterSequence > 5)
                    {
                        report.AddIssue(prefix + ":monsterSequence must be 1..5");
                    }

                    break;

                default:
                    report.AddIssue(prefix + ":invalid source");
                    break;
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
            List<EffectInstance> result,
            Func<string, bool> effectIdFilter = null)
        {
            if (effectIds == null || effectIds.Count == 0)
            {
                return;
            }

            // 一次性遗物效果（OnActivate + DeactivateSelfEffect，如黄金鱼竿给宝箱卡）
            // 消费后不再重挂：存档恢复 / 跨层重装 / StartNode 自愈统一走本口。
            var player = containerType == EffectContainerType.Relic
                ? this.GetModel<PlayerModel>()
                : null;

            var effectSystem = this.GetSystem<IEffectSystem>();
            for (var i = 0; i < effectIds.Count; i++)
            {
                if (effectIdFilter != null && !effectIdFilter(effectIds[i]))
                {
                    continue;
                }

                if (player != null && player.IsRelicEffectConsumed(effectIds[i]))
                {
                    continue;
                }

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
                case CardKind.Trap:
                    return EffectContainerType.Trap;
                case CardKind.Relic:
                    return EffectContainerType.Relic;
                default:
                    return EffectContainerType.Unknown;
            }
        }
    }
}
