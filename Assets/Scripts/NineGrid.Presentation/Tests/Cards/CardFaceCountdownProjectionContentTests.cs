using System.Collections.Generic;
using NineGrid.Cards.Presentation;
using NineGrid.Content.CardPresentation;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests.Cards
{
    /// <summary>
    /// 倒计时卡局内描述投影端到端契约（ADR-0035 / 倒计时票）：
    /// 磁盘内容走投影缝——Inspect 恒静态检查描述（初始实参插值）；Instance 无已提交剩余时
    /// 与检查同数值；有已提交剩余（Settled）时显示「剩余N次移动后…」。
    /// #157：trap.revive_stone 亦已接线；清除已提交剩余后回退初始阈值。
    /// </summary>
    public sealed class CardFaceCountdownProjectionContentTests
    {
        [SetUp]
        public void SetUp()
        {
            CardPresentationConfigCatalog.Invalidate();
        }

        [TearDown]
        public void TearDown()
        {
            CardPresentationConfigCatalog.Invalidate();
        }

        [Test]
        public void Flame_Instance_NoCommittedRemaining_ShowsInitialEvery()
        {
            Assert.IsTrue(CardPresentationConfigCatalog.TryGet("trap.flame", out var dto), "trap.flame JSON 应可加载");
            Assert.IsNotNull(dto);

            var output = CardFaceDescriptionProjector.Project(
                CardDescriptionProjectionMode.Instance,
                dto.description,
                dto.liveTemplate,
                dto.effectAssemblies);

            Assert.AreEqual("每移动对[adjacent]的玩家造成1点伤害；剩余3次移动后[death]", output);
        }

        [Test]
        public void Flame_Instance_CommittedRemaining_OverridesEvery()
        {
            Assert.IsTrue(CardPresentationConfigCatalog.TryGet("trap.flame", out var dto));
            Assert.IsNotNull(dto);

            var remaining = new Dictionary<string, string>
            {
                { "trap.flame.remove.every", "1" },
            };
            var output = CardFaceDescriptionProjector.Project(
                CardDescriptionProjectionMode.Instance,
                dto.description,
                dto.liveTemplate,
                dto.effectAssemblies,
                remaining);

            Assert.AreEqual("每移动对[adjacent]的玩家造成1点伤害；剩余1次移动后[death]", output);
        }

        [Test]
        public void Flame_Inspect_IgnoresCommittedRemaining_StaticCadence()
        {
            Assert.IsTrue(CardPresentationConfigCatalog.TryGet("trap.flame", out var dto));
            Assert.IsNotNull(dto);

            var remaining = new Dictionary<string, string>
            {
                { "trap.flame.remove.every", "1" },
            };
            var output = CardFaceDescriptionProjector.Project(
                CardDescriptionProjectionMode.Inspect,
                dto.description,
                dto.liveTemplate,
                dto.effectAssemblies,
                remaining);

            Assert.AreEqual("每移动1次，对[adjacent]的玩家造成1点伤害，3次后[death]", output);
        }

        [Test]
        public void ReviveStone_Instance_NoCommittedRemaining_ShowsInitialEvery()
        {
            Assert.IsTrue(CardPresentationConfigCatalog.TryGet("trap.revive_stone", out var dto), "trap.revive_stone JSON 应可加载");
            Assert.IsNotNull(dto);

            var output = CardFaceDescriptionProjector.Project(
                CardDescriptionProjectionMode.Instance,
                dto.description,
                dto.liveTemplate,
                dto.effectAssemblies);

            Assert.AreEqual("剩余6次互动后[death]，将1张巨剑骷髅打出到同格", output);
        }

        [Test]
        public void ReviveStone_Instance_CommittedRemaining_OverridesEvery()
        {
            Assert.IsTrue(CardPresentationConfigCatalog.TryGet("trap.revive_stone", out var dto));
            Assert.IsNotNull(dto);

            var remaining = new Dictionary<string, string>
            {
                { "trap.revive_stone.interact.every", "2" },
            };
            var output = CardFaceDescriptionProjector.Project(
                CardDescriptionProjectionMode.Instance,
                dto.description,
                dto.liveTemplate,
                dto.effectAssemblies,
                remaining);

            Assert.AreEqual("剩余2次互动后[death]，将1张巨剑骷髅打出到同格", output);
        }

        [Test]
        public void ReviveStone_Inspect_IgnoresCommittedRemaining_StaticCadence()
        {
            Assert.IsTrue(CardPresentationConfigCatalog.TryGet("trap.revive_stone", out var dto));
            Assert.IsNotNull(dto);

            var remaining = new Dictionary<string, string>
            {
                { "trap.revive_stone.interact.every", "2" },
            };
            var output = CardFaceDescriptionProjector.Project(
                CardDescriptionProjectionMode.Inspect,
                dto.description,
                dto.liveTemplate,
                dto.effectAssemblies,
                remaining);

            Assert.AreEqual("[action]6次后[death]，将1张巨剑骷髅打出到同格", output);
        }

        [Test]
        public void ReviveStone_ScopeMarker_NeverInRenderedText()
        {
            // #157：作用域标记仅作者/系统可见——渲染字符串不得含 battle/run 标记。
            Assert.IsTrue(CardPresentationConfigCatalog.TryGet("trap.revive_stone", out var dto));
            Assert.IsNotNull(dto);

            var remaining = new Dictionary<string, string>
            {
                { "trap.revive_stone.interact.every", "2" },
            };
            var output = CardFaceDescriptionProjector.Project(
                CardDescriptionProjectionMode.Instance,
                dto.description,
                dto.liveTemplate,
                dto.effectAssemblies,
                remaining);

            Assert.IsFalse(output.IndexOf("battle", System.StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.IsFalse(output.IndexOf("run", System.StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}
