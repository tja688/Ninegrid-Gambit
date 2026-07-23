using System;
using System.IO;
using System.Text.RegularExpressions;
using NineGrid.Flow.Diagnostics;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Systems;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NineGrid.Presentation.Tests.BehaviorBaseline
{
    /// <summary>
    /// #49/#51：输入所有权轴 + ExternalHold / MainlineBusy 只读投影；
    /// 弃用 BattleBusy/FieldBusy 独立门禁；OccupancyDesync 降诊断断言。
    /// </summary>
    public sealed class InputGateMatrixBaselineTests
    {
        [TearDown]
        public void TearDown()
        {
            PresentationInputGates.Reset("InputGateMatrixBaselineTests");
            ChoreoTraceContext.Reset();
        }

        [Test]
        public void GateFlags_AreIndependent_UntilResetClearsAll()
        {
            using (var arch = PresentationArchitectureFixture.CreateBare())
            using (var runtime = PresentationRuntimeFixture.Install(arch, new AcceptAllScriptFactory()))
            {
                var input = PresentationInputStateSystem.EnsureRegistered(arch.Architecture);

                input.SetOpeningPresentationActive(true);
                input.SetChoiceOverlayActive(true);
                input.SetBoardSelectModeActive(true);
                Assert.IsTrue(runtime.Runtime.TryBeginExternalHold("busy-mirror"));

                Assert.IsTrue(input.OpeningPresentationActive.Value);
                Assert.IsTrue(input.ChoiceOverlayActive.Value);
                Assert.IsTrue(input.BoardSelectModeActive.Value);
                Assert.AreEqual(InputOwner.ChoiceOverlay, input.CurrentOwner);
                Assert.IsTrue(input.MainlineBusy);

                input.SetOpeningPresentationActive(false);
                Assert.IsFalse(input.OpeningPresentationActive.Value);
                Assert.IsTrue(input.ChoiceOverlayActive.Value);
                Assert.IsTrue(input.BoardSelectModeActive.Value);
                Assert.AreEqual(InputOwner.ChoiceOverlay, input.CurrentOwner);

                input.ResetGates("matrix");
                runtime.Tick();
                Assert.AreEqual(InputOwner.ProtectedField, input.CurrentOwner);
                Assert.IsFalse(input.OpeningPresentationActive.Value);
                Assert.IsFalse(input.ChoiceOverlayActive.Value);
                Assert.IsFalse(input.BoardSelectModeActive.Value);
                Assert.IsFalse(input.MainlineBusy);
                Assert.IsFalse(input.HasExternalHold);
            }
        }

        [Test]
        public void ExternalHold_RejectsReentry_AndForceEndAlwaysReleases()
        {
            using (var arch = PresentationArchitectureFixture.CreateBare())
            using (var runtime = PresentationRuntimeFixture.Install(
                       arch,
                       new RecordingScriptFactory(continueTicks: 1)))
            {
                PresentationInputStateSystem.EnsureRegistered(arch.Architecture);

                Assert.IsTrue(PresentationInputGates.TryBeginExternalHold("pickup"));
                Assert.IsTrue(PresentationInputGates.HasExternalHold);
                Assert.IsFalse(PresentationInputGates.TryBeginExternalHold("pickup-reentry"));

                PresentationInputGates.EndExternalHold("pickup");
                runtime.Tick();
                Assert.IsFalse(PresentationInputGates.HasExternalHold);

                Assert.IsTrue(PresentationInputGates.TryBeginExternalHold("drain"));
                PresentationInputGates.ForceEndExternalHold("cancel");
                runtime.Tick();
                Assert.IsFalse(PresentationInputGates.HasExternalHold);

                PresentationInputGates.ForceEndExternalHold("already-clear");
                runtime.Tick();
                Assert.IsFalse(PresentationInputGates.HasExternalHold);
            }
        }

        [Test]
        public void Opening_Overlay_BoardSelect_DoNotImplyExternalHold()
        {
            using (var arch = PresentationArchitectureFixture.CreateBare())
            {
                var input = PresentationInputStateSystem.EnsureRegistered(arch.Architecture);
                input.SetOpeningPresentationActive(true);
                input.SetChoiceOverlayActive(true);
                input.SetBoardSelectModeActive(true);

                Assert.IsFalse(input.HasExternalHold);
                Assert.IsFalse(input.MainlineBusy);
                Assert.AreEqual(InputOwner.ChoiceOverlay, input.CurrentOwner);
            }
        }

        [Test]
        public void CurrentOwner_OrthogonalToMainlineBusy()
        {
            using (var arch = PresentationArchitectureFixture.CreateBare())
            using (var runtime = PresentationRuntimeFixture.Install(arch, new AcceptAllScriptFactory()))
            {
                var input = PresentationInputStateSystem.EnsureRegistered(arch.Architecture);

                Assert.AreEqual(InputOwner.ProtectedField, input.CurrentOwner);
                Assert.IsTrue(runtime.Runtime.TryBeginExternalHold("hold"));
                Assert.IsTrue(input.MainlineBusy);
                Assert.AreEqual(InputOwner.ProtectedField, input.CurrentOwner);

                input.SetChoiceOverlayActive(true);
                Assert.AreEqual(InputOwner.ChoiceOverlay, input.CurrentOwner);
                Assert.IsTrue(input.MainlineBusy);
            }
        }

        [Test]
        public void OccupancyDesyncLatched_DoesNotRejectIntentIntake()
        {
            using (var arch = PresentationArchitectureFixture.CreateBare())
            using (var runtime = PresentationRuntimeFixture.Install(
                       arch,
                       new RecordingScriptFactory(continueTicks: 0)))
            {
                var intake = IntentIntakeSystem.EnsureRegistered(
                    arch.Architecture,
                    legalityOverride: _ => true);

                LogAssert.Expect(LogType.Error, new Regex("OccupancyDesyncLatched"));
                LogAssert.Expect(LogType.Assert, new Regex("OccupancyDesyncLatched"));
                ChoreoTraceContext.LatchOccupancyDesync("contract-test");
                Assert.IsTrue(ChoreoTraceContext.OccupancyDesyncLatched);

                bool preview;
                var disposition = intake.Submit(
                    new InputIntent(InputIntentKinds.Explore, 2),
                    InputOwner.ProtectedField,
                    out preview);

                Assert.AreEqual(IntentDisposition.Allow, disposition);
                Assert.AreEqual(1, runtime.ScriptFactory.Built.Count);
            }
        }

        [Test]
        public void BlockingPresentMainlineHold_GatesIntentIntake_SameAsMainlineBusy()
        {
            using (var arch = PresentationArchitectureFixture.CreateBare())
            using (var runtime = PresentationRuntimeFixture.Install(
                       arch,
                       new RecordingScriptFactory(continueTicks: 2)))
            {
                var intake = IntentIntakeSystem.EnsureRegistered(
                    arch.Architecture,
                    legalityOverride: _ => true);

                bool acquiredHere;
                Assert.IsTrue(PresentationMainlineHold.TryAcquire("FieldBattlePresent", out acquiredHere));
                Assert.IsTrue(acquiredHere);
                Assert.IsTrue(runtime.MainlineBusy.Value);

                bool preview;
                Assert.AreEqual(
                    IntentDisposition.BufferToDirector,
                    intake.Submit(
                        new InputIntent(InputIntentKinds.Attack, 3),
                        InputOwner.ProtectedField,
                        out preview));
                Assert.IsTrue(preview);

                PresentationMainlineHold.Release(acquiredHere, "FieldBattlePresent");
                runtime.TickUntilIdle();
                Assert.IsFalse(runtime.MainlineBusy.Value);
                Assert.AreEqual(1, runtime.ScriptFactory.Built.Count);
                Assert.AreEqual(3, runtime.ScriptFactory.Built[0].TargetId);
            }
        }

        [Test]
        public void IntakeAndInputStateSources_DoNotPollBattleOrFieldBusyOrDesyncGate()
        {
            var root = Path.GetFullPath(
                Path.Combine(Application.dataPath, "Scripts", "NineGrid.Presentation"));
            var files = new[]
            {
                Path.Combine(root, "Systems", "IntentIntakeSystem.cs"),
                Path.Combine(root, "Systems", "PresentationInputStateSystem.cs"),
                Path.Combine(root, "Cards", "GroundSlotHitProxy.cs"),
            };

            var forbidden = new Regex(
                @"OccupancyDesyncLatched|EvaluateAttack|IsFieldBusy|IsBattlePresentationBusy|field\.IsBusy|hand\.IsBusy|presentationInputLocked",
                RegexOptions.CultureInvariant);

            for (var i = 0; i < files.Length; i++)
            {
                var path = files[i];
                Assert.IsTrue(File.Exists(path), path);
                var match = forbidden.Match(File.ReadAllText(path));
                Assert.IsFalse(
                    match.Success,
                    Path.GetFileName(path) + " 仍含独立 busy/desync 门禁: " + match.Value);
            }

            var legalityPath = Path.Combine(root, "Flow", "Presentation", "BoardIntentLegality.cs");
            Assert.IsTrue(File.Exists(legalityPath), legalityPath);
            Assert.IsFalse(
                File.ReadAllText(legalityPath).Contains("presentationInputLocked"),
                "BoardIntentLegality 不得再以 presentationInputLocked 硬拒（ADR-0004 缓冲轴）");

            var groundCardPath = Path.Combine(root, "Cards", "GroundCardHitProxy.cs");
            var groundCardText = File.ReadAllText(groundCardPath);
            var gateMethod = Regex.Match(
                groundCardText,
                @"private bool TryPassGroundInputGate\(ManagedCard card, out string blockReason\)[\s\S]*?return true;\s*\}");
            Assert.IsTrue(gateMethod.Success, "找不到 TryPassGroundInputGate 方法体");
            Assert.IsFalse(
                gateMethod.Value.Contains("field.IsBusy"),
                "GroundCardHitProxy.TryPassGroundInputGate 不得以 field.IsBusy 作门禁");

            var battlePath = Path.Combine(
                root, "Cards", "Battle", "FieldBattlePresentationExecutor.cs");
            var battleText = File.ReadAllText(battlePath);
            Assert.IsFalse(
                Regex.IsMatch(
                    battleText,
                    @"TryHandleBattleClick[\s\S]*?geometry\.IsFieldBusy"),
                "TryHandleBattleClick 不得以 geometry.IsFieldBusy 作独立互斥");
            Assert.IsFalse(
                Regex.IsMatch(
                    battleText,
                    @"TryHandleBattleClick[\s\S]*?\|\|\s*_isBusy"),
                "TryHandleBattleClick 不得以本地 BattleBusy(_isBusy) 作独立输入互斥");

            var explorePath = Path.Combine(root, "Cards", "Ground", "GroundMotionExecutor.cs");
            var exploreText = File.ReadAllText(explorePath);
            var tryHandle = Regex.Match(
                exploreText,
                @"public bool TryHandleEmptySlotClick\(int slot\)[\s\S]*?return ExploreInputHook");
            Assert.IsTrue(tryHandle.Success, "找不到 TryHandleEmptySlotClick");
            Assert.IsFalse(
                tryHandle.Value.Contains("_isBusy")
                || tryHandle.Value.Contains("IsBattlePresentationBusy"),
                "TryHandleEmptySlotClick 不得以 FieldBusy/BattleBusy 作独立互斥");

            var handPath = Path.Combine(root, "Cards", "CardHandManagerSingleton.cs");
            var handText = File.ReadAllText(handPath);
            Assert.IsFalse(
                Regex.IsMatch(
                    handText,
                    @"public bool IsBusy\s*\{[\s\S]*?field\.IsBusy"),
                "CardHandManagerSingleton.IsBusy 不得再聚合 field.IsBusy 作门禁");
            Assert.IsFalse(
                Regex.IsMatch(
                    handText,
                    @"TryPickupFromGround[\s\S]{0,1200}?if \(IsBusy \|\|"),
                "TryPickupFromGround 不得用含 MainlineBusy 的 IsBusy 提前吞掉缓冲路径");
            Assert.IsFalse(
                Regex.IsMatch(
                    handText,
                    @"TryPickupFromGround[\s\S]{0,1200}?FieldBusy"),
                "拾取入口不得再以 FieldBusy 作独立互斥门禁");

            var pickupBody = Regex.Match(
                handText,
                @"public bool TryPickupFromGround\(ManagedCard card\)[\s\S]*?RunPickupFromGroundAsync");
            Assert.IsTrue(pickupBody.Success, "找不到 TryPickupFromGround 主体");
            var applyIdx = pickupBody.Value.IndexOf("PickupInputHook.TryApplyPickup", StringComparison.Ordinal);
            var holdIdx = pickupBody.Value.IndexOf("TryBeginExternalHold(\"Pickup\")", StringComparison.Ordinal);
            Assert.GreaterOrEqual(applyIdx, 0, "TryPickupFromGround 须调用 TryApplyPickup");
            Assert.GreaterOrEqual(holdIdx, 0, "TryPickupFromGround 须在 Allow 后取 ExternalHold");
            Assert.Less(
                applyIdx,
                holdIdx,
                "idle Pickup 须先 IntentIntake/TryApplyPickup，再 TryBeginExternalHold（否则 busy 误判）");
        }

        private sealed class AcceptAllScriptFactory : IIntentScriptFactory
        {
            public void BuildScript(InputIntent intent, BattleTimeline mainline)
            {
            }
        }
    }
}
