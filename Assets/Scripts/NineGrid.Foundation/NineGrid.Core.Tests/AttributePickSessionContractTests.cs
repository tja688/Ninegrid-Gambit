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
    /// #136 属性房三选二：进房生成 3 个加权候选（策划权重为准，允许同种重复）→ 玩家选 2
    /// → 选择结果提交 RunModel 并推进节点 → 下一战斗开局注入选择结果（不再自动随机注入）。
    /// Seam：EnterRoom → ResolveRoom(Attribute) → PendingChoiceModel；BuildNodeDeckOptions 消费 RunModel.AttributePickDefIds。
    /// </summary>
    public sealed class AttributePickSessionContractTests
    {
        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IActionPipelineSystem mPipeline;
        private IRewardSystem mReward;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, BuildCatalog());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 42UL });
            mPhase = mArch.GetSystem<IPhaseSystem>();
            mPipeline = mArch.GetSystem<IActionPipelineSystem>();
            mReward = mArch.GetSystem<IRewardSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
            mArch = null;
            mPhase = null;
            mPipeline = null;
            mReward = null;
        }

        [Test]
        public void EnterAttributeRoom_OffersThreeWeightedCandidates()
        {
            EnterAttributeRoom();
            var pending = mArch.GetModel<PendingChoiceModel>();
            Assert.AreEqual(PendingChoiceKind.AttributePick, pending.Kind.Value);
            Assert.AreEqual(PendingChoiceModel.AttributePickPoolId, pending.PoolId.Value);
            Assert.AreEqual(3, pending.RewardOptions.Count);
            Assert.AreEqual(GamePhase.RewardItemChoice, mPhase.CurrentPhase);
            for (var i = 0; i < pending.RewardOptions.Count; i++)
            {
                Assert.IsTrue(IsAttributeCard(pending.RewardOptions[i].DefId), "候选必须来自属性池");
            }
        }

        [Test]
        public void CandidatePool_FollowsPlanningWeights_AllThreeKindsAppear()
        {
            var seen = new HashSet<string>();
            for (var seed = 1UL; seed <= 60UL; seed++)
            {
                ResetWithSeed(seed);
                EnterAttributeRoom();
                var pending = mArch.GetModel<PendingChoiceModel>();
                Assert.AreEqual(3, pending.RewardOptions.Count);
                for (var i = 0; i < pending.RewardOptions.Count; i++)
                {
                    Assert.IsTrue(IsAttributeCard(pending.RewardOptions[i].DefId));
                    seen.Add(pending.RewardOptions[i].DefId);
                }
            }

            Assert.IsTrue(seen.Contains("help.hp_card"), "血量卡 40% 权重应出现");
            Assert.IsTrue(seen.Contains("help.armor_card"), "加甲卡 40% 权重应出现");
            Assert.IsTrue(seen.Contains("help.attack_card"), "加攻卡 20% 权重应出现");
        }

        [Test]
        public void FirstSelection_KeepsSessionOpen_AndRemovesCandidateInstance()
        {
            EnterAttributeRoom();
            var pending = mArch.GetModel<PendingChoiceModel>();
            var firstEntry = pending.RewardOptions[0];
            var nodeBefore = mArch.GetModel<RunModel>().NodeIndex.Value;

            Assert.IsTrue(mPhase.SelectReward(0).Accepted);
            Assert.AreEqual(PendingChoiceKind.AttributePick, pending.Kind.Value, "第一次选择后会话保持");
            Assert.AreEqual(2, pending.RewardOptions.Count, "选中候选实例从剩余候选移除");
            Assert.IsFalse(ContainsReference(pending.RewardOptions, firstEntry), "同实例不可重复选择");
            Assert.AreEqual(GamePhase.RewardItemChoice, mPhase.CurrentPhase);
            Assert.AreEqual(nodeBefore, mArch.GetModel<RunModel>().NodeIndex.Value, "第一次选择不推进节点");
            Assert.AreEqual(0, mArch.GetModel<RunModel>().AttributePickDefIds.Count, "未选满不提交 RunModel");
        }

        [Test]
        public void SecondSelection_CompletesSession_CommitsPicks_AndAdvancesNode()
        {
            EnterAttributeRoom();
            var pending = mArch.GetModel<PendingChoiceModel>();
            var firstDefId = pending.RewardOptions[0].DefId;
            var secondDefId = pending.RewardOptions[1].DefId;
            var nodeBefore = mArch.GetModel<RunModel>().NodeIndex.Value;
            var deck = mArch.GetModel<DeckModel>();
            var itemSlotsBefore = deck.ItemSlotUids.Count;

            Assert.IsTrue(mPhase.SelectReward(0).Accepted);
            var second = mPhase.SelectReward(0);
            Assert.IsTrue(second.Accepted, second.Reason);
            Assert.AreEqual(PendingChoiceKind.None, pending.Kind.Value, "选满两张后会话结束");
            Assert.AreEqual(nodeBefore + 1, mArch.GetModel<RunModel>().NodeIndex.Value, "节点推进一次");
            Assert.AreEqual(GamePhase.NodeCompleted, mPhase.CurrentPhase);
            Assert.AreEqual(2, mArch.GetModel<RunModel>().AttributePickDefIds.Count);
            Assert.AreEqual(firstDefId, mArch.GetModel<RunModel>().AttributePickDefIds[0]);
            Assert.AreEqual(secondDefId, mArch.GetModel<RunModel>().AttributePickDefIds[1]);
            Assert.AreEqual(itemSlotsBefore, deck.ItemSlotUids.Count, "选择结果不写道具卡格");
        }

        [Test]
        public void SelectionAfterCompletion_IsRejected_NoDuplicateSettlement()
        {
            EnterAttributeRoom();
            var pending = mArch.GetModel<PendingChoiceModel>();
            Assert.IsTrue(mPhase.SelectReward(0).Accepted);
            Assert.IsTrue(mPhase.SelectReward(0).Accepted);
            var picks = new List<string>(mArch.GetModel<RunModel>().AttributePickDefIds);
            var deck = mArch.GetModel<DeckModel>();
            var itemSlots = deck.ItemSlotUids.Count;

            var extra = mPhase.SelectReward(0);
            Assert.IsFalse(extra.Accepted, "会话结束后选择必须被拒绝");
            Assert.AreEqual(itemSlots, deck.ItemSlotUids.Count, "无重复奖励");
            Assert.AreEqual(picks.Count, mArch.GetModel<RunModel>().AttributePickDefIds.Count, "无重复提交");
            Assert.AreEqual(picks[0], mArch.GetModel<RunModel>().AttributePickDefIds[0]);
            Assert.AreEqual(picks[1], mArch.GetModel<RunModel>().AttributePickDefIds[1]);
        }

        [Test]
        public void OutOfRangeSelection_IsRejected()
        {
            EnterAttributeRoom();
            var pending = mArch.GetModel<PendingChoiceModel>();
            Assert.AreEqual(3, pending.RewardOptions.Count);
            Assert.IsFalse(mPhase.SelectReward(3).Accepted);
            Assert.IsFalse(mPhase.SelectReward(-1).Accepted);
            Assert.AreEqual(PendingChoiceKind.AttributePick, pending.Kind.Value, "非法选择不破坏会话");
            Assert.AreEqual(0, mArch.GetModel<RunModel>().AttributePickDefIds.Count);
        }

        [Test]
        public void SkipMidSession_AbandonsPicks_AndAdvancesNode()
        {
            EnterAttributeRoom();
            var pending = mArch.GetModel<PendingChoiceModel>();
            Assert.IsTrue(mPhase.SelectReward(0).Accepted);
            var nodeBefore = mArch.GetModel<RunModel>().NodeIndex.Value;

            var skip = mPhase.SkipHelpChoice();
            Assert.IsTrue(skip.Accepted, skip.Reason);
            Assert.AreEqual(PendingChoiceKind.None, pending.Kind.Value);
            Assert.AreEqual(GamePhase.NodeCompleted, mPhase.CurrentPhase);
            Assert.AreEqual(nodeBefore + 1, mArch.GetModel<RunModel>().NodeIndex.Value);
            Assert.AreEqual(0, mArch.GetModel<RunModel>().AttributePickDefIds.Count, "离开放弃未选完的候选");
            Assert.AreEqual(0, mArch.GetModel<DeckModel>().ItemSlotUids.Count, "离开不发卡");
        }

        [Test]
        public void ItemSlotsFull_DoesNotBlockAttributeSession()
        {
            var registry = mArch.GetModel<CardRegistry>();
            var deck = mArch.GetModel<DeckModel>();
            var content = mArch.GetSystem<IContentSystem>();
            for (var i = 0; i < 3; i++)
            {
                var card = content.CreateDraft("help.hp_card").Create(registry);
                content.ApplyContentToCard(card);
                deck.AddToItemSlots(card);
            }

            Assert.IsTrue(mArch.GetModel<PlayerModel>().IsItemSlotsFull(deck));
            EnterAttributeRoom();

            Assert.IsTrue(mPhase.SelectReward(0).Accepted, "满格不阻止属性房选择");
            var second = mPhase.SelectReward(0);
            Assert.IsTrue(second.Accepted, second.Reason);
            Assert.AreEqual(2, mArch.GetModel<RunModel>().AttributePickDefIds.Count);
            Assert.AreEqual(3, deck.ItemSlotUids.Count, "选择结果不进道具卡格");
        }

        [Test]
        public void CompletedPicks_InjectIntoBattleDeck_ThenCleared()
        {
            EnterAttributeRoom();
            var pending = mArch.GetModel<PendingChoiceModel>();
            var firstDefId = pending.RewardOptions[0].DefId;
            var secondDefId = pending.RewardOptions[1].DefId;
            Assert.IsTrue(mPhase.SelectReward(0).Accepted);
            Assert.IsTrue(mPhase.SelectReward(0).Accepted);
            mArch.GetModel<PlayerModel>().SetItemDeckCapacity(0);
            Assert.AreEqual(GamePhase.NodeCompleted, mPhase.CurrentPhase);

            var options = mReward.BuildNodeDeckOptions(2, null);
            Assert.AreEqual(2, options.PlayerCards.Count);
            Assert.AreEqual(firstDefId, options.PlayerCards[0].DefId);
            Assert.AreEqual(secondDefId, options.PlayerCards[1].DefId);
            Assert.AreEqual(0, mArch.GetModel<RunModel>().AttributePickDefIds.Count, "消费后清空");

            var again = mReward.BuildNodeDeckOptions(2, null);
            Assert.AreEqual(0, again.PlayerCards.Count, "不重复注入");
        }

        [Test]
        public void NewRun_ResetsAttributePicks()
        {
            mArch.GetModel<RunModel>().SetAttributePicks(new[] { "help.hp_card", "help.armor_card" });
            Assert.AreEqual(2, mArch.GetModel<RunModel>().AttributePickDefIds.Count);

            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 7UL });
            Assert.AreEqual(0, mArch.GetModel<RunModel>().AttributePickDefIds.Count, "新 Run 重置三选二选择");
        }

        private void EnterAttributeRoom()
        {
            Assert.IsTrue(mPhase.StartNode(new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 0
            }).Accepted);
            mPipeline.Enqueue(new ChangePhaseAction(GamePhase.RoomEvent));
            mPipeline.RunToCompletion();
            mArch.GetModel<PendingChoiceModel>().SelectRoom(RoomKind.Attribute);

            var enter = mPhase.EnterRoom();
            Assert.IsTrue(enter.Accepted, enter.Reason);
            Assert.AreEqual(GamePhase.RewardItemChoice, mPhase.CurrentPhase);
            Assert.AreEqual(PendingChoiceModel.AttributePickPoolId, mArch.GetModel<PendingChoiceModel>().PoolId.Value);
        }

        private void ResetWithSeed(ulong seed)
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, BuildCatalog());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = seed });
            mPhase = mArch.GetSystem<IPhaseSystem>();
            mPipeline = mArch.GetSystem<IActionPipelineSystem>();
            mReward = mArch.GetSystem<IRewardSystem>();
        }

        private static bool ContainsReference(IReadOnlyList<RewardEntry> entries, RewardEntry target)
        {
            if (entries == null || target == null)
            {
                return false;
            }

            for (var i = 0; i < entries.Count; i++)
            {
                if (ReferenceEquals(entries[i], target))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsAttributeCard(string defId)
        {
            return defId == "help.hp_card"
                || defId == "help.armor_card"
                || defId == "help.attack_card";
        }

        private static GameContentCatalog BuildCatalog()
        {
            var catalog = new GameContentCatalog();
            catalog.Economy.SkipHelpChoiceGold = 10;

            catalog.AddCard(new CardContentDefinition("help.hp_card", "血量卡", CardKind.HelpCard));
            catalog.AddCard(new CardContentDefinition("help.armor_card", "加甲卡", CardKind.HelpCard));
            catalog.AddCard(new CardContentDefinition("help.attack_card", "加攻卡", CardKind.HelpCard));

            catalog.Rewards.AddNodeRule(new NodeDeckRule { NodeIndex = 2, Seq1Count = 1 });
            catalog.Rewards.AddRoom(new RoomDefinition(RoomKind.Attribute, "属性房") { Weight = 10 }
                .AddOpeningInject(new RoomInjectDeclaration
                {
                    Side = RoomInjectSide.Player,
                    SourceKind = RoomInjectSourceKind.WeightedPool,
                    Count = 2,
                    AllowDuplicates = true
                }
                    .AddPoolOption("help.hp_card", 40)
                    .AddPoolOption("help.armor_card", 40)
                    .AddPoolOption("help.attack_card", 20)));

            return catalog;
        }
    }
}
