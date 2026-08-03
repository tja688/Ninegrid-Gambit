using NineGrid.Cards;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests.Cards
{
    /// <summary>
    /// ADR-0024：mode 倍率为相对预制体基准的乘数，且可精确还原。
    /// </summary>
    public sealed class CardDisplayModeVisualsTests
    {
        [Test]
        public void ModeScale_IsRelativeToAuthoredBaseline()
        {
            var authored = new Vector3(0.5f, 0.5f, 0.5f);

            Assert.AreEqual(
                authored,
                CardDisplayModeVisuals.GetBaseLocalScale(CardDisplayMode.GroundCardMode, authored));
            Assert.AreEqual(
                authored * CardDisplayModeVisuals.DragCardScaleMultiplier,
                CardDisplayModeVisuals.GetBaseLocalScale(CardDisplayMode.DragCardMode, authored));
            Assert.AreEqual(
                authored * CardDisplayModeVisuals.RemovedModeScaleMultiplier,
                CardDisplayModeVisuals.GetBaseLocalScale(CardDisplayMode.RemovedMode, authored));
        }

        [Test]
        public void ModeScale_UnitAuthored_MatchesLegacyAbsoluteMultipliers()
        {
            Assert.AreEqual(
                Vector3.one,
                CardDisplayModeVisuals.GetBaseLocalScale(CardDisplayMode.GroundCardMode));
            Assert.AreEqual(
                Vector3.one * CardDisplayModeVisuals.DragCardScaleMultiplier,
                CardDisplayModeVisuals.GetBaseLocalScale(CardDisplayMode.DragCardMode));
            Assert.AreEqual(
                Vector3.one * CardDisplayModeVisuals.RemovedModeScaleMultiplier,
                CardDisplayModeVisuals.GetBaseLocalScale(CardDisplayMode.RemovedMode));
        }

        [Test]
        public void HoverAndHop_MultipliersRestoreExactlyToModeBase()
        {
            var authored = new Vector3(2f, 2f, 2f);
            var modeBase = CardDisplayModeVisuals.GetBaseLocalScale(
                CardDisplayMode.GroundCardMode,
                authored);
            const float hoverIntensity = 0.05f;
            const float hopPeak = 0.06f;
            const float hopLand = 0.04f;

            var hover = modeBase * (1f + hoverIntensity);
            var peak = modeBase * (1f + hopPeak);
            var land = modeBase * (1f - hopLand);

            Assert.AreEqual(modeBase, hover / (1f + hoverIntensity));
            Assert.AreEqual(modeBase, peak / (1f + hopPeak));
            Assert.AreEqual(modeBase, land / (1f - hopLand));
        }
    }
}
