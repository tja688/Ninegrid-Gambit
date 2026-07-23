using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Commands;
using NineGrid.Presentation.Setup;
using NineGrid.Presentation.Systems;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// #50：任何输入路径不得绕过 IntentIntake 的结构护栏。
    /// </summary>
    public sealed class IntentIntakeStructuralTests
    {
        [Test]
        public void IntentIntake_AndAccelerationSink_Exist()
        {
            Assert.IsNotNull(typeof(IIntentIntake));
            Assert.IsNotNull(typeof(IntentIntakeSystem));
            Assert.IsNotNull(typeof(IAccelerationSink));
            Assert.IsNotNull(typeof(NoOpAccelerationSink));
            Assert.IsNotNull(typeof(IntentDisposition));
        }

        [Test]
        public void SubmitBoardIntentCommands_ReferenceIntentIntake()
        {
            AssertSourceMentions(
                "Commands/SubmitExploreIntentCommand.cs",
                "IIntentIntake");
            AssertSourceMentions(
                "Commands/SubmitAttackIntentCommand.cs",
                "IIntentIntake");
            AssertSourceMentions(
                "Commands/SubmitUseItemIntentCommand.cs",
                "IIntentIntake");
            AssertSourceMentions(
                "Controllers/PickupInputController.cs",
                "IIntentIntake");
            AssertSourceMentions(
                "Controllers/RoomChoiceInputController.cs",
                "IIntentIntake");
            AssertSourceMentions(
                "Controllers/RewardChoiceInputController.cs",
                "IIntentIntake");
            AssertSourceMentions(
                "Cards/BoardCardSelectModeController.cs",
                "IntentIntakeSystem");
        }

        [Test]
        public void ProductionSources_TrySubmitIntent_OnlyInsideIntakeOrRuntime()
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "Scripts", "NineGrid.Presentation"));
            var allowed = new[]
            {
                "IntentIntakeSystem.cs",
                "PresentationRuntimeSystem.cs",
                "PresentationDirector.cs",
                "IPresentationIntentRuntime.cs",
                "IPresentationRuntimeSystem.cs",
            };
            var offenders = Directory
                .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(path => path.IndexOf("\\Tests\\", StringComparison.OrdinalIgnoreCase) < 0
                               && path.IndexOf("/Tests/", StringComparison.OrdinalIgnoreCase) < 0)
                .Where(path =>
                {
                    var name = Path.GetFileName(path);
                    for (var i = 0; i < allowed.Length; i++)
                    {
                        if (string.Equals(name, allowed[i], StringComparison.OrdinalIgnoreCase))
                        {
                            return false;
                        }
                    }

                    return Regex.IsMatch(File.ReadAllText(path), @"\bTrySubmitIntent\s*\(");
                })
                .Select(path => path.Substring(Application.dataPath.Length).TrimStart('\\', '/'))
                .ToArray();

            Assert.IsEmpty(
                offenders,
                "生产路径禁止绕过 IntentIntake 直接 TrySubmitIntent：\n" + string.Join("\n", offenders));
        }

        [Test]
        public void SubmitExploreCommand_TypeStillExists_ForControllerSeam()
        {
            Assert.IsNotNull(typeof(SubmitExploreIntentCommand));
            Assert.IsNotNull(typeof(PresentationCompositionRoot));
        }

        private static void AssertSourceMentions(string relativeUnderPresentation, string needle)
        {
            var path = Path.GetFullPath(Path.Combine(
                Application.dataPath, "Scripts", "NineGrid.Presentation", relativeUnderPresentation));
            Assert.IsTrue(File.Exists(path), "missing " + relativeUnderPresentation);
            Assert.IsTrue(
                File.ReadAllText(path).IndexOf(needle, StringComparison.Ordinal) >= 0,
                relativeUnderPresentation + " 应引用 " + needle);
        }
    }
}
