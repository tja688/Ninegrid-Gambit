using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Commands;
using NineGrid.Presentation.Systems;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests.FlowShell
{
    /// <summary>
    /// V8：房间 / 奖励 / 进入房间经 Presentation Command → Core。
    /// </summary>
    public sealed class FlowChoiceCommandTests
    {
        private static readonly SlotId sAdjacentSlot = SlotId.Board(2);

        [Test]
        public void SelectReward_ThenSelectRoom_ThenEnterRoom_AdvancesPhasesViaCommands()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGameWithCatalog(seed: 11UL))
            {
                Assert.IsTrue(arch.Phase.StartNode(CreateSingleMonsterNode(hp: 1, attack: 0)).Accepted);
                arch.PlaceSoleBoardCardAt(sAdjacentSlot);
                Assert.IsTrue(arch.Phase.Attack(sAdjacentSlot).Accepted);
                Assert.AreEqual(GamePhase.RewardItemChoice, arch.Phase.CurrentPhase);

                var pending = arch.Architecture.GetModel<PendingChoiceModel>();
                Assert.Greater(pending.RewardOptions.Count, 0);
                var selectReward = arch.Architecture.SendCommand(new SubmitSelectRewardCommand(0));
                Assert.IsTrue(selectReward.Accepted);
                Assert.AreEqual(GamePhase.RoomChoice, arch.Phase.CurrentPhase);

                var selectRoom = arch.Architecture.SendCommand(new SubmitSelectRoomCommand(0));
                Assert.IsTrue(selectRoom.Accepted);
                Assert.AreEqual(GamePhase.RoomEvent, arch.Phase.CurrentPhase);

                var enter = arch.Architecture.SendCommand(new SubmitEnterRoomCommand());
                Assert.IsTrue(enter.Accepted);
                Assert.AreEqual(GamePhase.NodeCompleted, arch.Phase.CurrentPhase);
            }
        }

        [Test]
        public void SkipHelpChoice_AfterNodeClear_AdvancesTowardRoomChoice()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGameWithCatalog(seed: 11UL))
            {
                Assert.IsTrue(arch.Phase.StartNode(CreateSingleMonsterNode(hp: 1, attack: 0)).Accepted);
                arch.PlaceSoleBoardCardAt(sAdjacentSlot);
                Assert.IsTrue(arch.Phase.Attack(sAdjacentSlot).Accepted);
                Assert.AreEqual(GamePhase.RewardItemChoice, arch.Phase.CurrentPhase);

                var skip = arch.Architecture.SendCommand(new SubmitSkipHelpChoiceCommand());
                Assert.IsTrue(skip.Accepted);
                Assert.AreEqual(GamePhase.RoomChoice, arch.Phase.CurrentPhase);
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
