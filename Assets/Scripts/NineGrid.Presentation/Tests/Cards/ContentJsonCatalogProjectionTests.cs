using NUnit.Framework;
using NineGrid.Content;
using NineGrid.Content.CardPresentation;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Effects;

namespace NineGrid.Presentation.Tests.Cards
{
    /// <summary>
    /// #68/#69：遗物 / 怪物 / 技能（及牌组、房间）JSON → GameContentCatalog 投影；无 BusinessOverlay。
    /// </summary>
    public sealed class ContentJsonCatalogProjectionTests
    {
        [SetUp]
        public void SetUp()
        {
            CardPresentationConfigCatalog.Invalidate();
        }

        [TearDown]
        public void TearDown()
        {
            CardPresentationConfigCatalog.Invalidate();
        }

        [Test]
        public void TryProjectRelic_MapsIdentityMounts_FromSchema2Json()
        {
            var dto = new CardPresentationConfigDto
            {
                schemaVersion = 2,
                contentId = "relic.proj_demo",
                kind = "Relic",
                displayName = "投影遗物",
                description = "设计文案",
                rarity = "Gold",
                tags = new[] { "经济" },
                effectIds = new[] { "relic.proj_demo.use" },
            };

            Assert.IsTrue(ContentJsonCatalogProjector.TryProjectRelic(dto, out var relic));
            Assert.AreEqual("relic.proj_demo", relic.DefId);
            Assert.AreEqual("投影遗物", relic.DisplayName);
            Assert.AreEqual(ContentRarity.Gold, relic.Rarity);
            Assert.AreEqual("设计文案", relic.DesignText);
            Assert.AreEqual(1, relic.Tags.Count);
            Assert.AreEqual("经济", relic.Tags[0]);
            Assert.AreEqual(1, relic.EffectIds.Count);
            Assert.AreEqual("relic.proj_demo.use", relic.EffectIds[0]);
        }

        [Test]
        public void TryProjectSkill_MapsMonsterSkillMounts()
        {
            var dto = new CardPresentationConfigDto
            {
                schemaVersion = 2,
                contentId = "skill.proj_demo",
                kind = "Skill",
                displayName = "投影技能",
                description = "怪技说明",
                containerType = "MonsterSkill",
                effectIds = new[] { "skill.proj_demo.move" },
            };

            Assert.IsTrue(ContentJsonCatalogProjector.TryProjectSkill(dto, out var skill));
            Assert.AreEqual("skill.proj_demo", skill.DefId);
            Assert.AreEqual(EffectContainerType.MonsterSkill, skill.ContainerType);
            Assert.AreEqual(1, skill.EffectIds.Count);
            Assert.AreEqual("skill.proj_demo.move", skill.EffectIds[0]);
        }

        [Test]
        public void TryProjectMonster_MapsStatsFlagsSkills_AndKillGold()
        {
            var dto = new CardPresentationConfigDto
            {
                schemaVersion = 2,
                contentId = "monster.proj_demo",
                kind = "Monster",
                displayName = "投影怪",
                deckId = "deck.void",
                gold = 7,
                rarity = "None",
                level = 2,
                isElite = true,
                skillIds = new[] { "skill.proj_demo" },
                effectIds = new[] { "monster.proj_demo.aura" },
                tags = new[] { "火" },
                attackPattern = "无",
                stats = new CardPresentationStatsDto { hp = 9, attack = 3, armor = 1, recovery = 2 },
            };

            Assert.IsTrue(ContentJsonCatalogProjector.TryProjectCard(dto, out var card));
            Assert.AreEqual(CardKind.Monster, card.Kind);
            Assert.AreEqual(AttackPattern.None, card.AttackPattern);
            Assert.AreEqual(0, card.Stats.Action);
            Assert.AreEqual(7, card.KillGold);
            Assert.AreEqual(0, card.Price);
            Assert.AreEqual(2, card.Level);
            Assert.IsTrue(card.IsElite);
            Assert.AreEqual("deck.void", card.DeckId);
            Assert.AreEqual(9, card.Stats.MaxHp);
            Assert.AreEqual(3, card.Stats.Attack);
            Assert.AreEqual(1, card.Stats.Armor);
            Assert.AreEqual(2, card.Stats.Recovery);
            Assert.AreEqual(1, card.SkillIds.Count);
            Assert.AreEqual("skill.proj_demo", card.SkillIds[0]);
            Assert.AreEqual(1, card.EffectIds.Count);
            Assert.AreEqual("火", card.Tags[0]);
        }

        [Test]
        public void TryProjectDeckAndRoom_MapsStructuralFields()
        {
            Assert.IsTrue(ContentJsonCatalogProjector.TryProjectDeck(new CardPresentationConfigDto
            {
                schemaVersion = 2,
                contentId = "deck.proj_demo",
                kind = "Deck",
                displayName = "投影牌组",
                deckKind = "Boss",
                monsterDefIds = new[] { "monster.a", "monster.b" },
            }, out var deck));
            Assert.AreEqual(MonsterDeckKind.Boss, deck.Kind);
            Assert.AreEqual(2, deck.MonsterDefIds.Count);

            Assert.IsTrue(ContentJsonCatalogProjector.TryProjectRoom(new CardPresentationConfigDto
            {
                schemaVersion = 2,
                contentId = "Fountain",
                kind = "Room",
                displayName = "温泉房",
                weight = 20,
                maxHpDelta = 4,
                healToFull = true,
            }, out var room));
            Assert.AreEqual(RoomKind.Fountain, room.Kind);
            Assert.AreEqual(20, room.Weight);
            Assert.AreEqual(4, room.MaxHpDelta);
            Assert.IsTrue(room.HealToFull);
        }

