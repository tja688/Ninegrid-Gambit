using NineGrid.Core.Content;
using NineGrid.Presentation.Ui;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests
{
    public sealed class RunSummaryDifficultyPresentationTests
    {
        [Test]
        public void RunSetupSelection_DefaultLabels_MatchDifficultyTiers()
        {
            Assert.AreEqual("旅途", RunSetupSelection.GetDefaultLabel(RunDifficultyIds.Normal));
            Assert.AreEqual("冒险", RunSetupSelection.GetDefaultLabel(RunDifficultyIds.Advanced));
            Assert.AreEqual("血色", RunSetupSelection.GetDefaultLabel(RunDifficultyIds.Hard));
        }

        [Test]
        public void RunSetupSelection_ResetToDefault_RestoresDefaultTier()
        {
            RunSetupSelection.SetDifficulty(RunDifficultyIds.Hard, "血色", null);
            Assert.AreEqual(RunDifficultyIds.Hard, RunSetupSelection.DifficultyId);
            Assert.AreEqual("血色", RunSetupSelection.DifficultyLabel);

            RunSetupSelection.ResetToDefault();
            Assert.AreEqual(RunDifficultyIds.Advanced, RunSetupSelection.DifficultyId);
            Assert.AreEqual("冒险", RunSetupSelection.DifficultyLabel);
        }
    }
}
