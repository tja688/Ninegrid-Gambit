using NineGrid.Core;
using NineGrid.Flow;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Systems;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests.IntentIntake
{
    /// <summary>
    /// #50 主 seam：IntentIntake 分型处置 / 两轴 / impatience-tap / 同帧突发。
    /// </summary>
    public sealed class IntentIntakeDispositionTests
    {
        [Test]
        public void Idle_BoardAction_OwnsField_AllowsAndBuildsScript()
        {
            using (var arch = PresentationArchitectureFixture.CreateBare())
            using (var runtime = PresentationRuntimeFixture.Install(
                       arch, new RecordingScriptFactory(continueTicks: 0)))
            {
                var accel = new RecordingAccelerationSink();
                var intake = IntentIntakeSystem.EnsureRegistered(
                    arch.Architecture, accel, _ => true);

                bool preview;
                var disposition = intake.Submit(
                    new InputIntent(InputIntentKinds.Explore, 2),
                    InputOwner.ProtectedField,
                    out preview);

                Assert.AreEqual(IntentDisposition.Allow, disposition);
                Assert.IsFalse(preview);
                Assert.AreEqual(1, runtime.ScriptFactory.Built.Count);
                Assert.AreEqual(0, accel.Taps.Count);
            }
        }

        [Test]
        public void Busy_BoardActions_LatestWinsBuffer_AndTapEachClick()
        {
            using (var arch = PresentationArchitectureFixture.CreateBare())
            using (var runtime = PresentationRuntimeFixture.Install(
                       arch,
                       new RecordingScriptFactory(continueTicks: 2),
                       out var uiPick))
            {
                var accel = new RecordingAccelerationSink();
                var intake = IntentIntakeSystem.EnsureRegistered(
                    arch.Architecture, accel, _ => true);

                bool preview;
                Assert.AreEqual(
                    IntentDisposition.Allow,
                    intake.Submit(
                        new InputIntent(InputIntentKinds.Explore, 1),
                        InputOwner.ProtectedField,
                        out preview));
                Assert.IsTrue(runtime.MainlineBusy.Value);

                Assert.AreEqual(
                    IntentDisposition.BufferToDirector,
                    intake.Submit(
                        new InputIntent(InputIntentKinds.Attack, 3),
                        InputOwner.ProtectedField,
                        out preview));
                Assert.IsTrue(preview);

                Assert.AreEqual(
                    IntentDisposition.BufferToDirector,
                    intake.Submit(
                        new InputIntent(InputIntentKinds.Pickup, 9),
                        InputOwner.ProtectedField,
                        out preview));
                Assert.IsTrue(preview);

                Assert.AreEqual(2, accel.Taps.Count);
                Assert.AreEqual(2, uiPick.Previews.Count);
                Assert.AreEqual(9, uiPick.Previews[1].TargetId);

                runtime.TickUntilIdle();
                Assert.AreEqual(2, runtime.ScriptFactory.Built.Count);
                Assert.AreEqual(1, runtime.ScriptFactory.Built[0].TargetId);
                Assert.AreEqual(9, runtime.ScriptFactory.Built[1].TargetId);
                Assert.AreEqual(InputIntentKinds.Pickup, runtime.ScriptFactory.Built[1].Kind);
            }
        }

        [Test]
        public void Busy_BoardSelectBegin_RejectsWithoutBuffer()
        {
            using (var arch = PresentationArchitectureFixture.CreateBare())
            using (var runtime = PresentationRuntimeFixture.Install(
                       arch, new RecordingScriptFactory(continueTicks: 2)))
            {
                var accel = new RecordingAccelerationSink();
                var intake = IntentIntakeSystem.EnsureRegistered(
                    arch.Architecture, accel, _ => true);

                bool preview;
                intake.Submit(
                    new InputIntent(InputIntentKinds.Explore, 1),
                    InputOwner.ProtectedField,
                    out preview);
                Assert.IsTrue(runtime.MainlineBusy.Value);

                var disposition = intake.Submit(
                    new InputIntent(InputIntentKinds.BoardSelectBegin, 7),
                    InputOwner.ProtectedField,
                    out preview);

                Assert.AreEqual(IntentDisposition.Reject, disposition);
                Assert.IsFalse(preview);
                Assert.AreEqual(1, accel.Taps.Count);
                Assert.AreEqual(1, runtime.ScriptFactory.Built.Count);
            }
        }

        [Test]
        public void Busy_SelectReward_OnChoiceOverlay_Allows_ForMidBattleChestPresent()
        {
            // 局内宝箱 Bounce 挂在 UseItem Present 主线上；主线忙时仍须放行 SelectReward。
            using (var arch = PresentationArchitectureFixture.CreateBare())
            using (var runtime = PresentationRuntimeFixture.Install(
                       arch, new RecordingScriptFactory(continueTicks: 2)))
            {
                var accel = new RecordingAccelerationSink();
                var intake = IntentIntakeSystem.EnsureRegistered(
                    arch.Architecture, accel, _ => true);
                var input = PresentationInputStateSystem.EnsureRegistered(arch.Architecture);

                Assert.IsTrue(runtime.Runtime.TryBeginExternalHold("useItem-present"));
                Assert.IsTrue(runtime.MainlineBusy.Value);
                input.SetChoiceOverlayActive(true);

                bool preview;
                Assert.AreEqual(
                    IntentDisposition.Allow,
                    intake.Submit(
                        new InputIntent(InputIntentKinds.SelectReward, 0),
                        InputOwner.ChoiceOverlay,
                        out preview));
                Assert.IsFalse(preview);
                Assert.AreEqual(1, accel.Taps.Count);
                Assert.AreEqual(0, runtime.ScriptFactory.Built.Count);
            }
        }

        [Test]
        public void Busy_SelectReward_WithoutChoiceOverlay_Rejects()
        {
            using (var arch = PresentationArchitectureFixture.CreateBare())
            using (var runtime = PresentationRuntimeFixture.Install(
                       arch, new RecordingScriptFactory(continueTicks: 2)))
            {
                var accel = new RecordingAccelerationSink();
                var intake = IntentIntakeSystem.EnsureRegistered(
                    arch.Architecture, accel, _ => true);

                Assert.IsTrue(runtime.Runtime.TryBeginExternalHold("modal-busy"));
                Assert.IsTrue(runtime.MainlineBusy.Value);

                bool preview;
                Assert.AreEqual(
                    IntentDisposition.Reject,
                    intake.Submit(
                        new InputIntent(InputIntentKinds.SelectReward, 0),
                        InputOwner.ChoiceOverlay,
                        out preview));
                Assert.IsFalse(preview);
                Assert.AreEqual(1, accel.Taps.Count);
            }
        }

        [Test]
        public void Busy_SelectRoom_OnChoiceOverlay_Allows()
        {
            // 通关后房间选择与结算 Drain 重叠时，overlay 持有期间必须仍能 SelectRoom。
            using (var arch = PresentationArchitectureFixture.CreateBare())
            using (var runtime = PresentationRuntimeFixture.Install(
                       arch, new RecordingScriptFactory(continueTicks: 2)))
            {
                var accel = new RecordingAccelerationSink();
                var intake = IntentIntakeSystem.EnsureRegistered(
                    arch.Architecture, accel, _ => true);
                var input = PresentationInputStateSystem.EnsureRegistered(arch.Architecture);

                Assert.IsTrue(runtime.Runtime.TryBeginExternalHold("settlement-drain"));
                Assert.IsTrue(runtime.MainlineBusy.Value);
                input.SetChoiceOverlayActive(true);

                bool preview;
                Assert.AreEqual(
                    IntentDisposition.Allow,
                    intake.Submit(
                        new InputIntent(InputIntentKinds.SelectRoom, 0),
                        InputOwner.ChoiceOverlay,
                        out preview));
                Assert.IsFalse(preview);
            }
        }

        [Test]
        public void Busy_SelectRoom_WithoutChoiceOverlay_Rejects()
        {
            // 复现：GameFlow 先关 overlay 再 SubmitSelectRoom → intentIntakeReject → Bootstrap 清遗物。
            using (var arch = PresentationArchitectureFixture.CreateBare())
            using (var runtime = PresentationRuntimeFixture.Install(
                       arch, new RecordingScriptFactory(continueTicks: 2)))
            {
                var accel = new RecordingAccelerationSink();
                var intake = IntentIntakeSystem.EnsureRegistered(
                    arch.Architecture, accel, _ => true);
                PresentationInputStateSystem.EnsureRegistered(arch.Architecture);

                Assert.IsTrue(runtime.Runtime.TryBeginExternalHold("settlement-drain"));
                Assert.IsTrue(runtime.MainlineBusy.Value);

                bool preview;
                Assert.AreEqual(
                    IntentDisposition.Reject,
                    intake.Submit(
                        new InputIntent(InputIntentKinds.SelectRoom, 0),
                        InputOwner.ChoiceOverlay,
                        out preview));
                Assert.IsFalse(preview);
            }
        }

        [Test]
        public void Idle_ModalOnOverlay_Allows()
        {
            using (var arch = PresentationArchitectureFixture.CreateBare())
            using (PresentationRuntimeFixture.Install(arch, new RecordingScriptFactory()))
            {
                var intake = IntentIntakeSystem.EnsureRegistered(
                    arch.Architecture, legalityOverride: _ => true);
                PresentationInputStateSystem.EnsureRegistered(arch.Architecture)
                    .SetChoiceOverlayActive(true);

                bool preview;
                Assert.AreEqual(
                    IntentDisposition.Allow,
                    intake.Submit(
                        new InputIntent(InputIntentKinds.SelectRoom, 1),
                        InputOwner.ChoiceOverlay,
                        out preview));
            }
        }

        [Test]
        public void OverlayOwner_FieldClick_RejectsNoBufferNoPenetrate()
        {
            using (var arch = PresentationArchitectureFixture.CreateBare())
            using (var runtime = PresentationRuntimeFixture.Install(
                       arch, new RecordingScriptFactory(continueTicks: 0)))
            {
                var accel = new RecordingAccelerationSink();
                var intake = IntentIntakeSystem.EnsureRegistered(
                    arch.Architecture, accel, _ => true);
                PresentationInputStateSystem.EnsureRegistered(arch.Architecture)
                    .SetChoiceOverlayActive(true);

                bool preview;
                var disposition = intake.Submit(
                    new InputIntent(InputIntentKinds.Explore, 4),
                    InputOwner.ProtectedField,
                    out preview);

                Assert.AreEqual(IntentDisposition.Reject, disposition);
                Assert.AreEqual(0, runtime.ScriptFactory.Built.Count);
                Assert.AreEqual(0, accel.Taps.Count);
            }
        }

        [Test]
        public void SameFrameBurst_BusySubmits_AreDeterministicLatestWins()
        {
            using (var arch = PresentationArchitectureFixture.CreateBare())
            using (var runtime = PresentationRuntimeFixture.Install(
                       arch, new RecordingScriptFactory(continueTicks: 1)))
            {
                var accel = new RecordingAccelerationSink();
                var intake = IntentIntakeSystem.EnsureRegistered(
                    arch.Architecture, accel, _ => true);

                bool preview;
                intake.Submit(
                    new InputIntent(InputIntentKinds.Explore, 1),
                    InputOwner.ProtectedField,
                    out preview);

                for (var slot = 2; slot <= 6; slot++)
                {
                    Assert.AreEqual(
                        IntentDisposition.BufferToDirector,
                        intake.Submit(
                            new InputIntent(InputIntentKinds.Explore, slot),
                            InputOwner.ProtectedField,
                            out preview));
                }

                Assert.AreEqual(5, accel.Taps.Count);
                runtime.TickUntilIdle();
                Assert.AreEqual(2, runtime.ScriptFactory.Built.Count);
                Assert.AreEqual(6, runtime.ScriptFactory.Built[1].TargetId);
            }
        }

        [Test]
        public void Idle_UseItemMultiBoardSelect_RoutesToBoardSelect()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGameWithCatalog(seed: 42UL))
            using (PresentationRuntimeFixture.Install(arch, new RecordingScriptFactory()))
            {
                arch.Architecture.GetSystem<NineGrid.Core.Systems.IContentSystem>()
                    .Load(NineGrid.Content.TableNineContentCatalog.CreateDefault());

                Assert.IsTrue(arch.Phase.StartNode(new NodeDeckOptions
                {
                    PlayerOpeningCount = 0,
                    EnemyOpeningCount = 0
                }).Accepted);

                const string defId = "help.swap_card";
                HelpCardPlayKind kind;
                Assert.IsTrue(HelpCardBoardSelectResolver.TryGetPlayKind(defId, out kind, out _));
                Assert.AreEqual(HelpCardPlayKind.MultiBoardSelect, kind);

                arch.Pipeline.Enqueue(
                    new SpawnCardAction(defId, CardKind.HelpCard, ZoneId.ItemSlots, SlotId.None, 1, "test"));
                Assert.Greater(arch.Pipeline.RunToCompletion(), 0);
                var itemUid = 0;
                var deck = arch.Architecture.GetModel<DeckModel>();
                for (var i = 0; i < deck.ItemSlotUids.Count; i++)
                {
                    if (deck.ItemSlotUids[i] > 0)
                    {
                        itemUid = deck.ItemSlotUids[i];
                        break;
                    }
                }

                Assert.Greater(itemUid, 0);

                var intake = IntentIntakeSystem.EnsureRegistered(arch.Architecture);
                bool preview;
                Assert.AreEqual(
                    IntentDisposition.RouteToBoardSelect,
                    intake.Submit(
                        new InputIntent(InputIntentKinds.UseItem, itemUid),
                        InputOwner.ProtectedField,
                        out preview));
            }
        }

        [Test]
        public void Busy_IllegalBoardAction_StillTaps()
        {
            using (var arch = PresentationArchitectureFixture.CreateBare())
            using (var runtime = PresentationRuntimeFixture.Install(
                       arch, new RecordingScriptFactory(continueTicks: 2)))
            {
                var accel = new RecordingAccelerationSink();
                var intake = IntentIntakeSystem.EnsureRegistered(
                    arch.Architecture,
                    accel,
                    intent => intent.TargetId != 99);

                bool preview;
                intake.Submit(
                    new InputIntent(InputIntentKinds.Explore, 1),
                    InputOwner.ProtectedField,
                    out preview);
                Assert.IsTrue(runtime.MainlineBusy.Value);

                Assert.AreEqual(
                    IntentDisposition.Reject,
                    intake.Submit(
                        new InputIntent(InputIntentKinds.Explore, 99),
                        InputOwner.ProtectedField,
                        out preview));
                Assert.AreEqual(1, accel.Taps.Count);
                Assert.AreEqual(1, runtime.ScriptFactory.Built.Count);
            }
        }
    }
}
