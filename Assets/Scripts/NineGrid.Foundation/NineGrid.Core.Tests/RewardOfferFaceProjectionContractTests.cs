using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// #62：奖励提供类指令携带候选项展示用攻/甲/血绝对值（「拿了就是」）。
    /// </summary>
    public sealed class RewardOfferFaceProjectionContractTests
    {
        private IArchitecture mArch;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, BuildCatalog());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 62UL });
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
            mArch = null;
        }

        [Test]
        public void OfferRewardChoice_ProjectsFaceAbsolutes_OntoPendingAndMessage()
        {
            var pipeline = mArch.GetSystem<IActionPipelineSystem>();
            var startIndex = pipeline.EventLog.Entries.Count;

            pipeline.Enqueue(new OfferRewardChoiceAction("help.choice", 3));
            Assert.Greater(pipeline.RunToCompletion(), 0);

            var pending = mArch.GetModel<PendingChoiceModel>();
            Assert.AreEqual(PendingChoiceKind.Reward, pending.Kind.Value);
            Assert.Greater(pending.RewardOptions.Count, 0);

            var content = mArch.GetSystem<IContentSystem>();
            for (var i = 0; i < pending.RewardOptions.Count; i++)
            {
                var entry = pending.RewardOptions[i];
                var draft = content.CreateDraft(entry.DefId);
                var expectedHp = draft.Hp > 0 ? draft.Hp : draft.MaxHp;
                Assert.AreEqual(draft.Attack, entry.Attack, entry.DefId + " Attack");
                Assert.AreEqual(draft.Armor, entry.Armor, entry.DefId + " Armor");
                Assert.AreEqual(expectedHp, entry.Hp, entry.DefId + " Hp");
            }

            CoreGameEvent rewardOffered = null;
            var entries = pipeline.EventLog.Entries;
            for (var i = startIndex; i < entries.Count; i++)
            {
                if (entries[i].Type == CoreEventType.RewardOffered)
                {
                    rewardOffered = entries[i];
                    break;
                }
            }

            Assert.IsNotNull(rewardOffered, "应发出 RewardOffered");
            Assert.IsTrue(
                RewardOfferFaceEncoding.TryParse(rewardOffered.Message, out var poolId, out var parsed),
                "Message 应可解析投影绝对值");
            Assert.AreEqual("help.choice", poolId);
            Assert.AreEqual(pending.RewardOptions.Count, parsed.Count);
            for (var i = 0; i < parsed.Count; i++)
            {
                Assert.AreEqual(pending.RewardOptions[i].DefId, parsed[i].DefId);
                Assert.AreEqual(pending.RewardOptions[i].Attack, parsed[i].Attack);
                Assert.AreEqual(pending.RewardOptions[i].Armor, parsed[i].Armor);
                Assert.AreEqual(pending.RewardOptions[i].Hp, parsed[i].Hp);
            }
        }

        private static GameContentCatalog BuildCatalog()
        {
            var catalog = new GameContentCatalog();
            catalog.AddCard(new CardContentDefinition("help.gold_card", "金币卡", CardKind.HelpCard)
                .WithStats(0, 0, 0));
            catalog.AddCard(new CardContentDefinition("help.throwing_knife", "飞刀", CardKind.HelpCard)
                .WithStats(1, 3, 2));
            catalog.AddCard(new CardContentDefinition("help.healing_potion", "恢复药水", CardKind.HelpCard)
                .WithStats(4, 0, 1));

            catalog.Rewards.AddPool(new RewardPoolDefinition("help.choice", 3)
                .Add("help.gold_card", CardKind.HelpCard, 40)
                .Add("help.throwing_knife", CardKind.HelpCard, 30)
                .Add("help.healing_potion", CardKind.HelpCard, 30));

            return catalog;
        }
    }
}
