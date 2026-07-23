using NineGrid.Core;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Commands;
using NineGrid.Presentation.Systems;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests.BattleSession
{
    /// <summary>
    /// 回归：CancelPresentationWork 软取消不得关停导演；否则 Opening Renew 后点击全灭。
    /// </summary>
    public sealed class CancelPresentationWorkRuntimeLifecycleTests
    {
        private static readonly SlotId sAdjacentSlot = SlotId.Board(2);

        [Test]
        public void CancelPresentationWork_KeepsRuntimeStarted_AndAllowsSubmitAttack()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGame(seed: 42UL))
            {
                Assert.IsTrue(arch.Phase.StartNode(CreateSingleMonsterNode(hp: 1, attack: 0)).Accepted);
                arch.PlaceSoleBoardCardAt(sAdjacentSlot);

                var factory = new AttackIntentScriptFactory(
                    arch.Architecture,
                    arch.Dispatcher,
                    new RecordingPresentChannel(ticksUntilComplete: 1),
                    new RecordingPresentChannel(ticksUntilComplete: 1));

                using (var runtime = PresentationRuntimeFixture.Install(arch, factory))
                {
                    Assert.IsTrue(runtime.Runtime.IsStarted);

                    var session = BattleSessionSystem.EnsureRegistered(arch.Architecture);
                    session.CancelPresentationWork();

                    Assert.IsTrue(
                        runtime.Runtime.IsStarted,
                        "软取消不得 Stop 导演（RenewPresentationToken 依赖此语义）。");

                    Assert.IsTrue(arch.Architecture.SendCommand(
                        new SubmitAttackIntentCommand(sAdjacentSlot.Index)));
                    Assert.IsTrue(runtime.MainlineBusy.Value);
                    runtime.TickUntilIdle();
                }
            }
        }

        [Test]
        public void CancelAfterEnsure_ThenRenewTokenPath_KeepsRuntimeStarted()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGame(seed: 42UL))
            {
                Assert.IsTrue(arch.Phase.StartNode(CreateSingleMonsterNode(hp: 99, attack: 0)).Accepted);
                arch.PlaceSoleBoardCardAt(sAdjacentSlot);

                var scriptFactory = new RecordingScriptFactory(continueTicks: 1);
                using (var runtime = PresentationRuntimeFixture.Install(arch, scriptFactory))
                {
                    var session = BattleSessionSystem.EnsureRegistered(arch.Architecture);

                    // 模拟 Opening：Ensure（已 Install）后 Renew → CancelPresentationWork（软）。
                    Assert.IsTrue(runtime.Runtime.IsStarted);
                    session.CancelPresentationWork();
                    Assert.IsTrue(runtime.Runtime.IsStarted);

                    Assert.IsTrue(arch.Architecture.SendCommand(
                        new SubmitAttackIntentCommand(sAdjacentSlot.Index)));
                    Assert.AreEqual(1, scriptFactory.Built.Count);
                    Assert.AreEqual(InputIntentKinds.Attack, scriptFactory.Built[0].Kind);
                    runtime.TickUntilIdle();
                }
            }
        }

        [Test]
        public void TeardownPresentationRuntime_StopsRuntime_WithoutSceneRootCallback()
        {
            using (var arch = PresentationArchitectureFixture.CreateBare())
            using (var runtime = PresentationRuntimeFixture.Install(
                       arch,
                       new RecordingScriptFactory()))
            {
                Assert.IsTrue(runtime.Runtime.IsStarted);

                var session = BattleSessionSystem.EnsureRegistered(arch.Architecture);
                session.TeardownPresentationRuntime(IntentClearReason.LayerChange);

                Assert.IsFalse(
                    runtime.Runtime.IsStarted,
                    "无 SceneRoot 回调时 Teardown 回退应 Stop 已注册 Runtime。");
            }
        }

        private static NodeDeckOptions CreateSingleMonsterNode(int hp, int attack)
        {
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 1
            }.AddEnemyCard(new CardDraft("monster.test", CardKind.Monster) { MaxHp = hp, Attack = attack });
        }
    }
}
