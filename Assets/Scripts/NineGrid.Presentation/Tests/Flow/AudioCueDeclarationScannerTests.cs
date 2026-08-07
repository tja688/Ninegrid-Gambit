using System;
using NineGrid.Content.Editor;
using NineGrid.Flow;
using NineGrid.Flow.Presentation;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests
{
    public sealed class AudioCueDeclarationScannerTests
    {
        [AudioCue("test.audio.duplicate", "重复提示 A", "Tests", "AudioCueDeclarationScannerTests", AudioCueContexts.None)]
        private const string DuplicateCueA = "test.audio.duplicate";

        [AudioCue("test.audio.duplicate", "重复提示 B", "Tests", "AudioCueDeclarationScannerTests", AudioCueContexts.None)]
        private const string DuplicateCueB = "test.audio.duplicate";

        [AudioCue("test.audio.empty", "", "Tests", "AudioCueDeclarationScannerTests", AudioCueContexts.None)]
        private const string EmptyNoteCue = "test.audio.empty";

        [Test]
        public void ProductionAssembly_ContainsMainMenuCue_WithNoDeclarationFindings()
        {
            var result = AudioCueDeclarationScanner.Scan(typeof(GameFlowController).Assembly);

            Assert.IsTrue(ContainsCue(result, "ui.main_menu.start"));
            Assert.IsEmpty(result.Findings);
        }

        [Test]
        public void Scanner_ReportsDuplicateCueIdAndEmptyChineseNote()
        {
            var result = AudioCueDeclarationScanner.Scan(typeof(AudioCueDeclarationScannerTests).Assembly);

            Assert.IsTrue(ContainsMessage(result, "重复"));
            Assert.IsTrue(ContainsMessage(result, "中文音效说明不能为空"));
        }

        private static bool ContainsCue(AudioCueDeclarationScanResult result, string cueId)
        {
            for (var i = 0; i < result.Declarations.Count; i++)
            {
                if (string.Equals(result.Declarations[i].Attribute.CueId, cueId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsMessage(AudioCueDeclarationScanResult result, string message)
        {
            for (var i = 0; i < result.Findings.Count; i++)
            {
                if (result.Findings[i].Message.IndexOf(message, StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
