#if UNITY_EDITOR
using NineGrid.Content.Editor;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests.Cards
{
    /// <summary>过渡卡组 + 序列槽位交付门禁。</summary>
    public sealed class MonsterLoadoutPresentationValidatorTests
    {
        [Test]
        public void StagingValidation_Fails_WhileTransitionArchived()
        {
            var report = MonsterLoadoutPresentationValidator.Validate(
                MonsterLoadoutPresentationValidator.ValidationMode.Staging);

            Assert.IsFalse(
                report.Passed,
                "过渡期已结束（#134）：deck.transition 已归档为 Reserve，过渡期校验应失败。\n"
                + Format(report));
            Assert.IsTrue(report.HasErrors);
        }

        [Test]
        public void DeliveryReadyValidation_Passes_AfterTransitionArchived()
        {
            var report = MonsterLoadoutPresentationValidator.Validate(
                MonsterLoadoutPresentationValidator.ValidationMode.DeliveryReady);

            Assert.IsTrue(
                report.Passed,
                "七套已正式启用且过渡组已归档，交付就绪必须通过。\n" + Format(report));
            Assert.IsFalse(report.HasErrors);
        }

        private static string Format(MonsterLoadoutPresentationValidator.Report report)
        {
            if (report.Issues.Count == 0)
            {
                return "no issues";
            }

            var parts = new System.Collections.Generic.List<string>();
            for (var i = 0; i < report.Issues.Count; i++)
            {
                var issue = report.Issues[i];
                parts.Add(issue.Severity + "/" + issue.Code + ": " + issue.Message);
            }

            return string.Join(" | ", parts);
        }
    }
}
#endif
