using System.Collections.Generic;
using NineGrid.Cards.Presentation;
using NineGrid.Content.CardPresentation;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests.Cards
{
    /// <summary>
    /// 首张倒计时卡（trap.flame）局内描述投影端到端契约（ADR-0035 / 倒计时票）：
    /// 磁盘内容走投影缝——Inspect 恒静态检查描述（初始实参插值）；Instance 无已提交剩余时
    /// 与检查同数值；有已提交剩余（Settled）时显示「剩余N次移动后…」。
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
    }
}