        [Test]
        public void ApplyToCatalog_OverwritesLubanRelicSkillMonster()
        {
            var catalog = new GameContentCatalog();
            catalog.AddRelic(new RelicContentDefinition("relic.proj_overwrite", "旧", ContentRarity.White, "旧文")
                .AddEffect("relic.proj_overwrite.old"));
            catalog.AddSkill(new SkillContentDefinition(
                "skill.proj_overwrite", "旧技", EffectContainerType.MonsterSkill, "旧")
                .AddEffect("skill.proj_overwrite.old"));
            catalog.AddCard(new CardContentDefinition("monster.proj_overwrite", "旧怪", CardKind.Monster)
                .WithStats(1, 1, 0)
                .AddSkill("skill.old"));

            CardPresentationConfigCatalog.UpsertForTests(new CardPresentationConfigDto
            {
                schemaVersion = 2,
                contentId = "relic.proj_overwrite",
                kind = "Relic",
                displayName = "新遗物",
                description = "新文",
                rarity = "Blue",
                effectIds = new[] { "relic.proj_overwrite.use" },
            });
            CardPresentationConfigCatalog.UpsertForTests(new CardPresentationConfigDto
            {
                schemaVersion = 2,
                contentId = "skill.proj_overwrite",
                kind = "Skill",
                displayName = "新技",
                description = "新技文",
                containerType = "MonsterSkill",
                effectIds = new[] { "skill.proj_overwrite.move" },
            });
            CardPresentationConfigCatalog.UpsertForTests(new CardPresentationConfigDto
            {
                schemaVersion = 2,
                contentId = "monster.proj_overwrite",
                kind = "Monster",
                displayName = "新怪",
                gold = 11,
                attackPattern = "无",
                stats = new CardPresentationStatsDto { hp = 8, attack = 4, armor = 2 },
                skillIds = new[] { "skill.proj_overwrite" },
            });

            var applied = ContentJsonCatalogProjector.ApplyToCatalog(catalog);
            Assert.GreaterOrEqual(applied, 3);

            Assert.AreEqual("新遗物", catalog.Relics["relic.proj_overwrite"].DisplayName);
            Assert.AreEqual(1, catalog.Relics["relic.proj_overwrite"].EffectIds.Count);
            Assert.AreEqual("新技", catalog.Skills["skill.proj_overwrite"].DisplayName);
            Assert.AreEqual("新怪", catalog.Cards["monster.proj_overwrite"].DisplayName);
            Assert.AreEqual(11, catalog.Cards["monster.proj_overwrite"].KillGold);
            Assert.AreEqual(8, catalog.Cards["monster.proj_overwrite"].Stats.MaxHp);
            Assert.AreEqual("skill.proj_overwrite", catalog.Cards["monster.proj_overwrite"].SkillIds[0]);
        }

        [Test]
        public void PresentationJson_Alone_DoesNotMutateSeededMonsterStats()
        {
            var catalog = new GameContentCatalog();
            catalog.AddCard(
                new CardContentDefinition("monster.overlay_skip", "FromJson", CardKind.Monster)
                    .WithStats(5, 2, 1));
            catalog.Cards["monster.overlay_skip"].KillGold = 9;

            CardPresentationConfigCatalog.UpsertForTests(new CardPresentationConfigDto
            {
                schemaVersion = 2,
                contentId = "monster.overlay_skip",
                kind = "Monster",
                displayName = "WouldChangeIfProjected",
                gold = 1,
                stats = new CardPresentationStatsDto { hp = 99, attack = 88, armor = 77 },
            });

            // #69：无 Overlay；未再投影时 Catalog 种子值不变。
            var card = catalog.Cards["monster.overlay_skip"];
            Assert.AreEqual("FromJson", card.DisplayName);
            Assert.AreEqual(9, card.KillGold);
            Assert.AreEqual(5, card.Stats.MaxHp);
            Assert.AreEqual(2, card.Stats.Attack);
            Assert.AreEqual(1, card.Stats.Armor);
        }

        [Test]
        public void TryProject_SkipsOldSchemaAndChoiceOption()
        {
            Assert.IsFalse(ContentJsonCatalogProjector.TryProjectRelic(new CardPresentationConfigDto
            {
                schemaVersion = 1,
                contentId = "relic.old",
                kind = "Relic",
                displayName = "旧",
            }, out _));

            Assert.IsFalse(ContentJsonCatalogProjector.TryProjectCard(new CardPresentationConfigDto
            {
                schemaVersion = 2,
                contentId = "Attack",
                kind = "ChoiceOption",
                displayName = "攻击+1",
            }, out _));
        }
    }
}
