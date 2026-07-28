using NineGrid.Content;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// #57：CardSpawned 携带结算后卡面绝对值（攻/甲/血），供表现层赋值。
    /// </summary>
    public sealed class CardSpawnedFaceAbsoluteTests
    {
        private IArchitecture mArch;
        private IActionPipelineSystem mPipeline;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(
                ContentConfigKeys.DefaultCatalog,
                ContentCatalogBootstrap.Load());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 57UL });
            mPipeline = mArch.GetSystem<IActionPipelineSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void SpawnCard_Emits_CardSpawned_With_Face_Absolutes_Matching_Instance()
        {
            var startIndex = mPipeline.EventLog.Entries.Count;
            mPipeline.Enqueue(new SpawnCardAction(
                "monster.stone_shrimp",
                CardKind.Monster,
                ZoneId.ItemSlots,
                SlotId.None,
                1,
                "test:#57"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            var spawn = FindLastCardSpawned(startIndex);
            Assert.IsNotNull(spawn, "应产出 CardSpawned");
            Assert.Greater(spawn.CardUid, 0);

            var card = mArch.GetModel<CardRegistry>().Get(spawn.CardUid);
            var stats = mArch.GetSystem<IStatSystem>();
            Assert.AreEqual(stats.GetEffectiveInt(card, StatId.Hp), spawn.RemainingHp);
            Assert.AreEqual(StatArmorUtility.GetCurrentArmor(card), spawn.RemainingArmor);
            Assert.AreEqual(stats.GetEffectiveInt(card, StatId.Attack), spawn.ResultValue);
            Assert.Greater(spawn.ResultValue, 0, "石虾攻击应为正，避免默认 0 假绿");
            Assert.Greater(spawn.RemainingHp, 0, "石虾血量应为正");
        }

        [Test]
        public void SpawnCard_Face_Absolutes_Freeze_At_Spawn_Moment()
        {
            var startIndex = mPipeline.EventLog.Entries.Count;
            mPipeline.Enqueue(new SpawnCardAction(
                "monster.stone_shrimp",
                CardKind.Monster,
                ZoneId.ItemSlots,
                SlotId.None,
                1,
                "test:#57-freeze"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            var spawn = FindLastCardSpawned(startIndex);
            Assert.IsNotNull(spawn);
            var card = mArch.GetModel<CardRegistry>().Get(spawn.CardUid);
            var birthAttack = spawn.ResultValue;
            card.Stats.SetBase(StatId.Attack, birthAttack + 99);

            Assert.AreNotEqual(
                (int)card.Stats.GetBase(StatId.Attack),
                spawn.ResultValue,
                "事件绝对值冻结在造卡时刻");
        }

        private CoreGameEvent FindLastCardSpawned(int startIndex)
        {
            var entries = mPipeline.EventLog.Entries;
            CoreGameEvent found = null;
            for (var i = startIndex; i < entries.Count; i++)
            {
                if (entries[i].Type == CoreEventType.CardSpawned && entries[i].CardUid > 0)
                {
                    found = entries[i];
                }
            }

            return found;
        }
    }
}
