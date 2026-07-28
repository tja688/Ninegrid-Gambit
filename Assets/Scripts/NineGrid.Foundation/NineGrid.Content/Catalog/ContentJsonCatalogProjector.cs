using System;
using NineGrid.Content.CardPresentation;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Effects;
using UnityEngine;

namespace NineGrid.Content
{
    /// <summary>
    /// 将一卡一文件 JSON（schema≥2）投影进 <see cref="GameContentCatalog"/>（覆盖同 DefId）。
    /// 支持 HelpCard / Monster / Skill / Relic / Deck / Room。
    /// 效果：优先解析 <c>effectAssemblies</c>（模板+实参）写入 Catalog.Effects；
    /// 奖励池 / 经济 / 节点规则由 <see cref="ContentCatalogTableLoader"/> 加载。
    /// </summary>
    public static class ContentJsonCatalogProjector
    {
        public const int MinSchemaVersion = 2;

        public static int ApplyToCatalog(GameContentCatalog catalog)
        {
            if (catalog == null)
            {
                return 0;
            }

            var applied = 0;
            foreach (var contentId in CardPresentationConfigCatalog.AllContentIds)
            {
                if (!CardPresentationConfigCatalog.TryGet(contentId, out var dto) || dto == null)
                {
                    continue;
                }

                if (TryProjectCard(dto, catalog, out var card))
                {
                    catalog.AddCard(card);
                    applied++;
                    continue;
                }

                if (TryProjectSkill(dto, catalog, out var skill))
                {
                    catalog.AddSkill(skill);
                    applied++;
                    continue;
                }

                if (TryProjectRelic(dto, catalog, out var relic))
                {
                    catalog.AddRelic(relic);
                    applied++;
                    continue;
                }

                if (TryProjectDeck(dto, out var deck))
                {
                    catalog.AddMonsterDeck(deck);
                    applied++;
                    continue;
                }

                if (TryProjectRoom(dto, out var room))
                {
                    catalog.Rewards.AddRoom(room);
                    applied++;
                }
            }

            return applied;
        }

        public static bool TryProjectCard(CardPresentationConfigDto dto, out CardContentDefinition card)
        {
            return TryProjectCard(dto, null, out card);
        }

        public static bool TryProjectCard(
            CardPresentationConfigDto dto,
            GameContentCatalog catalog,
            out CardContentDefinition card)
        {
            card = null;
            if (!IsReady(dto))
            {
                return false;
            }

            if (!TryParseCardKind(dto.kind, out var kind))
            {
                return false;
            }

            var displayName = ResolveDisplayName(dto);
            card = new CardContentDefinition(dto.contentId.Trim(), displayName, kind)
                .WithRarity(ParseRarity(dto.rarity));

            if (kind == CardKind.Monster)
            {
                card.KillGold = Math.Max(0, dto.gold);
            }
            else
            {
                card.WithPrice(Math.Max(0, dto.gold));
            }

            if (!string.IsNullOrWhiteSpace(dto.deckId))
            {
                card.InDeck(dto.deckId.Trim());
            }

            card.WithRole(ParseRole(dto.role));

            if (dto.level > 0)
            {
                card.WithLevel(dto.level);
            }

            if (dto.isElite)
            {
                card.AsElite();
            }

            if (dto.isBoss)
            {
                card.AsBoss();
            }

            if (dto.isReserve)
            {
                card.AsReserve();
            }

            var stats = dto.stats;
            if (stats != null)
            {
                card.WithStats(
                    Math.Max(0, stats.hp),
                    Math.Max(0, stats.attack),
                    Math.Max(0, stats.armor));
                card.Stats.Recovery = Math.Max(0, stats.recovery);
            }

            var projected = card;
            AddTokens(dto.tags, value => projected.AddTag(value));
            ApplyEffectMounts(dto, catalog, id => projected.AddEffect(id));
            AddTokens(dto.skillIds, value => projected.AddSkill(value));
            return true;
        }

