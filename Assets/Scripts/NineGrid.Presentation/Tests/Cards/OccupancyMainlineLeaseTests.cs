using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests.Cards
{
    /// <summary>
    /// P4：Core Apply 必须落在 Director 主线或成功 ExternalHold 租约内。
    /// </summary>
    public sealed class OccupancyMainlineLeaseTests
    {
        [Test]
        public void IdlePickup_HoldBeforeApply_AndFlushNotifyWired()
        {
            var controller = ReadPresentationSource("Controllers/PickupInputController.cs");
            Assert.IsTrue(
                Regex.IsMatch(
                    controller,
                    @"TryBeginExternalHold\(""Pickup""\)[\s\S]{0,400}ApplyPickupItemCommand"),
                "idle Pickup 须 Hold→Apply");
            Assert.IsFalse(
                Regex.IsMatch(
                    controller,
                    @"ApplyPickupItemCommand[\s\S]{0,200}TryBeginExternalHold\(""Pickup""\)"),
                "不得先 Apply 再抢 Pickup 租约");

            var hand = ReadPresentationSource("Cards/CardHandManagerSingleton.cs");
            Assert.IsTrue(
                hand.Contains("PickupIntentFlushHook.Notify", System.StringComparison.Ordinal),
                "busy flush 须接线 PickupIntentFlushHook.Notify");
            Assert.IsTrue(
                hand.Contains("OnPickupIntentFlushed", System.StringComparison.Ordinal),
                "须实现 flush 表现承接");
        }

        [Test]
        public void IntentIntake_PickupIdleAllows_BusyBuffers()
        {
            var text = ReadPresentationSource("Systems/IntentIntakeSystem.cs");
            Assert.IsTrue(
                Regex.IsMatch(
                    text,
                    @"InputIntentKinds\.Pickup[\s\S]{0,400}!mainlineBusy[\s\S]{0,120}Allow"),
                "idle Pickup 须 Allow（由调用方 Hold→Apply）");
            Assert.IsTrue(
                text.Contains("BufferToDirector", System.StringComparison.Ordinal),
                "busy 须保持 latest-wins 缓冲");
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
