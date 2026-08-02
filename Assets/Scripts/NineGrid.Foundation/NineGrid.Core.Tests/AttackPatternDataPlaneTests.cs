using System.Linq;
using NineGrid.Content;
using NineGrid.Content.CardPresentation;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// #77 / #82 / ADR-0011：攻击模式必填枚举、频率可读、进场倒计时初始化；
    /// #82 内容赋模（非全员「无」）与加载校验。Seam：投影 / ValidateCatalog / CreateDraft→Create。
    /// </summary>
    public sealed class AttackPatternDataPlaneTests
    {
        private IArchitecture mArch;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            CardPresentationConfigCatalog.Invalidate();
            EffectTemplateCatalog.Invalidate();
        }

        [TearDown]
        public void TearDown()
        {
            CardPresentationConfigCatalog.Invalidate();
            EffectTemplateCatalog.Invalidate();
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void AttackPattern_FiveValues_HaveCanonicalFrequencies()
        {
            Assert.AreEqual(3, AttackPatternRules.Frequency(AttackPattern.OrthogonalMelee));
            Assert.AreEqual(3, AttackPatternRules.Frequency(AttackPattern.DiagonalMelee));
            Assert.AreEqual(3, AttackPatternRules.Frequency(AttackPattern.OmnidirectionalMelee));
            Assert.AreEqual(5, AttackPatternRules.Frequency(AttackPattern.Ranged));
            Assert.AreEqual(0, AttackPatternRules.Frequency(AttackPattern.None));
            Assert.AreEqual(0, AttackPatternRules.Frequency(AttackPattern.Unspecified));
        }

        [Test]
        public void TryParse_AcceptsFiveChineseTokens_RejectsBlankAndUnknown()
        {
            Assert.IsTrue(AttackPatternRules.TryParse("普通近战", out var orthogonal));
            Assert.AreEqual(AttackPattern.OrthogonalMelee, orthogonal);
            Assert.IsTrue(AttackPatternRules.TryParse("斜角近战", out var diagonal));
            Assert.AreEqual(AttackPattern.DiagonalMelee, diagonal);
            Assert.IsTrue(AttackPatternRules.TryParse("全向近战", out var omni));
            Assert.AreEqual(AttackPattern.OmnidirectionalMelee, omni);
            Assert.IsTrue(AttackPatternRules.TryParse("普通远程", out var ranged));
            Assert.AreEqual(AttackPattern.Ranged, ranged);
            Assert.IsTrue(AttackPatternRules.TryParse("无", out var none));
            Assert.AreEqual(AttackPattern.None, none);

            Assert.IsFalse(AttackPatternRules.TryParse(null, out var missing));
            Assert.AreEqual(AttackPattern.Unspecified, missing);
            Assert.IsFalse(AttackPatternRules.TryParse("", out var empty));
            Assert.AreEqual(AttackPattern.Unspecified, empty);
            Assert.IsFalse(AttackPatternRules.TryParse("近战", out var unknown));
            Assert.AreEqual(AttackPattern.Unspecified, unknown);
        }

        [Test]
        public void ProjectMonster_MissingAttackPattern_IsUnspecified_AndValidateFails()
        {
            var dto = CreateMonsterDto(attackPattern: null);
            Assert.IsTrue(ContentJsonCatalogProjector.TryProjectCard(dto, out var card));
            Assert.AreEqual(AttackPattern.Unspecified, card.AttackPattern);

            var catalog = new GameContentCatalog();
            catalog.AddCard(card);
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, catalog);

            var report = mArch.GetSystem<IContentSystem>().ValidateCatalog();
            Assert.IsFalse(report.IsValid);
            Assert.IsTrue(
                report.Issues.Any(issue => issue.Contains("attackPattern") && issue.Contains(dto.contentId)),
                "Expected missing attackPattern issue, got: " + string.Join("; ", report.Issues));
        }

        [Test]
        public void ProjectMonster_ExplicitNone_PassesValidation_AndDoesNotMountEffects()
        {
            var dto = CreateMonsterDto(attackPattern: "无");
            dto.effectAssemblies = new EffectAssemblyDto[0];
            dto.effectIds = new string[0];

            Assert.IsTrue(ContentJsonCatalogProjector.TryProjectCard(dto, out var card));
            Assert.AreEqual(AttackPattern.None, card.AttackPattern);
            Assert.AreEqual(0, card.Stats.Action);
            Assert.AreEqual(0, card.EffectIds.Count);

            var catalog = new GameContentCatalog();
            catalog.AddCard(card);
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, catalog);

            var report = mArch.GetSystem<IContentSystem>().ValidateCatalog();
            Assert.IsTrue(report.IsValid, string.Join("; ", report.Issues));
        }

        [Test]
        public void ProjectMonster_MeleeAndRanged_WriteReadableFrequencies()
        {
            Assert.IsTrue(ContentJsonCatalogProjector.TryProjectCard(
                CreateMonsterDto(attackPattern: "普通近战"), out var melee));
            Assert.AreEqual(AttackPattern.OrthogonalMelee, melee.AttackPattern);
            Assert.AreEqual(3, melee.Stats.Action);

            Assert.IsTrue(ContentJsonCatalogProjector.TryProjectCard(
                CreateMonsterDto(attackPattern: "普通远程"), out var ranged));
            Assert.AreEqual(AttackPattern.Ranged, ranged.AttackPattern);
            Assert.AreEqual(5, ranged.Stats.Action);
        }

        [Test]
        public void CreateDraft_NonNone_InitializesAttackPatternCountdown_NoneDoesNot()
        {
            var catalog = new GameContentCatalog();
            catalog.AddCard(ProjectMonster("monster.ap_melee", "普通近战"));
            catalog.AddCard(ProjectMonster("monster.ap_none", "无"));
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, catalog);

            var content = mArch.GetSystem<IContentSystem>();
            var registry = mArch.GetModel<CardRegistry>();

            var melee = content.CreateDraft("monster.ap_melee").Create(registry);
            Assert.AreEqual(3, melee.Counters.Get(CoreCounterKeys.AttackPatternCountdown));

            var none = content.CreateDraft("monster.ap_none").Create(registry);
            Assert.AreEqual(0, none.Counters.Get(CoreCounterKeys.AttackPatternCountdown));
        }

        [Test]
        public void BootstrapCatalog_MonstersHaveDesignAttackPatterns_NotAllNone()
        {
            var catalog = ContentCatalogBootstrap.Load();
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, catalog);

            var report = mArch.GetSystem<IContentSystem>().ValidateCatalog();
            Assert.IsTrue(report.IsValid, string.Join("; ", report.Issues));

            var monsters = catalog.Cards.Values.Where(card => card.Kind == CardKind.Monster).ToList();
            Assert.GreaterOrEqual(monsters.Count, 61);

            var expected = DesignAttackPatterns();
            foreach (var monster in monsters)
            {
                Assert.AreNotEqual(
                    AttackPattern.Unspecified,
                    monster.AttackPattern,
                    monster.DefId + " must have explicit attackPattern");
                Assert.AreEqual(
                    AttackPatternRules.Frequency(monster.AttackPattern),
                    monster.Stats.Action,
                    monster.DefId + " stats.action must match pattern frequency");

                AttackPattern want;
                if (expected.TryGetValue(monster.DefId, out want))
                {
                    Assert.AreEqual(want, monster.AttackPattern, monster.DefId);
                }
                else
                {
                    Assert.AreEqual(
                        AttackPattern.OrthogonalMelee,
                        monster.AttackPattern,
                        monster.DefId + " default design mode is 普通近战");
                }
            }

            Assert.AreEqual(AttackPattern.None, expected["monster.big_stone"]);
            Assert.AreEqual(AttackPattern.None, expected["monster.fire_priest"]);
            Assert.IsTrue(
                monsters.Any(m => m.AttackPattern != AttackPattern.None),
                "content must leave the all-无 migration intermediate");
            Assert.IsTrue(monsters.Any(m => m.AttackPattern == AttackPattern.Ranged));
            Assert.IsTrue(monsters.Any(m => m.AttackPattern == AttackPattern.OmnidirectionalMelee));
            Assert.IsTrue(monsters.Any(m => m.AttackPattern == AttackPattern.DiagonalMelee));
        }

        /// <summary>
        /// #82/#86 非默认（非普通近战）的设计赋模；攻 0 怪显式「无」；主题卡组回填后的序列怪按设计案。
        /// </summary>
        private static System.Collections.Generic.Dictionary<string, AttackPattern> DesignAttackPatterns()
        {
            return new System.Collections.Generic.Dictionary<string, AttackPattern>
            {
                { "monster.big_stone", AttackPattern.None },
                { "monster.fire_priest", AttackPattern.None },

                // 主题序列远程
                { "monster.salamander", AttackPattern.Ranged },
                { "monster.veteran_orc", AttackPattern.Ranged },
                { "monster.skull_head", AttackPattern.Ranged },
                { "monster.pickpocket", AttackPattern.Ranged },
                { "monster.smuggler", AttackPattern.Ranged },
                { "monster.observer", AttackPattern.Ranged },

                // Reserve 残留远程
                { "monster.stone_thrower", AttackPattern.Ranged },
                { "monster.skeleton_mage", AttackPattern.Ranged },

                // 主题序列斜角 / 全向
                { "monster.young_orc", AttackPattern.DiagonalMelee },
                { "monster.summon.special_omni", AttackPattern.OmnidirectionalMelee },

                // Reserve 残留非默认
                { "monster.big_orc", AttackPattern.OmnidirectionalMelee },
                { "monster.fire_cult_leader", AttackPattern.OmnidirectionalMelee },
                { "monster.growing_stone", AttackPattern.DiagonalMelee },
            };
        }

        private static CardContentDefinition ProjectMonster(string contentId, string attackPattern)
        {
            Assert.IsTrue(ContentJsonCatalogProjector.TryProjectCard(
                CreateMonsterDto(contentId, attackPattern), out var card));
            return card;
        }

        private static CardPresentationConfigDto CreateMonsterDto(string attackPattern)
        {
            return CreateMonsterDto("monster.ap_fixture", attackPattern);
        }

        private static CardPresentationConfigDto CreateMonsterDto(string contentId, string attackPattern)
        {
            return new CardPresentationConfigDto
            {
                schemaVersion = 2,
                contentId = contentId,
                kind = "Monster",
                displayName = "攻击模式夹具",
                attackPattern = attackPattern,
                gold = 1,
                stats = new CardPresentationStatsDto { hp = 3, attack = 1, armor = 0, action = 0 },
            };
        }
    }
}