        public static bool TryProjectSkill(CardPresentationConfigDto dto, out SkillContentDefinition skill)
        {
            return TryProjectSkill(dto, null, out skill);
        }

        public static bool TryProjectSkill(
            CardPresentationConfigDto dto,
            GameContentCatalog catalog,
            out SkillContentDefinition skill)
        {
            skill = null;
            if (!IsReady(dto) || !IsKind(dto.kind, "Skill"))
            {
                return false;
            }

            var container = ParseEnum(dto.containerType, EffectContainerType.MonsterSkill);
            skill = new SkillContentDefinition(
                dto.contentId.Trim(),
                ResolveDisplayName(dto),
                container,
                dto.description ?? string.Empty);

            var projected = skill;
            ApplyEffectMounts(dto, catalog, id => projected.AddEffect(id));
            return true;
        }

        public static bool TryProjectRelic(CardPresentationConfigDto dto, out RelicContentDefinition relic)
        {
            return TryProjectRelic(dto, null, out relic);
        }

        public static bool TryProjectRelic(
            CardPresentationConfigDto dto,
            GameContentCatalog catalog,
            out RelicContentDefinition relic)
        {
            relic = null;
            if (!IsReady(dto) || !IsKind(dto.kind, "Relic"))
            {
                return false;
            }

            relic = new RelicContentDefinition(
                dto.contentId.Trim(),
                ResolveDisplayName(dto),
                ParseRarity(dto.rarity),
                dto.description ?? string.Empty);

            if (!string.IsNullOrWhiteSpace(dto.deckId))
            {
                relic.InDeck(dto.deckId.Trim());
            }

            relic.WithRole(ParseRole(dto.role));

            var projected = relic;
            AddTokens(dto.tags, value => projected.AddTag(value));
            ApplyEffectMounts(dto, catalog, id => projected.AddEffect(id));
            return true;
        }

        public static bool TryProjectDeck(CardPresentationConfigDto dto, out MonsterDeckDefinition deck)
        {
            deck = null;
            if (!IsReady(dto) || !IsKind(dto.kind, "Deck"))
            {
                return false;
            }

            // 纯表现卡组（帮助卡/遗物卡背归属）不进入遭遇编排 MonsterDecks。
            if (IsKind(dto.deckKind, "Presentation")
                || string.IsNullOrWhiteSpace(dto.deckKind))
            {
                return false;
            }

            deck = new MonsterDeckDefinition(
                dto.contentId.Trim(),
                ResolveDisplayName(dto),
                ParseEnum(dto.deckKind, MonsterDeckKind.Unknown));

            var projected = deck;
            AddTokens(dto.monsterDefIds, value => projected.AddMonster(value));
            return true;
        }

        public static bool TryProjectRoom(CardPresentationConfigDto dto, out RoomDefinition room)
        {
            room = null;
            if (!IsReady(dto) || !IsKind(dto.kind, "Room"))
            {
                return false;
            }

            if (!Enum.TryParse(dto.contentId.Trim(), ignoreCase: true, out RoomKind roomKind)
                || roomKind == RoomKind.None)
            {
                return false;
            }

            room = new RoomDefinition(roomKind, ResolveDisplayName(dto))
            {
                Weight = Math.Max(0, dto.weight),
                GoldDelta = dto.goldDelta,
                MaxHpDelta = dto.maxHpDelta,
                HealToFull = dto.healToFull,
                RewardPoolId = dto.rewardPoolId ?? string.Empty,
                ShopOfferCount = Math.Max(0, dto.shopOfferCount),
            };
            return true;
        }

        private static bool IsReady(CardPresentationConfigDto dto)
        {
            return dto != null
                && !string.IsNullOrWhiteSpace(dto.contentId)
                && dto.schemaVersion >= MinSchemaVersion;
        }

