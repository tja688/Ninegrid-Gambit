using System.Collections.Generic;
using NineGrid.Content;
using NineGrid.Content.CardPresentation;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Effects;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// #118：R1 留用遗物策划对齐 — 名称匹配的 keepers 保留 contentId，修正装配/描述漂移。
    /// </summary>
    public sealed class RelicR1KeeperAuditContractTests
    {
        private static readonly string[] KeeperIds =
        {
            "relic.junk_recycler",
            "relic.wood_shield",
            "relic.wood_sword",
            "relic.wood_armor",
            "relic.lucky_coin",
            "relic.throwing_knife_bag",
            "relic.potion_bag",
            "relic.junk_launcher",
            "relic.junk_coating",
            "relic.sling",
            "relic.shield_knife",
            "relic.gold_knife",
            "relic.heavy_armor",
            "relic.gold_armor",
            "relic.swap_button",
            "relic.rotation_button",
            "relic.vitality_amulet",
            "relic.dragon_scale_armor",
            "relic.phoenix_feather",
            "relic.craving",
        };

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
            EffectTemplateCatalog.Invalidate();
            CardPresentationConfigCatalog.Invalidate();
            NineGridArchitecture.ResetForTests();
            mArch = null;
        }

        [Test]
        public void Bootstrap_TwentyKeepers_LiveDeck_NamedDescriptionsAndAssemblies()
        {
            var catalog = ContentCatalogBootstrap.Load();
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, catalog);

            Assert.AreEqual(20, KeeperIds.Length);
            for (var i = 0; i < KeeperIds.Length; i++)
            {
                var id = KeeperIds[i];
                Assert.IsTrue(catalog.Relics.TryGetValue(id, out var relic), "missing " + id);
                Assert.AreEqual(RelicDecks.Live, relic.DeckId, id);
                Assert.IsFalse(string.IsNullOrEmpty(relic.DisplayName), id + " displayName");
                Assert.IsTrue(
                    CardPresentationAuthority.TryGetOwnedDescription(id, out var description),
                    id + " description");
                Assert.IsFalse(string.IsNullOrWhiteSpace(description), id + " description blank");
                Assert.Greater(relic.EffectIds.Count, 0, id + " needs effect assemblies");
            }
        }

        [Test]
        public void Craving_GrantsMaxHpTen_AndDoublesRecovery()
        {
            var catalog = ContentCatalogBootstrap.Load();
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, catalog);
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 118UL, AvatarArmor = 0 });

            var registry = mArch.GetModel<CardRegistry>();
            var board = mArch.GetModel<BoardModel>();
            var pipeline = mArch.GetSystem<IActionPipelineSystem>();
            var avatar = registry.Get(board.AvatarUid.Value);
            var maxBefore = (int)avatar.Stats.GetBase(StatId.MaxHp);

            mArch.GetSystem<IContentSystem>().ActivateRelic("relic.craving");
            pipeline.RunToCompletion();

            Assert.AreEqual(maxBefore + 10, (int)avatar.Stats.GetBase(StatId.MaxHp), "渴望应提供血量上限+10");
            Assert.IsTrue(catalog.Effects.ContainsKey("relic.craving.rule"));
            var ruleJson = EffectDefinitionParser.ParseJson(catalog.Effects["relic.craving.rule"].Json);
            Assert.AreEqual("RecoveryMultiplier", ruleJson.RuleModifier.Get("rule").AsString(string.Empty));
            Assert.AreEqual(2, ruleJson.RuleModifier.Get("value").AsInt(0));
        }

        [Test]
        public void LuckyCoin_OnlyBossKillAssembly_NotElite()
        {
            var catalog = ContentCatalogBootstrap.Load();
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, catalog);

            Assert.IsTrue(catalog.Relics.TryGetValue("relic.lucky_coin", out var coin));
            Assert.IsFalse(ContainsEffectId(coin.EffectIds, "relic.lucky_coin.elite_kill"), "层主限定，不应挂精英击杀装配");
            Assert.IsTrue(ContainsEffectId(coin.EffectIds, "relic.lucky_coin.boss_kill"), "应保留层主击杀装配");
        }

        [Test]
        public void JunkRelics_UseOnAnyHelpCardUsedTrigger()
        {
            var catalog = ContentCatalogBootstrap.Load();
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, catalog);

            AssertTriggerAtom(catalog, "relic.junk_recycler.use", "OnAnyHelpCardUsed");
            AssertTriggerAtom(catalog, "relic.junk_coating.use", "OnAnyHelpCardUsed");
            AssertTriggerAtom(catalog, "relic.junk_launcher.use", "OnAnyHelpCardUsed");
        }

        [Test]
        public void JunkRecycler_HealsOnHelpCardUse()
        {
            var catalog = ContentCatalogBootstrap.Load();
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, catalog);
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 218UL, AvatarArmor = 0 });

            var phase = mArch.GetSystem<IPhaseSystem>();
            var pipeline = mArch.GetSystem<IActionPipelineSystem>();
            var registry = mArch.GetModel<CardRegistry>();
            var board = mArch.GetModel<BoardModel>();
            var deck = mArch.GetModel<DeckModel>();

            Assert.IsTrue(phase.StartNode(NodeDeckOptions.CreateDefaultBattle()).Accepted);
            mArch.GetSystem<IContentSystem>().ActivateRelic("relic.junk_recycler");

            var avatar = registry.Get(board.AvatarUid.Value);
            avatar.Stats.SetBase(StatId.Hp, 5);
            avatar.Stats.SetBase(StatId.MaxHp, 40);

            pipeline.Enqueue(new SpawnCardAction("help.healing_potion", CardKind.HelpCard, ZoneId.ItemSlots, SlotId.None, 1, "test"));
            Assert.Greater(pipeline.RunToCompletion(), 0);
            Assert.Greater(deck.ItemSlotUids.Count, 0);
            var helpUid = deck.ItemSlotUids[deck.ItemSlotUids.Count - 1];

            var use = phase.UseItem(helpUid);
            Assert.IsTrue(use.Accepted, use.Reason);
            pipeline.RunToCompletion();

            // 恢复药水 +10，废物利用机额外 +2
            Assert.AreEqual(17, (int)avatar.Stats.GetBase(StatId.Hp), "废物利用机使用道具卡应额外回 2 血");
        }

        private static void AssertTriggerAtom(GameContentCatalog catalog, string effectId, string atom)
        {
            Assert.IsTrue(catalog.Effects.TryGetValue(effectId, out var effect), effectId);
            var parsed = EffectDefinitionParser.ParseJson(effect.Json);
            Assert.AreEqual(atom, parsed.Trigger.Get("atom").AsString(string.Empty), effectId);
        }

        private static bool ContainsEffectId(IReadOnlyList<string> ids, string target)
        {
            for (var i = 0; i < ids.Count; i++)
            {
                if (ids[i] == target)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
