using NineGrid.Cards;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Controllers;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Tests.Output
{
    /// <summary>
    /// V9：伤害飘字经 Hook → Event；CombatHitSink 飘字入口退役。
    /// </summary>
    public sealed class DamageNumberOutputControllerTests
    {
        [Test]
        public void Controller_RequestSpawn_SendsDamageNumberRequestedEvent()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGame(seed: 42UL))
            {
                DamageNumberRequested? received = null;
                var unreg = arch.Architecture.RegisterEvent<DamageNumberRequested>(e => received = e);

                var controller = DamageNumberOutputController.EnsureInstalled();
                try
                {
                    var pos = new Vector3(1.5f, -2f, 0f);
                    DamageNumberHook.RequestSpawn(pos, 7);

                    Assert.IsTrue(received.HasValue);
                    Assert.AreEqual(pos, received.Value.WorldPosition);
                    Assert.AreEqual(7, received.Value.Amount);
                }
                finally
                {
                    unreg.UnRegister();
                    UnityEngine.Object.DestroyImmediate(controller.gameObject);
                }
            }
        }

        [Test]
        public void Controller_NonPositiveAmount_DoesNotSendEvent()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGame(seed: 42UL))
            {
                var count = 0;
                var unreg = arch.Architecture.RegisterEvent<DamageNumberRequested>(_ => count++);

                var controller = DamageNumberOutputController.EnsureInstalled();
                try
                {
                    DamageNumberHook.RequestSpawn(Vector3.zero, 0);
                    DamageNumberHook.RequestSpawn(Vector3.zero, -3);
                    DamageNumberHook.RequestSpawnHeal(Vector3.zero, 0);

                    Assert.AreEqual(0, count);
                }
                finally
                {
                    unreg.UnRegister();
                    UnityEngine.Object.DestroyImmediate(controller.gameObject);
                }
            }
        }

        [Test]
        public void Controller_RequestSpawnHeal_SendsHealMarkedEvent()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGame(seed: 42UL))
            {
                DamageNumberRequested? received = null;
                var unreg = arch.Architecture.RegisterEvent<DamageNumberRequested>(e => received = e);

                var controller = DamageNumberOutputController.EnsureInstalled();
                try
                {
                    var pos = new Vector3(-2f, 1f, 0f);
                    DamageNumberHook.RequestSpawnHeal(pos, 5);

                    Assert.IsTrue(received.HasValue);
                    Assert.AreEqual(pos, received.Value.WorldPosition);
                    Assert.AreEqual(5, received.Value.Amount);
                    Assert.IsTrue(received.Value.IsHeal, "治疗飘字应标记 IsHeal");
                }
                finally
                {
                    unreg.UnRegister();
                    UnityEngine.Object.DestroyImmediate(controller.gameObject);
                }
            }
        }

        [Test]
        public void Controller_RequestSpawn_SendsNonHealEvent()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGame(seed: 42UL))
            {
                DamageNumberRequested? received = null;
                var unreg = arch.Architecture.RegisterEvent<DamageNumberRequested>(e => received = e);

                var controller = DamageNumberOutputController.EnsureInstalled();
                try
                {
                    DamageNumberHook.RequestSpawn(Vector3.zero, 7);

                    Assert.IsTrue(received.HasValue);
                    Assert.IsFalse(received.Value.IsHeal, "普通伤害飘字不应标记 IsHeal");
                }
                finally
                {
                    unreg.UnRegister();
                    UnityEngine.Object.DestroyImmediate(controller.gameObject);
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
    }
}