        private static bool TryParseCardKind(string kind, out CardKind cardKind)
        {
            cardKind = CardKind.Unknown;
            if (string.IsNullOrWhiteSpace(kind))
            {
                return false;
            }

            if (IsKind(kind, nameof(CardKind.HelpCard)) || IsKind(kind, "Help"))
            {
                cardKind = CardKind.HelpCard;
                return true;
            }

            if (IsKind(kind, nameof(CardKind.Monster)))
            {
                cardKind = CardKind.Monster;
                return true;
            }

            // Item 等若出现 schema≥2 亦可投影；当前生产表仅 Help/Monster。
            // Relic 走 RelicContentDefinition，不得落入 Cards。
            if (Enum.TryParse(kind.Trim(), ignoreCase: true, out CardKind parsed)
                && parsed != CardKind.Unknown
                && parsed != CardKind.Relic)
            {
                cardKind = parsed;
                return true;
            }

            return false;
        }

        private static bool IsKind(string kind, string expected)
        {
            return string.Equals(kind, expected, StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveDisplayName(CardPresentationConfigDto dto)
        {
            return string.IsNullOrWhiteSpace(dto.displayName)
                ? dto.contentId.Trim()
                : dto.displayName.Trim();
        }

        private static ContentRarity ParseRarity(string raw)
        {
            return ParseEnum(raw, ContentRarity.None);
        }

        private static ContentRole ParseRole(string raw)
        {
            return ParseEnum(raw, ContentRole.None);
        }

        private static T ParseEnum<T>(string raw, T fallback) where T : struct
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return fallback;
            }

            return Enum.TryParse(raw.Trim(), ignoreCase: true, out T value)
                ? value
                : fallback;
        }

        private static void ApplyEffectMounts(
            CardPresentationConfigDto dto,
            GameContentCatalog catalog,
            Action<string> addEffectId)
        {
            if (dto == null || addEffectId == null)
            {
                return;
            }

            if (dto.effectAssemblies != null && dto.effectAssemblies.Length > 0)
            {
                for (var i = 0; i < dto.effectAssemblies.Length; i++)
                {
                    var assembly = dto.effectAssemblies[i];
                    if (assembly == null || string.IsNullOrWhiteSpace(assembly.id))
                    {
                        continue;
                    }

                    var mountId = assembly.id.Trim();
                    if (catalog != null)
                    {
                        TryResolveAssemblyIntoCatalog(assembly, catalog);
                    }

                    addEffectId(mountId);
                }

                return;
            }

            AddTokens(dto.effectIds, addEffectId);
        }

        private static void TryResolveAssemblyIntoCatalog(EffectAssemblyDto assembly, GameContentCatalog catalog)
        {
            if (assembly == null || catalog == null || string.IsNullOrWhiteSpace(assembly.templateId))
            {
                return;
            }

            EffectTemplateDefinition template;
            if (!EffectTemplateCatalog.TryGet(assembly.templateId.Trim(), out template) || template == null)
            {
                UnityEngine.Debug.LogWarning(
                    "[ContentJsonCatalogProjector] Missing effect template: " + assembly.templateId);
                return;
            }

            var container = ParseEnum(assembly.containerType, EffectContainerType.Unknown);
            if (container == EffectContainerType.Unknown)
            {
                UnityEngine.Debug.LogWarning(
                    "[ContentJsonCatalogProjector] Missing containerType on assembly: " + assembly.id);
                return;
            }

            try
            {
                var args = EffectAssemblyResolver.ParseArgsJson(assembly.argsJson);
                var resolved = EffectAssemblyResolver.Resolve(template, assembly.id.Trim(), container, args);
                catalog.AddEffect(resolved);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning(
                    "[ContentJsonCatalogProjector] Failed resolving " + assembly.id + ": " + ex.Message);
            }
        }

        private static void AddTokens(string[] values, Action<string> add)
        {
            if (values == null || add == null)
            {
                return;
            }

            for (var i = 0; i < values.Length; i++)
            {
                var value = values[i];
                if (!string.IsNullOrWhiteSpace(value))
                {
                    add(value.Trim());
                }
            }
        }
    }
}
