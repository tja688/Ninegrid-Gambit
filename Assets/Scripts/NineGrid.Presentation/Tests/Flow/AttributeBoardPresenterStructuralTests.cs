using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests.Flow
{
    /// <summary>
    /// #137 护栏：属性房三选二表现接线——点击必须经 RewardChoiceCoreHook
    /// （IntentIntake → Core SelectReward），禁止 View 直改 Model；重复点击同实例须拦截；
    /// 候选随会话清理。
    /// </summary>
    public sealed class AttributeBoardPresenterStructuralTests
    {
        private static string PresenterPath => Path.GetFullPath(Path.Combine(
            Application.dataPath,
            "Scripts",
            "NineGrid.Presentation",
            "Flow",
            "AttributeBoard",
            "AttributeBoardPresenter.cs"));

        [Test]
        public void Click_GoesThroughRewardChoiceCoreHook()
        {
            var text = File.ReadAllText(PresenterPath);
            Assert.IsTrue(
                text.IndexOf("RewardChoiceCoreHook.SelectReward", StringComparison.Ordinal) >= 0,
                "候选点击须经 RewardChoiceCoreHook.SelectReward（IntentIntake → Core）");
            Assert.IsTrue(
                text.IndexOf("RewardChoiceCoreHook.RequestWire", StringComparison.Ordinal) >= 0,
                "须先 RequestWire 确保输入 Controller 已注册");
            Assert.IsTrue(
                text.IndexOf("RewardChoiceCoreHook.SkipHelpChoice", StringComparison.Ordinal) >= 0,
                "离开须经 RewardChoiceCoreHook.SkipHelpChoice");
        }

        [Test]
        public void Click_DoesNotTouchCoreModelDirectly()
        {
            var text = File.ReadAllText(PresenterPath);
            Assert.IsFalse(
                text.IndexOf("PendingChoiceModel", StringComparison.Ordinal) >= 0
                && text.IndexOf("AddAttributeSelection", StringComparison.Ordinal) >= 0,
                "Presenter 不得直接写 PendingChoiceModel 选择状态");
            Assert.IsFalse(
                text.IndexOf("GetSystem<IPhaseSystem>().SelectReward", StringComparison.Ordinal) >= 0,
                "Presenter 不得旁路 PhaseSystem.SelectReward");
        }

        [Test]
        public void Picks_AreNotPulledToHandOrItemSlots()
        {
            // ADR-0031：选择结果进本关开局注入容器，不写道具卡格、不接入手牌。
            var text = File.ReadAllText(PresenterPath);
            Assert.IsFalse(
                text.IndexOf("InRoomItemAcquirePresentation", StringComparison.Ordinal) >= 0,
                "属性房候选不得接入手牌（商店/特殊房货架语义）");
            Assert.IsFalse(
                text.IndexOf("PullFromGroundAsync", StringComparison.Ordinal) >= 0,
                "属性房候选不得 PullFromGround 入手牌");
        }

        [Test]
        public void DuplicateClickOnSameInstance_IsIgnored()
        {
            var text = File.ReadAllText(PresenterPath);
            Assert.IsTrue(
                text.IndexOf("mSelectedFlags[candidateIndex]", StringComparison.Ordinal) >= 0,
                "须以候选实例选中标记拦截重复点击");
            Assert.IsTrue(
                text.IndexOf("AttributePickIndexResolver.ResolveCurrentPendingIndex", StringComparison.Ordinal) >= 0,
                "点击须把视觉候选解析为当前 Pending 索引（实例移除后索引会前移）");
        }

        [Test]
        public void FirstPick_KeepsCandidateAndWaitsForSecond()
        {
            var text = File.ReadAllText(PresenterPath);
            Assert.IsTrue(
                text.IndexOf("RewardSystem.AttributePickCount", StringComparison.Ordinal) >= 0,
                "须按 Core AttributePickCount 判断是否选满");
            Assert.IsTrue(
                text.IndexOf("SelectedCount() < RewardSystem.AttributePickCount", StringComparison.Ordinal) >= 0,
                "第一次选择后不得结束会话，须保留视觉确认继续等待第二次");
        }

        [Test]
        public void Despawn_ReleasesCandidatesAndClearsOccupancy()
        {
            var text = File.ReadAllText(PresenterPath);
            Assert.IsTrue(
                text.IndexOf("cards.Release(card, \"AttributeBoard.Despawn\")", StringComparison.Ordinal) >= 0,
                "DespawnAll 须释放候选卡");
            Assert.IsTrue(
                text.IndexOf("RoomIconOccupancy.Current.Clear()", StringComparison.Ordinal) >= 0,
                "DespawnAll 须清场地软占登记");
            Assert.IsTrue(
                text.IndexOf("BoardBriefTipPresenter.InstanceOrNull()?.HardClear()", StringComparison.Ordinal) >= 0,
                "DespawnAll 须硬清简要解释/Notice");
        }
    }
}
