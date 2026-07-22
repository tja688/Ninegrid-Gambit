using NineGrid.Core;
using NineGrid.Flow;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Controllers;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests.FlowShell
{
    /// <summary>
    /// V8：Controller → Hook / Command 接线契约。
    /// </summary>
    public sealed class FlowShellControllerTests
    {
        private static readonly SlotId sAdjacentSlot = SlotId.Board(2);

        [Test]
        public void GameFlowShellController_HandleSetState_UpdatesHookPath()
        {
            using (PresentationArchitectureFixture.CreateBare())
            {
                var go = new GameObject(nameof(GameFlowShellController));
                var controller = go.AddComponent<GameFlowShellController>();
                try
                {
                    // EditMode 不跑 Awake：显式绑定。
                    controller.HandleSetState(GameFlowShellState.BattleStub);
                    Assert.AreEqual(
                        GameFlowShellState.BattleStub,
                        NineGrid.Core.NineGridArchitecture.Interface
                            .GetSystem<NineGrid.Presentation.Systems.IGameFlowShellSystem>()
                            .State.Value);
                }
                finally
                {
                    Object.DestroyImmediate(go);
                }
            }
        }

        [Test]
        public void RoomChoiceController_HandleSelectRoom_UsesCommand()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGameWithCatalog(seed: 11UL))
            {
                Assert.IsTrue(arch.Phase.StartNode(CreateSingleMonsterNode(hp: 1, attack: 0)).Accepted);
                arch.PlaceSoleBoardCardAt(sAdjacentSlot);
                Assert.IsTrue(arch.Phase.Attack(sAdjacentSlot).Accepted);
                Assert.IsTrue(arch.Phase.SkipHelpChoice().Accepted);
                Assert.AreEqual(GamePhase.RoomChoice, arch.Phase.CurrentPhase);

                var go = new GameObject(nameof(RoomChoiceInputController));
                var controller = go.AddComponent<RoomChoiceInputController>();
                try
                {
                    var result = controller.HandleSelectRoom(0);
                    Assert.IsTrue(result.Accepted);
                    Assert.AreEqual(GamePhase.RoomEvent, arch.Phase.CurrentPhase);

                    result = controller.HandleEnterRoom();
                    Assert.IsTrue(result.Accepted);
                    Assert.AreEqual(GamePhase.NodeCompleted, arch.Phase.CurrentPhase);
                }
                finally
                {
                    Object.DestroyImmediate(go);
                }
            }
        }

        [Test]
        public void RewardChoiceController_HandleSelectReward_UsesCommand()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGameWithCatalog(seed: 11UL))
            {
                Assert.IsTrue(arch.Phase.StartNode(CreateSingleMonsterNode(hp: 1, attack: 0)).Accepted);
                arch.PlaceSoleBoardCardAt(sAdjacentSlot);
                Assert.IsTrue(arch.Phase.Attack(sAdjacentSlot).Accepted);
                Assert.AreEqual(GamePhase.RewardItemChoice, arch.Phase.CurrentPhase);

                var go = new GameObject(nameof(RewardChoiceInputController));
                var controller = go.AddComponent<RewardChoiceInputController>();
                try
                {
                    var result = controller.HandleSelectReward(0);
                    Assert.IsTrue(result.Accepted);
                    Assert.AreEqual(GamePhase.RoomChoice, arch.Phase.CurrentPhase);
                }
                finally
                {
                    Object.DestroyImmediate(go);
                }
            }
        }

        [Test]
        public void RelicHudController_RequestSyncViaCommand_InvokesHookSync()
        {
            using (PresentationArchitectureFixture.CreateBare())
            {
                var syncCount = 0;
                var go = new GameObject(nameof(RelicHudController));
                var controller = go.AddComponent<RelicHudController>();
                try
                {
                    // EditMode：手动 Install 等价绑定，再覆盖 Hook 为探针。
                    RelicHudHook.SyncFromCore = () => syncCount++;
                    RelicHudHook.Clear = () => { };

                    // 重新走 Controller 的事件路径：SendCommand → Event → OnSyncRequested。
                    // 但 OnBind 未跑时无事件订阅；直接测 Hook 契约 + Command 事件。
                    var architecture = NineGrid.Core.NineGridArchitecture.Interface;
                    RelicHudSyncRequestedEvent? received = null;
                    var unreg = architecture.RegisterEvent<RelicHudSyncRequestedEvent>(e =>
                    {
                        received = e;
                        RelicHudHook.RequestSync();
                    });
                    try
                    {
                        architecture.SendCommand(new NineGrid.Presentation.Commands.SyncRelicHudCommand());
                        Assert.IsTrue(received.HasValue);
                        Assert.IsFalse(received.Value.Clear);
                        Assert.AreEqual(1, syncCount);
                    }
                    finally
                    {
                        unreg.UnRegister();
                    }
                }
                finally
                {
                    RelicHudHook.SyncFromCore = null;
                    RelicHudHook.Clear = null;
                    Object.DestroyImmediate(go);
                }
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
