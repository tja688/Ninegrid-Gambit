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
    /// #121：泡沫盔甲 — 本关首次甲归零后武装下一击首段免疫；归零击本身不吃盾；多段只免第一段。
    /// Seam：IActionPipelineSystem.Enqueue(DealDamageAction) + ActivateRelic。
    /// QuickTest：授予 relic.foam_armor，承伤打穿护甲后再挨一击应免疫首段。
    /// </summary>
    public sealed class RelicR3FoamContractTests
    {
        private const string FoamId = "relic.foam_armor";
        private const string CanArmKey = "relic.foam_armor.can_arm";

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
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 121UL, AvatarArmor = 0 });
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
        public void Bootstrap_FoamArmor_LiveWhitePoolEligible_WithAssemblies()
        {
            var catalog = ContentCatalogBootstrap.Load();
            AssertRelicBootstrap(catalog, FoamId, ContentRarity.White);
            AssertPoolContains(catalog, "relic.common_chest", FoamId);
            AssertPoolContains(catalog, "relic.blood_conversion", FoamId);
        }

        [Test]
        public void FoamArmor_GrantsBaseArmorPlusOne()
        {
            mArch.GetSystem<IContentSystem>().ActivateRelic(FoamId);
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);

            var avatar = GetAvatar();
            Assert.AreEqual(1, mStats.GetEffectiveInt(avatar, StatId.Armor));
            Assert.AreEqual(1, StatArmorUtility.GetCurrentArmor(avatar));
            Assert.AreEqual(1, avatar.Counters.Get(CanArmKey), "开局应允许武装");
        }

        [Test]
        public void FoamArmor_ZeroingHitIsNotShielded_NextHitIsImmune_ThenSpent()
        {
            mArch.GetSystem<IContentSystem>().ActivateRelic(FoamId);
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);

            var avatar = GetAvatar();
            StatArmorUtility.SetCurrentArmor(avatar, 2);
            var hp0 = (int)avatar.Stats.GetBase(StatId.Hp);

            // 归零击：2 甲被打穿，溢出 1 点应扣血；本击不吃盾。
            mPipeline.Enqueue(new DealDamageAction(0, avatar.Uid, 3, "test.foam.zero"));
            mPipeline.RunToCompletion();
            Assert.AreEqual(0, StatArmorUtility.GetCurrentArmor(avatar));
            Assert.AreEqual(hp0 - 1, (int)avatar.Stats.GetBase(StatId.Hp), "归零击本身不应免疫");
            Assert.AreEqual(0, avatar.Counters.Get(CanArmKey), "归零后应武装并耗尽本关配额");

            var hp1 = (int)avatar.Stats.GetBase(StatId.Hp);
            mPipeline.Enqueue(new DealDamageAction(0, avatar.Uid, 4, "test.foam.shield"));
            mPipeline.RunToCompletion();
            Assert.AreEqual(hp1, (int)avatar.Stats.GetBase(StatId.Hp), "下一击应全额免疫");

            var hp2 = (int)avatar.Stats.GetBase(StatId.Hp);
            mPipeline.Enqueue(new DealDamageAction(0, avatar.Uid, 2, "test.foam.after"));
            mPipeline.RunToCompletion();
            Assert.AreEqual(hp2 - 2, (int)avatar.Stats.GetBase(StatId.Hp), "盾消耗后应正常受伤");
        }

        [Test]
        public void FoamArmor_DoesNotRearmSameNode_AfterShieldConsumed()
        {
            mArch.GetSystem<IContentSystem>().ActivateRelic(FoamId);
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);

            var avatar = GetAvatar();
            StatArmorUtility.SetCurrentArmor(avatar, 1);

            mPipeline.Enqueue(new DealDamageAction(0, avatar.Uid, 1, "test.foam.break1"));
            mPipeline.RunToCompletion();
            Assert.AreEqual(0, avatar.Counters.Get(CanArmKey));

            mPipeline.Enqueue(new DealDamageAction(0, avatar.Uid, 3, "test.foam.consume"));
            mPipeline.RunToCompletion();

            // 人为回甲再归零：同关不应再武装。
            StatArmorUtility.SetCurrentArmor(avatar, 2);
            var hpBefore = (int)avatar.Stats.GetBase(StatId.Hp);
            mPipeline.Enqueue(new DealDamageAction(0, avatar.Uid, 2, "test.foam.break2"));
            mPipeline.RunToCompletion();
            Assert.AreEqual(0, StatArmorUtility.GetCurrentArmor(avatar));
            Assert.AreEqual(0, avatar.Counters.Get(CanArmKey));

            mPipeline.Enqueue(new DealDamageAction(0, avatar.Uid, 5, "test.foam.no_shield"));
            mPipeline.RunToCompletion();
            Assert.AreEqual(hpBefore - 5, (int)avatar.Stats.GetBase(StatId.Hp), "同关二次归零后不应再免疫");
        }

        [Test]
        public void FoamArmor_MultiSegment_OnlyFirstSegmentCancelled()
        {
            mArch.GetSystem<IContentSystem>().ActivateRelic(FoamId);
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);

            var avatar = GetAvatar();
            StatArmorUtility.SetCurrentArmor(avatar, 1);
            mPipeline.Enqueue(new DealDamageAction(0, avatar.Uid, 1, "test.foam.arm"));
            mPipeline.RunToCompletion();

            var hp0 = (int)avatar.Stats.GetBase(StatId.Hp);
            // 多段 = 连续两次 DealDamageAction（与 Sequence 多段同构）。
            mPipeline.Enqueue(new DealDamageAction(0, avatar.Uid, 3, "test.foam.seg1"));
            mPipeline.Enqueue(new DealDamageAction(0, avatar.Uid, 2, "test.foam.seg2"));
            mPipeline.RunToCompletion();

            Assert.AreEqual(hp0 - 2, (int)avatar.Stats.GetBase(StatId.Hp), "仅第一段免疫，第二段仍扣血");
        }

        [Test]
        public void FoamArmor_RearmsOnNewNode()
        {
            mArch.GetSystem<IContentSystem>().ActivateRelic(FoamId);
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);

            var avatar = GetAvatar();
            StatArmorUtility.SetCurrentArmor(avatar, 1);
            mPipeline.Enqueue(new DealDamageAction(0, avatar.Uid, 1, "test.foam.spend"));
            mPipeline.RunToCompletion();
            Assert.AreEqual(0, avatar.Counters.Get(CanArmKey));

            mPipeline.Enqueue(new NodeStartedAction());
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            Assert.AreEqual(1, avatar.Counters.Get(CanArmKey), "新关卡应重新允许武装");
        }

        [Test]
        public void FoamArmor_UnspentShieldClearsOnNewNode()
        {
            mArch.GetSystem<IContentSystem>().ActivateRelic(FoamId);
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);

            var avatar = GetAvatar();
            StatArmorUtility.SetCurrentArmor(avatar, 1);
            mPipeline.Enqueue(new DealDamageAction(0, avatar.Uid, 1, "test.foam.arm_only"));
            mPipeline.RunToCompletion();
            Assert.AreEqual(0, avatar.Counters.Get(CanArmKey));

            mPipeline.Enqueue(new NodeStartedAction());
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            Assert.AreEqual(1, avatar.Counters.Get(CanArmKey));

            var hp0 = (int)avatar.Stats.GetBase(StatId.Hp);
            mPipeline.Enqueue(new DealDamageAction(0, avatar.Uid, 3, "test.foam.no_carry"));
            mPipeline.RunToCompletion();
            Assert.AreEqual(hp0 - 3, (int)avatar.Stats.GetBase(StatId.Hp), "未消耗的泡沫盾不应跨关残留");
        }

        private CardInstance GetAvatar()
        {
            var board = mArch.GetModel<BoardModel>();
            return mArch.GetModel<CardRegistry>().Get(board.AvatarUid.Value);
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
