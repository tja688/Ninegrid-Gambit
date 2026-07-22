using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Controllers;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests.Attack
{
    /// <summary>
    /// V2：AttackInputController → Command；并锁定 CombatHitSink 攻击入口已退役。
    /// </summary>
    public sealed class AttackInputControllerTests
    {
        private static readonly SlotId sAdjacentSlot = SlotId.Board(2);

        [Test]
        public void Controller_HandleMonsterSlotClicked_SendsAttackCommand()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGame(seed: 42UL))
            {
                Assert.IsTrue(arch.Phase.StartNode(CreateSingleMonsterNode(hp: 1, attack: 0)).Accepted);
                arch.PlaceSoleBoardCardAt(sAdjacentSlot);

                var scriptFactory = new RecordingScriptFactory();
                using (var runtime = PresentationRuntimeFixture.Install(arch, scriptFactory))
                {
                    var controllerGo = new GameObject("AttackInputControllerProbe");
                    var controller = controllerGo.AddComponent<AttackInputController>();

                    try
                    {
                        controller.HandleMonsterSlotClicked(sAdjacentSlot.Index);

                        Assert.AreEqual(1, scriptFactory.Built.Count);
                        Assert.AreEqual(InputIntentKinds.Attack, scriptFactory.Built[0].Kind);
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
        public void CombatHitSink_NoLongerExposesAttackRuleBridges()
        {
            var sinkType = typeof(CombatHitSink);
            Assert.IsNull(
                sinkType.GetField("TrySubmitAttackIntent"),
                "TrySubmitAttackIntent 应已从 CombatHitSink 删除");
            Assert.IsNull(
                sinkType.GetMethod("RequestAttackIntent"),
                "RequestAttackIntent 应已从 CombatHitSink 删除");
            Assert.IsNull(
                sinkType.GetField("EstimateWillKill"),
                "EstimateWillKill 应已从 CombatHitSink 删除");
            Assert.IsNull(
                sinkType.GetField("ResolvePlayerAttackTarget"),
                "ResolvePlayerAttackTarget 应已从 CombatHitSink 删除");
            Assert.IsNull(
                sinkType.GetMethod("RequestEstimateWillKill"),
                "RequestEstimateWillKill 应已从 CombatHitSink 删除");
            Assert.IsNull(
                sinkType.GetMethod("RequestResolvePlayerAttackTarget"),
                "RequestResolvePlayerAttackTarget 应已从 CombatHitSink 删除");
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
