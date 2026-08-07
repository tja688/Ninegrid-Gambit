using System.Collections.Generic;
using NineGrid.Content;
using NineGrid.Content.CardPresentation;
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
    /// #119：OnCumulative 计量扩展（monsterRemoved / helpCardUsed / damageTaken）+ 六件累计遗物。
    /// Seam：IEffectSystem.Activate + Kill/UseItem/DealDamage；Relic 内容投影与奖池。
    /// </summary>
    public sealed class RelicR2CumulativeContractTests
    {
        private const string MonsterRemovedEvery3Json =
            "{\"id\":\"test.r2.monster_removed\",\"typeTag\":\"【类型遗物】\",\"containerType\":\"Relic\","
            + "\"kind\":\"Triggered\",\"requires\":[\"NoOwnerEntity\"],"
            + "\"trigger\":{\"atom\":\"OnCumulative\",\"metric\":\"monsterRemoved\",\"threshold\":3,"
            + "\"counterKey\":\"test.r2.monsterRemoved\"},"
            + "\"target\":{\"atom\":\"Player\"},"
            + "\"action\":{\"atom\":\"ModifyBaseStat\",\"stat\":\"Attack\",\"delta\":1,\"reason\":\"test.r2.monster_removed\"}}";

        private const string HelpCardUsedEvery2Json =
            "{\"id\":\"test.r2.help_used\",\"typeTag\":\"【类型遗物】\",\"containerType\":\"Relic\","
            + "\"kind\":\"Triggered\",\"requires\":[\"NoOwnerEntity\"],"
            + "\"trigger\":{\"atom\":\"OnCumulative\",\"metric\":\"helpCardUsed\",\"threshold\":2,"
            + "\"counterKey\":\"test.r2.helpCardUsed\"},"
            + "\"target\":{\"atom\":\"Player\"},"
            + "\"action\":{\"atom\":\"ModifyBaseStat\",\"stat\":\"MaxHp\",\"delta\":1,\"reason\":\"test.r2.help_used\"}}";

        private const string DamageTakenEvery5Json =
            "{\"id\":\"test.r2.damage_taken\",\"typeTag\":\"【类型遗物】\",\"containerType\":\"Relic\","
            + "\"kind\":\"Triggered\",\"requires\":[\"NoOwnerEntity\"],"
            + "\"trigger\":{\"atom\":\"OnCumulative\",\"metric\":\"damageTaken\",\"threshold\":5,"
            + "\"targetIsPlayer\":true,\"counterKey\":\"test.r2.damageTaken\"},"
            + "\"target\":{\"atom\":\"Player\"},"
            + "\"action\":{\"atom\":\"ModifyBaseStat\",\"stat\":\"MaxHp\",\"delta\":1,\"reason\":\"test.r2.damage_taken\"}}";

        private static readonly string[] SixRelicIds =
        {
            "relic.punch_card_knife",
            "relic.rotation_trick",
            "relic.terror_mask",
            "relic.junk_cycler",
            "relic.junk_body",
            "relic.blood_demon",
        };

        private static readonly string[] WhiteIds =
        {
            "relic.punch_card_knife",
            "relic.rotation_trick",
            "relic.blood_demon",
        };

        private static readonly string[] BlueIds =
        {
            "relic.terror_mask",
            "relic.junk_cycler",
            "relic.junk_body",
        };

        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IActionPipelineSystem mPipeline;
        private IEffectSystem mEffects;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            CardPresentationConfigCatalog.Invalidate();
            EffectTemplateCatalog.Invalidate();
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, ContentCatalogBootstrap.Load());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 119UL, AvatarArmor = 0 });
            mPhase = mArch.GetSystem<IPhaseSystem>();
            mPipeline = mArch.GetSystem<IActionPipelineSystem>();
            mEffects = mArch.GetSystem<IEffectSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            EffectTemplateCatalog.Invalidate();
            CardPresentationConfigCatalog.Invalidate();
            NineGridArchitecture.ResetForTests();
            mArch = null;
            mPhase = null;
            mPipeline = null;
            mEffects = null;
        }

        [Test]
        public void OnCumulative_MonsterRemoved_TriggersEvery3Kills()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);
            ActivateRelicEffect(MonsterRemovedEvery3Json, "test.r2.monster_removed");

            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var avatar = registry.Get(board.AvatarUid.Value);
            var atk0 = (int)avatar.Stats.GetBase(StatId.Attack);

            KillMonsterAt(SlotId.Board(2));
            Assert.AreEqual(atk0, (int)avatar.Stats.GetBase(StatId.Attack));
            Assert.AreEqual(2, avatar.Counters.Get("test.r2.monsterRemoved"));

            KillMonsterAt(SlotId.Board(3));
            Assert.AreEqual(atk0, (int)avatar.Stats.GetBase(StatId.Attack));

            KillMonsterAt(SlotId.Board(4));
            Assert.AreEqual(atk0 + 1, (int)avatar.Stats.GetBase(StatId.Attack), "第 3 次移除怪物应触发");
            Assert.AreEqual(3, avatar.Counters.Get("test.r2.monsterRemoved"));
        }

        [Test]
        public void OnCumulative_HelpCardUsed_TriggersEvery2Uses()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);
            ActivateRelicEffect(HelpCardUsedEvery2Json, "test.r2.help_used");

            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var avatar = registry.Get(board.AvatarUid.Value);
            var max0 = (int)avatar.Stats.GetBase(StatId.MaxHp);

            UseHelpCard("help.healing_potion");
            Assert.AreEqual(max0, (int)avatar.Stats.GetBase(StatId.MaxHp));
            Assert.AreEqual(1, avatar.Counters.Get("test.r2.helpCardUsed"));

            UseHelpCard("help.healing_potion");
            Assert.AreEqual(max0 + 1, (int)avatar.Stats.GetBase(StatId.MaxHp), "第 2 次使用道具应触发");
            Assert.AreEqual(2, avatar.Counters.Get("test.r2.helpCardUsed"));
        }

        [Test]
        public void OnCumulative_DamageTaken_CountsArmorAbsorbedDamage()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);
            ActivateRelicEffect(DamageTakenEvery5Json, "test.r2.damage_taken");

            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var avatar = registry.Get(board.AvatarUid.Value);
            avatar.Stats.SetBase(StatId.Armor, 10);
            StatArmorUtility.SetCurrentArmor(avatar, 10);
            var max0 = (int)avatar.Stats.GetBase(StatId.MaxHp);

            // 4 点全由护甲吸收 → 未达阈值
            mPipeline.Enqueue(new DealDamageAction(0, avatar.Uid, 4, "test.r2"));
            mPipeline.RunToCompletion();
            Assert.AreEqual(max0, (int)avatar.Stats.GetBase(StatId.MaxHp));
            Assert.AreEqual(1, avatar.Counters.Get("test.r2.damageTaken"));

            // 再 1 点（仍可全由甲吸收）→ 累计 5 触发
            mPipeline.Enqueue(new DealDamageAction(0, avatar.Uid, 1, "test.r2"));
            mPipeline.RunToCompletion();
            Assert.AreEqual(max0 + 1, (int)avatar.Stats.GetBase(StatId.MaxHp), "damageTaken 应计护甲吸收");
            Assert.AreEqual(5, avatar.Counters.Get("test.r2.damageTaken"));
        }

        [Test]
        public void BodyPotential_LargeHpLoss_TriggersOncePerThreshold_AndPreservesRemainder()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);
            mArch.GetSystem<IContentSystem>().ActivateRelic("relic.body_potential");

            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var deck = mArch.GetModel<DeckModel>();
            var avatar = registry.Get(board.AvatarUid.Value);
            avatar.Stats.SetBase(StatId.MaxHp, 99);
            avatar.Stats.SetBase(StatId.Hp, 99);
            StatArmorUtility.SetCurrentArmor(avatar, 0);

            mPipeline.Enqueue(new DealDamageAction(
                0,
                avatar.Uid,
                25,
                "test.body_potential",
                null,
                ignoreArmor: true));
            mPipeline.RunToCompletion();

            Assert.AreEqual(2, deck.DrawPileUids.Count, "25 点实际掉血应跨过两个 10 点阈值");
            Assert.AreEqual(5, avatar.Counters.Get("relic.body_potential.hp"), "应保留跨阈值后的 5 点余量");
        }

        [Test]
        public void BodyPotential_HpLossCounter_PersistsAcrossNodeSetup()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);
            mArch.GetSystem<IContentSystem>().ActivateRelic("relic.body_potential");

            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var deck = mArch.GetModel<DeckModel>();
            var avatar = registry.Get(board.AvatarUid.Value);
            avatar.Stats.SetBase(StatId.MaxHp, 99);
            avatar.Stats.SetBase(StatId.Hp, 99);
            StatArmorUtility.SetCurrentArmor(avatar, 0);

            mPipeline.Enqueue(new DealDamageAction(0, avatar.Uid, 6, "test.body_potential", null, true));
            mPipeline.RunToCompletion();
            Assert.AreEqual(0, deck.DrawPileUids.Count, "累计 6 点时不应加牌");
            Assert.AreEqual(4, avatar.Counters.Get("relic.body_potential.hp"));

            mPipeline.Enqueue(new SetupNodeDeckAction(CreateEmptyEnemyNode()));
            mPipeline.Enqueue(new NodeStartedAction());
            mPipeline.RunToCompletion();
            Assert.AreEqual(4, avatar.Counters.Get("relic.body_potential.hp"), "战斗/节点切换不得清除累计值");

            mPipeline.Enqueue(new DealDamageAction(0, avatar.Uid, 4, "test.body_potential", null, true));
            mPipeline.RunToCompletion();

            Assert.AreEqual(1, deck.DrawPileUids.Count, "跨节点补足 10 点后应加 1 张牌");
            Assert.AreEqual(10, avatar.Counters.Get("relic.body_potential.hp"));
        }

        [Test]
        public void Bootstrap_SixCumulativeRelics_ExistPoolEligible_WithAssemblies()
        {
            var catalog = ContentCatalogBootstrap.Load();
            Assert.AreEqual(6, SixRelicIds.Length);
            for (var i = 0; i < SixRelicIds.Length; i++)
            {
                var id = SixRelicIds[i];
                Assert.IsTrue(catalog.Relics.TryGetValue(id, out var relic), "missing " + id);
                Assert.AreEqual(RelicDecks.Live, relic.DeckId, id);
                Assert.IsFalse(string.IsNullOrEmpty(relic.DisplayName), id + " displayName");
                Assert.IsTrue(
                    CardPresentationAuthority.TryGetOwnedDescription(id, out var description),
                    id + " description");
                Assert.IsFalse(string.IsNullOrWhiteSpace(description), id + " description blank");
                Assert.Greater(relic.EffectIds.Count, 0, id + " needs effect assemblies");
            }

            AssertRarity(catalog, WhiteIds, ContentRarity.White);
            AssertRarity(catalog, BlueIds, ContentRarity.Blue);
            AssertPoolContainsAll(catalog, "relic.common_chest", WhiteIds);
            AssertPoolContainsAll(catalog, "relic.common_chest", BlueIds);
            AssertPoolContainsAll(catalog, "relic.blood_conversion", WhiteIds);
            AssertPoolContainsAll(catalog, "relic.blood_conversion", BlueIds);
        }

        [Test]
        public void CumulativeRelics_UseExpectedOnCumulativeMetrics()
        {
            var catalog = ContentCatalogBootstrap.Load();
            AssertTriggerMetric(catalog, "relic.punch_card_knife.remove", "monsterRemoved", 5);
            AssertTriggerMetric(catalog, "relic.rotation_trick.remove", "monsterRemoved", 6);
            AssertTriggerMetric(catalog, "relic.terror_mask.remove", "monsterRemoved", 6);
            AssertTriggerMetric(catalog, "relic.junk_cycler.use", "helpCardUsed", 9);
            AssertTriggerMetric(catalog, "relic.junk_body.use", "helpCardUsed", 3);
            AssertTriggerMetric(catalog, "relic.blood_demon.damage", "damageTaken", 5);
        }

        [Test]
        public void TerrorMask_RemovesOnlyNormalRankBoardMonsters()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);
            mArch.GetSystem<IContentSystem>().ActivateRelic("relic.terror_mask");

            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            ClearNonAvatarBoardSlots(board);

            var normal = registry.Create("monster.melee_6", CardKind.Monster);
            normal.Stats.SetBase(StatId.Hp, 99);
            normal.Stats.SetBase(StatId.Attack, 1);
            board.PlaceCard(normal, SlotId.Board(2));

            var floorBoss = registry.Create("monster.melee_6", CardKind.Monster);
            floorBoss.Stats.SetBase(StatId.Hp, 99);
            floorBoss.Stats.SetBase(StatId.Attack, 1);
            floorBoss.Counters.Set(CoreCounterKeys.Boss, 1);
            board.PlaceCard(floorBoss, SlotId.Board(3));

            // 先移除 5 只外来怪推进计数，第 6 次触发面罩
            for (var i = 0; i < 5; i++)
            {
                KillMonsterAt(SlotId.Board(5));
            }

            Assert.AreEqual(normal.Uid, board.GetCardUid(SlotId.Board(2)));
            Assert.AreEqual(floorBoss.Uid, board.GetCardUid(SlotId.Board(3)));

            KillMonsterAt(SlotId.Board(5));
            mPipeline.RunToCompletion();

            Assert.AreEqual(0, board.GetCardUid(SlotId.Board(2)), "应移除普通等级怪");
            Assert.AreEqual(floorBoss.Uid, board.GetCardUid(SlotId.Board(3)), "层主不应被恐怖面罩移除");
        }

        [Test]
        public void TerrorMask_MonsterRemovedCounter_ResetsOnNodeStart()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);
            mArch.GetSystem<IContentSystem>().ActivateRelic("relic.terror_mask");

            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var avatar = registry.Get(board.AvatarUid.Value);

            for (var i = 0; i < 5; i++)
            {
                KillMonsterAt(SlotId.Board(5));
            }

            Assert.AreEqual(1, avatar.Counters.Get("relic.terror_mask.remove"), "本关应累计到还差 1 次");

            // 直接打 OnNodeStart 拍（完整 StartNode 在 InteractionLoop 非法）；验证关卡开始清零。
            mPipeline.Enqueue(new NodeStartedAction());
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            Assert.AreEqual(0, avatar.Counters.Get("relic.terror_mask.remove"), "新关卡应清零本关累计");
        }

        [Test]
        public void BloodDemon_GrowsMaxHp_Every5DamageTakenIncludingArmor()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);
            mArch.GetSystem<IContentSystem>().ActivateRelic("relic.blood_demon");

            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var avatar = registry.Get(board.AvatarUid.Value);
            avatar.Stats.SetBase(StatId.Armor, 10);
            StatArmorUtility.SetCurrentArmor(avatar, 10);
            var max0 = (int)avatar.Stats.GetBase(StatId.MaxHp);

            mPipeline.Enqueue(new DealDamageAction(0, avatar.Uid, 5, "test.blood_demon"));
            mPipeline.RunToCompletion();

            Assert.AreEqual(max0 + 1, (int)avatar.Stats.GetBase(StatId.MaxHp), "血魔每 5 点承伤 +1 上限");
        }

        private void ActivateRelicEffect(string json, string sourceDefId)
        {
            var definition = mEffects.ParseJson(json);
            Assert.IsTrue(mEffects.Validate(definition).IsValid, json);
            mEffects.Activate(definition, new EffectOwner(EffectContainerType.Relic, sourceDefId, 0));
        }

        private void KillMonsterAt(SlotId slot)
        {
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            if (board.GetCardUid(slot) != 0)
            {
                board.ClearSlot(slot);
            }

            var monster = registry.Create("monster.melee_6", CardKind.Monster);
            monster.Stats.SetBase(StatId.Hp, 1);
            monster.Stats.SetBase(StatId.Attack, 1);
            board.PlaceCard(monster, slot);

            var avatarUid = board.AvatarUid.Value;
            Assert.IsTrue(mPhase.ApplyCombatHit(avatarUid, monster.Uid).Accepted);
            mPipeline.RunToCompletion();
        }

        private void UseHelpCard(string defId)
        {
            var deck = mArch.GetModel<DeckModel>();
            mPipeline.Enqueue(new SpawnCardAction(defId, CardKind.HelpCard, ZoneId.ItemSlots, SlotId.None, 1, "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            Assert.Greater(deck.ItemSlotUids.Count, 0);
            var helpUid = deck.ItemSlotUids[deck.ItemSlotUids.Count - 1];
            var use = mPhase.UseItem(helpUid);
            Assert.IsTrue(use.Accepted, use.Reason);
            mPipeline.RunToCompletion();
        }

        private static NodeDeckOptions CreateEmptyEnemyNode()
        {
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 0
            };
        }

        private static void ClearNonAvatarBoardSlots(BoardModel board)
        {
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var slot = SlotId.Board(i);
                if (slot == board.AvatarSlot.Value)
                {
                    continue;
                }

                if (board.GetCardUid(slot) != 0)
                {
                    board.ClearSlot(slot);
                }
            }
        }

        private static void AssertTriggerMetric(
            GameContentCatalog catalog,
            string effectId,
            string metric,
            int threshold)
        {
            Assert.IsTrue(catalog.Effects.TryGetValue(effectId, out var effect), effectId);
            var parsed = EffectDefinitionParser.ParseJson(effect.Json);
            Assert.AreEqual("OnCumulative", parsed.Trigger.Get("atom").AsString(string.Empty), effectId);
            Assert.AreEqual(metric, parsed.Trigger.Get("metric").AsString(string.Empty), effectId);
            Assert.AreEqual(threshold, parsed.Trigger.Get("threshold").AsInt(0), effectId);
        }

        private static void AssertRarity(
            GameContentCatalog catalog,
            IReadOnlyList<string> ids,
            ContentRarity rarity)
        {
            for (var i = 0; i < ids.Count; i++)
            {
                Assert.IsTrue(catalog.Relics.TryGetValue(ids[i], out var relic), ids[i]);
                Assert.AreEqual(rarity, relic.Rarity, ids[i]);
            }
        }

        private static void AssertPoolContainsAll(
            GameContentCatalog catalog,
            string poolId,
            IReadOnlyList<string> ids)
        {
            Assert.IsTrue(catalog.Rewards.TryGetPool(poolId, out var pool), poolId);
            for (var i = 0; i < ids.Count; i++)
            {
                var found = false;
                for (var j = 0; j < pool.Entries.Count; j++)
                {
                    if (pool.Entries[j].DefId == ids[i])
                    {
                        found = true;
                        break;
                    }
                }

                Assert.IsTrue(found, poolId + " missing " + ids[i]);
            }
        }
    }
}
