#if UNITY_EDITOR
using NineGrid.Content.Editor;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests.Cards
{
    /// <summary>过渡卡组 + 序列槽位交付门禁。</summary>
    public sealed class MonsterLoadoutPresentationValidatorTests
    {
        [Test]
        public void StagingValidation_Passes_WithTransitionDeckCoveringSequences()
        {
            var report = MonsterLoadoutPresentationValidator.Validate(
                MonsterLoadoutPresentationValidator.ValidationMode.Staging);

            Assert.IsTrue(
                report.Passed,
                Format(report));
        }

        [Test]
        public void DeliveryReadyValidation_Fails_WhileMonstersStillInTransition()
        {
            var report = MonsterLoadoutPresentationValidator.Validate(
                MonsterLoadoutPresentationValidator.ValidationMode.DeliveryReady);

            Assert.IsFalse(report.Passed, "过渡期尚未交付，交付就绪应失败");
            Assert.IsTrue(report.HasErrors);
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
