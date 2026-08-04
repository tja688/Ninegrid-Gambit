using NineGrid.Content;
using NineGrid.Content.CardPresentation;
using NineGrid.Core.Content;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// #120：遗物 run 内成长（RelicRunContribution）+ 锻造器具 + 金剑。
    /// Seam：RelicRunContributionModel + IStatSystem 有效属性；内容投影与奖池。
    /// QuickTest：授予 relic.forge_tool / relic.golden_sword 各验一条成长路径。
    /// </summary>
    public sealed class RelicR3GrowthContractTests
    {
        private const string ForgeToolId = "relic.forge_tool";
        private const string GoldenSwordId = "relic.golden_sword";

        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IActionPipelineSystem mPipeline;
        private IStatSystem mStats;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            CardPresentationConfigCatalog.Invalidate();
            EffectTemplateCatalog.Invalidate();
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, ContentCatalogBootstrap.Load());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 120UL, AvatarArmor = 0 });
            mPhase = mArch.GetSystem<IPhaseSystem>();
            mPipeline = mArch.GetSystem<IActionPipelineSystem>();
            mStats = mArch.GetSystem<IStatSystem>();
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
            mStats = null;
        }

        [Test]
        public void Bootstrap_ForgeToolAndGoldenSword_LivePoolEligible_WithAssemblies()
        {
            var catalog = ContentCatalogBootstrap.Load();
            AssertRelicBootstrap(catalog, ForgeToolId, ContentRarity.Blue);
            AssertRelicBootstrap(catalog, GoldenSwordId, ContentRarity.Gold);
            AssertPoolContains(catalog, "relic.common_chest", ForgeToolId);
            AssertPoolContains(catalog, "relic.common_chest", GoldenSwordId);
            AssertPoolContains(catalog, "relic.blood_conversion", ForgeToolId);
            AssertPoolContains(catalog, "relic.blood_conversion", GoldenSwordId);
        }

        [Test]
        public void ForgeTool_GrantsArmorContribution_AndGrowsOnNodeStartWhenArmorAtLeast5()
        {
            mArch.GetSystem<IContentSystem>().ActivateRelic(ForgeToolId);
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);

            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var avatar = registry.Get(board.AvatarUid.Value);
            var contrib = mArch.GetModel<RelicRunContributionModel>();

            Assert.AreEqual(2, contrib.Get(ForgeToolId, StatId.Armor));
            Assert.AreEqual(2, mStats.GetEffectiveInt(avatar, StatId.Armor));
            Assert.AreEqual(2, StatArmorUtility.GetCurrentArmor(avatar));

            // 抬高当前甲至 ≥5；再打 OnNodeStart（完整 StartNode 在 InteractionLoop 非法）
            StatArmorUtility.SetCurrentArmor(avatar, 5);
            mPipeline.Enqueue(new NodeStartedAction());
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            Assert.AreEqual(0, StatArmorUtility.GetCurrentArmor(avatar), "应扣除 5 点当前护甲");
            Assert.AreEqual(3, contrib.Get(ForgeToolId, StatId.Armor), "本遗物基础甲应永久 +1");
            Assert.AreEqual(3, mStats.GetEffectiveInt(avatar, StatId.Armor));
        }

        [Test]
        public void ForgeTool_DoesNotGrow_WhenCurrentArmorBelow5()
        {
            mArch.GetSystem<IContentSystem>().ActivateRelic(ForgeToolId);
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);

            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var avatar = registry.Get(board.AvatarUid.Value);
            var contrib = mArch.GetModel<RelicRunContributionModel>();

            Assert.AreEqual(2, StatArmorUtility.GetCurrentArmor(avatar));
            mPipeline.Enqueue(new NodeStartedAction());
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            Assert.AreEqual(2, contrib.Get(ForgeToolId, StatId.Armor));
            Assert.AreEqual(2, StatArmorUtility.GetCurrentArmor(avatar));
        }

        [Test]
        public void ForgeTool_GrowthPersistsAcrossNodeStart()
        {
            mArch.GetSystem<IContentSystem>().ActivateRelic(ForgeToolId);
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);

            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var avatar = registry.Get(board.AvatarUid.Value);
            var contrib = mArch.GetModel<RelicRunContributionModel>();

            StatArmorUtility.SetCurrentArmor(avatar, 5);
            mPipeline.Enqueue(new NodeStartedAction());
            mPipeline.RunToCompletion();
            Assert.AreEqual(3, contrib.Get(ForgeToolId, StatId.Armor));

            // 再一次 OnNodeStart 不应清零成长（甲不足不再成长，但贡献保留）
            mPipeline.Enqueue(new NodeStartedAction());
            mPipeline.RunToCompletion();
            Assert.AreEqual(3, contrib.Get(ForgeToolId, StatId.Armor));
            Assert.AreEqual(3, mStats.GetEffectiveInt(avatar, StatId.Armor));
        }

        [Test]
        public void GoldenSword_StartsAt8_DecaysOnBattle_GrowsOnKill()
        {
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var avatar = registry.Get(board.AvatarUid.Value);
            var atkBefore = mStats.GetEffectiveInt(avatar, StatId.Attack);

            mArch.GetSystem<IContentSystem>().ActivateRelic(GoldenSwordId);
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);

            var contrib = mArch.GetModel<RelicRunContributionModel>();
            Assert.AreEqual(8, contrib.Get(GoldenSwordId, StatId.Attack));
            Assert.AreEqual(atkBefore + 8, mStats.GetEffectiveInt(avatar, StatId.Attack));

            // 非击杀交战：只衰减
            PlaceMonster(SlotId.Board(2), hp: 99);
            var monsterUid = board.GetCardUid(SlotId.Board(2));
            Assert.IsTrue(mPhase.ApplyCombatHit(board.AvatarUid.Value, monsterUid).Accepted);
            mPipeline.RunToCompletion();
            Assert.AreEqual(7, contrib.Get(GoldenSwordId, StatId.Attack), "战斗应 -1");
            Assert.AreEqual(atkBefore + 7, mStats.GetEffectiveInt(avatar, StatId.Attack));

            // 击杀：先战斗 -1 再击杀 +1，净值 0
            KillMonsterAt(SlotId.Board(3));
            Assert.AreEqual(7, contrib.Get(GoldenSwordId, StatId.Attack), "击杀当次净贡献应不变");
            Assert.AreEqual(atkBefore + 7, mStats.GetEffectiveInt(avatar, StatId.Attack));
        }

        [Test]
        public void GoldenSword_DecayFloorsAtZero()
        {
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var avatar = registry.Get(board.AvatarUid.Value);
            var atkBefore = mStats.GetEffectiveInt(avatar, StatId.Attack);

            mArch.GetSystem<IContentSystem>().ActivateRelic(GoldenSwordId);
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);

            var contrib = mArch.GetModel<RelicRunContributionModel>();

            // 直接压到 0 后继续战斗不应为负
            mPipeline.Enqueue(new ModifyRelicRunContributionAction(
                GoldenSwordId, StatId.Attack, 0, absolute: true, absoluteValue: 0, floor: 0));
            mPipeline.RunToCompletion();
            Assert.AreEqual(0, contrib.Get(GoldenSwordId, StatId.Attack));
            Assert.AreEqual(atkBefore, mStats.GetEffectiveInt(avatar, StatId.Attack));

            PlaceMonster(SlotId.Board(2), hp: 99);
            Assert.IsTrue(mPhase.ApplyCombatHit(board.AvatarUid.Value, board.GetCardUid(SlotId.Board(2))).Accepted);
            mPipeline.RunToCompletion();
            Assert.AreEqual(0, contrib.Get(GoldenSwordId, StatId.Attack));
            Assert.AreEqual(atkBefore, mStats.GetEffectiveInt(avatar, StatId.Attack));
        }

        [Test]
        public void GoldenSword_ContributionPersistsAcrossNodeStart()
        {
            mArch.GetSystem<IContentSystem>().ActivateRelic(GoldenSwordId);
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);

            var contrib = mArch.GetModel<RelicRunContributionModel>();
            KillMonsterAt(SlotId.Board(2));
            // 击杀净值 0 → 仍为 8；再非击杀交战压到 7
            PlaceMonster(SlotId.Board(3), hp: 99);
            var board = mArch.GetModel<BoardModel>();
            Assert.IsTrue(mPhase.ApplyCombatHit(board.AvatarUid.Value, board.GetCardUid(SlotId.Board(3))).Accepted);
            mPipeline.RunToCompletion();
            Assert.AreEqual(7, contrib.Get(GoldenSwordId, StatId.Attack));

            mPipeline.Enqueue(new NodeStartedAction());
            mPipeline.RunToCompletion();
            Assert.AreEqual(7, contrib.Get(GoldenSwordId, StatId.Attack), "跨节点应保留");
        }

        [Test]
        public void RelicRunContribution_ClearsOnNewRun()
        {
            mArch.GetSystem<IContentSystem>().ActivateRelic(GoldenSwordId);
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);
            var contrib = mArch.GetModel<RelicRunContributionModel>();
            Assert.AreEqual(8, contrib.Get(GoldenSwordId, StatId.Attack));

            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 121UL, AvatarArmor = 0 });
            Assert.AreEqual(0, contrib.Get(GoldenSwordId, StatId.Attack));
            Assert.AreEqual(0, contrib.Get(ForgeToolId, StatId.Armor));
        }

        [Test]
        public void DiscardRelic_ClearsContributionAndModifier()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var avatar = registry.Get(board.AvatarUid.Value);
            var atkBefore = mStats.GetEffectiveInt(avatar, StatId.Attack);

            mPipeline.Enqueue(new GrantRelicAction(GoldenSwordId));
            mPipeline.RunToCompletion();

            var contrib = mArch.GetModel<RelicRunContributionModel>();
            Assert.AreEqual(8, contrib.Get(GoldenSwordId, StatId.Attack));
            Assert.AreEqual(atkBefore + 8, mStats.GetEffectiveInt(avatar, StatId.Attack));

            mPipeline.Enqueue(new DiscardRelicAction(GoldenSwordId));
            mPipeline.RunToCompletion();
            Assert.AreEqual(0, contrib.Get(GoldenSwordId, StatId.Attack));
            Assert.AreEqual(atkBefore, mStats.GetEffectiveInt(avatar, StatId.Attack));
        }

        private void KillMonsterAt(SlotId slot)
        {
            PlaceMonster(slot, hp: 1);
            var board = mArch.GetModel<BoardModel>();
            Assert.IsTrue(mPhase.ApplyCombatHit(board.AvatarUid.Value, board.GetCardUid(slot)).Accepted);
            mPipeline.RunToCompletion();
        }

        private void PlaceMonster(SlotId slot, int hp)
        {
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            if (board.GetCardUid(slot) != 0)
            {
                board.ClearSlot(slot);
            }

            var monster = registry.Create("monster.melee_6", CardKind.Monster);
            monster.Stats.SetBase(StatId.Hp, hp);
            monster.Stats.SetBase(StatId.Attack, 1);
            board.PlaceCard(monster, slot);
        }

        private static NodeDeckOptions CreateEmptyEnemyNode()
        {
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 0
            };
        }

        private static void AssertRelicBootstrap(
            GameContentCatalog catalog,
            string id,
            ContentRarity rarity)
        {
            Assert.IsTrue(catalog.Relics.TryGetValue(id, out var relic), "missing " + id);
            Assert.AreEqual(RelicDecks.Live, relic.DeckId, id);
            Assert.AreEqual(rarity, relic.Rarity, id);
            Assert.IsFalse(string.IsNullOrEmpty(relic.DisplayName), id + " displayName");
            Assert.IsTrue(
                CardPresentationAuthority.TryGetOwnedDescription(id, out var description),
                id + " description");
            Assert.IsFalse(string.IsNullOrWhiteSpace(description), id + " description blank");
            Assert.Greater(relic.EffectIds.Count, 0, id + " needs effect assemblies");
        }

        private static void AssertPoolContains(
            GameContentCatalog catalog,
            string poolId,
            string relicId)
        {
            Assert.IsTrue(catalog.Rewards.TryGetPool(poolId, out var pool), poolId);
            var found = false;
            for (var i = 0; i < pool.Entries.Count; i++)
            {
                if (pool.Entries[i].DefId == relicId)
                {
                    found = true;
                    break;
                }
            }

            Assert.IsTrue(found, poolId + " missing " + relicId);
        }
    }
}
