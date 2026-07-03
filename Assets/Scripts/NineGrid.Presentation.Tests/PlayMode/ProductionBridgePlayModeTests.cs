using System.Collections;
using NineGrid.Core;
using NineGrid.Core.Commands;
using NineGrid.Core.Systems;
using NineGrid.Presentation.Bridge;
using NineGrid.Presentation.Debugging.Slices;
using NineGrid.Presentation.Tests.Support;
using NUnit.Framework;
using QFramework;
using UnityEngine.TestTools;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// 生产桥最小 PlayMode 闭环：Bootstrap → StartNode/Attack → Batch 回放 → PresentationFinished → 输入解锁。
    /// </summary>
    public sealed class ProductionBridgePlayModeTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            NineGridArchitecture.ResetForTests();
            yield return PlayModeTestSupport.LoadMainScene();
            yield return PlayModeTestSupport.WaitForBootstrapReady();
        }

        [UnityTearDown]
        public void TearDown()
        {
            PlayModeTestSupport.CleanupPlayMode();
        }

        [UnityTest]
        public IEnumerator BootstrapReady_Attack_CompletesBatchAndUnlocksInput()
        {
            var bootstrap = NineGridSceneBootstrap.Current;
            Assert.IsNotNull(bootstrap);
            Assert.IsNotNull(bootstrap.Gateway);
            Assert.IsNotNull(bootstrap.BatchPlayer);

            var gateway = bootstrap.Gateway;
            Assert.IsFalse(gateway.IsInputLocked, "Bootstrap should not leave input locked.");

            var startOptions = new NodeDeckOptions { PlayerOpeningCount = 0, EnemyOpeningCount = 1 }
                .AddEnemyCard(new CardDraft("monster.playmode", CardKind.Monster)
                {
                    MaxHp = 1,
                    Attack = 0,
                });

            var startResult = gateway.Send(new StartNodeCommand(startOptions));
            Assert.IsTrue(startResult.Accepted, "StartNode should be accepted after bootstrap.");
            Assert.IsTrue(startResult.BatchOpened, "StartNode should open a presentation batch.");
            Assert.IsTrue(gateway.IsInputLocked, "StartNode batch should lock input.");

            yield return PlayModeTestSupport.WaitForInputUnlock(gateway, 45f);
            Assert.IsFalse(gateway.IsInputLocked, "StartNode playback should unlock input.");

            var monsterSlot = PlayModeTestSupport.FindFirstMonsterSlot(bootstrap.Architecture);
            var attackResult = gateway.Send(new AttackCommand(monsterSlot));
            Assert.IsTrue(attackResult.Accepted, "Attack should be accepted in InteractionLoop.");
            Assert.IsTrue(attackResult.BatchOpened, "Attack should open a presentation batch.");
            Assert.IsTrue(gateway.IsInputLocked, "Attack batch should lock input.");
            Assert.Greater(attackResult.Batch.Instructions.Count, 0, "Attack batch should contain playback instructions.");

            yield return PlayModeTestSupport.WaitForInputUnlock(gateway, 45f);
            Assert.IsFalse(gateway.IsInputLocked, "Attack playback should finish and unlock input.");
        }

        [UnityTest]
        public IEnumerator MinimalCatalogDeck_StartNodeAndClearNode_CompletesNodeCompletedBatch()
        {
            var bootstrap = NineGridSceneBootstrap.Current;
            Assert.IsNotNull(bootstrap);

            var architecture = bootstrap.Architecture;
            var gateway = bootstrap.Gateway;
            var deckSystem = architecture.GetSystem<IDeckSystem>();
            var rewardSystem = architecture.GetSystem<IRewardSystem>();

            var catalogOptions = rewardSystem.BuildNodeDeckOptions(1, BattleSessionDriver.DefaultMonsterDeckId);
            Assert.IsTrue(BattleSessionDeckOptions.UsesCatalogMonsterDefIds(catalogOptions));
            Assert.Greater(catalogOptions.EnemyCards.Count, 0);

            var startOptions = BattleSessionDeckOptions.BuildMinimalClearDeck(
                architecture,
                catalogOptions,
                monsterCount: 1);
            Assert.AreEqual(1, startOptions.EnemyCards.Count);
            Assert.IsTrue(BattleSessionDeckOptions.UsesCatalogMonsterDefIds(startOptions));

            var startResult = gateway.Send(new StartNodeCommand(startOptions));
            Assert.IsTrue(startResult.Accepted);
            Assert.IsTrue(startResult.BatchOpened);
            yield return PlayModeTestSupport.WaitForInputUnlock(gateway, 60f);

            var attempts = 0;
            const int maxAttempts = 60;
            while (!deckSystem.IsNodeCleared() && attempts < maxAttempts)
            {
                var targetSlot = PlayModeTestSupport.FindFirstAdjacentMonsterSlot(architecture);
                if (!targetSlot.IsNone)
                {
                    var attackResult = gateway.Send(new AttackCommand(targetSlot));
                    if (!attackResult.Accepted)
                    {
                        break;
                    }

                    yield return PlayModeTestSupport.WaitForInputUnlock(gateway, 60f);
                    attempts++;
                    continue;
                }

                var emptySlot = PlayModeTestSupport.FindFirstAdjacentEmptySlot(architecture);
                if (!emptySlot.IsNone)
                {
                    var clickResult = gateway.Send(new ClickEmptyCommand(emptySlot));
                    if (!clickResult.Accepted)
                    {
                        break;
                    }

                    yield return PlayModeTestSupport.WaitForInputUnlock(gateway, 60f);
                    attempts++;
                    continue;
                }

                break;
            }

            Assert.IsTrue(deckSystem.IsNodeCleared(), "Minimal catalog deck should be cleared.");
            Assert.IsTrue(
                PlayModeTestSupport.EventLogContains(architecture, CoreEventType.NodeCompleted),
                "Clearing the node should emit NodeCompleted.");
            Assert.IsFalse(gateway.IsInputLocked, "NodeCompleted batch should unlock input.");
        }
    }
}
