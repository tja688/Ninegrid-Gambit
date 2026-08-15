using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NineGrid.Flow.Presentation;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// ADR-0027 addendum / #143 回归：遗物栏满时拒开宝箱类卡并回手（不消耗、不开三选一）。
    /// 覆盖三层防线：
    /// 1. Core 权威门禁（PhaseSystem.ApplyUseItem/ExecuteUseItem）经共享 ChestUseRelicPoolRule 识别；
    /// 2. 表现层 idle 合法性镜像（BoardIntentLegality.TryExplainUseItem）同源拒绝，拖放路径直接回手；
    /// 3. 时间线解算步（BranchableResolveStep）对拒收分支而非静默 Abort。
    /// </summary>
    public class ChestRelicFullRejectTests
    {
        private IArchitecture mArch;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Interface;
            var content = mArch.GetSystem<IContentSystem>();
            content.Load(NineGrid.Content.ContentCatalogBootstrap.Load());
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, content.Catalog);
            Assert.IsTrue(content.HasCatalog, "需要真实内容目录");
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void ChestRule_AllThreeChestCards_Detected()
        {
            var content = mArch.GetSystem<IContentSystem>();
            Assert.IsTrue(
                ChestUseRelicPoolRule.IsChestUseOfferingRelicPool(content, "help.common_chest_card"),
                "普通宝箱应被识别为开遗物类卡");
            Assert.IsTrue(
                ChestUseRelicPoolRule.IsChestUseOfferingRelicPool(content, "help.blue_chest_card"),
                "蓝宝箱应被识别为开遗物类卡");
            Assert.IsTrue(
                ChestUseRelicPoolRule.IsChestUseOfferingRelicPool(content, "help.golden_chest_card"),
                "金宝箱应被识别为开遗物类卡");
        }

        [Test]
        public void ChestRule_NonChestItems_NotDetected()
        {
            var content = mArch.GetSystem<IContentSystem>();
            Assert.IsFalse(
                ChestUseRelicPoolRule.IsChestUseOfferingRelicPool(content, "help.healing_potion"),
                "恢复药水不是开遗物类卡");
            Assert.IsFalse(
                ChestUseRelicPoolRule.IsChestUseOfferingRelicPool(content, "help.attack_card"),
                "属性卡不是开遗物类卡");
            Assert.IsFalse(
                ChestUseRelicPoolRule.IsChestUseOfferingRelicPool(content, "help.common_chest_card_missing"),
                "不存在的卡不应误判");
        }

        [Test]
        public void CoreApplyUseItem_RelicFull_ChestRejected_StaysInItemSlots()
        {
            var snapshot = InitialGameFactory.Create(mArch, new InitialGameOptions());
            var phase = mArch.GetSystem<IPhaseSystem>();
            Assert.IsTrue(phase.StartNode(null).Accepted, "StartNode 应被接受");

            FillRelicInventory();

            var registry = mArch.GetModel<CardRegistry>();
            var deck = mArch.GetModel<DeckModel>();
            var content = mArch.GetSystem<IContentSystem>();
            var chest = content.CreateDraft("help.common_chest_card").Create(registry);
            content.ApplyContentToCard(chest);
            chest.Zone.Value = ZoneId.ItemSlots;
            deck.AddToItemSlots(chest);

            var result = phase.ApplyUseItem(chest.Uid, null, null);

            Assert.IsFalse(result.Accepted, "遗物栏满时开宝箱应被拒绝");
            StringAssert.Contains("满", result.Reason, "拒绝理由应提示遗物格子已满");
            Assert.AreEqual(
                ZoneId.ItemSlots,
                registry.Get(chest.Uid).Zone.Value,
                "宝箱卡不得被消耗，应仍留在道具卡格");
            Assert.AreEqual(
                1,
                deck.ItemSlotUids.Count,
                "道具卡格注册数不变");

            var pending = mArch.GetModel<PendingChoiceModel>();
            Assert.AreEqual(
                PendingChoiceKind.None,
                pending.Kind.Value,
                "不得打开遗物三选一 PendingChoice");
            Assert.IsTrue(string.IsNullOrEmpty(pending.PoolId.Value), "不得写入遗物奖池 id");
        }

        [Test]
        public void CoreApplyUseItem_RelicNotFull_ChestAccepted()
        {
            var snapshot = InitialGameFactory.Create(mArch, new InitialGameOptions());
            var phase = mArch.GetSystem<IPhaseSystem>();
            Assert.IsTrue(phase.StartNode(null).Accepted, "StartNode 应被接受");

            var registry = mArch.GetModel<CardRegistry>();
            var deck = mArch.GetModel<DeckModel>();
            var content = mArch.GetSystem<IContentSystem>();
            var chest = content.CreateDraft("help.common_chest_card").Create(registry);
            content.ApplyContentToCard(chest);
            chest.Zone.Value = ZoneId.ItemSlots;
            deck.AddToItemSlots(chest);

            var result = phase.ApplyUseItem(chest.Uid, null, null);

            Assert.IsTrue(result.Accepted, "遗物栏未满时开宝箱应被接受: " + result.Reason);
            var pending = mArch.GetModel<PendingChoiceModel>();
            Assert.AreEqual(
                PendingChoiceKind.Reward,
                pending.Kind.Value,
                "应打开遗物三选一 PendingChoice");
            Assert.IsTrue(
                PendingChoiceModel.IsRelicRewardPool(pending.PoolId.Value),
                "奖池应为 relic.*");
        }

        [Test]
        public void BoardIntentLegality_RelicFull_ChestRejected()
        {
            var snapshot = InitialGameFactory.Create(mArch, new InitialGameOptions());
            var phase = mArch.GetSystem<IPhaseSystem>();
            Assert.IsTrue(phase.StartNode(null).Accepted, "StartNode 应被接受");

            FillRelicInventory();

            var registry = mArch.GetModel<CardRegistry>();
            var deck = mArch.GetModel<DeckModel>();
            var content = mArch.GetSystem<IContentSystem>();
            var chest = content.CreateDraft("help.common_chest_card").Create(registry);
            content.ApplyContentToCard(chest);
            chest.Zone.Value = ZoneId.ItemSlots;
            deck.AddToItemSlots(chest);

            string rejectReason;
            var legal = BoardIntentLegality.TryExplainUseItem(
                mArch,
                chest.Uid,
                null,
                null,
                out rejectReason);

            Assert.IsFalse(legal, "表现层 idle 门禁应在拖放路径提前拒绝宝箱使用");
            StringAssert.Contains("满", rejectReason, "拒绝理由应提示遗物格子已满");
        }

        [Test]
        public void BoardIntentLegality_RelicFull_NonChestItem_Allowed()
        {
            var snapshot = InitialGameFactory.Create(mArch, new InitialGameOptions());
            var phase = mArch.GetSystem<IPhaseSystem>();
            Assert.IsTrue(phase.StartNode(null).Accepted, "StartNode 应被接受");

            FillRelicInventory();

            var registry = mArch.GetModel<CardRegistry>();
            var deck = mArch.GetModel<DeckModel>();
            var content = mArch.GetSystem<IContentSystem>();
            var potion = content.CreateDraft("help.healing_potion").Create(registry);
            content.ApplyContentToCard(potion);
            potion.Zone.Value = ZoneId.ItemSlots;
            deck.AddToItemSlots(potion);

            string rejectReason;
            var legal = BoardIntentLegality.TryExplainUseItem(
                mArch,
                potion.Uid,
                null,
                null,
                out rejectReason);

            Assert.IsTrue(
                legal,
                "遗物栏满不应影响非宝箱类道具使用（ADR-0027 addendum 仅对宝箱生效）: " + rejectReason);
        }

        /// <summary>
        /// 时间线拒收分支：解算门返回 Failed 时 BranchableResolveStep 必须标记 Rejected 并 Finished，
        /// 而不是像 ResolveBatchStep 那样 Abort 清空整条主线（那正是「场面出错」的来源之一）。
        /// </summary>
        [Test]
        public void BranchableResolveStep_OnFailedGate_MarksRejectedAndFinishes()
        {
            var gate = new StubGate(BatchOpenResult.Failed);
            var step = new BranchableResolveStepStub(gate);

            Assert.AreEqual(
                TimelineStepStatus.Finished,
                step.Tick(0f),
                "拒收不得 Abort 主线");
            Assert.IsTrue(step.Rejected, "拒收应标记 Rejected 供后续分支承接");
        }

        [Test]
        public void BranchableResolveStep_OnOpenedGate_FinishesWithoutRejected()
        {
            var gate = new StubGate(BatchOpenResult.Opened, 42);
            var step = new BranchableResolveStepStub(gate);

            Assert.AreEqual(
                TimelineStepStatus.Finished,
                step.Tick(0f),
                "正常解算应正常完成");
            Assert.IsFalse(step.Rejected, "正常解算不得标记 Rejected");
        }

        private void FillRelicInventory()
        {
            var player = mArch.GetModel<PlayerModel>();
            for (var i = 0; i < PlayerModel.MaxRelicSlots; i++)
            {
                player.AddRelic("relic.test_fill_" + i);
            }

            Assert.IsTrue(player.IsRelicInventoryFull, "测试前提：遗物栏应已满");
        }

        private sealed class StubGate : IPresentationBatchGate
        {
            private readonly BatchOpenResult mResult;
            private readonly int mBatchId;

            public StubGate(BatchOpenResult result, int batchId = 0)
            {
                mResult = result;
                mBatchId = batchId;
            }

            public bool HasOpenBatch
            {
                get { return false; }
            }

            public int ActiveBatchId
            {
                get { return 0; }
            }

            public BatchOpenResult TryOpenNextBatch(out int batchId)
            {
                batchId = mBatchId;
                return mResult;
            }

            public bool TryAcknowledge(int batchId)
            {
                return true;
            }
        }

        /// <summary>BranchableResolveStep 是 UseItemIntentScriptFactory 的私有嵌套类，经反射驱动。</summary>
        private sealed class BranchableResolveStepStub
        {
            private readonly object mInner;
            private readonly System.Reflection.MethodInfo mTick;

            public BranchableResolveStepStub(IPresentationBatchGate gate)
            {
                var type = typeof(UseItemIntentScriptFactory).GetNestedType(
                    "BranchableResolveStep",
                    System.Reflection.BindingFlags.NonPublic);
                Assert.IsNotNull(type, "BranchableResolveStep 私有嵌套类型应存在");
                mInner = System.Activator.CreateInstance(type, gate);
                mTick = type.GetMethod(
                    "Tick",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            }

            public bool Rejected
            {
                get
                {
                    var prop = mInner.GetType().GetProperty("Rejected");
                    return (bool)prop.GetValue(mInner);
                }
            }

            public TimelineStepStatus Tick(float deltaTime)
            {
                return (TimelineStepStatus)mTick.Invoke(mInner, new object[] { deltaTime });
            }
        }
    }
}
