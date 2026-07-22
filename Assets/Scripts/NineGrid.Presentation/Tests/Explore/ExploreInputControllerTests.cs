using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Controllers;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests.Explore
{
    /// <summary>
    /// V1：ExploreInputController → Command；并锁定 CombatHitSink 探索入口已退役。
    /// </summary>
    public sealed class ExploreInputControllerTests
    {
        private static readonly SlotId sAdjacentSlot = SlotId.Board(2);
        private static readonly SlotId sFarCornerSlot = SlotId.Board(1);

        [Test]
        public void Controller_HandleEmptySlotClicked_SendsExploreCommand()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGame(seed: 42UL))
            {
                Assert.IsTrue(arch.Phase.StartNode(CreateSingleMonsterNode(hp: 1, attack: 0)).Accepted);
                arch.PlaceSoleBoardCardAt(sFarCornerSlot);

                var scriptFactory = new RecordingScriptFactory();
                using (var runtime = PresentationRuntimeFixture.Install(arch, scriptFactory))
                {
                    var controllerGo = new GameObject("ExploreInputControllerProbe");
                    var controller = controllerGo.AddComponent<ExploreInputController>();

                    try
                    {
                        controller.HandleEmptySlotClicked(sAdjacentSlot.Index);

                        Assert.AreEqual(1, scriptFactory.Built.Count);
                        Assert.AreEqual(InputIntentKinds.Explore, scriptFactory.Built[0].Kind);
                        Assert.AreEqual(sAdjacentSlot.Index, scriptFactory.Built[0].TargetId);
                        Assert.IsTrue(runtime.MainlineBusy.Value);
                    }
                    finally
                    {
                        Object.DestroyImmediate(controllerGo);
                    }
                }
            }
        }

        [Test]
        public void CombatHitSink_NoLongerExposesExploreEntry()
        {
            var sinkType = typeof(CombatHitSink);
            Assert.IsNull(
                sinkType.GetField("TrySubmitExploreIntent"),
                "TrySubmitExploreIntent 应已从 CombatHitSink 删除");
            Assert.IsNull(
                sinkType.GetMethod("RequestExploreIntent"),
                "RequestExploreIntent 应已从 CombatHitSink 删除");
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
