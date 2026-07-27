using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// #55 结构护栏：卡面数值提交入口与攻击路径直读捷径。
    /// </summary>
    public sealed class CardFaceBeatStructuralTests
    {
        [Test]
        public void ProductionSources_DoNotContain_StoneLoverCardFaceSync()
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "Scripts", "NineGrid.Presentation"));
            var banned = new[]
            {
                "TrySyncStoneLoverCardPresentation",
                "IsStoneLoverArmorLostTrigger",
                "StoneLoverArmorLostCause",
            };
            var offenders = Directory
                .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(path => path.IndexOf("\\Tests\\", StringComparison.OrdinalIgnoreCase) < 0
                               && path.IndexOf("/Tests/", StringComparison.OrdinalIgnoreCase) < 0)
                .SelectMany(path =>
                {
                    var text = File.ReadAllText(path);
                    return banned
                        .Where(token => text.IndexOf(token, StringComparison.Ordinal) >= 0)
                        .Select(token => path.Substring(Application.dataPath.Length).TrimStart('\\', '/') + " :: " + token);
                })
                .ToArray();

            Assert.IsEmpty(offenders, "观察型提前同步应已删除：\n" + string.Join("\n", offenders));
        }

        [Test]
        public void ProductionSources_HitFrame_DoesNotSyncManagedCardPresentation()
        {
            var path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "Scripts",
                "NineGrid.Presentation",
                "Cards",
                "Battle",
                "FieldBattlePresentationExecutor.cs"));
            var text = File.ReadAllText(path);
            Assert.IsFalse(
                Regex.IsMatch(text, @"ApplyHitFrameVisuals[\s\S]{0,400}SyncManagedCardPresentation"),
                "命中帧不得直读 SyncManagedCardPresentation");
            Assert.IsTrue(
                text.IndexOf("BattleBeatHook.NotifyBeat(PresentationBeat.Impact)", StringComparison.Ordinal) >= 0,
                "命中帧应报 Impact 锚点");
        }

        [Test]
        public void PresentStep_ReportsSettled_BeforeAcknowledge()
        {
            var path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "Scripts",
                "NineGrid.Presentation",
                "Flow",
                "Presentation",
                "BatchLockstepSteps.cs"));
            var text = File.ReadAllText(path);
            var settled = text.IndexOf(
                "BattleBeatHook.NotifyBeat(PresentationBeat.Settled)",
                StringComparison.Ordinal);
            var ack = text.IndexOf("mGate.TryAcknowledge(mBatchId)", StringComparison.Ordinal);
            Assert.Greater(settled, 0, "PresentStep 应报 Settled");
            Assert.Greater(ack, settled, "Settled 须在 TryAcknowledge 之前");
        }

        [Test]
        public void JsonPresentation_DoesNotOverwrite_RuntimeStats()
        {
            var path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "Scripts",
                "NineGrid.Presentation",
                "Flow",
                "CoreCardPresentationMapper.cs"));
            var text = File.ReadAllText(path);
            Assert.IsFalse(
                Regex.IsMatch(text, @"dto\.stats\.attack\s*>\s*0"),
                "JSON stats 不得盖写运行时卡面数值");
        }
    }
}
