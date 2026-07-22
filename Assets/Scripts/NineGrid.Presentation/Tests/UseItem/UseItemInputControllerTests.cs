using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Controllers;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests.UseItem
{
    /// <summary>
    /// V3：UseItemInputController → Command；并锁定 CombatHitSink 用牌入口已退役。
    /// </summary>
    public sealed class UseItemInputControllerTests
    {
        private static readonly SlotId sAdjacentSlot = SlotId.Board(2);

        [Test]
        public void Controller_HandleUseItemRequested_SendsUseItemCommand()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGame(seed: 42UL))
            {
                Assert.IsTrue(arch.Phase.StartNode(CreateSingleMonsterNode(hp: 1, attack: 0)).Accepted);
                arch.PlaceSoleBoardCardAt(sAdjacentSlot);
                var targetUid = arch.Board.GetCardUid(sAdjacentSlot);
                var knifeUid = SpawnHelpIntoItemSlots(arch, "help.throwing_knife");

                var scriptFactory = new RecordingScriptFactory();
                using (var runtime = PresentationRuntimeFixture.Install(arch, scriptFactory))
                {
                    var controllerGo = new GameObject("UseItemInputControllerProbe");
                    var controller = controllerGo.AddComponent<UseItemInputController>();

                    try
                    {
                        Assert.IsTrue(controller.HandleUseItemRequested(
                            knifeUid,
                            new[] { targetUid },
                            null));

                        Assert.AreEqual(1, scriptFactory.Built.Count);
                        Assert.AreEqual(InputIntentKinds.UseItem, scriptFactory.Built[0].Kind);
                        Assert.AreEqual(knifeUid, scriptFactory.Built[0].TargetId);
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
        public void CombatHitSink_TypeIsDeleted()
        {
            Assert.IsNull(
                System.Type.GetType("NineGrid.Cards.CombatHitSink, NineGrid.Presentation"),
                "CombatHitSink 应已删除");
        }

        private static int SpawnHelpIntoItemSlots(PresentationArchitectureFixture arch, string defId)
        {
            arch.Pipeline.Enqueue(
                new SpawnCardAction(defId, CardKind.HelpCard, ZoneId.ItemSlots, SlotId.None, 1, "test"));
            Assert.Greater(arch.Pipeline.RunToCompletion(), 0);
            var deck = arch.Architecture.GetModel<DeckModel>();
            Assert.Greater(deck.ItemSlotUids.Count, 0);
            return deck.ItemSlotUids[deck.ItemSlotUids.Count - 1];
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
