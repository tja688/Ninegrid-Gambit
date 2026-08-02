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
    /// #52：结构护栏——玩家输入不得绕过 IntentIntake；门禁/收口决策禁用壁钟。
    /// </summary>
    public sealed class IntentIntakeStructuralTests
    {
        private static readonly string[] WallClockDecisionSurfaces =
        {
            "Systems/IntentIntakeSystem.cs",
            "Systems/PresentationInputStateSystem.cs",
            "Systems/PresentationRuntimeSystem.cs",
            "Flow/Presentation/PresentationDirector.cs",
            "PresentationInputGates.cs",
        };

        private static readonly Regex WallClockBan = new Regex(
            @"Time\.realtimeSinceStartup|DateTime\.Now|DateTime\.UtcNow|\bStopwatch\b",
            RegexOptions.CultureInvariant);

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
        public void Adr0004_StatusIsAccepted()
        {
            var path = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", "docs", "adr", "0004-input-intake-two-axis-gating.md"));
            Assert.IsTrue(File.Exists(path), "missing ADR-0004");
            var text = File.ReadAllText(path);
            Assert.IsTrue(
                Regex.IsMatch(text, @"^---\s*\r?\nstatus:\s*accepted\s*\r?\n---", RegexOptions.Multiline),
                "ADR-0004 status 应为 accepted");
        }

        [Test]
        public void BoardActionSubmitCommands_CallIntentIntakeSubmit()
        {
            AssertSourceMatches(
                "Commands/SubmitExploreIntentCommand.cs",
                @"intake\.Submit\s*\(");
            AssertSourceMatches(
                "Commands/SubmitAttackIntentCommand.cs",
                @"intake\.Submit\s*\(");
            AssertSourceMatches(
                "Commands/SubmitUseItemIntentCommand.cs",
                @"intake\.Submit\s*\(");
            AssertSourceMatches(
                "Commands/SubmitRevealFaceIntentCommand.cs",
                @"intake\.Submit\s*\(");
            AssertSourceMatches(
                "Commands/SubmitBoardWalkIntentCommand.cs",
                @"intake\.Submit\s*\(");
        }

        [Test]
        public void PlayerInputControllers_RouteThroughIntentIntake()
        {
            // Explore/Attack/UseItem：Controller → Submit*IntentCommand → intake.Submit（见 BoardAction 测例）。
            AssertSourceMentions(
                "Controllers/ExploreInputController.cs",
                "SubmitExploreIntentCommand");
            AssertSourceMentions(
                "Controllers/BoardWalkInputController.cs",
                "SubmitBoardWalkIntentCommand");
            AssertSourceMentions(
                "Controllers/AttackInputController.cs",
                "SubmitAttackIntentCommand");
            AssertSourceMentions(
                "Controllers/AttackInputController.cs",
                "SubmitRevealFaceIntentCommand");
            AssertSourceMentions(
                "Controllers/UseItemInputController.cs",
                "SubmitUseItemIntentCommand");
            // Pickup / 模态 / BoardSelect：Controller 内直接 intake.Submit。
            AssertSourceMatches(
                "Controllers/PickupInputController.cs",
                @"intake\.Submit\s*\(");
            AssertSourceMatches(
                "Controllers/RoomChoiceInputController.cs",
                @"intake\.Submit\s*\(");
            AssertSourceMatches(
                "Controllers/RewardChoiceInputController.cs",
                @"intake\.Submit\s*\(");
            AssertSourceMatches(
                "Cards/BoardCardSelectModeController.cs",
                @"intake\.Submit\s*\(");
        }

        [Test]
        public void ProductionSources_TrySubmitIntent_OnlyInsideIntakeOrRuntime()
        {
            var root = PresentationRoot();
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
                .Where(IsProductionSource)
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
                .Select(ToDataRelative)
                .ToArray();

            Assert.IsEmpty(
                offenders,
                "生产路径禁止绕过 IntentIntake 直接 TrySubmitIntent：\n" + string.Join("\n", offenders));
        }

        [Test]
        public void ProductionSources_ApplyPickupItemCommand_OnlyViaIntakeOrFlushFactory()
        {
            var root = PresentationRoot();
            var allowed = new[]
            {
                "PickupInputController.cs",
                "PickupIntentScriptFactory.cs",
                "ApplyPickupItemCommand.cs",
            };
            var offenders = Directory
                .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(IsProductionSource)
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

                    return Regex.IsMatch(
                        File.ReadAllText(path),
                        @"\bnew\s+ApplyPickupItemCommand\b");
                })
                .Select(ToDataRelative)
                .ToArray();

            Assert.IsEmpty(
                offenders,
                "ApplyPickupItemCommand 仅允许经 Intake Allow 或 flush ScriptFactory：\n"
                + string.Join("\n", offenders));
        }

        [Test]
        public void ProductionSources_ModalSelectCommands_OnlyFromIntakeGatedControllers()
        {
            var root = PresentationRoot();
            var allowed = new[]
            {
                "RoomChoiceInputController.cs",
                "RewardChoiceInputController.cs",
                "SubmitSelectRoomCommand.cs",
                "SubmitSelectRewardCommand.cs",
                "SubmitEnterRoomCommand.cs",
                "SubmitSkipHelpChoiceCommand.cs",
                "SubmitRefreshShopCommand.cs",
            };
            var pattern = new Regex(
                @"\bnew\s+Submit(?:SelectRoom|SelectReward|EnterRoom|SkipHelpChoice|RefreshShop)Command\b",
                RegexOptions.CultureInvariant);
            var offenders = Directory
                .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(IsProductionSource)
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

                    return pattern.IsMatch(File.ReadAllText(path));
                })
                .Select(ToDataRelative)
                .ToArray();

            Assert.IsEmpty(
                offenders,
                "模态选择 Command 仅允许经 IntentIntake 门禁后的 Controller：\n"
                + string.Join("\n", offenders));
        }

        [Test]
        public void GateAndIntakeDecisionSurfaces_DoNotUseWallClock()
        {
            for (var i = 0; i < WallClockDecisionSurfaces.Length; i++)
            {
                var relative = WallClockDecisionSurfaces[i];
                var path = Path.GetFullPath(Path.Combine(PresentationRoot(), relative));
                Assert.IsTrue(File.Exists(path), "missing " + relative);
                var match = WallClockBan.Match(File.ReadAllText(path));
                Assert.IsFalse(
                    match.Success,
                    relative + " 门禁/收口决策禁用壁钟，发现: " + match.Value);
            }
        }

        [Test]
        public void SubmitExploreCommand_TypeStillExists_ForControllerSeam()
        {
            Assert.IsNotNull(typeof(SubmitExploreIntentCommand));
            Assert.IsNotNull(typeof(PresentationCompositionRoot));
        }

        private static string PresentationRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "Scripts", "NineGrid.Presentation"));
        }

        private static bool IsProductionSource(string path)
        {
            return path.IndexOf("\\Tests\\", StringComparison.OrdinalIgnoreCase) < 0
                   && path.IndexOf("/Tests/", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static string ToDataRelative(string path)
        {
            return path.Substring(Application.dataPath.Length).TrimStart('\\', '/');
        }

        private static void AssertSourceMentions(string relativeUnderPresentation, string needle)
        {
            var path = Path.GetFullPath(Path.Combine(PresentationRoot(), relativeUnderPresentation));
            Assert.IsTrue(File.Exists(path), "missing " + relativeUnderPresentation);
            Assert.IsTrue(
                File.ReadAllText(path).IndexOf(needle, StringComparison.Ordinal) >= 0,
                relativeUnderPresentation + " 应引用 " + needle);
        }

        private static void AssertSourceMatches(string relativeUnderPresentation, string pattern)
        {
            var path = Path.GetFullPath(Path.Combine(PresentationRoot(), relativeUnderPresentation));
            Assert.IsTrue(File.Exists(path), "missing " + relativeUnderPresentation);
            Assert.IsTrue(
                Regex.IsMatch(File.ReadAllText(path), pattern),
                relativeUnderPresentation + " 应符合 " + pattern);
        }
    }
}
