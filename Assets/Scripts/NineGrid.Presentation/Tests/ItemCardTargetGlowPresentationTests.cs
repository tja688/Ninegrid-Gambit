using System.Collections.Generic;
using NineGrid.Cards;
using NineGrid.Cards.Vfx;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Flow;
using NUnit.Framework;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// 道具卡目标与棋盘光圈提醒视觉逻辑验收测试：
    /// 1. 离散/单体指向目标（如飞刀）：合法目标格亮起光圈，非目标格与棋盘大光圈不亮；
    /// 2. 全局/棋盘目标（如治疗药水）：棋盘大光圈亮起，单格光圈不亮；
    /// 3. 多选模式（如交换）：进入时全候选亮起；点选第1张后该卡光圈熄灭且其余候选保持；反选后光圈恢复；选满或中途取消时所有光圈彻底清除无残留。
    /// </summary>
    public class ItemCardTargetGlowPresentationTests
    {
        private IArchitecture mArch;
        private GameObject mTestHostGo;
        private DummyGlowRequester mRequester;

        private class DummyGlowRequester : MonoBehaviour
        {
        }

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Interface;
            mArch.GetModel<RunModel>().SetPhase(GamePhase.InteractionLoop);

            mTestHostGo = new GameObject("TestHost_TargetGlow");
            mRequester = mTestHostGo.AddComponent<DummyGlowRequester>();
        }

        [TearDown]
        public void TearDown()
        {
            BoardRangeGlowFx.ForceHideAll();
            BoardCardSelectModeController.End();

            if (mTestHostGo != null)
            {
                Object.DestroyImmediate(mTestHostGo);
            }

            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void BoardRangeGlowFx_ShowItemTargetSlots_SetsSpecifiedSlots_AndClearsOthers()
        {
            var runner = BoardRangeGlowRunner.Ensure();
            var validSlots = new[] { 2, 3, 6 };

            BoardRangeGlowFx.ShowItemTargetSlots(mRequester, validSlots);

            for (var slot = GroundSlotTopology.MinSlot; slot <= GroundSlotTopology.MaxSlot; slot++)
            {
                if (slot == 2 || slot == 3 || slot == 6)
                {
                    Assert.AreEqual(1f, runner.GetTargetWeight(slot), $"Slot {slot} 应有点亮权重");
                }
                else
                {
                    Assert.AreEqual(0f, runner.GetTargetWeight(slot), $"Slot {slot} 不应有点亮权重");
                }
            }

            Assert.AreEqual(0f, runner.TargetBoardWeight, "单格道具目标时不应点亮棋盘大光圈");

            // 隐藏
            BoardRangeGlowFx.Hide(mRequester);
            for (var slot = GroundSlotTopology.MinSlot; slot <= GroundSlotTopology.MaxSlot; slot++)
            {
                Assert.AreEqual(0f, runner.GetTargetWeight(slot), $"Hide 后 Slot {slot} 权重应归零");
            }
        }

        [Test]
        public void BoardRangeGlowFx_ShowBoardApplyZone_SetsBoardWeight_AndClearsSlotWeights()
        {
            var runner = BoardRangeGlowRunner.Ensure();

            BoardRangeGlowFx.ShowBoardApplyZone(mRequester);

            Assert.AreEqual(1f, runner.TargetBoardWeight, "棋盘目标应点亮大光圈");
            Assert.IsTrue(runner.IsBoardApplyZoneActive, "IsBoardApplyZoneActive 应为 true");

            for (var slot = GroundSlotTopology.MinSlot; slot <= GroundSlotTopology.MaxSlot; slot++)
            {
                Assert.AreEqual(0f, runner.GetTargetWeight(slot), $"棋盘大光圈时 Slot {slot} 不应单独亮起");
            }

            // 隐藏
            BoardRangeGlowFx.Hide(mRequester);
            Assert.AreEqual(0f, runner.TargetBoardWeight, "Hide 后棋盘大光圈权重应归零");
            Assert.IsFalse(runner.IsBoardApplyZoneActive, "Hide 后 IsBoardApplyZoneActive 应为 false");
        }

        [Test]
        public void BoardRangeGlowFx_ShowMultiSelectCandidates_OnlyHighlightsUnselectedCandidates()
        {
            var runner = BoardRangeGlowRunner.Ensure();
            var candidates = new[] { 1, 2, 3, 4 };
            var selected = new[] { 2 };

            BoardRangeGlowFx.ShowMultiSelectCandidates(mRequester, candidates, selected);

            // 候选 1, 3, 4 未被选中，应点亮
            Assert.AreEqual(1f, runner.GetTargetWeight(1), "候选 1 应点亮");
            Assert.AreEqual(0f, runner.GetTargetWeight(2), "已选 2 应熄灭");
            Assert.AreEqual(1f, runner.GetTargetWeight(3), "候选 3 应点亮");
            Assert.AreEqual(1f, runner.GetTargetWeight(4), "候选 4 应点亮");

            // 非候选 5~9 不点亮
            for (var slot = 5; slot <= 9; slot++)
            {
                Assert.AreEqual(0f, runner.GetTargetWeight(slot), $"非候选 {slot} 不应点亮");
            }

            // 更新：2 和 4 被选中
            selected = new[] { 2, 4 };
            BoardRangeGlowFx.ShowMultiSelectCandidates(mRequester, candidates, selected);

            Assert.AreEqual(1f, runner.GetTargetWeight(1), "候选 1 仍应点亮");
            Assert.AreEqual(0f, runner.GetTargetWeight(2), "已选 2 应熄灭");
            Assert.AreEqual(1f, runner.GetTargetWeight(3), "候选 3 仍应点亮");
            Assert.AreEqual(0f, runner.GetTargetWeight(4), "新选中 4 应熄灭");

            // 隐藏
            BoardRangeGlowFx.Hide(mRequester);
            for (var slot = GroundSlotTopology.MinSlot; slot <= GroundSlotTopology.MaxSlot; slot++)
            {
                Assert.AreEqual(0f, runner.GetTargetWeight(slot), $"Hide 后 Slot {slot} 权重应归零");
            }
        }

        [Test]
        public void BoardCardSelectMode_Begin_And_Toggle_SynchronizesCandidateGlows()
        {
            var runner = BoardRangeGlowRunner.Ensure();

            // 开启多选模式（需 2 个目标）
            var started = BoardCardSelectModeController.Begin(100, "help.swap_card", 2);
            Assert.IsTrue(started, "多选模式应能正常启动");
            Assert.IsTrue(BoardCardSelectModeController.IsActive, "多选模式应为 Active");

            // 模拟有候选槽位 1, 2, 3，尚未选中
            BoardRangeGlowFx.ShowMultiSelectCandidates(runner, new[] { 1, 2, 3 }, System.Array.Empty<int>());
            Assert.AreEqual(1f, runner.GetTargetWeight(1));
            Assert.AreEqual(1f, runner.GetTargetWeight(2));
            Assert.AreEqual(1f, runner.GetTargetWeight(3));

            // 点选第 1 张（假设为 slot 1）
            BoardRangeGlowFx.ShowMultiSelectCandidates(runner, new[] { 1, 2, 3 }, new[] { 1 });
            Assert.AreEqual(0f, runner.GetTargetWeight(1), "Slot 1 选中时光圈熄灭");
            Assert.AreEqual(1f, runner.GetTargetWeight(2), "Slot 2 仍为候选，光圈保持");
            Assert.AreEqual(1f, runner.GetTargetWeight(3), "Slot 3 仍为候选，光圈保持");

            // 反选第 1 张（Slot 1 取消选中）
            BoardRangeGlowFx.ShowMultiSelectCandidates(runner, new[] { 1, 2, 3 }, System.Array.Empty<int>());
            Assert.AreEqual(1f, runner.GetTargetWeight(1), "Slot 1 反选后光圈重新亮起");
            Assert.AreEqual(1f, runner.GetTargetWeight(2));
            Assert.AreEqual(1f, runner.GetTargetWeight(3));

            // 选满或结束模式
            BoardCardSelectModeController.End();
            Assert.IsFalse(BoardCardSelectModeController.IsActive);
            for (var slot = GroundSlotTopology.MinSlot; slot <= GroundSlotTopology.MaxSlot; slot++)
            {
                Assert.AreEqual(0f, runner.GetTargetWeight(slot), $"模式结束后 Slot {slot} 权重应彻底归零");
            }
        }

        [Test]
        public void BoardCardSelectMode_AbortByParkedClick_ClearsAllCandidateGlows()
        {
            var runner = BoardRangeGlowRunner.Ensure();

            BoardCardSelectModeController.Begin(200, "help.swap_card", 2);
            BoardCardSelectModeController.SetParkedItem(200);

            BoardRangeGlowFx.ShowMultiSelectCandidates(runner, new[] { 1, 2, 3 }, System.Array.Empty<int>());
            Assert.AreEqual(1f, runner.GetTargetWeight(1));

            // 点击驻留卡反悔取消
            var aborted = BoardCardSelectModeController.TryAbortByParkedItemClick(200);
            Assert.IsTrue(aborted, "点击驻留卡应触发反悔取消");

            for (var slot = GroundSlotTopology.MinSlot; slot <= GroundSlotTopology.MaxSlot; slot++)
            {
                Assert.AreEqual(0f, runner.GetTargetWeight(slot), $"反悔取消后 Slot {slot} 权重应清空");
            }
        }

        [Test]
        public void DoorCard_HasMagicImmunity_IsImmuneToItemTargeting_ReturnsTrue()
        {
            // 离开机关/门卡 trap.leave 或带 door 关键字应被识别为道具免疫
            Assert.IsTrue(
                HelpCardBoardSelectResolver.IsImmuneToItemTargeting(0, "trap.leave"),
                "trap.leave 必须被识别为道具目标免疫");
            Assert.IsTrue(
                HelpCardBoardSelectResolver.IsImmuneToItemTargeting(0, "trap.leave.door"),
                "trap.leave.door 必须被识别为道具目标免疫");
            Assert.IsTrue(
                HelpCardBoardSelectResolver.IsImmuneToItemTargeting(0, "trap.tutorial.door"),
                "trap.tutorial.door 必须被识别为道具目标免疫");
            Assert.IsTrue(
                HelpCardBoardSelectResolver.IsImmuneToItemTargeting(0, "trap.tutorial.boss_door"),
                "trap.tutorial.boss_door 必须被识别为道具目标免疫");

            // 普通怪物与普通机关不应免疫
            Assert.IsFalse(
                HelpCardBoardSelectResolver.IsImmuneToItemTargeting(0, "monster.slime"),
                "普通怪物不能被误判为道具免疫");
            Assert.IsFalse(
                HelpCardBoardSelectResolver.IsImmuneToItemTargeting(0, "trap.bear_trap"),
                "普通机关不能被误判为道具免疫");
        }

        [Test]
        public void DoorCard_IsNotEligibleTargetForBoardCardSelectMode()
        {
            BoardCardSelectModeController.Begin(300, "help.swap_card", 2);

            var doorCard = new ManagedCard(999, "trap.leave", null)
            {
                CoreKind = CardPresentationKind.Trap,
                DisplayMode = CardDisplayMode.GroundCardMode,
            };

            var monsterCard = new ManagedCard(888, "monster.slime", null)
            {
                CoreKind = CardPresentationKind.Monster,
                DisplayMode = CardDisplayMode.GroundCardMode,
            };

            // 门卡即便处于 GroundCardMode 也不能作为合法目标
            Assert.IsFalse(
                BoardCardSelectModeController.IsEligibleTarget(doorCard),
                "门机关卡绝不能作为多选模式的合法目标");

            BoardCardSelectModeController.End();
        }
    }
}
