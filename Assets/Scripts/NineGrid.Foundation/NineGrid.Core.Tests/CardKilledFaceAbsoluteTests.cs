using NineGrid.Content;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// #58：CardKilled 携带结算后剩余血量绝对值，供卡面可见归零。
    /// </summary>
    public sealed class CardKilledFaceAbsoluteTests
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
                TableNineContentCatalog.CreateDefault());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 58UL });
            mPipeline = mArch.GetSystem<IActionPipelineSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void Kill_Emits_CardKilled_With_RemainingHp_Zero()
        {
            var startIndex = mPipeline.EventLog.Entries.Count;
            mPipeline.Enqueue(new SpawnCardAction(
                "monster.stone_shrimp",
                CardKind.Monster,
                ZoneId.ItemSlots,
                SlotId.None,
                1,
                "test:#58"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            var uid = 0;
            var entries = mPipeline.EventLog.Entries;
            for (var i = startIndex; i < entries.Count; i++)
            {
                if (entries[i].Type == CoreEventType.CardSpawned)
                {
                    uid = entries[i].CardUid;
                    break;
                }
            }

            Assert.Greater(uid, 0);

            var killStart = mPipeline.EventLog.Entries.Count;
            mPipeline.Enqueue(new KillAction(killerUid: 0, targetUid: uid));
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            CoreGameEvent killed = null;
            entries = mPipeline.EventLog.Entries;
            for (var i = killStart; i < entries.Count; i++)
            {
                if (entries[i].Type == CoreEventType.CardKilled)
                {
                    killed = entries[i];
                    break;
                }
            }

            Assert.IsNotNull(killed, "应产出 CardKilled");
            Assert.AreEqual(0, killed.RemainingHp, "击杀后剩余血量应为 0");
            Assert.AreEqual(uid, killed.CardUid);
        }
    }
}
