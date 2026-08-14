using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Flow.BattleLog;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// 战斗日志：每个节点开始应记录开战基础护甲（resetCurrentArmor / AvatarAppeared）。
    /// </summary>
    public class BattleLogOpeningBaselineArmorTests
    {
        [Test]
        public void TryFindOpeningBaselineArmor_PrefersResetCurrentArmorOverAvatarAppeared()
        {
            var entries = new List<CoreGameEvent>
            {
                new CoreGameEvent(CoreEventType.AvatarAppeared, 1, "FillEmptySlots")
                    .WithCard(1)
                    .WithRemaining(10, 5),
                new CoreGameEvent(CoreEventType.ArmorChanged, 2, "ResetCurrentArmor")
                    .WithCard(1)
                    .WithTarget(1)
                    .WithDelta(0)
                    .WithRemaining(10, 5)
                    .WithMessage("resetCurrentArmor"),
                new CoreGameEvent(CoreEventType.NodeStarted, 3, "NodeStarted"),
            };

            var ok = BattleLogRecorder.TryFindOpeningBaselineArmor(entries, 2, out var uid, out var armor);

            Assert.IsTrue(ok);
            Assert.AreEqual(1, uid);
            Assert.AreEqual(5, armor);
        }

        [Test]
        public void TryFindOpeningBaselineArmor_FallsBackToAvatarAppeared()
        {
            var entries = new List<CoreGameEvent>
            {
                new CoreGameEvent(CoreEventType.AvatarAppeared, 1, "FillEmptySlots")
                    .WithCard(1)
                    .WithRemaining(10, 5),
                new CoreGameEvent(CoreEventType.NodeStarted, 2, "NodeStarted"),
            };

            var ok = BattleLogRecorder.TryFindOpeningBaselineArmor(entries, 1, out var uid, out var armor);

            Assert.IsTrue(ok);
            Assert.AreEqual(1, uid);
            Assert.AreEqual(5, armor);
        }

        [Test]
        public void TryFindOpeningBaselineArmor_UsesLatestResetInWindow()
        {
            var entries = new List<CoreGameEvent>
            {
                new CoreGameEvent(CoreEventType.NodeStarted, 1, "NodeStarted"),
                new CoreGameEvent(CoreEventType.ArmorChanged, 2, "ResetCurrentArmor")
                    .WithCard(1)
                    .WithTarget(1)
                    .WithDelta(-2)
                    .WithRemaining(10, 5)
                    .WithMessage("resetCurrentArmor"),
                new CoreGameEvent(CoreEventType.NodeStarted, 3, "NodeStarted"),
            };

            var ok = BattleLogRecorder.TryFindOpeningBaselineArmor(entries, 2, out _, out var armor);

            Assert.IsTrue(ok);
            Assert.AreEqual(5, armor);
        }

        [Test]
        public void BuildOpeningBaselineArmorLine_ContainsBaselineLabelAndValue()
        {
            var line = BattleLogRecorder.BuildOpeningBaselineArmorLine(1, 5);

            Assert.IsTrue(line.Contains("基础护甲"));
            Assert.IsTrue(line.Contains("5"));
            Assert.IsTrue(line.Contains("甲"));
        }
    }
}
