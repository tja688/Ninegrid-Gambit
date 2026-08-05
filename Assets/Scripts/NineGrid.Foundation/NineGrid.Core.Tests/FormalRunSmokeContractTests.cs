using System.Collections.Generic;
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
    /// #142：正式流程自动烟雾——正式入口（无 QuickTest 载荷）走完整 24 节点骨架
    /// （3 层 × 8 节点）：初始化 Run → 构建节点牌组 → 进入战斗 → 结算信号 → 推进下一节点。
    /// 确定性种子；不断言随机顺序与动画帧。
    /// 覆盖：三层主题不重复绑定、节点 3/6 房间选项、节点 4/7 非战斗推进、
    /// 节点 8 层主与最终 Victory；正式入口无 HP99/ATK5。
    /// </summary>
    public sealed class FormalRunSmokeContractTests
    {
        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IRewardSystem mReward;
        private RunModel mRun;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, ContentCatalogBootstrap.Load());
            // 正式入口：InitialGameFactory 无 QuickTest 载荷；固定种子确定性。
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 42UL });
            mPhase = mArch.GetSystem<IPhaseSystem>();
            mReward = mArch.GetSystem<IRewardSystem>();
            mRun = mArch.GetModel<RunModel>();
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void FormalEntry_AvatarStats_AreProfessionDefaults_NotQuickTestCheats()
        {
            // 正式入口无 HP99/ATK5：Avatar 数值须为职业默认（非 QuickTest 作弊值）。
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var avatar = registry.Get(board.AvatarUid.Value);
            Assert.IsNotNull(avatar);

            var stats = mArch.GetSystem<IStatSystem>();
            var hp = stats.GetEffectiveInt(avatar, StatId.Hp);
            var atk = stats.GetEffectiveInt(avatar, StatId.Attack);
            Assert.AreNotEqual(99, hp, "正式入口不得携带 QuickTest HP99");
            Assert.AreNotEqual(5, atk, "正式入口不得携带 QuickTest ATK5");
            Assert.Greater(hp, 0, "正式 Avatar HP 应为正（数值以内容为准）");
            Assert.Greater(atk, 0, "正式 Avatar ATK 应为正（数值以内容为准）");
        }

        [Test]
        public void FormalSmoke_FirstBattle_EntersLoop_Settles_AdvancesToNextNode()
        {
            // 节点 1：构建节点牌组 → 进入首场战斗 → 结算信号 → 推进下一节点。
            var options = mReward.BuildNodeDeckOptions(1, null);
            Assert.IsNotNull(options);
            Assert.Greater(options.EnemyCards.Count, 0, "节点 1 必须能抽出怪物");

            var start = mPhase.StartNode(options);
            Assert.IsTrue(start.Accepted, start.Reason);
            Assert.AreEqual(GamePhase.InteractionLoop, mPhase.CurrentPhase, "正式首场战斗应进入 InteractionLoop");

            mArch.GetModel<BattleContextModel>().MarkLeaveTrapBroken();
            var settle = mPhase.TryCompleteClearedNode();
            Assert.IsTrue(settle.Accepted, settle.Reason);
            Assert.AreEqual(GamePhase.RoomChoice, mPhase.CurrentPhase, "清关后应回到 RoomChoice");

            // 推进下一节点。
            Assert.IsTrue(mPhase.CanExecute(GameCommandKind.SelectRoom));
            var select = mPhase.SelectRoom(0);
            Assert.IsTrue(select.Accepted, select.Reason);
            Assert.IsTrue(mPhase.EnterRoom().Accepted);
            Assert.AreEqual(GamePhase.NodeCompleted, mPhase.CurrentPhase);
            Assert.IsTrue(mPhase.CanExecute(GameCommandKind.StartNode), "推进后应能 StartNode 下一节点");
        }

        [Test]
        public void FullRun_24Nodes_ReachesVictory_Deterministically()
        {
            // 整局 24 节点骨架（3 层 × 8 节点），固定种子可重复。
            var floorThemes = new List<string>();
            for (var floor = 1; floor <= RunModel.FinalFloor; floor++)
            {
                for (var displayNode = 1; displayNode <= RunModel.NodesPerFloor; displayNode++)
                {
                    var contentNodeIndex = (floor - 1) * RunModel.NodesPerFloor + displayNode;
                    var expectedDisplay = mRun.NodeIndex.Value + 1;
                    Assert.AreEqual(
                        displayNode,
                        expectedDisplay,
                        "RunModel.NodeIndex 应在层内 0..7（display " + displayNode + "）");

                    WalkNode(contentNodeIndex, displayNode, floor == RunModel.FinalFloor && displayNode == RunModel.NodesPerFloor);

                    // 每层节点 1 是战斗节点，构建牌组时绑定本层主题；层推进后才清空，此刻捕获。
                    if (displayNode == 1)
                    {
                        var floorDeck = mRun.FloorMonsterDeckId.Value;
                        Assert.IsFalse(string.IsNullOrEmpty(floorDeck), "第 " + floor + " 层必须绑定主题卡组");
                        Assert.IsFalse(floorThemes.Contains(floorDeck), "跨层不得重复主题：" + floorDeck);
                        floorThemes.Add(floorDeck);
                    }
                }
            }

            Assert.AreEqual(3, floorThemes.Count, "三层主题互不重复");
            Assert.AreEqual(RunModel.FinalFloor, mRun.Floor.Value);
            Assert.AreEqual(GamePhase.Victory, mPhase.CurrentPhase, "第 3 层节点 8 通关应进入 Victory");
        }

        /// <summary>走一个节点：战斗节点 StartNode→结算；非战斗节点直接 RoomChoice；最后 SelectRoom+EnterRoom 推进。</summary>
        private void WalkNode(int contentNodeIndex, int displayNode, bool finalFloorNode8)
        {
            if (MapNodeProgression.EntersInteractionLoop(mRun.NodeIndex.Value))
            {
                var options = mReward.BuildNodeDeckOptions(contentNodeIndex, null);
                Assert.IsNotNull(options, "节点 " + contentNodeIndex + " 必须能构建牌组");
                Assert.Greater(options.EnemyCards.Count, 0, "战斗节点 " + contentNodeIndex + " 必须能抽出怪物（跨层不得 fallback）");

                if (displayNode == RunModel.NodesPerFloor)
                {
                    Assert.IsTrue(options.RequireElite, "节点 8 必须要求层主（Seq5Count=1）");
                    Assert.IsTrue(ContainsFloorBoss(options), "节点 8 必须包含该层 seq5 层主");
                }

                var start = mPhase.StartNode(options);
                Assert.IsTrue(start.Accepted, "节点 " + contentNodeIndex + " StartNode 被拒: " + start.Reason);
                Assert.AreEqual(GamePhase.InteractionLoop, mPhase.CurrentPhase);

                // 结算信号：离开机关击破 → 清关。
                mArch.GetModel<BattleContextModel>().MarkLeaveTrapBroken();
                var settle = mPhase.TryCompleteClearedNode();
                Assert.IsTrue(settle.Accepted, settle.Reason);
            }
            else
            {
                // 节点 4/7：非战斗推进，不进 InteractionLoop。
                var start = mPhase.StartNode(NodeDeckOptions.CreateDefaultBattle());
                Assert.IsTrue(start.Accepted, "非战斗节点 " + contentNodeIndex + " StartNode 被拒: " + start.Reason);
                Assert.AreNotEqual(GamePhase.InteractionLoop, mPhase.CurrentPhase, "节点 4/7 不得进 InteractionLoop");
            }

            Assert.AreEqual(GamePhase.RoomChoice, mPhase.CurrentPhase);

            var pending = mArch.GetModel<PendingChoiceModel>();
            if (pending.Kind.Value == PendingChoiceKind.Navigation)
            {
                Assert.IsTrue(mPhase.SelectRoom(0).Accepted, "导航推进 SelectRoom 被拒");
            }
            else
            {
                Assert.AreEqual(PendingChoiceKind.Room, pending.Kind.Value);
                Assert.IsNotEmpty(pending.RoomOptions, "节点 " + contentNodeIndex + " 应放出房间选项");
                if (displayNode == 3)
                {
                    AssertAllIn(pending.RoomOptions, new[] { RoomKind.Shop, RoomKind.Tavern }, "节点 3 只应放出消费房（ConsumerRooms）");
                }
                else if (displayNode == 6)
                {
                    AssertAllIn(pending.RoomOptions, new[] { RoomKind.TreasureReward, RoomKind.ItemReward }, "节点 6 只应放出特殊房（SpecialRooms）");
                }

                Assert.IsTrue(mPhase.SelectRoom(0).Accepted, "选房 SelectRoom 被拒");
            }

            Assert.IsTrue(mPhase.EnterRoom().Accepted, "EnterRoom 被拒");

            // 消费/特殊/属性房进房后进入房内会话（RewardItemChoice）：经离开推进节点（与正式流程一致）。
            if (mPhase.CurrentPhase == GamePhase.RewardItemChoice)
            {
                var leave = mPhase.SkipHelpChoice();
                Assert.IsTrue(leave.Accepted, "房内会话离开被拒: " + leave.Reason);
            }

            if (finalFloorNode8)
            {
                Assert.AreEqual(GamePhase.Victory, mPhase.CurrentPhase, "第 3 层节点 8 EnterRoom 后应 Victory");
            }
            else
            {
                Assert.AreEqual(GamePhase.NodeCompleted, mPhase.CurrentPhase, "节点 " + contentNodeIndex + " EnterRoom 后应 NodeCompleted");
            }
        }

        private bool ContainsFloorBoss(NodeDeckOptions options)
        {
            var content = mArch.GetSystem<IContentSystem>();
            for (var i = 0; i < options.EnemyCards.Count; i++)
            {
                if (content.Catalog.TryGetCard(options.EnemyCards[i].DefId, out var card)
                    && card != null
                    && card.IsBoss
                    && card.Sequence == 5
                    && string.Equals(card.DeckId, mRun.FloorMonsterDeckId.Value, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AssertAllIn(IReadOnlyList<RoomKind> options, RoomKind[] allowed, string message)
        {
            for (var i = 0; i < options.Count; i++)
            {
                var contains = false;
                for (var j = 0; j < allowed.Length; j++)
                {
                    if (options[i] == allowed[j])
                    {
                        contains = true;
                        break;
                    }
                }

                Assert.IsTrue(contains, message + "（出现 " + options[i] + "）");
            }
        }
    }
}
