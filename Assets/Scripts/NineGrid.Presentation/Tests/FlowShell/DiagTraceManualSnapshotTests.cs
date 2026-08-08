using System.IO;
using NUnit.Framework;
using NineGrid.Flow.Diagnostics;

namespace NineGrid.Presentation.Tests.FlowShell
{
    public sealed class DiagTraceManualSnapshotTests
    {
        [Test]
        public void SanitizeForFileName_ReplacesInvalidAndTrimsLength()
        {
            var raw = "顺劈斧/攻击异常?*<>| 问题";
            var safe = DiagTraceManualSnapshot.SanitizeForFileName(raw, 12);
            Assert.IsFalse(safe.Contains("/"));
            Assert.IsFalse(safe.Contains("?"));
            Assert.IsFalse(safe.Contains("*"));
            Assert.LessOrEqual(safe.Length, 12);
            Assert.IsFalse(string.IsNullOrWhiteSpace(safe));
        }

        [Test]
        public void ResolveRootDirectory_EndsWithManualBugSnapshots()
        {
            var root = DiagTraceManualSnapshot.ResolveRootDirectory();
            Assert.AreEqual(
                DiagTraceManualSnapshot.RootFolderName,
                Path.GetFileName(root));
        }

        [Test]
        public void AiMarkers_AreExtremelyVisible()
        {
            Assert.IsTrue(DiagTraceManualSnapshot.AiAttentionMarker.Contains("AI"));
            Assert.IsTrue(DiagTraceManualSnapshot.AiReadmeFileName.StartsWith("!!!"));
            Assert.IsTrue(DiagTraceManualSnapshot.AiFileNamePrefix.StartsWith("!!!"));
        }
    }
}
