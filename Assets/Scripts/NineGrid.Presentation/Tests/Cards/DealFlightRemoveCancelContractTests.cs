using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests.Cards
{
    /// <summary>
    /// 捕熊等「同批 Deal→立刻 Remove 飞行中 uid」不得泄漏 dealFlight：
    /// Vacate/PresentRemoved 必须取消 in-flight 飞牌，否则 WaitAllActiveDealFlights 挂死、Pickup 永久 Buffered。
    /// </summary>
    public sealed class DealFlightRemoveCancelContractTests
    {
        [Test]
        public void PresentRemovedFieldCard_CancelsInFlightDealBeforeRelease()
        {
            var text = ReadPresentationSource("Cards/Battle/FieldBattlePresentationExecutor.cs");
            Assert.IsTrue(
                Regex.IsMatch(
                    text,
                    @"PresentRemovedFieldCardAsync[\s\S]{0,800}CancelDealFlightForUid"),
                "PresentRemovedFieldCardAsync 须在 Release 前取消同 uid 的 dealFlight");
        }

        [Test]
        public void VacateSlotForExplore_CancelsInFlightDealForOccupant()
        {
            var text = ReadPresentationSource("Cards/Ground/GroundMotionExecutor.cs");
            Assert.IsTrue(
                Regex.IsMatch(
                    text,
                    @"VacateSlotForExplore[\s\S]{0,900}CancelDealFlightForUid"),
                "VacateSlotForExplore 须取消占格卡上的 in-flight dealFlight（捕熊同批 Remove 入口）");
        }

        [Test]
        public void DealFlightCoordinator_CancelFlight_DropsActiveAndEndsChoreoEagerly()
        {
            var text = ReadPresentationSource("Cards/Ground/DealFlightCoordinator.cs");
            Assert.IsTrue(
                Regex.IsMatch(text, @"bool CancelFlightForUid\s*\("),
                "DealFlightCoordinator 须暴露 CancelFlightForUid");
            Assert.IsTrue(
                Regex.IsMatch(text, @"ChoreoClosedEagerly"),
                "取消须标记 ChoreoClosedEagerly，避免 finally 双重 EndChoreo 弹错栈");
            Assert.IsTrue(
                Regex.IsMatch(
                    text,
                    @"WaitProbeConvergenceAsync[\s\S]{0,600}Card\?\.Transform\s*==\s*null"),
                "WaitProbeConvergenceAsync 须在卡已销毁时退出，防止 ActiveCount 粘死");
        }

        private static string ReadPresentationSource(string relativeUnderPresentation)
        {
            var path = Path.GetFullPath(
                Path.Combine(
                    Application.dataPath,
                    "Scripts",
                    "NineGrid.Presentation",
                    relativeUnderPresentation.Replace('/', Path.DirectorySeparatorChar)));
            Assert.IsTrue(File.Exists(path), "missing " + relativeUnderPresentation);
            return File.ReadAllText(path);
        }
    }
}
